using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Domain.Extensions.Fixed;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Robustness tests using varied range patterns for
/// <see cref="VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// Uses a deterministic seed for reproducibility.
/// All tests call WaitForIdleAsync between accesses to ensure background normalization
/// completes before the next read, avoiding the known SnapshotAppendBufferStorage
/// race window between Normalize() and concurrent FindIntersecting() calls.
/// </summary>
public sealed class RandomRangeRobustnessTests : IAsyncDisposable
{
    private readonly IntegerFixedStepDomain _domain = new();
    private readonly SpyDataSource _dataSource = new();
    private readonly EventCounterCacheDiagnostics _diagnostics = new();
    private readonly Random _random = new(42);
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;

    public async ValueTask DisposeAsync()
    {
        if (_cache != null)
        {
            await _cache.WaitForIdleAsync();
            await _cache.DisposeAsync();
        }

        _dataSource.Reset();
    }

    private VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateCache(int maxSegmentCount = 100)
    {
        _cache = TestHelpers.CreateCache(
            _dataSource, _domain, TestHelpers.CreateDefaultOptions(), _diagnostics, maxSegmentCount);
        return _cache;
    }

    // ============================================================
    // VARIED RANGE REQUESTS — DATA CORRECTNESS
    // ============================================================

    /// <summary>
    /// Fetching 20 non-overlapping ranges in succession returns data of the correct length
    /// for each. Uses GetDataAndWaitForIdleAsync to ensure stable state between requests.
    /// </summary>
    [Fact]
    public async Task NonOverlappingRanges_20Iterations_CorrectDataLength()
    {
        // ARRANGE
        var cache = CreateCache();

        // ACT & ASSERT — non-overlapping ranges spaced 1000 units apart
        for (var i = 0; i < 20; i++)
        {
            // Use wide spacing to guarantee full-miss on each request (no partial hits)
            var start = i * 500;
            var length = _random.Next(5, 30);
            var range = Factories.Range.Closed<int>(start, start + length - 1);

            var result = await cache.GetDataAndWaitForIdleAsync(range);

            Assert.Equal((int)range.Span(_domain), result.Data.Length);
            Assert.Equal(start, result.Data.Span[0]);
        }
    }

    /// <summary>
    /// After warming a segment, subsequent requests inside the cached range produce full hits
    /// with correct data content.
    /// </summary>
    [Fact]
    public async Task CachedSubrange_AfterWarmup_FullHitWithCorrectData()
    {
        // ARRANGE
        var cache = CreateCache();
        var warmRange = Factories.Range.Closed<int>(1000, 1099);
        await cache.GetDataAndWaitForIdleAsync(warmRange);

        // ACT & ASSERT — 10 sub-ranges inside the warm segment are full hits
        for (var i = 0; i < 10; i++)
        {
            var subStart = 1000 + i * 10;
            var subEnd = subStart + 9;
            var range = Factories.Range.Closed<int>(subStart, subEnd);

            var result = await cache.GetDataAndWaitForIdleAsync(range);

            Assert.Equal(10, result.Data.Length);
            Assert.Equal(subStart, result.Data.Span[0]);
            Assert.Equal(subEnd, result.Data.Span[9]);
        }

        // Data source was called only once (for the warm-up, not for sub-range hits)
        Assert.Equal(1, _dataSource.TotalFetchCount);
    }

    /// <summary>
    /// Fetching ranges that extend just beyond a cached segment correctly fills gaps
    /// and returns data of the full requested length.
    /// </summary>
    [Fact]
    public async Task ExtendBeyondCachedRange_GapFilled_CorrectLength()
    {
        // ARRANGE
        var cache = CreateCache();
        var warmRange = Factories.Range.Closed<int>(2000, 2049);
        await cache.GetDataAndWaitForIdleAsync(warmRange);

        // ACT — request extends 10 units beyond the right edge (gap of [2050, 2059])
        var extendedRange = Factories.Range.Closed<int>(2000, 2059);
        var result = await cache.GetDataAndWaitForIdleAsync(extendedRange);

        // ASSERT — 60 elements: 50 cached + 10 fetched
        Assert.Equal(60, result.Data.Length);
        Assert.Equal(2000, result.Data.Span[0]);
        Assert.Equal(2059, result.Data.Span[59]);
        Assert.Equal(2, _dataSource.TotalFetchCount);
    }

    /// <summary>
    /// Fetching ranges that extend beyond the left edge of a cached segment correctly
    /// fills gaps and returns data of the full requested length.
    /// </summary>
    [Fact]
    public async Task ExtendBeforeCachedRange_GapFilled_CorrectLength()
    {
        // ARRANGE
        var cache = CreateCache();
        var warmRange = Factories.Range.Closed<int>(3000, 3049);
        await cache.GetDataAndWaitForIdleAsync(warmRange);

        // ACT — request extends 10 units before the left edge (gap of [2990, 2999])
        var extendedRange = Factories.Range.Closed<int>(2990, 3049);
        var result = await cache.GetDataAndWaitForIdleAsync(extendedRange);

        // ASSERT — 60 elements: 10 fetched + 50 cached
        Assert.Equal(60, result.Data.Length);
        Assert.Equal(2990, result.Data.Span[0]);
        Assert.Equal(3049, result.Data.Span[59]);
        Assert.Equal(2, _dataSource.TotalFetchCount);
    }

    /// <summary>
    /// Multiple independent segments at different locations are all retrievable with correct data.
    /// </summary>
    [Fact]
    public async Task MultipleSegmentsAtDifferentLocations_AllCorrect()
    {
        // ARRANGE
        var cache = CreateCache();
        var ranges = new[]
        {
            Factories.Range.Closed<int>(100, 109),
            Factories.Range.Closed<int>(500, 519),
            Factories.Range.Closed<int>(2000, 2024),
            Factories.Range.Closed<int>(9000, 9009),
        };

        // Warm all segments
        foreach (var range in ranges)
        {
            await cache.GetDataAndWaitForIdleAsync(range);
        }

        // ACT & ASSERT — re-fetch each segment and verify correct data (full hits)
        _dataSource.Reset();
        foreach (var range in ranges)
        {
            var result = await cache.GetDataAndWaitForIdleAsync(range);
            var expected = (int)range.Span(_domain);
            Assert.Equal(expected, result.Data.Length);
            Assert.Equal((int)range.Start, result.Data.Span[0]);
        }

        // All re-fetches should be full hits — data source not called again
        Assert.Equal(0, _dataSource.TotalFetchCount);
    }

    // ============================================================
    // STRESS / STABILITY
    // ============================================================

    /// <summary>
    /// 30 sequential fetches with periodic idle-waits produce valid, non-empty results
    /// and leave diagnostics in a consistent lifecycle state.
    /// </summary>
    [Fact]
    public async Task SequentialRequests_30WithPeriodicIdle_SystemStable()
    {
        // ARRANGE
        var cache = CreateCache(maxSegmentCount: 50);

        // ACT — fetch 30 ranges with WaitForIdleAsync every 10 to flush background normalization
        for (var i = 0; i < 30; i++)
        {
            var start = _random.Next(0, 5000);
            var length = _random.Next(10, 40);
            var range = Factories.Range.Closed<int>(start, start + length - 1);

            var result = await cache.GetDataAsync(range, CancellationToken.None);
            Assert.True(result.Data.Length > 0, $"Request {i}: data should be non-empty.");

            if (i % 10 == 9)
            {
                await cache.WaitForIdleAsync();
            }
        }

        // ASSERT — diagnostic lifecycle invariant holds
        await cache.WaitForIdleAsync();
        TestHelpers.AssertBackgroundLifecycleIntegrity(_diagnostics);
        TestHelpers.AssertNoBackgroundFailures(_diagnostics);
        Assert.True(_dataSource.TotalFetchCount > 0, "Data source should have been called.");
    }
}
