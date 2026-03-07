using Intervals.NET.Caching.VisitedPlaces.Core;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Evaluators;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Eviction.Evaluators;

/// <summary>
/// Unit tests for <see cref="MaxSegmentCountEvaluator{TRange,TData}"/>.
/// </summary>
public sealed class MaxSegmentCountEvaluatorTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidMaxCount_SetsMaxCount()
    {
        // ARRANGE & ACT
        var evaluator = new MaxSegmentCountEvaluator<int, int>(5);

        // ASSERT
        Assert.Equal(5, evaluator.MaxCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithMaxCountLessThanOne_ThrowsArgumentOutOfRangeException(int invalidMaxCount)
    {
        // ARRANGE & ACT
        var exception = Record.Exception(() => new MaxSegmentCountEvaluator<int, int>(invalidMaxCount));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Constructor_WithMaxCountOfOne_IsValid()
    {
        // ARRANGE & ACT
        var exception = Record.Exception(() => new MaxSegmentCountEvaluator<int, int>(1));

        // ASSERT
        Assert.Null(exception);
    }

    #endregion

    #region ComputeEvictionCount Tests — No Eviction

    [Fact]
    public void ComputeEvictionCount_WhenCountBelowMax_ReturnsZero()
    {
        // ARRANGE
        var evaluator = new MaxSegmentCountEvaluator<int, int>(3);
        var segments = CreateSegments(2);

        // ACT
        var result = evaluator.ComputeEvictionCount(segments.Count, segments);

        // ASSERT
        Assert.Equal(0, result);
    }

    [Fact]
    public void ComputeEvictionCount_WhenCountEqualsMax_ReturnsZero()
    {
        // ARRANGE
        var evaluator = new MaxSegmentCountEvaluator<int, int>(3);
        var segments = CreateSegments(3);

        // ACT
        var result = evaluator.ComputeEvictionCount(segments.Count, segments);

        // ASSERT
        Assert.Equal(0, result);
    }

    [Fact]
    public void ComputeEvictionCount_WhenStorageEmpty_ReturnsZero()
    {
        // ARRANGE
        var evaluator = new MaxSegmentCountEvaluator<int, int>(1);
        var segments = CreateSegments(0);

        // ACT
        var result = evaluator.ComputeEvictionCount(segments.Count, segments);

        // ASSERT
        Assert.Equal(0, result);
    }

    #endregion

    #region ComputeEvictionCount Tests — Eviction Triggered

    [Fact]
    public void ComputeEvictionCount_WhenCountExceedsMax_ReturnsPositive()
    {
        // ARRANGE
        var evaluator = new MaxSegmentCountEvaluator<int, int>(3);
        var segments = CreateSegments(4);

        // ACT
        var result = evaluator.ComputeEvictionCount(segments.Count, segments);

        // ASSERT
        Assert.True(result > 0, $"Expected a positive eviction count, got {result}");
    }

    [Fact]
    public void ComputeEvictionCount_WhenCountExceedsByOne_ReturnsOne()
    {
        // ARRANGE
        var evaluator = new MaxSegmentCountEvaluator<int, int>(3);
        var segments = CreateSegments(4);

        // ACT
        var count = evaluator.ComputeEvictionCount(segments.Count, segments);

        // ASSERT
        Assert.Equal(1, count);
    }

    [Fact]
    public void ComputeEvictionCount_WhenCountExceedsByMany_ReturnsExcess()
    {
        // ARRANGE
        var evaluator = new MaxSegmentCountEvaluator<int, int>(3);
        var segments = CreateSegments(7);

        // ACT
        var count = evaluator.ComputeEvictionCount(segments.Count, segments);

        // ASSERT
        Assert.Equal(4, count);
    }

    #endregion

    #region Helpers

    private static IReadOnlyList<CachedSegment<int, int>> CreateSegments(int count)
    {
        var result = new List<CachedSegment<int, int>>();
        for (var i = 0; i < count; i++)
        {
            var start = i * 10;
            var range = TestHelpers.CreateRange(start, start + 5);
            result.Add(new CachedSegment<int, int>(
                range,
                new ReadOnlyMemory<int>(new int[6]),
                new SegmentStatistics(DateTime.UtcNow)));
        }
        return result;
    }

    #endregion
}
