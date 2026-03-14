namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Tracks whether an eviction constraint is satisfied. Updated incrementally as segments
/// are removed during eviction execution.
/// See docs/visited-places/ for design details.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
public interface IEvictionPressure<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Gets whether the constraint is currently violated and more segments need to be removed.
    /// </summary>
    bool IsExceeded { get; }

    /// <summary>
    /// Updates the pressure state to account for the removal of <paramref name="removedSegment"/>.
    /// Called by the executor after each segment is removed from storage.
    /// </summary>
    /// <param name="removedSegment">The segment that was just removed from storage.</param>
    void Reduce(CachedSegment<TRange, TData> removedSegment);
}
