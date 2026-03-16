using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC multiple-gaps partial-hit benchmarks.
/// Measures write-side scaling: K+1 existing segments hit with K internal gaps.
/// K gaps → K stores → K/AppendBufferSize normalizations.
///
/// Isolates: normalization cost as GapCount grows, and how AppendBufferSize amortizes it.
///
/// Methodology:
/// - Learning pass in GlobalSetup: throwaway cache exercises PopulateWithGaps (pattern +
///   fillers) and the multi-gap request so the data source can be frozen.
/// - Cache pre-populated with alternating segment/gap layout in IterationSetup.
/// - Request spans the entire alternating pattern, hitting all K gaps.
/// - Fresh cache per iteration (benchmark stores K new gap segments each time).
/// - Derived classes control whether WaitForIdleAsync is inside the measurement boundary
///   (strong) or deferred to IterationCleanup (eventual).
///
/// Parameters:
/// - GapCount: {1, 10, 100, 1_000} — write-side scaling (K stores per invocation)
/// - MultiGapTotalSegments: {1_000, 10_000} — background segment count
/// - StorageStrategy: Snapshot vs LinkedList
/// - AppendBufferSize: {1, 8} — normalization frequency (every store vs every 8 stores)
/// </summary>
public abstract class VpcMultipleGapsPartialHitBenchmarksBase
{
    protected VisitedPlacesCache<int, int, IntegerFixedStepDomain>? Cache;
    private FrozenDataSource _frozenDataSource = null!;
    private IntegerFixedStepDomain _domain;
    protected Range<int> MultipleGapsRange;

    private const int SegmentSpan = 10;
    private const int GapSize = SegmentSpan; // Gap size = segment span for uniform layout

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
    /// Append buffer size — controls normalization frequency.
    /// 1 = normalize every store, 8 = normalize every 8 stores (default).
    /// </summary>
    [Params(1, 8)]
    public int AppendBufferSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();

        // Request spans all non-adjacent segments (hitting all gaps).
        // Layout: alternating segments and gaps, each span=10.
        // stride = SegmentSpan + GapSize = 20
        // GapCount+1 segments exist: at positions 0, 20, 40, ...
        const int stride = SegmentSpan + GapSize;
        var requestEnd = GapCount * stride + SegmentSpan - 1;
        MultipleGapsRange = Factories.Range.Closed<int>(0, requestEnd);

        var nonAdjacentCount = GapCount + 1;

        // Learning pass: exercise PopulateWithGaps (pattern + fillers) and the multi-gap request.
        var learningSource = new SynchronousDataSource(_domain);
        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, _domain, StorageStrategy,
            maxSegmentCount: MultiGapTotalSegments + 1000,
            appendBufferSize: AppendBufferSize);

        // Populate the gap-pattern region.
        VpcCacheHelpers.PopulateWithGaps(throwaway, nonAdjacentCount, SegmentSpan, GapSize);

        // Populate filler segments beyond the pattern.
        var remainingCount = MultiGapTotalSegments - nonAdjacentCount;
        if (remainingCount > 0)
        {
            var startAfterPattern = nonAdjacentCount * stride + GapSize;
            VpcCacheHelpers.PopulateWithGaps(throwaway, remainingCount, SegmentSpan, GapSize, startAfterPattern);
        }

        // Fire the multi-gap request to learn all gap fetch ranges.
        throwaway.GetDataAsync(MultipleGapsRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.WaitForIdleAsync().GetAwaiter().GetResult();

        _frozenDataSource = learningSource.Freeze();
    }

    /// <summary>
    /// Creates a fresh cache and populates it for the multi-gap benchmark.
    /// Call from a derived [IterationSetup].
    /// </summary>
    protected void SetupCache()
    {
        const int stride = SegmentSpan + GapSize;
        var nonAdjacentCount = GapCount + 1;

        Cache = VpcCacheHelpers.CreateCache(
            _frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: MultiGapTotalSegments + 1000,
            appendBufferSize: AppendBufferSize);

        // Populate the gap-pattern region: GapCount+1 non-adjacent segments separated by gaps.
        VpcCacheHelpers.PopulateWithGaps(Cache, nonAdjacentCount, SegmentSpan, GapSize);

        // Populate filler segments beyond the pattern to reach MultiGapTotalSegments.
        var remainingCount = MultiGapTotalSegments - nonAdjacentCount;
        if (remainingCount > 0)
        {
            var startAfterPattern = nonAdjacentCount * stride + GapSize;
            VpcCacheHelpers.PopulateWithGaps(Cache, remainingCount, SegmentSpan, GapSize, startAfterPattern);
        }
    }
}
