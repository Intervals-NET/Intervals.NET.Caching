using Intervals.NET.Caching.Infrastructure.Scheduling.Base;
using Intervals.NET.Caching.Infrastructure.Scheduling.Concurrent;
using Intervals.NET.Caching.Infrastructure.Scheduling.Serial;
using Intervals.NET.Caching.Infrastructure.Scheduling.Supersession;

namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Represents a unit of work that can be scheduled, cancelled, and disposed by a work scheduler.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <para>
/// This interface is the <c>TWorkItem</c> constraint for
/// <see cref="IWorkScheduler{TWorkItem}"/>, <see cref="ISerialWorkScheduler{TWorkItem}"/>,
/// <see cref="ISupersessionWorkScheduler{TWorkItem}"/>,
/// <see cref="WorkSchedulerBase{TWorkItem}"/>, <see cref="SerialWorkSchedulerBase{TWorkItem}"/>,
/// <see cref="UnboundedSerialWorkScheduler{TWorkItem}"/>,
/// <see cref="BoundedSerialWorkScheduler{TWorkItem}"/>,
/// <see cref="UnboundedSupersessionWorkScheduler{TWorkItem}"/>,
/// <see cref="BoundedSupersessionWorkScheduler{TWorkItem}"/>, and
/// <see cref="ConcurrentWorkScheduler{TWorkItem}"/>.
/// It combines the two operations that the scheduler must perform on a work item
/// beyond passing it to the executor:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Cancel"/> — signal early exit to the running or waiting work item</description></item>
/// <item><description><see cref="IDisposable.Dispose"/> — release owned resources (e.g. <see cref="CancellationTokenSource"/>)</description></item>
/// </list>
/// <para><strong>Implementations:</strong></para>
/// <para>
/// SlidingWindow's <c>ExecutionRequest&lt;TRange,TData,TDomain&gt;</c> is the canonical supersession
/// implementation: it owns a <see cref="CancellationTokenSource"/> and supports meaningful
/// <see cref="Cancel"/> (signals the CTS) and <see cref="IDisposable.Dispose"/> (disposes the CTS).
/// VisitedPlacesCache's <c>CacheNormalizationRequest&lt;TRange,TData&gt;</c> is the canonical serial
/// FIFO implementation, where <see cref="Cancel"/> and <see cref="IDisposable.Dispose"/> are
/// intentional no-ops because requests are never cancelled (Invariant VPC.A.11) and own no
/// disposable resources.
/// VisitedPlacesCache's <c>TtlExpirationWorkItem&lt;TRange,TData&gt;</c> is the canonical concurrent
/// implementation, where both methods are intentional no-ops because cancellation is driven by
/// a shared <see cref="CancellationToken"/> passed in at construction.
/// </para>
/// <para><strong>Thread Safety:</strong></para>
/// <para>
/// Both <see cref="Cancel"/> and <see cref="IDisposable.Dispose"/> must be safe to call
/// multiple times and must handle disposal races gracefully (e.g. by catching
/// <see cref="ObjectDisposedException"/>).
/// </para>
/// </remarks>
internal interface ISchedulableWorkItem : IDisposable
{
    /// <summary>
    /// The cancellation token associated with this work item.
    /// Cancelled when <see cref="Cancel"/> is called or when the item is superseded.
    /// Passed to the executor delegate by the scheduler.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Signals this work item to exit early.
    /// Safe to call multiple times and after <see cref="IDisposable.Dispose"/>.
    /// </summary>
    void Cancel();
}
