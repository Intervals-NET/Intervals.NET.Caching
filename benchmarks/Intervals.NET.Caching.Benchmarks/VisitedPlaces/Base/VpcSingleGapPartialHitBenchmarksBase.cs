using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC single-gap partial-hit benchmarks.
/// Holds layout constants and protected factory helpers only.
/// [Params] and [GlobalSetup] live in each derived class because Eventual and Strong
/// measure different things and require different parameter sets.
///
/// Layout uses alternating [gap][segment] pattern (stride = SegmentSpan + GapSize):
///   Gaps:     [0,4],  [15,19], [30,34], ...
///   Segments: [5,14], [20,29], [35,44], ...
/// (SegmentSpan=10, GapSize=5 — a SegmentSpan-wide request can straddle any gap.)
///
/// Two benchmark methods isolate the two structural cases:
///   - OneHit:  request [0,9]   → 1 gap [0,4]   + 1 segment hit [5,9]  from [5,14]
///   - TwoHits: request [12,21] → 1 gap [15,19] + 2 segment hits [12,14]+[20,21]
///
/// Both trigger exactly one data source fetch and one normalization event per invocation.
///
/// See <see cref="VpcSingleGapPartialHitEventualBenchmarks"/> and
/// <see cref="VpcSingleGapPartialHitStrongBenchmarks"/> for parameter sets and methodology.
/// </summary>
public abstract class VpcSingleGapPartialHitBenchmarksBase
{
    protected const int SegmentSpan = 10;
    protected const int GapSize = SegmentSpan / 2; // = 5
    protected const int Stride = SegmentSpan + GapSize; // = 15
    protected const int SegmentStart = GapSize; // = 5, gaps come first

    // OneHit: request [0,9] → gap [0,4], hit [5,9] from segment [5,14]
    protected static readonly Range<int> OneHitRange =
        Factories.Range.Closed<int>(0, SegmentSpan - 1);

    // TwoHits: request [12,21] → hit [12,14] from [5,14], gap [15,19], hit [20,21] from [20,29]
    protected static readonly Range<int> TwoHitsRange =
        Factories.Range.Closed<int>(
            SegmentSpan + GapSize / 2,                    // = 12
            SegmentSpan + GapSize / 2 + SegmentSpan - 1); // = 21

    /// <summary>
    /// Runs the learning pass: exercises PopulateWithGaps and both benchmark request ranges
    /// on a throwaway cache so the data source learns every range before freezing.
    /// </summary>
    protected static FrozenDataSource RunLearningPass(
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int totalSegments,
        int appendBufferSize)
    {
        var learningSource = new SynchronousDataSource(domain);

        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, domain, strategyType,
            maxSegmentCount: totalSegments + 100,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(throwaway, totalSegments, SegmentSpan, GapSize, SegmentStart);
        throwaway.GetDataAsync(OneHitRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.GetDataAsync(TwoHitsRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.WaitForIdleAsync().GetAwaiter().GetResult();

        return learningSource.Freeze();
    }

    /// <summary>
    /// Creates a fresh cache and populates it for the OneHit benchmark.
    /// Call from a derived [IterationSetup] targeting the OneHit benchmark method.
    /// </summary>
    protected static VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateOneHitCache(
        FrozenDataSource frozenDataSource,
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int totalSegments,
        int appendBufferSize)
    {
        var cache = VpcCacheHelpers.CreateCache(
            frozenDataSource, domain, strategyType,
            maxSegmentCount: totalSegments + 100,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(cache, totalSegments, SegmentSpan, GapSize, SegmentStart);
        return cache;
    }

    /// <summary>
    /// Creates a fresh cache and populates it for the TwoHits benchmark.
    /// Call from a derived [IterationSetup] targeting the TwoHits benchmark method.
    /// </summary>
    protected static VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateTwoHitsCache(
        FrozenDataSource frozenDataSource,
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int totalSegments,
        int appendBufferSize)
    {
        var cache = VpcCacheHelpers.CreateCache(
            frozenDataSource, domain, strategyType,
            maxSegmentCount: totalSegments + 100,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(cache, totalSegments, SegmentSpan, GapSize, SegmentStart);
        return cache;
    }
}
