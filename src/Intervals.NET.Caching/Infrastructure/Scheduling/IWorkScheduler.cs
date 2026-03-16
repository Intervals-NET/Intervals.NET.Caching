namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Abstraction for scheduling and executing background work items.
/// See docs/shared/components/infrastructure.md for implementation catalog and design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal interface IWorkScheduler<TWorkItem> : IAsyncDisposable
    where TWorkItem : class, ISchedulableWorkItem
{
    /// <summary>
    /// Publishes a work item to be processed according to the scheduler's dispatch strategy.
    /// </summary>
    /// <param name="workItem">The work item to schedule for execution.</param>
    /// <param name="loopCancellationToken">
    /// Cancellation token from the caller's processing loop.
    /// Used by bounded strategies to unblock a blocked <c>WriteAsync</c> during disposal.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes synchronously for unbounded and concurrent
    /// strategies or asynchronously for bounded strategies when the channel is full.
    /// </returns>
    ValueTask PublishWorkItemAsync(TWorkItem workItem, CancellationToken loopCancellationToken);
}
