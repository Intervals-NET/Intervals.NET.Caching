using Intervals.NET.Caching.Infrastructure.Scheduling.Concurrent;
using Intervals.NET.Caching.Infrastructure.Scheduling.Serial;
using Intervals.NET.Caching.Infrastructure.Scheduling.Supersession;

namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Abstraction for scheduling and executing background work items.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
/// <remarks>
/// <para><strong>Architectural Role — Cache-Agnostic Work Scheduler:</strong></para>
/// <para>
/// This interface abstracts the mechanism for dispatching and executing background work items.
/// The concrete implementation determines how work items are queued, scheduled,
/// and dispatched — serially (FIFO), with supersession, or concurrently.
/// </para>
/// <para><strong>Implementations:</strong></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="UnboundedSerialWorkScheduler{TWorkItem}"/> —
/// Serialized FIFO execution via unbounded task chaining; lightweight, default for most scenarios.
/// Implements <see cref="ISerialWorkScheduler{TWorkItem}"/>.
/// </description></item>
/// <item><description>
/// <see cref="BoundedSerialWorkScheduler{TWorkItem}"/> —
/// Serialized FIFO execution via bounded channel with backpressure.
/// Implements <see cref="ISerialWorkScheduler{TWorkItem}"/>.
/// </description></item>
/// <item><description>
/// <see cref="UnboundedSupersessionWorkScheduler{TWorkItem}"/> —
/// Serialized execution via unbounded task chaining with automatic cancel-previous supersession.
/// Implements <see cref="ISupersessionWorkScheduler{TWorkItem}"/>.
/// </description></item>
/// <item><description>
/// <see cref="BoundedSupersessionWorkScheduler{TWorkItem}"/> —
/// Serialized execution via bounded channel with backpressure and automatic cancel-previous supersession.
/// Implements <see cref="ISupersessionWorkScheduler{TWorkItem}"/>.
/// </description></item>
/// <item><description>
/// <see cref="ConcurrentWorkScheduler{TWorkItem}"/> —
/// Independent concurrent execution via ThreadPool dispatch; no ordering or exclusion guarantees.
/// </description></item>
/// </list>
/// <para><strong>Serial vs Supersession vs Concurrent:</strong></para>
/// <para>
/// Consumers that require serialized (one-at-a-time) FIFO execution should depend on
/// <see cref="ISerialWorkScheduler{TWorkItem}"/> — a marker interface that expresses the
/// single-writer execution guarantee without adding new members.
/// Consumers that additionally require supersession semantics (latest item wins, previous
/// automatically cancelled) should depend on <see cref="ISupersessionWorkScheduler{TWorkItem}"/>,
/// which extends <see cref="ISerialWorkScheduler{TWorkItem}"/> with <c>LastWorkItem</c> access
/// and the cancel-previous-on-publish contract.
/// </para>
/// <para><strong>Execution Context:</strong></para>
/// <para>
/// All implementations execute work on background threads (ThreadPool). The caller's
/// (user-facing) path is never blocked. The task-based serial implementation enforces this via
/// <c>await Task.Yield()</c> as the very first statement of <c>ChainExecutionAsync</c>,
/// which immediately frees the caller's thread so the entire method body runs on the ThreadPool.
/// </para>
/// </remarks>
internal interface IWorkScheduler<TWorkItem> : IAsyncDisposable
    where TWorkItem : class, ISchedulableWorkItem
{
    /// <summary>
    /// Publishes a work item to be processed according to the scheduler's dispatch strategy.
    /// </summary>
    /// <param name="workItem">The work item to schedule for execution.</param>
    /// <param name="loopCancellationToken">
    /// Cancellation token from the caller's processing loop.
    /// Used by the channel-based strategy to unblock a blocked <c>WriteAsync</c> during disposal.
    /// Other strategies accept the parameter for API consistency but do not use it.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes synchronously for unbounded serial and concurrent
    /// strategies (fire-and-forget) or asynchronously for the bounded serial strategy when the
    /// channel is full (backpressure).
    /// </returns>
    /// <remarks>
    /// <para><strong>Strategy-Specific Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>
    /// <strong>Unbounded Serial / Unbounded Supersession:</strong>
    /// chains the new item to the previous task and returns immediately.
    /// Supersession variant additionally cancels the previous work item before chaining.
    /// </description></item>
    /// <item><description>
    /// <strong>Bounded Serial / Bounded Supersession:</strong>
    /// enqueues the item; awaits <c>WriteAsync</c> if the channel is at capacity, creating
    /// intentional backpressure on the caller's loop.
    /// Supersession variant additionally cancels the previous work item before enqueuing.
    /// </description></item>
    /// <item><description>
    /// <strong>Concurrent (<see cref="ConcurrentWorkScheduler{TWorkItem}"/>):</strong>
    /// dispatches the item to the ThreadPool immediately and returns synchronously.
    /// </description></item>
    /// </list>
    /// </remarks>
    ValueTask PublishWorkItemAsync(TWorkItem workItem, CancellationToken loopCancellationToken);
}
