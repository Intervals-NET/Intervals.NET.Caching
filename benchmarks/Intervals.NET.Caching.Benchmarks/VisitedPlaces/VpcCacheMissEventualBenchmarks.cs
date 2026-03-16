using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency cache-miss benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: data source fetch + normalization event enqueue.
/// Background segment storage and eviction are NOT inside the measurement boundary.
///
/// Parameters: TotalSegments and StorageStrategy only.
/// AppendBufferSize is omitted: the append buffer is always flushed at the end of
/// GlobalSetup population, so it has no effect on the User Path miss cost.
/// NoEviction/WithEviction is omitted: eviction runs on the Background Path, which is
/// outside the measurement boundary for eventual mode.
///
/// Setup strategy (no IterationSetup re-population):
/// - Cache populated once in GlobalSetup with FrozenDataSource.
/// - MaxIterations unique miss ranges pre-computed and learned in GlobalSetup.
/// - Each iteration picks the next range via a rotating counter — the cache accumulates
///   at most MaxIterations extra segments (+0.2% at 100K, +20% at 1K, +2000% at 10).
///   For the TotalSegments=10 param value, FindIntersecting is sub-microsecond regardless
///   of absolute count, so the drift is acceptable.
/// - IterationCleanup drains background normalization before the next iteration.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcCacheMissEventualBenchmarks : VpcCacheMissBenchmarksBase
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private IntegerFixedStepDomain _domain;
    private Range<int>[] _missRanges = null!;
    private int _iterationIndex;

    /// <summary>
    /// Total segments in cache — tests scaling from small to large segment counts.
    /// Values straddle the ~50K crossover point between Snapshot and LinkedList strategies.
    /// </summary>
    [Params(10, 1_000, 100_000)]
    public int TotalSegments { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot vs LinkedList.
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    /// <summary>
    /// Runs once per parameter combination.
    /// Populates the cache and pre-computes MaxIterations unique miss ranges so that
    /// IterationSetup requires no re-population.
    /// AppendBufferSize is fixed at 8 (default); it does not affect User Path miss cost.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _missRanges = BuildMissRanges(TotalSegments);

        var frozenDataSource = RunLearningPass(
            _domain, StorageStrategy,
            totalSegments: TotalSegments,
            appendBufferSize: 8,
            missRanges: _missRanges);

        _cache = CreateAndPopulate(
            frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + MaxIterations + 1000,
            appendBufferSize: 8,
            totalSegments: TotalSegments);

        _iterationIndex = 0;
    }

    /// <summary>
    /// Advances to the next pre-computed miss range.
    /// No re-population: the cache accumulates one new segment per iteration.
    /// </summary>
    [IterationSetup]
    public void IterationSetup()
    {
        _iterationIndex++;
    }

    /// <summary>
    /// Measures User Path cache-miss cost: data source fetch + normalization event enqueue.
    /// Background segment storage is enqueued but not awaited.
    /// </summary>
    [Benchmark]
    public async Task CacheMiss()
    {
        await _cache!.GetDataAsync(_missRanges[_iterationIndex % MaxIterations], CancellationToken.None);
    }

    /// <summary>
    /// Drains background normalization (segment storage) published during the benchmark
    /// iteration so the next iteration sees a consistent storage state.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        _cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
