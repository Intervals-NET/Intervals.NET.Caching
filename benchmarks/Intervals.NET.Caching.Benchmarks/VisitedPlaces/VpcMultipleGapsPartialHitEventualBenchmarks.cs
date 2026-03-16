using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency multiple-gaps partial-hit benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: data source fetches for all K gaps + normalization
/// event enqueue. Background segment storage is NOT included in the measurement.
/// IterationCleanup drains the background loop after each iteration so the next
/// IterationSetup starts with a clean slate.
///
/// Parameters: GapCount, MultiGapTotalSegments, and StorageStrategy only.
/// AppendBufferSize is omitted: the append buffer is always flushed at the end of
/// IterationSetup population (WaitForIdleAsync in PopulateWithGaps), so it has no
/// effect on User Path partial-hit cost.
///
/// See <see cref="VpcMultipleGapsPartialHitBenchmarksBase"/> for layout details.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcMultipleGapsPartialHitEventualBenchmarks : VpcMultipleGapsPartialHitBenchmarksBase
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;
    private Range<int> _multipleGapsRange;

    /// <summary>
    /// Number of internal gaps — each gap produces one data source fetch and one store.
    /// </summary>
    [Params(1, 10, 100, 1_000)]
    public int GapCount { get; set; }

    /// <summary>
    /// Total background segments in cache (beyond the gap pattern).
    /// Controls storage overhead and FindIntersecting baseline cost.
    /// </summary>
    [Params(1_000, 10_000)]
    public int MultiGapTotalSegments { get; set; }

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
        _multipleGapsRange = BuildMultipleGapsRange(GapCount);
        _frozenDataSource = RunLearningPass(_domain, StorageStrategy, GapCount, MultiGapTotalSegments, appendBufferSize: 8);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _cache = SetupCache(_frozenDataSource, _domain, StorageStrategy, GapCount, MultiGapTotalSegments, appendBufferSize: 8);
    }

    /// <summary>
    /// Measures User Path partial-hit cost with multiple gaps.
    /// GapCount+1 existing segments hit; GapCount gaps fetched from the data source.
    /// Background storage of K gap segments is enqueued but not awaited.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_MultipleGaps()
    {
        await _cache!.GetDataAsync(_multipleGapsRange, CancellationToken.None);
    }

    /// <summary>
    /// Drains background normalization (K gap segment stores) published during the
    /// benchmark iteration before the next IterationSetup creates a fresh cache.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        _cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
