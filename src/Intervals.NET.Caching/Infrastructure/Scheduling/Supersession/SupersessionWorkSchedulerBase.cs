using Intervals.NET.Caching.Infrastructure.Concurrency;
using Intervals.NET.Caching.Infrastructure.Diagnostics;
using Intervals.NET.Caching.Infrastructure.Scheduling.Base;

namespace Intervals.NET.Caching.Infrastructure.Scheduling.Supersession;

/// <summary>
/// Intermediate base class for supersession work schedulers.
/// Cancels the previous work item when a new one is published, and tracks the last item
/// for pending-state inspection. See docs/shared/components/infrastructure.md for design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal abstract class SupersessionWorkSchedulerBase<TWorkItem>
    : SerialWorkSchedulerBase<TWorkItem>, ISupersessionWorkScheduler<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
    // Supersession state: last published work item.
    // Written via Volatile.Write on every publish (release fence for cross-thread visibility).
    // Read via Volatile.Read in OnBeforeEnqueue, OnBeforeSerialDispose, and LastWorkItem.
    private TWorkItem? _lastWorkItem;

    /// <summary>
    /// Initializes the shared fields.
    /// </summary>
    private protected SupersessionWorkSchedulerBase(
        Func<TWorkItem, CancellationToken, Task> executor,
        Func<TimeSpan> debounceProvider,
        IWorkSchedulerDiagnostics diagnostics,
        AsyncActivityCounter activityCounter,
        TimeProvider? timeProvider = null)
        : base(executor, debounceProvider, diagnostics, activityCounter, timeProvider)
    {
    }

    /// <inheritdoc/>
    public TWorkItem? LastWorkItem => Volatile.Read(ref _lastWorkItem);

    /// <summary>
    /// Cancels the current <see cref="LastWorkItem"/> (if any) and stores the new item
    /// as the last work item before it is enqueued.
    /// </summary>
    /// <param name="workItem">The new work item about to be enqueued.</param>
    private protected sealed override void OnBeforeEnqueue(TWorkItem workItem)
    {
        // Cancel previous item so it can exit early from debounce or I/O.
        Volatile.Read(ref _lastWorkItem)?.Cancel();

        // Store new item as the current last work item (release fence for cross-thread visibility).
        Volatile.Write(ref _lastWorkItem, workItem);
    }

    /// <summary>
    /// Cancels the last work item so it can exit early during disposal.
    /// </summary>
    private protected sealed override void OnBeforeSerialDispose()
    {
        Volatile.Read(ref _lastWorkItem)?.Cancel();
    }
}
