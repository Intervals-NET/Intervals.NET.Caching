using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Tests for exception handling in the User Path of <see cref="VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// Verifies that exceptions thrown by the data source during user-path fetches propagate to the caller
/// (unlike the Background Path, where exceptions are swallowed and reported via diagnostics).
/// </summary>
public sealed class UserPathExceptionHandlingTests : IAsyncDisposable
{
    private readonly IntegerFixedStepDomain _domain = new();
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

    private VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateCacheWith(
        IDataSource<int, int> dataSource,
        int maxSegmentCount = 100)
    {
        _cache = TestHelpers.CreateCache(
            dataSource,
            _domain,
            TestHelpers.CreateDefaultOptions(),
            _diagnostics,
            maxSegmentCount);
        return _cache;
    }

    // ============================================================
    // DATA SOURCE EXCEPTION — propagates on full miss
    // ============================================================

    /// <summary>
    /// When the data source throws during a full-miss fetch on the User Path,
    /// the exception propagates directly to the caller (not swallowed).
    /// </summary>
    [Fact]
    public async Task DataSourceThrows_OnFullMiss_ExceptionPropagatesT0Caller()
    {
        // ARRANGE — data source always throws
        var dataSource = new FaultyDataSource<int, int>(
            _ => throw new InvalidOperationException("Simulated data source failure"));
        var cache = CreateCacheWith(dataSource);
        _cache = null; // prevent WaitForIdleAsync in DisposeAsync from being called before we handle this
        await using var _ = cache;

        var range = TestHelpers.CreateRange(0, 9);

        // ACT
        var exception = await Record.ExceptionAsync(
            () => cache.GetDataAsync(range, CancellationToken.None).AsTask());

        // ASSERT — exception propagates to caller
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Simulated data source failure", exception.Message);
    }

    /// <summary>
    /// When the data source throws during a partial-miss gap fetch on the User Path,
    /// the exception propagates directly to the caller.
    /// </summary>
    [Fact]
    public async Task DataSourceThrows_OnGapFetch_ExceptionPropagesToCaller()
    {
        // ARRANGE — succeed on the first call (populates cache for [0,9]),
        // then throw on subsequent calls (gap fetch for the partial-hit request)
        var callCount = 0;
        var dataSource = new FaultyDataSource<int, int>(range =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Generate sequential integers [start, end] inclusive
                var start = (int)range.Start;
                var end = (int)range.End;
                var data = new int[end - start + 1];
                for (var i = 0; i < data.Length; i++) { data[i] = start + i; }
                return data;
            }

            throw new InvalidOperationException("Gap fetch failed");
        });

        var cache = CreateCacheWith(dataSource);

        // Warm up: cache [0, 9] with the first (succeeding) fetch
        await cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(0, 9));

        var range = TestHelpers.CreateRange(5, 14); // [5,14] — overlaps [0,9], gap is [10,14]

        // ACT
        var exception = await Record.ExceptionAsync(
            () => cache.GetDataAsync(range, CancellationToken.None).AsTask());

        // ASSERT — exception propagates from gap fetch
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Gap fetch failed", exception.Message);

        await cache.WaitForIdleAsync();
    }

    /// <summary>
    /// When the data source throws, the exception type is preserved faithfully.
    /// </summary>
    [Fact]
    public async Task DataSourceThrows_ExceptionTypePreserved()
    {
        // ARRANGE
        var dataSource = new FaultyDataSource<int, int>(
            _ => throw new ArgumentOutOfRangeException("id", "Range ID out of bounds"));
        var cache = CreateCacheWith(dataSource);
        _cache = null;
        await using var _ = cache;

        // ACT
        var exception = await Record.ExceptionAsync(
            () => cache.GetDataAsync(TestHelpers.CreateRange(0, 9), CancellationToken.None).AsTask());

        // ASSERT — original exception type is preserved
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>
    /// After a User Path fetch throws, the cache remains operational for subsequent requests
    /// that can succeed (e.g., hitting cached data that was stored before the failure).
    /// </summary>
    [Fact]
    public async Task DataSourceThrows_CacheRemainsOperationalForCachedRanges()
    {
        // ARRANGE — succeed for [0,9] then fail for any other range
        var dataSource = new FaultyDataSource<int, int>(range =>
        {
            var start = (int)range.Start;
            if (start == 0)
            {
                var s = (int)range.Start;
                var e = (int)range.End;
                var d = new int[e - s + 1];
                for (var i = 0; i < d.Length; i++) { d[i] = s + i; }
                return d;
            }

            throw new InvalidOperationException("Out of range");
        });

        var cache = CreateCacheWith(dataSource);

        // Warm up: cache [0,9]
        await cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(0, 9));

        // ACT — request that would call data source (range not in cache) → should throw
        var failException = await Record.ExceptionAsync(
            () => cache.GetDataAsync(TestHelpers.CreateRange(100, 109), CancellationToken.None).AsTask());

        // Request fully in cache → should succeed
        var hitResult = await cache.GetDataAsync(TestHelpers.CreateRange(0, 9), CancellationToken.None);

        // ASSERT
        Assert.NotNull(failException);
        Assert.IsType<InvalidOperationException>(failException);

        // Cache is still operational for the already-cached range
        Assert.Equal(10, hitResult.Data.Length);
        TestHelpers.AssertUserDataCorrect(hitResult.Data, TestHelpers.CreateRange(0, 9));

        await cache.WaitForIdleAsync();
    }
}
