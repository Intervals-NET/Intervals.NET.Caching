using Intervals.NET.Caching.Infrastructure.Scheduling.Supersession;

namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Abstraction for serial work schedulers that implement supersession semantics:
/// when a new work item is published, the previous item is automatically cancelled and replaced.
/// Exposes the most recently published work item for pending-state inspection.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
/// <remarks>
/// <para><strong>Supersession Contract:</strong></para>
/// <para>
/// Every call to <see cref="IWorkScheduler{TWorkItem}.PublishWorkItemAsync"/> automatically
/// cancels the previously published work item (if any) before enqueuing the new one.
/// The scheduler calls <see cref="ISchedulableWorkItem.Cancel"/> on the previous item, signalling
/// early exit from debounce or in-progress I/O. Only the latest work item is the intended
/// pending work; all earlier items are considered superseded.
/// </para>
/// <para><strong>Cancel-Previous Ownership:</strong></para>
/// <para>
/// Cancellation of the previous item is the <strong>scheduler's responsibility</strong>, not the
/// caller's. Callers must NOT call <see cref="ISchedulableWorkItem.Cancel"/> on the previous item
/// before publishing a new one — the scheduler handles this atomically inside
/// <see cref="IWorkScheduler{TWorkItem}.PublishWorkItemAsync"/>. Callers may still read
/// <see cref="LastWorkItem"/> before publishing to inspect the pending desired state
/// (e.g. for anti-thrashing decisions), but must not cancel it themselves.
/// </para>
/// <para><strong>LastWorkItem — Pending-State Inspection:</strong></para>
/// <para>
/// <see cref="LastWorkItem"/> enables callers to inspect the pending desired state of the
/// most recently enqueued work item before publishing a new one. This is used, for example,
/// by <c>IntentController</c> to read <c>DesiredNoRebalanceRange</c> from the last
/// <c>ExecutionRequest</c> for anti-thrashing decisions in the <c>RebalanceDecisionEngine</c>.
/// The scheduler automatically supersedes that item when the new one is published.
/// </para>
/// <para><strong>Single-Writer Guarantee (inherited):</strong></para>
/// <para>
/// As an extension of <see cref="ISerialWorkScheduler{TWorkItem}"/>, all implementations
/// MUST guarantee serialized (one-at-a-time) execution: no two work items may execute
/// concurrently. This is the foundational invariant that allows consumers (such as
/// SlidingWindow's <c>RebalanceExecutor</c>) to perform single-writer mutations without locks.
/// </para>
/// <para><strong>Implementations:</strong></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="UnboundedSupersessionWorkScheduler{TWorkItem}"/> —
/// Unbounded task chaining with supersession; lightweight, default recommendation for most scenarios.
/// </description></item>
/// <item><description>
/// <see cref="BoundedSupersessionWorkScheduler{TWorkItem}"/> —
/// Bounded channel with backpressure and supersession; for high-frequency or resource-constrained scenarios.
/// </description></item>
/// </list>
/// <para><strong>Hierarchy:</strong></para>
/// <code>
/// IWorkScheduler&lt;TWorkItem&gt;
///   └── ISerialWorkScheduler&lt;TWorkItem&gt;       — single-writer serialization guarantee
///         └── ISupersessionWorkScheduler&lt;TWorkItem&gt; — adds cancel-previous + LastWorkItem
/// </code>
/// </remarks>
internal interface ISupersessionWorkScheduler<TWorkItem> : ISerialWorkScheduler<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
    /// <summary>
    /// Gets the most recently published work item, or <see langword="null"/> if none has been published yet.
    /// </summary>
    /// <remarks>
    /// <para><strong>Usage:</strong></para>
    /// <para>
    /// Callers (e.g. <c>IntentController</c>) read this before publishing a new item to inspect
    /// the pending desired state (e.g. <c>DesiredNoRebalanceRange</c>) for anti-thrashing decisions.
    /// The scheduler automatically cancels this item when a new one is published —
    /// callers must NOT cancel it themselves.
    /// </para>
    /// <para><strong>Thread Safety:</strong></para>
    /// <para>Implementations use <c>Volatile.Read</c> to ensure cross-thread visibility.</para>
    /// </remarks>
    TWorkItem? LastWorkItem { get; }
}
