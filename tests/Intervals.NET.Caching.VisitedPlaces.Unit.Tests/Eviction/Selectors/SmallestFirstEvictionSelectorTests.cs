using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Core;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Eviction.Selectors;

/// <summary>
/// Unit tests for <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>.
/// Validates that candidates are ordered ascending by span (smallest span first).
/// </summary>
public sealed class SmallestFirstEvictionSelectorTests
{
    private readonly IntegerFixedStepDomain _domain = TestHelpers.CreateIntDomain();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDomain_DoesNotThrow()
    {
        // ARRANGE & ACT
        var exception = Record.Exception(() =>
            new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain));

        // ASSERT
        Assert.Null(exception);
    }

    #endregion

    #region InitializeMetadata Tests

    [Fact]
    public void InitializeMetadata_SetsSpanOnEvictionMetadata()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var segment = CreateSegmentRaw(10, 19); // span = 10

        // ACT
        selector.InitializeMetadata(segment, DateTime.UtcNow);

        // ASSERT
        var meta = Assert.IsType<SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>.SmallestFirstMetadata>(
            segment.EvictionMetadata);
        Assert.Equal(10L, meta.Span);
    }

    [Fact]
    public void InitializeMetadata_OnSegmentWithExistingMetadata_OverwritesMetadata()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var segment = CreateSegmentRaw(0, 4); // span = 5
        selector.InitializeMetadata(segment, DateTime.UtcNow);

        // ACT — re-initialize (e.g., segment re-stored after selector swap)
        selector.InitializeMetadata(segment, DateTime.UtcNow);

        // ASSERT — still correct metadata, not stale
        var meta = Assert.IsType<SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>.SmallestFirstMetadata>(
            segment.EvictionMetadata);
        Assert.Equal(5L, meta.Span);
    }

    #endregion

    #region OrderCandidates Tests

    [Fact]
    public void OrderCandidates_ReturnsSmallestSpanFirst()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        var small = CreateSegment(selector, 0, 2);    // span 3
        var medium = CreateSegment(selector, 10, 15); // span 6
        var large = CreateSegment(selector, 20, 29);  // span 10

        // ACT
        var ordered = selector.OrderCandidates([large, small, medium]);

        // ASSERT — ascending by span
        Assert.Same(small, ordered[0]);
        Assert.Same(medium, ordered[1]);
        Assert.Same(large, ordered[2]);
    }

    [Fact]
    public void OrderCandidates_WithAlreadySortedInput_PreservesOrder()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        var small = CreateSegment(selector, 0, 2);    // span 3
        var medium = CreateSegment(selector, 10, 15); // span 6
        var large = CreateSegment(selector, 20, 29);  // span 10

        // ACT
        var ordered = selector.OrderCandidates([small, medium, large]);

        // ASSERT
        Assert.Same(small, ordered[0]);
        Assert.Same(medium, ordered[1]);
        Assert.Same(large, ordered[2]);
    }

    [Fact]
    public void OrderCandidates_WithSingleCandidate_ReturnsSingleElement()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var seg = CreateSegment(selector, 0, 5);

        // ACT
        var ordered = selector.OrderCandidates([seg]);

        // ASSERT
        Assert.Single(ordered);
        Assert.Same(seg, ordered[0]);
    }

    [Fact]
    public void OrderCandidates_WithEmptyList_ReturnsEmptyList()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        // ACT
        var ordered = selector.OrderCandidates([]);

        // ASSERT
        Assert.Empty(ordered);
    }

    [Fact]
    public void OrderCandidates_WithNoMetadata_FallsBackToLiveSpanComputation()
    {
        // ARRANGE — segments without InitializeMetadata called (metadata = null)
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var small = CreateSegmentRaw(0, 2);    // span 3
        var large = CreateSegmentRaw(20, 29);  // span 10

        // ACT
        var ordered = selector.OrderCandidates([large, small]);

        // ASSERT — fallback path still produces correct ordering
        Assert.Same(small, ordered[0]);
        Assert.Same(large, ordered[1]);
    }

    #endregion

    #region Helpers

    private static CachedSegment<int, int> CreateSegment(
        SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain> selector,
        int start, int end)
    {
        var segment = CreateSegmentRaw(start, end);
        selector.InitializeMetadata(segment, DateTime.UtcNow);
        return segment;
    }

    private static CachedSegment<int, int> CreateSegmentRaw(int start, int end)
    {
        var range = TestHelpers.CreateRange(start, end);
        return new CachedSegment<int, int>(
            range,
            new ReadOnlyMemory<int>(new int[end - start + 1]));
    }

    #endregion
}
