using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC single-gap partial-hit benchmarks.
/// Measures partial hit cost when a request crosses exactly one cached/uncached boundary.
///
/// Layout uses alternating [gap][segment] pattern (stride = SegmentSpan + GapSize):
///   Gaps:     [0,4],  [15,19], [30,34], ...
///   Segments: [5,14], [20,29], [35,44], ...
/// (SegmentSpan=10, GapSize=5 — so a SegmentSpan-wide request can straddle any gap.)
///
/// Two benchmark methods isolate the two structural cases:
///   - OneHit:  request [0,9]   → 1 gap [0,4]   + 1 segment hit [5,9]  from [5,14]
///   - TwoHits: request [12,21] → 1 gap [15,19] + 2 segment hits [12,14]+[20,21]
///
/// Both trigger exactly one data source fetch and one normalization event per invocation.
///
/// Methodology:
/// - Learning pass in GlobalSetup: throwaway cache exercises PopulateWithGaps + both
///   benchmark request ranges so the data source can be frozen.
/// - Fresh cache per iteration via IterationSetup with FrozenDataSource.
/// - Derived classes control whether WaitForIdleAsync is inside the measurement boundary
///   (strong) or deferred to IterationCleanup (eventual).
///
/// Parameters:
///   - TotalSegments: {1_000, 10_000} — storage size (FindIntersecting cost)
///   - StorageStrategy: Snapshot vs LinkedList
/// </summary>
public abstract class VpcSingleGapPartialHitBenchmarksBase
{
    protected VisitedPlacesCache<int, int, IntegerFixedStepDomain>? Cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;

    // Layout constants: SegmentSpan=10, GapSize=5 → stride=15, segments start at offset GapSize=5
    private const int SegmentSpan = 10;
    private const int GapSize = SegmentSpan / 2; // = 5
    private const int Stride = SegmentSpan + GapSize; // = 15
    private const int SegmentStart = GapSize; // = 5, so gaps come first

    protected Range<int> OneHitRange;
    protected Range<int> TwoHitsRange;

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

    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();

        // OneHit: request [0,9] → gap [0,4], hit [5,9] from segment [5,14]
        OneHitRange = Factories.Range.Closed<int>(0, SegmentSpan - 1);

        // TwoHits: request [12,21] → hit [12,14] from [5,14], gap [15,19], hit [20,21] from [20,29]
        TwoHitsRange = Factories.Range.Closed<int>(
            SegmentSpan + GapSize / 2,                     // = 12
            SegmentSpan + GapSize / 2 + SegmentSpan - 1);  // = 21

        // Learning pass: exercise PopulateWithGaps and both benchmark request ranges.
        var learningSource = new SynchronousDataSource(_domain);
        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 100,
            appendBufferSize: 8);
        VpcCacheHelpers.PopulateWithGaps(throwaway, TotalSegments, SegmentSpan, GapSize, SegmentStart);
        throwaway.GetDataAsync(OneHitRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.GetDataAsync(TwoHitsRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.WaitForIdleAsync().GetAwaiter().GetResult();

        _frozenDataSource = learningSource.Freeze();
    }

    /// <summary>
    /// Creates a fresh cache and populates it for the OneHit benchmark.
    /// Call from a derived [IterationSetup] targeting the OneHit benchmark method.
    /// </summary>
    protected void SetupOneHitCache()
    {
        Cache = VpcCacheHelpers.CreateCache(
            _frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 100,
            appendBufferSize: 8);

        VpcCacheHelpers.PopulateWithGaps(Cache, TotalSegments, SegmentSpan, GapSize, SegmentStart);
    }

    /// <summary>
    /// Creates a fresh cache and populates it for the TwoHits benchmark.
    /// Call from a derived [IterationSetup] targeting the TwoHits benchmark method.
    /// </summary>
    protected void SetupTwoHitsCache()
    {
        Cache = VpcCacheHelpers.CreateCache(
            _frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 100,
            appendBufferSize: 8);

        VpcCacheHelpers.PopulateWithGaps(Cache, TotalSegments, SegmentSpan, GapSize, SegmentStart);
    }
}
