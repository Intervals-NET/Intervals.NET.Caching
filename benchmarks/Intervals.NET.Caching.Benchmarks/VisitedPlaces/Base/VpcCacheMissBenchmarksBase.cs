using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC cache-miss benchmarks.
/// Covers two eviction scenarios: NoEviction (ample capacity) and WithEviction (at capacity).
///
/// EXECUTION FLOW: User Request → Full miss → data source fetch → background segment
/// storage (+ optional eviction).
///
/// Methodology:
/// - Learning pass in GlobalSetup: throwaway cache exercises PopulateWithGaps + miss range
///   so the data source can be frozen before benchmark iterations begin.
/// - Pre-populated cache with TotalSegments segments separated by gaps.
/// - Request in a gap beyond all segments (guaranteed full miss).
/// - Fresh cache per iteration via IterationSetup.
/// - Derived classes control whether WaitForIdleAsync is inside the measurement boundary
///   (strong) or deferred to IterationCleanup (eventual).
///
/// Parameters:
/// - TotalSegments: {10, 1K, 100K} — straddles ~50K Snapshot/LinkedList crossover
/// - StorageStrategy: Snapshot vs LinkedList
/// - AppendBufferSize: {1, 8} — normalization frequency (every 1 vs every 8 stores)
/// </summary>
public abstract class VpcCacheMissBenchmarksBase
{
    protected VisitedPlacesCache<int, int, IntegerFixedStepDomain>? Cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;
    protected Range<int> MissRange;

    private const int SegmentSpan = 10;
    private const int GapSize = 10;

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
    /// Append buffer size — controls normalization frequency.
    /// 1 = normalize every store, 8 = normalize every 8 stores (default).
    /// </summary>
    [Params(1, 8)]
    public int AppendBufferSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();

        // Miss range: far beyond all populated segments.
        const int stride = SegmentSpan + GapSize;
        var beyondAll = TotalSegments * stride + 1000;
        MissRange = Factories.Range.Closed<int>(beyondAll, beyondAll + SegmentSpan - 1);

        // Learning pass: exercise PopulateWithGaps and the miss fetch on a throwaway cache.
        var learningSource = new SynchronousDataSource(_domain);
        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 1000,
            appendBufferSize: AppendBufferSize);
        VpcCacheHelpers.PopulateWithGaps(throwaway, TotalSegments, SegmentSpan, GapSize);
        throwaway.GetDataAsync(MissRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.WaitForIdleAsync().GetAwaiter().GetResult();

        _frozenDataSource = learningSource.Freeze();
    }

    /// <summary>
    /// Creates a fresh cache with ample capacity (no eviction) and populates it.
    /// Call from a derived [IterationSetup] targeting the NoEviction benchmark method.
    /// </summary>
    protected void SetupNoEvictionCache()
    {
        Cache = VpcCacheHelpers.CreateCache(
            _frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 1000,
            appendBufferSize: AppendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(Cache, TotalSegments, SegmentSpan, GapSize);
    }

    /// <summary>
    /// Creates a fresh cache at capacity (eviction triggered on each miss) and populates it.
    /// Call from a derived [IterationSetup] targeting the WithEviction benchmark method.
    /// </summary>
    protected void SetupWithEvictionCache()
    {
        Cache = VpcCacheHelpers.CreateCache(
            _frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments,
            appendBufferSize: AppendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(Cache, TotalSegments, SegmentSpan, GapSize);
    }
}
