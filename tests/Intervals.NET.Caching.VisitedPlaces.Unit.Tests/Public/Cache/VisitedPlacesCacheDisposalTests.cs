using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Public.Cache;

/// <summary>
/// Unit tests for <see cref="VisitedPlacesCache{TRange,TData,TDomain}"/> disposal behavior.
/// Validates proper resource cleanup, idempotency, and post-disposal guard enforcement.
/// </summary>
public sealed class VisitedPlacesCacheDisposalTests
{
    #region Test Infrastructure

    private static IntegerFixedStepDomain Domain => new();

    private static VisitedPlacesCache<int, int, IntegerFixedStepDomain> CreateCache(
        EventCounterCacheDiagnostics? diagnostics = null) =>
        TestHelpers.CreateCacheWithSimpleSource(
            Domain,
            diagnostics ?? new EventCounterCacheDiagnostics(),
            TestHelpers.CreateDefaultOptions());

    #endregion

    #region Basic Disposal Tests

    [Fact]
    public async Task DisposeAsync_WithoutUsage_DisposesSuccessfully()
    {
        // ARRANGE
        var cache = CreateCache();

        // ACT
        var exception = await Record.ExceptionAsync(async () => await cache.DisposeAsync());

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_AfterNormalUsage_DisposesSuccessfully()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(0, 10);

        // ACT — use the cache then dispose
        await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();

        var exception = await Record.ExceptionAsync(async () => await cache.DisposeAsync());

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_WithActiveBackgroundWork_WaitsForCompletion()
    {
        // ARRANGE
        var cache = CreateCache();

        // Trigger background work without waiting for idle
        await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None);
        await cache.GetDataAsync(TestHelpers.CreateRange(100, 110), CancellationToken.None);

        // ACT — dispose immediately while background processing may still be in progress
        var exception = await Record.ExceptionAsync(async () => await cache.DisposeAsync());

        // ASSERT
        Assert.Null(exception);
    }

    #endregion

    #region Idempotency Tests

    [Fact]
    public async Task DisposeAsync_CalledTwiceSequentially_IsIdempotent()
    {
        // ARRANGE
        var cache = CreateCache();

        // ACT
        await cache.DisposeAsync();
        var secondException = await Record.ExceptionAsync(async () => await cache.DisposeAsync());

        // ASSERT
        Assert.Null(secondException);
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_IsIdempotent()
    {
        // ARRANGE
        var cache = CreateCache();

        // ACT
        await cache.DisposeAsync();
        await cache.DisposeAsync();
        await cache.DisposeAsync();
        var fourthException = await Record.ExceptionAsync(async () => await cache.DisposeAsync());

        // ASSERT
        Assert.Null(fourthException);
    }

    [Fact]
    public async Task DisposeAsync_CalledConcurrently_HandlesRaceSafely()
    {
        // ARRANGE
        var cache = CreateCache();

        // ACT — trigger concurrent disposal from 10 threads
        var disposalTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () => await cache.DisposeAsync()))
            .ToList();

        var exceptions = new List<Exception?>();
        foreach (var task in disposalTasks)
        {
            exceptions.Add(await Record.ExceptionAsync(async () => await task));
        }

        // ASSERT — all concurrent disposal attempts succeed
        Assert.All(exceptions, ex => Assert.Null(ex));
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentLoserThread_WaitsForWinnerCompletion()
    {
        // ARRANGE
        var cache = CreateCache();
        await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None);

        // ACT — start two concurrent disposals simultaneously
        var firstDispose = cache.DisposeAsync().AsTask();
        var secondDispose = cache.DisposeAsync().AsTask();

        var exceptions = await Task.WhenAll(
            Record.ExceptionAsync(async () => await firstDispose),
            Record.ExceptionAsync(async () => await secondDispose));

        // ASSERT — both complete without exception (loser waits for winner)
        Assert.All(exceptions, ex => Assert.Null(ex));
    }

    #endregion

    #region Post-Disposal Operation Tests

    [Fact]
    public async Task GetDataAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // ARRANGE
        var cache = CreateCache();
        await cache.DisposeAsync();

        // ACT
        var exception = await Record.ExceptionAsync(
            async () => await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ObjectDisposedException>(exception);
    }

    [Fact]
    public async Task WaitForIdleAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // ARRANGE
        var cache = CreateCache();
        await cache.DisposeAsync();

        // ACT
        var exception = await Record.ExceptionAsync(
            async () => await cache.WaitForIdleAsync());

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ObjectDisposedException>(exception);
    }

    [Fact]
    public async Task MultipleOperations_AfterDisposal_AllThrowObjectDisposedException()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(0, 10);
        await cache.DisposeAsync();

        // ACT
        var getDataException = await Record.ExceptionAsync(
            async () => await cache.GetDataAsync(range, CancellationToken.None));
        var waitIdleException = await Record.ExceptionAsync(
            async () => await cache.WaitForIdleAsync());

        // ASSERT
        Assert.IsType<ObjectDisposedException>(getDataException);
        Assert.IsType<ObjectDisposedException>(waitIdleException);
    }

    #endregion

    #region Using Statement Pattern Tests

    [Fact]
    public async Task UsingStatement_DisposesAutomatically()
    {
        // ARRANGE & ACT
        await using (var cache = CreateCache())
        {
            var data = await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None);
            Assert.Equal(11, data.Data.Length);
        } // DisposeAsync called automatically

        // ASSERT — implicit: no exception thrown during disposal
    }

    [Fact]
    public async Task UsingDeclaration_DisposesAutomatically()
    {
        // ARRANGE & ACT
        await using var cache = CreateCache();
        var data = await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None);

        // ASSERT
        Assert.Equal(11, data.Data.Length);
        // DisposeAsync is called automatically at end of scope
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task DisposeAsync_ImmediatelyAfterConstruction_Succeeds()
    {
        // ARRANGE
        var cache = CreateCache();

        // ACT — dispose without any usage
        var exception = await Record.ExceptionAsync(async () => await cache.DisposeAsync());

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_WhileGetDataAsyncInProgress_CompletesGracefully()
    {
        // ARRANGE
        var cache = CreateCache();
        var range = TestHelpers.CreateRange(0, 10);

        // ACT — start a GetDataAsync without awaiting, then dispose immediately
        var getDataTask = cache.GetDataAsync(range, CancellationToken.None).AsTask();
        await cache.DisposeAsync();

        // Either the fetch completed before disposal or it throws ObjectDisposedException
        var exception = await Record.ExceptionAsync(async () => await getDataTask);

        // ASSERT — either succeeds or throws ObjectDisposedException; nothing else is acceptable
        if (exception != null)
        {
            Assert.IsType<ObjectDisposedException>(exception);
        }
    }

    [Fact]
    public async Task DisposeAsync_StopsBackgroundLoops_SubsequentOperationsThrow()
    {
        // ARRANGE
        var cache = CreateCache();
        await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ACT
        await cache.DisposeAsync();

        // ASSERT — all operations throw ObjectDisposedException after disposal
        var getDataException = await Record.ExceptionAsync(
            async () => await cache.GetDataAsync(TestHelpers.CreateRange(0, 10), CancellationToken.None));
        var waitIdleException = await Record.ExceptionAsync(
            async () => await cache.WaitForIdleAsync());

        Assert.IsType<ObjectDisposedException>(getDataException);
        Assert.IsType<ObjectDisposedException>(waitIdleException);
    }

    #endregion
}
