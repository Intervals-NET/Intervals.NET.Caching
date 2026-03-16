using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency single-gap partial-hit benchmarks for VisitedPlaces Cache.
/// Measures the complete per-request cost: User Path data assembly + data source fetch
/// for the gap + background segment storage. WaitForIdleAsync is inside the measurement
/// boundary.
///
/// Parameters: TotalSegments, StorageStrategy, and AppendBufferSize.
/// AppendBufferSize is included because normalization frequency directly affects the
/// background work measured by WaitForIdleAsync:
///   - AppendBufferSize=1: normalization fires on every store (WithNormalization).
///   - AppendBufferSize=8: normalization deferred until 8 stores accumulate (NoNormalization
///     for a single-gap benchmark since only 1 segment is stored per invocation).
///
/// See <see cref="VpcSingleGapPartialHitBenchmarksBase"/> for layout details.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcSingleGapPartialHitStrongBenchmarks : VpcSingleGapPartialHitBenchmarksBase
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
    /// Append buffer size — controls normalization frequency on the background path.
    /// 1 = normalize on every store (WithNormalization).
    /// 8 = normalization deferred; a single-gap invocation stores only 1 segment so
    ///     normalization never fires within a single measurement (NoNormalization).
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
        _frozenDataSource = RunLearningPass(_domain, StorageStrategy, TotalSegments, AppendBufferSize);
    }

    [IterationSetup(Target = nameof(PartialHit_SingleGap_OneHit))]
    public void IterationSetup_OneHit()
    {
        _cache = CreateOneHitCache(_frozenDataSource, _domain, StorageStrategy, TotalSegments, AppendBufferSize);
    }

    [IterationSetup(Target = nameof(PartialHit_SingleGap_TwoHits))]
    public void IterationSetup_TwoHits()
    {
        _cache = CreateTwoHitsCache(_frozenDataSource, _domain, StorageStrategy, TotalSegments, AppendBufferSize);
    }

    /// <summary>
    /// Partial hit: request [0,9] crosses the initial gap [0,4] into segment [5,14].
    /// Produces 1 gap fetch + 1 cache hit. Includes background segment storage cost.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_OneHit()
    {
        await _cache!.GetDataAsync(OneHitRange, CancellationToken.None);
        await _cache.WaitForIdleAsync();
    }

    /// <summary>
    /// Partial hit: request [12,21] spans across gap [15,19] touching segments [5,14] and [20,29].
    /// Produces 1 gap fetch + 2 cache hits. Includes background segment storage cost.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_TwoHits()
    {
        await _cache!.GetDataAsync(TwoHitsRange, CancellationToken.None);
        await _cache.WaitForIdleAsync();
    }
}
