namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Serial work scheduler with supersession semantics: publishing a new work item
/// automatically cancels and replaces the previous one.
/// Exposes the most recently published work item for pending-state inspection.
/// See docs/shared/components/infrastructure.md for design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal interface ISupersessionWorkScheduler<TWorkItem> : ISerialWorkScheduler<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
    /// <summary>
    /// Gets the most recently published work item, or <see langword="null"/> if none has been published yet.
    /// Used for pending-state inspection (e.g. anti-thrashing decisions).
    /// </summary>
    TWorkItem? LastWorkItem { get; }
}
