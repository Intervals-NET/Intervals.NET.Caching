using Intervals.NET.Caching.Infrastructure.Scheduling.Serial;
using Intervals.NET.Caching.Infrastructure.Scheduling.Supersession;

namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Marker abstraction for work schedulers that guarantee serialized (one-at-a-time) execution
/// of work items, ensuring single-writer access to shared state.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
/// <remarks>
/// <para><strong>Architectural Role — Single-Writer Serialization Guarantee:</strong></para>
/// <para>
/// This interface extends <see cref="IWorkScheduler{TWorkItem}"/> with the contract that
/// work items are executed one at a time — no two items may execute concurrently.
/// This serialization guarantee is the foundational invariant that allows consumers to perform
/// mutations on shared state (e.g. cache storage) without additional locking.
/// </para>
/// <para>
/// This is a marker interface: it adds no new members beyond <see cref="IWorkScheduler{TWorkItem}"/>.
/// Its purpose is to enforce type safety — restricting which scheduler implementations may be
/// used in contexts that require the single-writer guarantee, and enabling strategy swapping
/// between <see cref="UnboundedSerialWorkScheduler{TWorkItem}"/> and
/// <see cref="BoundedSerialWorkScheduler{TWorkItem}"/> via a stable interface.
/// </para>
/// <para><strong>Serial vs Supersession:</strong></para>
/// <para>
/// This interface covers FIFO (queue) serial scheduling where every work item is processed
/// in order and none are cancelled or superseded. For supersession semantics — where publishing
/// a new item automatically cancels the previous one — use
/// <see cref="ISupersessionWorkScheduler{TWorkItem}"/> instead, which extends this interface
/// with <c>LastWorkItem</c> access and the cancel-previous-on-publish contract.
/// </para>
/// <para><strong>Implementations:</strong></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="UnboundedSerialWorkScheduler{TWorkItem}"/> —
/// Unbounded task chaining; lightweight, default for most FIFO serial scenarios.
/// </description></item>
/// <item><description>
/// <see cref="BoundedSerialWorkScheduler{TWorkItem}"/> —
/// Bounded channel with backpressure; for high-frequency or resource-constrained FIFO scenarios.
/// </description></item>
/// <item><description>
/// <see cref="UnboundedSupersessionWorkScheduler{TWorkItem}"/> —
/// Unbounded task chaining with cancel-previous supersession.
/// Implements <see cref="ISupersessionWorkScheduler{TWorkItem}"/>.
/// </description></item>
/// <item><description>
/// <see cref="BoundedSupersessionWorkScheduler{TWorkItem}"/> —
/// Bounded channel with backpressure and cancel-previous supersession.
/// Implements <see cref="ISupersessionWorkScheduler{TWorkItem}"/>.
/// </description></item>
/// </list>
/// <para><strong>Hierarchy:</strong></para>
/// <code>
/// IWorkScheduler&lt;TWorkItem&gt;
///   └── ISerialWorkScheduler&lt;TWorkItem&gt;       — single-writer serialization guarantee (this)
///         └── ISupersessionWorkScheduler&lt;TWorkItem&gt; — adds cancel-previous + LastWorkItem
/// </code>
/// </remarks>
internal interface ISerialWorkScheduler<TWorkItem> : IWorkScheduler<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
}
