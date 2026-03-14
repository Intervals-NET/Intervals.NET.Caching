namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// Marker interface for work schedulers that guarantee serialized (one-at-a-time) execution,
/// ensuring single-writer access to shared state.
/// See docs/shared/components/infrastructure.md for implementation catalog and design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal interface ISerialWorkScheduler<TWorkItem> : IWorkScheduler<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
}
