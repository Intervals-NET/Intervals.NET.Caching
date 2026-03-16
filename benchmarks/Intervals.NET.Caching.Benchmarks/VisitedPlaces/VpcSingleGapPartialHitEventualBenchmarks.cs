using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency single-gap partial-hit benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: data source fetch for the gap + normalization event
/// enqueue. Background segment storage is NOT included in the measurement.
/// IterationCleanup drains the background loop after each iteration so the next
/// IterationSetup starts with a clean slate.
///
/// Parameters: TotalSegments and StorageStrategy only.
/// AppendBufferSize is omitted: the append buffer is always flushed at the end of
/// IterationSetup population (WaitForIdleAsync in PopulateWithGaps), so it has no
/// effect on User Path partial-hit cost.
///
/// See <see cref="VpcSingleGapPartialHitBenchmarksBase"/> for layout details.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcSingleGapPartialHitEventualBenchmarks : VpcSingleGapPartialHitBenchmarksBase
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;

    /// <summary>
    /// Total segments in cache — measures storage size impact on FindIntersecting.
    /// </summary>
    [Params(1_000, 10_000)]
    public int TotalSegments { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot vs LinkedList.
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    /// <summary>
    /// Runs once per parameter combination. AppendBufferSize is fixed at 8 (default);
    /// it does not affect User Path partial-hit cost.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _frozenDataSource = RunLearningPass(_domain, StorageStrategy, TotalSegments, appendBufferSize: 8);
    }

    [IterationSetup(Target = nameof(PartialHit_SingleGap_OneHit))]
    public void IterationSetup_OneHit()
    {
        _cache = CreateOneHitCache(_frozenDataSource, _domain, StorageStrategy, TotalSegments, appendBufferSize: 8);
    }

    [IterationSetup(Target = nameof(PartialHit_SingleGap_TwoHits))]
    public void IterationSetup_TwoHits()
    {
        _cache = CreateTwoHitsCache(_frozenDataSource, _domain, StorageStrategy, TotalSegments, appendBufferSize: 8);
    }

    /// <summary>
    /// Partial hit: request [0,9] crosses the initial gap [0,4] into segment [5,14].
    /// Produces 1 gap fetch + 1 cache hit. Background segment storage is not awaited.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_OneHit()
    {
        await _cache!.GetDataAsync(OneHitRange, CancellationToken.None);
    }

    /// <summary>
    /// Partial hit: request [12,21] spans across gap [15,19] touching segments [5,14] and [20,29].
    /// Produces 1 gap fetch + 2 cache hits. Background segment storage is not awaited.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_TwoHits()
    {
        await _cache!.GetDataAsync(TwoHitsRange, CancellationToken.None);
    }

    /// <summary>
    /// Drains background normalization (gap segment storage) published during the benchmark
    /// iteration before the next IterationSetup creates a fresh cache.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        _cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
