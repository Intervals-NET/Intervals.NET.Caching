using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC cache-hit benchmarks.
/// Measures user-facing read latency when all requested data is already cached.
///
/// EXECUTION FLOW: User Request → Full cache hit, zero data source calls
///
/// Methodology:
/// - Learning pass in GlobalSetup: throwaway cache exercises all FetchAsync paths so
///   the data source can be frozen before benchmark iterations begin.
/// - Real cache created and populated once in GlobalSetup with FrozenDataSource
///   (population is NOT part of the measurement).
/// - Request spans exactly HitSegments adjacent segments (guaranteed full hit).
/// - Every GetDataAsync publishes a normalization event (LRU metadata update) to the
///   background loop even on a full hit. Derived classes control when that background
///   work is drained relative to the measurement boundary.
///
/// Parameters:
/// - HitSegments: Number of segments the request spans (read-side scaling)
/// - TotalSegments: Total cached segments (storage size scaling, affects FindIntersecting)
/// - SegmentSpan: Data points per segment (10 vs 100 — reveals per-segment copy cost on read)
/// - StorageStrategy: Snapshot vs LinkedList (algorithm differences)
/// </summary>
public abstract class VpcCacheHitBenchmarksBase
{
    protected VisitedPlacesCache<int, int, IntegerFixedStepDomain>? Cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;
    protected Range<int> HitRange;

    /// <summary>
    /// Number of segments the request spans — measures read-side scaling.
    /// </summary>
    [Params(1, 10, 100, 1_000)]
    public int HitSegments { get; set; }

    /// <summary>
    /// Total segments in cache — measures storage size impact on FindIntersecting.
    /// </summary>
    [Params(1_000, 10_000)]
    public int TotalSegments { get; set; }

    /// <summary>
    /// Data points per segment — measures per-segment copy cost during read.
    /// 10 vs 100 isolates the cost of copying segment data into the result buffer.
    /// </summary>
    [Params(10, 100)]
    public int SegmentSpan { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot (sorted array + binary search) vs LinkedList (stride index).
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    /// <summary>
    /// GlobalSetup runs once per parameter combination.
    /// Learning pass exercises all FetchAsync paths on a throwaway cache, then freezes the
    /// data source. Real cache is populated with the frozen source so measurement iterations
    /// are allocation-free on the data source side.
    /// Population cost is paid once, not repeated every iteration.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();

        // Pre-calculate the hit range: spans HitSegments adjacent segments.
        // Segments are placed at [0,S-1], [S,2S-1], [2S,3S-1], ... where S=SegmentSpan.
        const int hitStart = 0;
        var hitEnd = (HitSegments * SegmentSpan) - 1;
        HitRange = Factories.Range.Closed<int>(hitStart, hitEnd);

        // Learning pass: exercise all FetchAsync paths on a throwaway cache.
        // MaxSegmentCount must accommodate TotalSegments without eviction.
        var learningSource = new SynchronousDataSource(_domain);
        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 1000);
        VpcCacheHelpers.PopulateSegments(throwaway, TotalSegments, SegmentSpan);

        // Freeze: learning source disabled, frozen source used for real benchmark.
        _frozenDataSource = learningSource.Freeze();

        // Real cache: populate once with frozen source (no allocation on FetchAsync).
        Cache = VpcCacheHelpers.CreateCache(
            _frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 1000);
        VpcCacheHelpers.PopulateSegments(Cache, TotalSegments, SegmentSpan);
    }
}
