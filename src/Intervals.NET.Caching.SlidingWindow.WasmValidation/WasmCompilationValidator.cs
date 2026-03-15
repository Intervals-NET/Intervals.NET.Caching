using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.Extensions;
using Intervals.NET.Caching.Layered;
using Intervals.NET.Caching.SlidingWindow.Public.Cache;
using Intervals.NET.Caching.SlidingWindow.Public.Configuration;
using Intervals.NET.Caching.SlidingWindow.Public.Extensions;

namespace Intervals.NET.Caching.SlidingWindow.WasmValidation;

/// <summary>
/// Minimal IDataSource implementation for WebAssembly compilation validation.
/// This is NOT a demo or test - it exists purely to ensure the library compiles for net8.0-browser.
/// </summary>
internal sealed class SimpleDataSource : IDataSource<int, int>
{
    public Task<RangeChunk<int, int>> FetchAsync(Range<int> range, CancellationToken cancellationToken)
    {
        // Generate deterministic sequential data for the range
        // Range.Start and Range.End are RangeValue<int>, use implicit conversion to int
        var start = range.Start.Value;
        var end = range.End.Value;
        var data = Enumerable.Range(start, end - start + 1).ToArray();
        return Task.FromResult(new RangeChunk<int, int>(range, data));
    }

    public Task<IEnumerable<RangeChunk<int, int>>> FetchAsync(
        IEnumerable<Range<int>> ranges,
        CancellationToken cancellationToken
    )
    {
        var chunks = ranges.Select(r =>
        {
            var start = r.Start.Value;
            var end = r.End.Value;
            return new RangeChunk<int, int>(r, Enumerable.Range(start, end - start + 1).ToArray());
        }).ToArray();
        return Task.FromResult<IEnumerable<RangeChunk<int, int>>>(chunks);
    }
}

/// <summary>
/// WebAssembly compilation validator for Intervals.NET.Caching.SlidingWindow.
/// Validates all internal strategy combinations (ReadMode × RebalanceQueueCapacity) and opt-in
/// consistency modes compile for net8.0-browser. Compilation success is the validation; not intended to be executed.
/// </summary>
public static class WasmCompilationValidator
{
    /// <summary>Validates Configuration 1: SnapshotReadStorage + Task-based serialization.</summary>
    // Strategy: SnapshotReadStorage (array-based) + Task-based serialization (unbounded queue)
    public static async Task ValidateConfiguration1_SnapshotMode_UnboundedQueue()
    {
        var dataSource = new SimpleDataSource();
        var domain = new IntegerFixedStepDomain();

        var options = new SlidingWindowCacheOptions(
            leftCacheSize: 1.0,
            rightCacheSize: 1.0,
            readMode: UserCacheReadMode.Snapshot,
            leftThreshold: 0.2,
            rightThreshold: 0.2,
            rebalanceQueueCapacity: null  // Task-based serialization
        );

        var cache = new SlidingWindowCache<int, int, IntegerFixedStepDomain>(
            dataSource,
            domain,
            options
        );

        var range = Factories.Range.Closed<int>(0, 10);
        var result = await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();
        _ = result.Data.Length;
    }

    /// <summary>Validates Configuration 2: CopyOnReadStorage + Task-based serialization.</summary>
    // Strategy: CopyOnReadStorage (List-based) + Task-based serialization (unbounded queue)
    public static async Task ValidateConfiguration2_CopyOnReadMode_UnboundedQueue()
    {
        var dataSource = new SimpleDataSource();
        var domain = new IntegerFixedStepDomain();

        var options = new SlidingWindowCacheOptions(
            leftCacheSize: 1.0,
            rightCacheSize: 1.0,
            readMode: UserCacheReadMode.CopyOnRead,
            leftThreshold: 0.2,
            rightThreshold: 0.2,
            rebalanceQueueCapacity: null  // Task-based serialization
        );

        var cache = new SlidingWindowCache<int, int, IntegerFixedStepDomain>(
            dataSource,
            domain,
            options
        );

        var range = Factories.Range.Closed<int>(0, 10);
        var result = await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();
        _ = result.Data.Length;
    }

    /// <summary>Validates Configuration 3: SnapshotReadStorage + Channel-based serialization.</summary>
    // Strategy: SnapshotReadStorage (array-based) + Channel-based serialization (bounded queue)
    public static async Task ValidateConfiguration3_SnapshotMode_BoundedQueue()
    {
        var dataSource = new SimpleDataSource();
        var domain = new IntegerFixedStepDomain();

        var options = new SlidingWindowCacheOptions(
            leftCacheSize: 1.0,
            rightCacheSize: 1.0,
            readMode: UserCacheReadMode.Snapshot,
            leftThreshold: 0.2,
            rightThreshold: 0.2,
            rebalanceQueueCapacity: 5  // Channel-based serialization
        );

        var cache = new SlidingWindowCache<int, int, IntegerFixedStepDomain>(
            dataSource,
            domain,
            options
        );

        var range = Factories.Range.Closed<int>(0, 10);
        var result = await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();
        _ = result.Data.Length;
    }

    /// <summary>Validates Configuration 4: CopyOnReadStorage + Channel-based serialization.</summary>
    // Strategy: CopyOnReadStorage (List-based) + Channel-based serialization (bounded queue)
    public static async Task ValidateConfiguration4_CopyOnReadMode_BoundedQueue()
    {
        var dataSource = new SimpleDataSource();
        var domain = new IntegerFixedStepDomain();

        var options = new SlidingWindowCacheOptions(
            leftCacheSize: 1.0,
            rightCacheSize: 1.0,
            readMode: UserCacheReadMode.CopyOnRead,
            leftThreshold: 0.2,
            rightThreshold: 0.2,
            rebalanceQueueCapacity: 5  // Channel-based serialization
        );

        var cache = new SlidingWindowCache<int, int, IntegerFixedStepDomain>(
            dataSource,
            domain,
            options
        );

        var range = Factories.Range.Closed<int>(0, 10);
        var result = await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();
        _ = result.Data.Length;
    }

    /// <summary>
    /// Validates strong consistency mode (<see cref="RangeCacheConsistencyExtensions.GetDataAndWaitForIdleAsync{TRange,TData,TDomain}"/>)
    /// compiles for net8.0-browser, including the cancellation graceful degradation path.
    /// </summary>
    // One configuration is sufficient: this extension introduces no new strategy axes.
    public static async Task ValidateStrongConsistencyMode_GetDataAndWaitForIdleAsync()
    {
        var dataSource = new SimpleDataSource();
        var domain = new IntegerFixedStepDomain();

        var options = new SlidingWindowCacheOptions(
            leftCacheSize: 1.0,
            rightCacheSize: 1.0,
            readMode: UserCacheReadMode.Snapshot,
            leftThreshold: 0.2,
            rightThreshold: 0.2
        );

        var cache = new SlidingWindowCache<int, int, IntegerFixedStepDomain>(
            dataSource,
            domain,
            options
        );

        var range = Factories.Range.Closed<int>(0, 10);

        // Normal path: waits for idle and returns the result
        var result = await cache.GetDataAndWaitForIdleAsync(range, CancellationToken.None);
        _ = result.Data.Length;
        _ = result.CacheInteraction;

        // Cancellation graceful degradation path: pre-cancelled token; WaitForIdleAsync
        // throws OperationCanceledException which is caught — result returned gracefully
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var degradedResult = await cache.GetDataAndWaitForIdleAsync(range, cts.Token);
        _ = degradedResult.Data.Length;
        _ = degradedResult.CacheInteraction;
    }

    /// <summary>
    /// Validates hybrid consistency mode (<see cref="SlidingWindowCacheConsistencyExtensions.GetDataAndWaitOnMissAsync{TRange,TData,TDomain}"/>)
    /// compiles for net8.0-browser, including FullHit, FullMiss, and cancellation graceful degradation paths.
    /// </summary>
    // One configuration is sufficient: this extension introduces no new strategy axes.
    public static async Task ValidateHybridConsistencyMode_GetDataAndWaitOnMissAsync()
    {
        var dataSource = new SimpleDataSource();
        var domain = new IntegerFixedStepDomain();

        var options = new SlidingWindowCacheOptions(
            leftCacheSize: 1.0,
            rightCacheSize: 1.0,
            readMode: UserCacheReadMode.Snapshot,
            leftThreshold: 0.2,
            rightThreshold: 0.2
        );

        var cache = new SlidingWindowCache<int, int, IntegerFixedStepDomain>(
            dataSource,
            domain,
            options
        );

        var range = Factories.Range.Closed<int>(0, 10);

        // FullMiss path (first request — cold cache): idle wait is triggered
        var missResult = await cache.GetDataAndWaitOnMissAsync(range, CancellationToken.None);
        _ = missResult.Data.Length;
        _ = missResult.CacheInteraction; // FullMiss

        // FullHit path (warm cache): no idle wait, returns immediately
        var hitResult = await cache.GetDataAndWaitOnMissAsync(range, CancellationToken.None);
        _ = hitResult.Data.Length;
        _ = hitResult.CacheInteraction; // FullHit

        // Cancellation graceful degradation path: pre-cancelled token on a miss scenario;
        // WaitForIdleAsync throws OperationCanceledException which is caught — result returned gracefully
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var degradedResult = await cache.GetDataAndWaitOnMissAsync(range, cts.Token);
        _ = degradedResult.Data.Length;
        _ = degradedResult.CacheInteraction;
    }

    /// <summary>
    /// Validates layered cache (<see cref="LayeredRangeCacheBuilder{TRange,TData,TDomain}"/>,
    /// <see cref="RangeCacheDataSourceAdapter{TRange,TData,TDomain}"/>, <see cref="LayeredRangeCache{TRange,TData,TDomain}"/>)
    /// compiles for net8.0-browser. Uses recommended config: CopyOnRead inner + Snapshot outer.
    /// </summary>
    // One method sufficient: layered types introduce no new strategy axes beyond Configurations 1–4.
    public static async Task ValidateLayeredCache_TwoLayer_RecommendedConfig()
    {
        var domain = new IntegerFixedStepDomain();

        // Inner layer: CopyOnRead + large buffers (recommended for deep/backing layers)
        var innerOptions = new SlidingWindowCacheOptions(
            leftCacheSize: 5.0,
            rightCacheSize: 5.0,
            readMode: UserCacheReadMode.CopyOnRead,
            leftThreshold: 0.3,
            rightThreshold: 0.3
        );

        // Outer (user-facing) layer: Snapshot + small buffers (recommended for user-facing layer)
        var outerOptions = new SlidingWindowCacheOptions(
            leftCacheSize: 0.5,
            rightCacheSize: 0.5,
            readMode: UserCacheReadMode.Snapshot,
            leftThreshold: 0.2,
            rightThreshold: 0.2
        );

        await using var layered = (LayeredRangeCache<int, int, IntegerFixedStepDomain>)await SlidingWindowCacheBuilder.Layered(new SimpleDataSource(), domain)
            .AddSlidingWindowLayer(innerOptions)
            .AddSlidingWindowLayer(outerOptions)
            .BuildAsync();

        var range = Factories.Range.Closed<int>(0, 10);
        var result = await layered.GetDataAsync(range, CancellationToken.None);
        await layered.WaitForIdleAsync();

        _ = result.Data.Length;
        _ = layered.LayerCount;
    }
}
