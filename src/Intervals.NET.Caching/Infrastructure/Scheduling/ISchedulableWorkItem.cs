namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Represents a unit of work that can be scheduled, cancelled, and disposed by a work scheduler.
/// Both <see cref="Cancel"/> and <see cref="IDisposable.Dispose"/> must be safe to call multiple times.
/// </summary>
internal interface ISchedulableWorkItem : IDisposable
{
    /// <summary>
    /// The cancellation token associated with this work item.
    /// Cancelled when <see cref="Cancel"/> is called or when the item is superseded.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Signals this work item to exit early.
    /// Safe to call multiple times and after <see cref="IDisposable.Dispose"/>.
    /// </summary>
    void Cancel();
}
