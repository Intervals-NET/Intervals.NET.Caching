using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC cache-miss benchmarks.
/// Holds layout constants and protected factory helpers only.
/// [Params] and [GlobalSetup] live in each derived class because Eventual and Strong
/// measure different things and therefore require different parameter sets.
///
/// EXECUTION FLOW: User Request → Full miss → data source fetch → background segment
/// storage (+ optional eviction).
///
/// Layout: segments of span SegmentSpan separated by gaps of GapSize.
/// Miss ranges are placed beyond all populated segments with the same stride so
/// consecutive miss ranges never overlap (each is a guaranteed cold miss).
///
/// See <see cref="VpcCacheMissEventualBenchmarks"/> and <see cref="VpcCacheMissStrongBenchmarks"/>
/// for parameter sets, setup methodology, and benchmark methods.
/// </summary>
public abstract class VpcCacheMissBenchmarksBase
{
    protected const int SegmentSpan = 10;
    protected const int GapSize = 10;
    protected const int Stride = SegmentSpan + GapSize; // = 20

    /// <summary>
    /// Number of miss ranges pre-computed in GlobalSetup.
    /// Must exceed BDN warmup + measurement iterations combined (typically ~30).
    /// 200 provides a wide margin without excessive learning-pass cost.
    /// </summary>
    protected const int MaxIterations = 200;

    /// <summary>
    /// Computes an array of MaxIterations unique miss ranges, all placed beyond the
    /// populated region. Each range is separated by GapSize so they never merge into
    /// a single segment when stored sequentially across iterations.
    /// </summary>
    protected static Range<int>[] BuildMissRanges(int totalSegments)
    {
        var beyondAll = totalSegments * Stride + 1000;
        var ranges = new Range<int>[MaxIterations];

        for (var i = 0; i < MaxIterations; i++)
        {
            var start = beyondAll + i * Stride;
            ranges[i] = Factories.Range.Closed<int>(start, start + SegmentSpan - 1);
        }

        return ranges;
    }

    /// <summary>
    /// Runs the learning pass: exercises PopulateWithGaps and all miss ranges on a
    /// throwaway cache so the data source learns every range before freezing.
    /// </summary>
    protected static FrozenDataSource RunLearningPass(
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int totalSegments,
        int appendBufferSize,
        Range<int>[] missRanges)
    {
        var learningSource = new SynchronousDataSource(domain);

        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, domain, strategyType,
            maxSegmentCount: totalSegments + 1000,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(throwaway, totalSegments, SegmentSpan, GapSize);

        foreach (var range in missRanges)
        {
            throwaway.GetDataAsync(range, CancellationToken.None).GetAwaiter().GetResult();
        }

        throwaway.WaitForIdleAsync().GetAwaiter().GetResult();

        return learningSource.Freeze();
    }

    /// <summary>
    /// Creates and populates a cache with TotalSegments segments.
    /// </summary>
    protected static VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateAndPopulate(
        FrozenDataSource frozenDataSource,
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int maxSegmentCount,
        int appendBufferSize,
        int totalSegments)
    {
        var cache = VpcCacheHelpers.CreateCache(
            frozenDataSource, domain, strategyType,
            maxSegmentCount: maxSegmentCount,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(cache, totalSegments, SegmentSpan, GapSize);

        return cache;
    }
}
