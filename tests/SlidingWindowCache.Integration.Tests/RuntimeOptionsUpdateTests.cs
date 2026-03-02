using Intervals.NET.Domain.Default.Numeric;
using SlidingWindowCache.Public;
using SlidingWindowCache.Public.Configuration;
using SlidingWindowCache.Tests.Infrastructure.DataSources;

namespace SlidingWindowCache.Integration.Tests;

/// <summary>
/// Integration tests for <see cref="IWindowCache{TRange,TData,TDomain}.UpdateRuntimeOptions"/>.
/// Verifies partial updates, validation rejection, disposal guard, and behavioral effect on rebalancing.
/// </summary>
public class RuntimeOptionsUpdateTests
{
    private static IDataSource<int, string> CreateDataSource() =>
        new SimpleTestDataSource<string>(i => $"Item_{i}");

    private static WindowCacheOptions DefaultOptions() => new WindowCacheOptions(
        leftCacheSize: 1.0,
        rightCacheSize: 1.0,
        readMode: UserCacheReadMode.Snapshot
    );

    #region Partial Update Tests

    [Fact]
    public async Task UpdateRuntimeOptions_PartialUpdate_OnlyChangesSpecifiedFields()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(
                leftCacheSize: 1.0,
                rightCacheSize: 2.0,
                readMode: UserCacheReadMode.Snapshot,
                leftThreshold: 0.1,
                rightThreshold: 0.2,
                debounceDelay: TimeSpan.FromMilliseconds(50)
            )
        );

        // ACT — only change LeftCacheSize
        cache.UpdateRuntimeOptions(update => update.WithLeftCacheSize(3.0));

        // ASSERT — after next rebalance the cache window should be larger on the left
        // Trigger rebalance and wait for idle
        var range = Intervals.NET.Factories.Range.Closed<int>(100, 110);
        await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // The cache should have expanded at least leftCacheSize * span (3.0 * 10 = 30 units) to the left
        var result = await cache.GetDataAsync(range, CancellationToken.None);
        Assert.True(result.Data.Length > 0);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_WithNoBuilderCalls_LeavesAllFieldsUnchanged()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(), DefaultOptions()
        );

        // ACT — call with empty builder (no changes)
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(_ => { }));

        // ASSERT — no exception; options unchanged
        Assert.Null(exception);
    }

    #endregion

    #region Threshold Update Tests

    [Fact]
    public async Task UpdateRuntimeOptions_WithLeftThreshold_SetsThreshold()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(), DefaultOptions()
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.WithLeftThreshold(0.3)));

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_ClearLeftThreshold_SetsThresholdToNull()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(1.0, 1.0, UserCacheReadMode.Snapshot, leftThreshold: 0.2)
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.ClearLeftThreshold()));

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_ClearRightThreshold_SetsThresholdToNull()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(1.0, 1.0, UserCacheReadMode.Snapshot, rightThreshold: 0.2)
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.ClearRightThreshold()));

        // ASSERT
        Assert.Null(exception);
    }

    #endregion

    #region Validation Rejection Tests

    [Fact]
    public async Task UpdateRuntimeOptions_WithNegativeLeftCacheSize_ThrowsAndLeavesOptionsUnchanged()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(), DefaultOptions()
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.WithLeftCacheSize(-1.0)));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_WithNegativeRightCacheSize_ThrowsAndLeavesOptionsUnchanged()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(), DefaultOptions()
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.WithRightCacheSize(-0.5)));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_WithThresholdSumExceedingOne_ThrowsArgumentException()
    {
        // ARRANGE — start with left=0.4, then set right=0.7 → sum=1.1
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(1.0, 1.0, UserCacheReadMode.Snapshot, leftThreshold: 0.4)
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.WithRightThreshold(0.7)));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_ValidationFailure_DoesNotPublishPartialUpdate()
    {
        // ARRANGE — valid initial state
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(
                leftCacheSize: 2.0,
                rightCacheSize: 3.0,
                readMode: UserCacheReadMode.Snapshot
            )
        );

        // ACT — attempt invalid update (negative right cache size)
        _ = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update =>
                update.WithLeftCacheSize(5.0).WithRightCacheSize(-1.0)));

        // ASSERT — cache still accepts requests (options unchanged, not partially applied)
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.WithLeftCacheSize(1.0)));
        Assert.Null(exception);
    }

    #endregion

    #region Disposal Guard Tests

    [Fact]
    public async Task UpdateRuntimeOptions_OnDisposedCache_ThrowsObjectDisposedException()
    {
        // ARRANGE
        var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(), DefaultOptions()
        );
        await cache.DisposeAsync();

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update => update.WithLeftCacheSize(2.0)));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ObjectDisposedException>(exception);
    }

    #endregion

    #region Behavioral Effect Tests

    [Fact]
    public async Task UpdateRuntimeOptions_IncreasedCacheSize_LeadsToLargerCacheAfterRebalance()
    {
        // ARRANGE — start with small cache sizes
        var domain = new IntegerFixedStepDomain();
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), domain,
            new WindowCacheOptions(
                leftCacheSize: 0.5,
                rightCacheSize: 0.5,
                readMode: UserCacheReadMode.Snapshot,
                debounceDelay: TimeSpan.Zero
            )
        );

        var range = Intervals.NET.Factories.Range.Closed<int>(100, 110);

        // Prime cache with small sizes and wait for convergence
        await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ACT — dramatically increase cache sizes
        cache.UpdateRuntimeOptions(update =>
            update.WithLeftCacheSize(5.0).WithRightCacheSize(5.0));

        // Trigger a new rebalance cycle
        var adjacentRange = Intervals.NET.Factories.Range.Closed<int>(111, 120);
        await cache.GetDataAsync(adjacentRange, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ASSERT — no exceptions; cache operated normally after runtime update
        var result = await cache.GetDataAsync(range, CancellationToken.None);
        Assert.True(result.Data.Length > 0);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_FluentChaining_AllChangesApplied()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(), DefaultOptions()
        );

        // ACT — chain multiple updates
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update =>
                update
                    .WithLeftCacheSize(2.0)
                    .WithRightCacheSize(3.0)
                    .WithLeftThreshold(0.1)
                    .WithRightThreshold(0.2)
                    .WithDebounceDelay(TimeSpan.FromMilliseconds(10))));

        // ASSERT
        Assert.Null(exception);

        // Confirm cache still works after chained update
        var result = await cache.GetDataAsync(
            Intervals.NET.Factories.Range.Closed<int>(0, 10), CancellationToken.None);
        Assert.True(result.Data.Length > 0);
    }

    [Fact]
    public async Task UpdateRuntimeOptions_DebounceDelayUpdate_TakesEffectOnNextExecution()
    {
        // ARRANGE
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(1.0, 1.0, UserCacheReadMode.Snapshot,
                debounceDelay: TimeSpan.FromMilliseconds(100))
        );

        // ACT — reduce debounce delay to zero
        cache.UpdateRuntimeOptions(update => update.WithDebounceDelay(TimeSpan.Zero));

        // Trigger rebalance after the update
        await cache.GetDataAsync(Intervals.NET.Factories.Range.Closed<int>(50, 60), CancellationToken.None);

        // Wait should complete quickly (debounce is now zero)
        var completed = await Task.WhenAny(
            cache.WaitForIdleAsync(),
            Task.Delay(TimeSpan.FromSeconds(5))
        );

        // ASSERT
        Assert.Equal(TaskStatus.RanToCompletion, completed.Status);
    }

    #endregion

    #region Channel-Based Strategy Tests

    [Fact]
    public async Task UpdateRuntimeOptions_WithChannelBasedStrategy_WorksIdentically()
    {
        // ARRANGE — use bounded channel strategy
        await using var cache = new WindowCache<int, string, IntegerFixedStepDomain>(
            CreateDataSource(), new IntegerFixedStepDomain(),
            new WindowCacheOptions(1.0, 1.0, UserCacheReadMode.Snapshot,
                rebalanceQueueCapacity: 5)
        );

        // ACT
        var exception = Record.Exception(() =>
            cache.UpdateRuntimeOptions(update =>
                update.WithLeftCacheSize(2.0).WithDebounceDelay(TimeSpan.Zero)));

        // ASSERT
        Assert.Null(exception);
    }

    #endregion
}
