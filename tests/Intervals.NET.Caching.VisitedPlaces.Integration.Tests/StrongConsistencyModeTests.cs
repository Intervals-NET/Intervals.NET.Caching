using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Integration tests for the strong consistency mode exposed by
/// <c>GetDataAndWaitForIdleAsync</c> on <see cref="VisitedPlacesCache{TRange,TData,TDomain}"/>.
///
/// Goal: Verify that the extension method behaves correctly end-to-end:
/// - Returns correct data (identical to plain GetDataAsync)
/// - Cache has converged (normalization processed) by the time the method returns
/// - Works across both storage strategies
/// - Cancellation and disposal integrate correctly
/// </summary>
public sealed class StrongConsistencyModeTests : IAsyncDisposable
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

    private VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateCache(
        StorageStrategyOptions<int, int>? strategy = null)
    {
        _cache = TestHelpers.CreateCacheWithSimpleSource(
            _domain, _diagnostics, TestHelpers.CreateDefaultOptions(strategy));
        return _cache;
    }

    public static IEnumerable<object[]> StorageStrategyTestData =>
    [
        [SnapshotAppendBufferStorageOptions<int, int>.Default],
        [LinkedListStrideIndexStorageOptions<int, int>.Default]
    ];

    // ============================================================
    // DATA CORRECTNESS
    // ============================================================

    /// <summary>
    /// Verifies GetDataAndWaitForIdleAsync returns correct data across both storage strategies.
    /// </summary>
    [Theory]
    [MemberData(nameof(StorageStrategyTestData))]
    public async Task GetDataAndWaitForIdleAsync_ReturnsCorrectData(
        StorageStrategyOptions<int, int> strategy)
    {
        // ARRANGE
        var cache = CreateCache(strategy);
        var range = TestHelpers.CreateRange(100, 110);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT
        Assert.NotNull(result.Range);
        TestHelpers.AssertUserDataCorrect(result.Data, range);
    }

    /// <summary>
    /// Verifies the result from GetDataAndWaitForIdleAsync is identical to plain GetDataAsync
    /// for the same warm cache (result passthrough fidelity).
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_ResultIdenticalToGetDataAsync()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(100, 110);

        // Warm the cache with plain GetDataAsync
        var regularResult = await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ACT — use strong consistency for same range (will be a full hit)
        var strongResult = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT — data content is identical
        Assert.Equal(regularResult.Range, strongResult.Range);
        Assert.Equal(regularResult.Data.Length, strongResult.Data.Length);
        Assert.True(regularResult.Data.Span.SequenceEqual(strongResult.Data.Span));
    }

    /// <summary>
    /// Verifies correct data is returned on cold start (first request must fetch from data source).
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_ColdStart_DataCorrect()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(200, 220);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT
        Assert.NotNull(result.Range);
        TestHelpers.AssertUserDataCorrect(result.Data, range);
    }

    // ============================================================
    // CONVERGENCE GUARANTEE
    // ============================================================

    /// <summary>
    /// After GetDataAndWaitForIdleAsync returns, the background normalization loop
    /// has processed at least one request — proving full convergence occurred.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_CacheHasConvergedAfterReturn()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(100, 110);

        // ACT
        await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT — normalization was processed (background ran to idle)
        Assert.True(_diagnostics.NormalizationRequestProcessed >= 1,
            "Background normalization must have processed at least one request after GetDataAndWaitForIdleAsync.");
    }

    /// <summary>
    /// After GetDataAndWaitForIdleAsync, a re-request of the same range is served
    /// as a full cache hit — the segment was stored during convergence.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_SubsequentRequestIsFullCacheHit()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(100, 110);

        // ACT — prime with strong consistency
        await cache.GetDataAndWaitForIdleAsync(range);

        // Reset to observe only the next request
        _diagnostics.Reset();

        // Re-request same range
        var result = await cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT — served from cache (full hit, no data source call)
        Assert.Equal(1, _diagnostics.UserRequestFullCacheHit);
        Assert.Equal(0, _diagnostics.DataSourceFetchGap);
        TestHelpers.AssertUserDataCorrect(result.Data, range);
    }

    // ============================================================
    // SEQUENTIAL REQUESTS
    // ============================================================

    /// <summary>
    /// Sequential GetDataAndWaitForIdleAsync calls return correct data for all ranges.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_SequentialRequests_EachReturnsConvergedState()
    {
        // ARRANGE
        var cache = CreateCache();
        var ranges = new[]
        {
            TestHelpers.CreateRange(100, 110),
            TestHelpers.CreateRange(200, 210),
            TestHelpers.CreateRange(300, 310),
        };

        // ACT & ASSERT
        foreach (var range in ranges)
        {
            var result = await cache.GetDataAndWaitForIdleAsync(range);
            Assert.NotNull(result.Range);
            TestHelpers.AssertUserDataCorrect(result.Data, range);
        }
    }

    // ============================================================
    // CANCELLATION
    // ============================================================

    /// <summary>
    /// A pre-cancelled token causes graceful degradation: either the result is returned
    /// anyway (if GetDataAsync completes before observing cancellation) or an
    /// OperationCanceledException is thrown — never a hang or crash.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_PreCancelledToken_ReturnsResultGracefully()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(100, 110);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // ACT
        var exception = await Record.ExceptionAsync(
            async () => await cache.GetDataAndWaitForIdleAsync(range, cts.Token));

        // ASSERT — graceful degradation: either no exception or OperationCanceledException
        if (exception is not null)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
        }
    }

    // ============================================================
    // POST-DISPOSAL
    // ============================================================

    /// <summary>
    /// Calling GetDataAndWaitForIdleAsync on a disposed cache throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // ARRANGE
        var cache = CreateCache();
        await cache.DisposeAsync();
        _cache = null; // prevent double-dispose in DisposeAsync

        var range = TestHelpers.CreateRange(100, 110);

        // ACT
        var exception = await Record.ExceptionAsync(
            async () => await cache.GetDataAndWaitForIdleAsync(range, CancellationToken.None));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ObjectDisposedException>(exception);
    }

    // ============================================================
    // EDGE CASES
    // ============================================================

    /// <summary>
    /// Single-element range is returned correctly with strong consistency.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_SingleElementRange_DataCorrect()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(42, 42);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT
        Assert.NotNull(result.Range);
        Assert.Single(result.Data.ToArray());
        Assert.Equal(42, result.Data.ToArray()[0]);
    }

    /// <summary>
    /// Large range is handled correctly and cache converges.
    /// </summary>
    [Fact]
    public async Task GetDataAndWaitForIdleAsync_LargeRange_DataCorrectAndConverged()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(0, 499);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT
        Assert.NotNull(result.Range);
        Assert.Equal(500, result.Data.Length);
        TestHelpers.AssertUserDataCorrect(result.Data, range);
        Assert.True(_diagnostics.NormalizationRequestProcessed >= 1);
    }
}
