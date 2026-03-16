using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency multiple-gaps partial-hit benchmarks for VisitedPlaces Cache.
/// Measures the complete end-to-end cost: User Path data assembly + data source fetches
/// for all K gaps + background segment storage (K stores, K/AppendBufferSize normalizations).
/// WaitForIdleAsync is inside the measurement boundary.
///
/// Parameters: GapCount, MultiGapTotalSegments, StorageStrategy, and AppendBufferSize.
/// AppendBufferSize is included because normalization frequency directly affects the
/// background work measured by WaitForIdleAsync:
///   - AppendBufferSize=1: normalization fires on every store.
///   - AppendBufferSize=8: normalization fires after every 8 stores (K/8 normalizations).
///
/// See <see cref="VpcMultipleGapsPartialHitBenchmarksBase"/> for layout details.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcMultipleGapsPartialHitStrongBenchmarks : VpcMultipleGapsPartialHitBenchmarksBase
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;
    private Range<int> _multipleGapsRange;

    /// <summary>
    /// Number of internal gaps — each gap produces one data source fetch and one store.
    /// K stores → K/AppendBufferSize normalizations.
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
    /// Append buffer size — controls normalization frequency on the background path.
    /// 1 = normalize on every store; 8 = normalize after every 8 stores.
    /// </summary>
    [Params(1, 8)]
    public int AppendBufferSize { get; set; }

    /// <summary>
    /// Runs once per parameter combination.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _multipleGapsRange = BuildMultipleGapsRange(GapCount);
        _frozenDataSource = RunLearningPass(_domain, StorageStrategy, GapCount, MultiGapTotalSegments, AppendBufferSize);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _cache = SetupCache(_frozenDataSource, _domain, StorageStrategy, GapCount, MultiGapTotalSegments, AppendBufferSize);
    }

    /// <summary>
    /// Measures complete partial-hit cost with multiple gaps.
    /// GapCount+1 existing segments hit; GapCount gaps fetched and stored.
    /// GapCount stores → GapCount/AppendBufferSize normalizations.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_MultipleGaps()
    {
        await _cache!.GetDataAsync(_multipleGapsRange, CancellationToken.None);
        await _cache.WaitForIdleAsync();
    }
}
