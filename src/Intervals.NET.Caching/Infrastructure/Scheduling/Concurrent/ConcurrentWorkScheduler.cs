using Intervals.NET.Caching.Infrastructure.Concurrency;
using Intervals.NET.Caching.Infrastructure.Diagnostics;
using Intervals.NET.Caching.Infrastructure.Scheduling.Base;

namespace Intervals.NET.Caching.Infrastructure.Scheduling.Concurrent;

/// <summary>
/// Concurrent work scheduler that launches each work item independently on the ThreadPool without
/// serialization. See docs/shared/components/infrastructure.md for design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal sealed class ConcurrentWorkScheduler<TWorkItem> : WorkSchedulerBase<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrentWorkScheduler{TWorkItem}"/>.
    /// </summary>
    /// <param name="executor">Delegate that performs the actual work for a given work item.</param>
    /// <param name="debounceProvider">Returns the current debounce delay.</param>
    /// <param name="diagnostics">Diagnostics for work lifecycle events.</param>
    /// <param name="activityCounter">Activity counter for tracking active operations.</param>
    /// <param name="timeProvider">
    /// Time provider for debounce delays. When <see langword="null"/>,
    /// <see cref="TimeProvider.System"/> is used.
    /// </param>
    public ConcurrentWorkScheduler(
        Func<TWorkItem, CancellationToken, Task> executor,
        Func<TimeSpan> debounceProvider,
        IWorkSchedulerDiagnostics diagnostics,
        AsyncActivityCounter activityCounter,
        TimeProvider? timeProvider = null
    ) : base(executor, debounceProvider, diagnostics, activityCounter, timeProvider)
    {
    }

    /// <summary>
    /// Publishes a work item by dispatching it to the ThreadPool independently.
    /// Returns immediately (fire-and-forget). No serialization with previously published items.
    /// </summary>
    /// <param name="workItem">The work item to schedule.</param>
    /// <param name="loopCancellationToken">
    /// Accepted for API consistency; not used by this strategy (never blocks on publishing).
    /// </param>
    /// <returns><see cref="ValueTask.CompletedTask"/> — always completes synchronously.</returns>
    public override ValueTask PublishWorkItemAsync(TWorkItem workItem, CancellationToken loopCancellationToken)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                nameof(ConcurrentWorkScheduler<TWorkItem>),
                "Cannot publish a work item to a disposed scheduler.");
        }

        // Increment activity counter before dispatching.
        ActivityCounter.IncrementActivity();

        // Launch independently via ThreadPool.QueueUserWorkItem.
        // This is used instead of Task.Run / Task.Factory.StartNew for three reasons:
        // 1. It always posts to the ThreadPool (ignores any caller SynchronizationContext),
        //    preserving the concurrent execution guarantee even inside test harnesses that
        //    install a custom SynchronizationContext (e.g. xUnit v2).
        // 2. Unlike ThreadPool.UnsafeQueueUserWorkItem, it captures and flows ExecutionContext,
        //    so diagnostic hooks executing inside the work item have access to AsyncLocal<T>
        //    values — tracing context, culture, activity IDs, etc. — from the publishing caller.
        // 3. It is available on net8.0-browser / WebAssembly, where Task.Run is not suitable
        //    in single-threaded environments.
        ThreadPool.QueueUserWorkItem(
            static state => _ = state.scheduler.ExecuteWorkItemCoreAsync(state.workItem),
            state: (scheduler: this, workItem),
            preferLocal: false);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    private protected override ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}
