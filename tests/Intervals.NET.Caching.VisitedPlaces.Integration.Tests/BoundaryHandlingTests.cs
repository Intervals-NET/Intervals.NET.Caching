using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Tests that validate boundary handling when the data source has physical limits.
/// Uses <see cref="BoundedDataSource"/> (MinId=1000, MaxId=9999) to simulate a bounded data store.
///
/// In VPC all fetching happens on the User Path (unlike SWC where rebalance also fetches).
/// When the data source returns a <c>null</c> Range in a <see cref="RangeChunk{TRange,TData}"/>
/// the result set for that gap is empty and the overall <see cref="RangeResult{TRange,TData}"/>
/// may have a null or truncated Range accordingly.
/// </summary>
public sealed class BoundaryHandlingTests : IAsyncDisposable
{
    private readonly IntegerFixedStepDomain _domain = new();
    private readonly BoundedDataSource _dataSource = new();
    private readonly EventCounterCacheDiagnostics _diagnostics = new();
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;

    public async ValueTask DisposeAsync()
    {
        if (_cache != null)
        {
            await _cache.WaitForIdleAsync();
            await _cache.DisposeAsync();
        }
    }

    private VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateCache(
        int maxSegmentCount = 100)
    {
        _cache = TestHelpers.CreateCache(
            _dataSource,
            _domain,
            TestHelpers.CreateDefaultOptions(),
            _diagnostics,
            maxSegmentCount);
        return _cache;
    }

    // ============================================================
    // FULL MISS — OUT OF BOUNDS
    // ============================================================

    /// <summary>
    /// When the entire request is below the data source's physical bounds,
    /// the result should contain no data and a null range.
    /// </summary>
    [Fact]
    public async Task UserPath_PhysicalDataMiss_BelowBounds_ReturnsNullRange()
    {
        // ARRANGE
        var cache = CreateCache();
        var requestBelowBounds = Factories.Range.Closed<int>(0, 999);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(requestBelowBounds);

        // ASSERT
        Assert.Null(result.Range);
        Assert.True(result.Data.IsEmpty);
        Assert.Equal(CacheInteraction.FullMiss, result.CacheInteraction);
    }

    /// <summary>
    /// When the entire request is above the data source's physical bounds,
    /// the result should contain no data and a null range.
    /// </summary>
    [Fact]
    public async Task UserPath_PhysicalDataMiss_AboveBounds_ReturnsNullRange()
    {
        // ARRANGE
        var cache = CreateCache();
        var requestAboveBounds = Factories.Range.Closed<int>(10000, 11000);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(requestAboveBounds);

        // ASSERT
        Assert.Null(result.Range);
        Assert.True(result.Data.IsEmpty);
        Assert.Equal(CacheInteraction.FullMiss, result.CacheInteraction);
    }

    // ============================================================
    // PARTIAL HIT — BOUNDARY TRUNCATION
    // ============================================================

    /// <summary>
    /// When the request overlaps the lower boundary, the data source returns a truncated chunk
    /// starting at MinId=1000. The result range and data should reflect only the available portion.
    /// </summary>
    [Fact]
    public async Task UserPath_PartialMiss_LowerBoundaryTruncation_ReturnsTruncatedRange()
    {
        // ARRANGE — data available in [1000, 9999]; request [500, 1500] straddles lower bound
        var cache = CreateCache();
        var requestedRange = Factories.Range.Closed<int>(500, 1500);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(requestedRange);

        // ASSERT — range is truncated to [1000, 1500]; 501 elements
        Assert.NotNull(result.Range);
        var expectedRange = Factories.Range.Closed<int>(1000, 1500);
        Assert.Equal(expectedRange, result.Range);
        Assert.Equal(501, result.Data.Length);
        Assert.Equal(1000, result.Data.Span[0]);
        Assert.Equal(1500, result.Data.Span[500]);
    }

    /// <summary>
    /// When the request overlaps the upper boundary, the data source returns a truncated chunk
    /// ending at MaxId=9999. The result range and data should reflect only the available portion.
    /// </summary>
    [Fact]
    public async Task UserPath_PartialMiss_UpperBoundaryTruncation_ReturnsTruncatedRange()
    {
        // ARRANGE — data available in [1000, 9999]; request [9500, 10500] straddles upper bound
        var cache = CreateCache();
        var requestedRange = Factories.Range.Closed<int>(9500, 10500);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(requestedRange);

        // ASSERT — range is truncated to [9500, 9999]; 500 elements
        Assert.NotNull(result.Range);
        var expectedRange = Factories.Range.Closed<int>(9500, 9999);
        Assert.Equal(expectedRange, result.Range);
        Assert.Equal(500, result.Data.Length);
        Assert.Equal(9500, result.Data.Span[0]);
        Assert.Equal(9999, result.Data.Span[499]);
    }

    // ============================================================
    // FULL HIT — WITHIN BOUNDS
    // ============================================================

    /// <summary>
    /// A request that falls entirely within the physical bounds should return the full
    /// requested range and correct data values.
    /// </summary>
    [Fact]
    public async Task UserPath_FullMiss_WithinBounds_ReturnsFullRange()
    {
        // ARRANGE — data available in [1000, 9999]; request [2000, 3000] is entirely within bounds
        var cache = CreateCache();
        var requestedRange = Factories.Range.Closed<int>(2000, 3000);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(requestedRange);

        // ASSERT — 1001 elements [2000..3000]
        Assert.NotNull(result.Range);
        Assert.Equal(requestedRange, result.Range);
        Assert.Equal(1001, result.Data.Length);
        Assert.Equal(2000, result.Data.Span[0]);
        Assert.Equal(3000, result.Data.Span[1000]);
    }

    /// <summary>
    /// A request spanning the exact physical boundaries [1000, 9999] should return all 9000
    /// elements without truncation.
    /// </summary>
    [Fact]
    public async Task UserPath_FullMiss_AtExactBoundaries_ReturnsFullRange()
    {
        // ARRANGE
        var cache = CreateCache();
        var requestedRange = Factories.Range.Closed<int>(1000, 9999);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(requestedRange);

        // ASSERT — 9000 elements [1000..9999]
        Assert.NotNull(result.Range);
        Assert.Equal(requestedRange, result.Range);
        Assert.Equal(9000, result.Data.Length);
        Assert.Equal(1000, result.Data.Span[0]);
        Assert.Equal(9999, result.Data.Span[8999]);
    }

    // ============================================================
    // DIAGNOSTICS — BOUNDARY SCENARIOS
    // ============================================================

    /// <summary>
    /// When a request is completely out of bounds, the cache still records it as served
    /// (no exception occurred), fires <c>DataSourceFetchGap</c> once (for the gap fetch),
    /// and records a full miss.
    /// </summary>
    [Fact]
    public async Task UserPath_PhysicalDataMiss_DiagnosticsAreCorrect()
    {
        // ARRANGE
        var cache = CreateCache();
        var requestBelowBounds = Factories.Range.Closed<int>(0, 999);

        // ACT
        await cache.GetDataAndWaitForIdleAsync(requestBelowBounds);

        // ASSERT
        Assert.Equal(1, _diagnostics.UserRequestServed);
        Assert.Equal(1, _diagnostics.UserRequestFullCacheMiss);
        Assert.Equal(0, _diagnostics.UserRequestFullCacheHit);
        Assert.Equal(0, _diagnostics.UserRequestPartialCacheHit);
        Assert.Equal(1, _diagnostics.DataSourceFetchGap);
    }

    /// <summary>
    /// After caching an in-bounds segment, re-requesting the same range produces a full hit
    /// regardless of the physical boundaries of the data source.
    /// </summary>
    [Fact]
    public async Task UserPath_AfterCachingWithinBounds_FullHitRequiresNoFetch()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = Factories.Range.Closed<int>(5000, 5009);

        // Warm cache
        await cache.GetDataAndWaitForIdleAsync(range);
        _diagnostics.Reset();

        // ACT — same range again
        var result = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT — no data source call, full hit
        Assert.Equal(CacheInteraction.FullHit, result.CacheInteraction);
        Assert.Equal(1, _diagnostics.UserRequestFullCacheHit);
        Assert.Equal(0, _diagnostics.DataSourceFetchGap);
    }
}
