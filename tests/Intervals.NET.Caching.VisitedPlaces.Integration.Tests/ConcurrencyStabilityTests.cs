using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Concurrency and stress stability tests for <see cref="VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// Validates that the system remains stable under concurrent load without crashes, deadlocks,
/// or data corruption.
///
/// VPC handles concurrency differently from SWC: all I/O is on the User Path (concurrent),
/// while the Background Storage Loop processes one FIFO event at a time. Tests here focus on
/// User Path concurrency safety and correctness.
/// </summary>
public sealed class ConcurrencyStabilityTests : IAsyncDisposable
{
    private readonly IntegerFixedStepDomain _domain = new();
    private readonly SpyDataSource _dataSource = new();
    private readonly EventCounterCacheDiagnostics _diagnostics = new();
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
    // BASIC CONCURRENCY
    // ============================================================

    [Fact]
    public async Task Concurrent_10SimultaneousRequests_AllSucceed()
    {
        // ARRANGE
        var cache = CreateCache();
        const int concurrentRequests = 10;

        // ACT — 10 concurrent requests to different non-overlapping ranges
        var tasks = new List<Task<ReadOnlyMemory<int>>>();
        for (var i = 0; i < concurrentRequests; i++)
        {
            var start = i * 100;
            var range = Factories.Range.Closed<int>(start, start + 20);
            tasks.Add(cache.GetDataAsync(range, CancellationToken.None).AsTask()
                .ContinueWith(t => t.Result.Data));
        }

        var results = await Task.WhenAll(tasks);

        // ASSERT — all requests completed and returned 21 elements each
        Assert.Equal(concurrentRequests, results.Length);
        foreach (var data in results)
        {
            Assert.Equal(21, data.Length);
        }

        Assert.True(_dataSource.TotalFetchCount > 0, "Data source should have been called.");
    }

    [Fact]
    public async Task Concurrent_SameRangeMultipleTimes_NoDeadlock()
    {
        // ARRANGE
        var cache = CreateCache();
        const int concurrentRequests = 20;
        var range = Factories.Range.Closed<int>(100, 120);

        // ACT — 20 concurrent requests for the same range
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => cache.GetDataAsync(range, CancellationToken.None).AsTask())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // ASSERT — all completed, no deadlock
        Assert.Equal(concurrentRequests, results.Length);
        foreach (var result in results)
        {
            var array = result.Data.ToArray();
            Assert.Equal(21, array.Length);
            Assert.Equal(100, array[0]);
            Assert.Equal(120, array[^1]);
        }
    }

    // ============================================================
    // OVERLAPPING RANGES
    // ============================================================

    [Fact]
    public async Task Concurrent_OverlappingRanges_AllDataValid()
    {
        // ARRANGE
        var cache = CreateCache();
        const int concurrentRequests = 15;

        // ACT — overlapping ranges around a center point
        var tasks = new List<Task<ReadOnlyMemory<int>>>();
        for (var i = 0; i < concurrentRequests; i++)
        {
            var offset = i * 5;
            var range = Factories.Range.Closed<int>(100 + offset, 150 + offset);
            tasks.Add(cache.GetDataAsync(range, CancellationToken.None).AsTask()
                .ContinueWith(t => t.Result.Data));
        }

        var results = await Task.WhenAll(tasks);

        // ASSERT — each result has 51 elements with correct starting value
        Assert.Equal(concurrentRequests, results.Length);
        for (var i = 0; i < results.Length; i++)
        {
            var data = results[i];
            Assert.Equal(51, data.Length);
            Assert.Equal(100 + i * 5, data.Span[0]);
        }
    }

    // ============================================================
    // HIGH VOLUME STRESS
    // ============================================================

    [Fact]
    public async Task HighVolume_100SequentialRequests_NoErrors()
    {
        // ARRANGE
        var cache = CreateCache();

        const int requestCount = 100;
        var exceptions = new List<Exception>();

        // ACT — non-overlapping sequential ranges; default AppendBufferSize (8) triggers ~12
        // normalization cycles during the 100 requests, actively exercising the Normalize()
        // / FindIntersecting() concurrent path.
        for (var i = 0; i < requestCount; i++)
        {
            try
            {
                var start = i * 20;
                var range = Factories.Range.Closed<int>(start, start + 9);
                var result = await cache.GetDataAsync(range, CancellationToken.None);
                Assert.Equal(10, result.Data.Length);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // ASSERT
        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task HighVolume_50ConcurrentBursts_SystemStable()
    {
        // ARRANGE
        var cache = CreateCache();
        const int burstSize = 50;

        // ACT — burst of concurrent requests with some overlap
        var tasks = new List<Task<ReadOnlyMemory<int>>>();
        for (var i = 0; i < burstSize; i++)
        {
            var start = (i % 10) * 50;
            var range = Factories.Range.Closed<int>(start, start + 25);
            tasks.Add(cache.GetDataAsync(range, CancellationToken.None).AsTask()
                .ContinueWith(t => t.Result.Data));
        }

        var results = await Task.WhenAll(tasks);

        // ASSERT — all results are non-empty with correct length
        Assert.Equal(burstSize, results.Length);
        Assert.All(results, r => Assert.Equal(26, r.Length));
    }

    // ============================================================
    // DATA INTEGRITY
    // ============================================================

    [Fact]
    public async Task DataIntegrity_ConcurrentReads_AllDataCorrect()
    {
        // ARRANGE — warm the cache first with the base range
        var cache = CreateCache();
        var baseRange = Factories.Range.Closed<int>(500, 600);
        await cache.GetDataAsync(baseRange, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ACT — many concurrent reads of overlapping sub-ranges
        const int concurrentReaders = 25;
        var tasks = new List<Task<(int length, int firstValue, int expectedFirst)>>();

        for (var i = 0; i < concurrentReaders; i++)
        {
            var offset = i * 4;
            var expectedFirst = 500 + offset;
            tasks.Add(Task.Run(async () =>
            {
                var range = Factories.Range.Closed<int>(500 + offset, 550 + offset);
                var data = await cache.GetDataAsync(range, CancellationToken.None);
                return (data.Data.Length, data.Data.Span[0], expectedFirst);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // ASSERT — no data corruption; each result matches expected first value
        foreach (var (length, firstValue, expectedFirst) in results)
        {
            Assert.Equal(51, length);
            Assert.Equal(expectedFirst, firstValue);
        }

        // ASSERT — all fetch calls used valid ranges
        var allRanges = _dataSource.GetAllRequestedRanges();
        Assert.All(allRanges, range =>
        {
            Assert.True((int)range.Start <= (int)range.End,
                "No data races should produce invalid ranges.");
        });
    }

    // ============================================================
    // CANCELLATION UNDER LOAD
    // ============================================================

    [Fact]
    public async Task CancellationUnderLoad_SystemStableWithCancellations()
    {
        // ARRANGE
        var cache = CreateCache();
        const int requestCount = 30;
        var ctsList = new List<CancellationTokenSource>();

        // ACT — mix of normal and cancellable requests
        var tasks = new List<Task<bool>>();
        for (var i = 0; i < requestCount; i++)
        {
            var cts = new CancellationTokenSource();
            ctsList.Add(cts);

            var start = i * 10;
            var range = Factories.Range.Closed<int>(start, start + 15);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await cache.GetDataAsync(range, cts.Token);
                    return true; // success
                }
                catch (OperationCanceledException)
                {
                    return false; // cancelled
                }
            }, CancellationToken.None));

            // Cancel some requests with a short delay
            if (i % 5 == 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5, CancellationToken.None);
                    await cts.CancelAsync();
                }, CancellationToken.None);
            }
        }

        var results = await Task.WhenAll(tasks);

        // ASSERT — at least some requests succeeded; system did not crash
        var successCount = results.Count(r => r);
        Assert.True(successCount > 0, "At least some requests should succeed.");

        // Cleanup
        foreach (var cts in ctsList)
        {
            cts.Dispose();
        }
    }

    // ============================================================
    // EVICTION UNDER CONCURRENCY
    // ============================================================

    [Fact]
    public async Task Concurrent_WithEvictionPressure_SystemStable()
    {
        // ARRANGE — very low maxSegmentCount forces frequent eviction
        var cache = CreateCache(maxSegmentCount: 3);
        const int concurrentRequests = 20;

        // ACT — concurrent requests to non-overlapping ranges, each creating a new segment
        var tasks = new List<Task>();
        for (var i = 0; i < concurrentRequests; i++)
        {
            var start = i * 100;
            var range = Factories.Range.Closed<int>(start, start + 9);
            tasks.Add(cache.GetDataAsync(range, CancellationToken.None).AsTask());
        }

        await Task.WhenAll(tasks);
        await cache.WaitForIdleAsync();

        // ASSERT — no crashes; diagnostics lifecycle is consistent
        TestHelpers.AssertBackgroundLifecycleIntegrity(_diagnostics);
        TestHelpers.AssertNoBackgroundFailures(_diagnostics);
    }
}
