using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Abstract base class for all storage strategy configuration objects.
/// Carries tuning parameters and constructs the corresponding storage implementation at build time.
/// </summary>
public abstract class StorageStrategyOptions<TRange, TData>
    where TRange : IComparable<TRange>
{
    // Prevent external inheritance outside this assembly while keeping the type public.
    internal StorageStrategyOptions() { }

    /// <summary>
    /// Creates and returns a new <see cref="ISegmentStorage{TRange,TData}"/> instance
    /// configured according to the options on this object.
    /// </summary>
    /// <param name="timeProvider">
    /// The time provider used by the storage for lazy TTL filtering in
    /// <c>FindIntersecting</c> and expiry discovery in <c>TryNormalize</c>.
    /// </param>
    internal abstract ISegmentStorage<TRange, TData> Create(TimeProvider timeProvider);
}
