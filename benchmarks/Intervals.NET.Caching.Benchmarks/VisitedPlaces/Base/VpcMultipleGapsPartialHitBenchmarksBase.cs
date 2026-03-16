using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

/// <summary>
/// Abstract base for VPC multiple-gaps partial-hit benchmarks.
/// Holds layout constants and protected factory helpers only.
/// [Params] and [GlobalSetup] live in each derived class because Eventual and Strong
/// measure different things and require different parameter sets.
///
/// Layout: alternating segment/gap pattern, each span=10 (stride=20).
///   GapCount+1 segments exist at positions 0, 20, 40, ...
///   Each segment covers [k*20, k*20+9]; each gap covers [k*20+10, k*20+19].
///
/// The benchmark request spans the entire alternating pattern, hitting all K gaps:
///   request = [0, GapCount*20+9]  →  K gaps fetched, K+1 segment hits.
///
/// See <see cref="VpcMultipleGapsPartialHitEventualBenchmarks"/> and
/// <see cref="VpcMultipleGapsPartialHitStrongBenchmarks"/> for parameter sets and methodology.
/// </summary>
public abstract class VpcMultipleGapsPartialHitBenchmarksBase
{
    protected const int SegmentSpan = 10;
    protected const int GapSize = SegmentSpan; // = 10, gap equals segment span
    protected const int Stride = SegmentSpan + GapSize; // = 20

    /// <summary>
    /// Runs the learning pass: exercises PopulateWithGaps (pattern + fillers) and the
    /// multi-gap request on a throwaway cache so the data source learns every range
    /// before freezing.
    /// </summary>
    protected static FrozenDataSource RunLearningPass(
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int gapCount,
        int multiGapTotalSegments,
        int appendBufferSize)
    {
        var learningSource = new SynchronousDataSource(domain);
        var multipleGapsRange = BuildMultipleGapsRange(gapCount);
        var nonAdjacentCount = gapCount + 1;

        var throwaway = VpcCacheHelpers.CreateCache(
            learningSource, domain, strategyType,
            maxSegmentCount: multiGapTotalSegments + 1000,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(throwaway, nonAdjacentCount, SegmentSpan, GapSize);

        var remainingCount = multiGapTotalSegments - nonAdjacentCount;
        if (remainingCount > 0)
        {
            var startAfterPattern = nonAdjacentCount * Stride + GapSize;
            VpcCacheHelpers.PopulateWithGaps(throwaway, remainingCount, SegmentSpan, GapSize, startAfterPattern);
        }

        throwaway.GetDataAsync(multipleGapsRange, CancellationToken.None).GetAwaiter().GetResult();
        throwaway.WaitForIdleAsync().GetAwaiter().GetResult();

        return learningSource.Freeze();
    }

    /// <summary>
    /// Creates a fresh cache and populates it with the alternating pattern and filler segments.
    /// Call from a derived [IterationSetup].
    /// </summary>
    protected static VisitedPlacesCache<int, int, IntegerFixedStepDomain> SetupCache(
        FrozenDataSource frozenDataSource,
        IntegerFixedStepDomain domain,
        StorageStrategyType strategyType,
        int gapCount,
        int multiGapTotalSegments,
        int appendBufferSize)
    {
        var nonAdjacentCount = gapCount + 1;

        var cache = VpcCacheHelpers.CreateCache(
            frozenDataSource, domain, strategyType,
            maxSegmentCount: multiGapTotalSegments + 1000,
            appendBufferSize: appendBufferSize);

        VpcCacheHelpers.PopulateWithGaps(cache, nonAdjacentCount, SegmentSpan, GapSize);

        var remainingCount = multiGapTotalSegments - nonAdjacentCount;
        if (remainingCount > 0)
        {
            var startAfterPattern = nonAdjacentCount * Stride + GapSize;
            VpcCacheHelpers.PopulateWithGaps(cache, remainingCount, SegmentSpan, GapSize, startAfterPattern);
        }

        return cache;
    }

    /// <summary>
    /// Computes the range that spans all GapCount gaps and GapCount+1 segments.
    /// </summary>
    protected static Range<int> BuildMultipleGapsRange(int gapCount)
    {
        var requestEnd = gapCount * Stride + SegmentSpan - 1;
        return Factories.Range.Closed<int>(0, requestEnd);
    }
}
