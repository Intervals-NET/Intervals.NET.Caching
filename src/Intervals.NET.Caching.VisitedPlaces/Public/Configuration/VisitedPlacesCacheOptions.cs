namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Immutable configuration options for <see cref="Cache.VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// All properties are validated in the constructor and are immutable after construction.
/// </summary>
/// <remarks>
/// <para><strong>All options are construction-time only.</strong> There are no runtime-updatable
/// options on the visited places cache. Construct a new cache instance to change configuration.</para>
/// <para><strong>Eviction configuration</strong> is supplied separately via
/// <see cref="Cache.VisitedPlacesCacheBuilder{TRange,TData,TDomain}.WithEviction"/>, not here.
/// This keeps storage strategy and eviction concerns cleanly separated.</para>
/// </remarks>
public sealed class VisitedPlacesCacheOptions : IEquatable<VisitedPlacesCacheOptions>
{
    /// <summary>
    /// The storage strategy used for the internal segment collection.
    /// </summary>
    public StorageStrategy StorageStrategy { get; }

    /// <summary>
    /// The bounded capacity of the internal background event channel, or <see langword="null"/>
    /// to use unbounded task-chaining scheduling instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="null"/> (the default), a <c>TaskBasedWorkScheduler</c> is used:
    /// unbounded, no backpressure, minimal memory overhead — suitable for most scenarios.
    /// </para>
    /// <para>
    /// When set to a positive integer, a <c>ChannelBasedWorkScheduler</c> with that capacity
    /// is used: bounded, applies backpressure to the user path when the queue is full.
    /// Must be &gt;= 1 when non-null.
    /// </para>
    /// </remarks>
    public int? EventChannelCapacity { get; }

    /// <summary>
    /// Initializes a new <see cref="VisitedPlacesCacheOptions"/> with the specified values.
    /// </summary>
    /// <param name="storageStrategy">The storage strategy to use.</param>
    /// <param name="eventChannelCapacity">
    /// The background event channel capacity, or <see langword="null"/> (default) to use
    /// unbounded task-chaining scheduling. Must be &gt;= 1 when non-null.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="eventChannelCapacity"/> is non-null and less than 1.
    /// </exception>
    public VisitedPlacesCacheOptions(
        StorageStrategy storageStrategy = StorageStrategy.SnapshotAppendBuffer,
        int? eventChannelCapacity = null)
    {
        if (eventChannelCapacity is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventChannelCapacity),
                "EventChannelCapacity must be greater than or equal to 1 when specified.");
        }

        StorageStrategy = storageStrategy;
        EventChannelCapacity = eventChannelCapacity;
    }

    /// <inheritdoc/>
    public bool Equals(VisitedPlacesCacheOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return StorageStrategy == other.StorageStrategy
               && EventChannelCapacity == other.EventChannelCapacity;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VisitedPlacesCacheOptions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(StorageStrategy, EventChannelCapacity);

    /// <summary>Returns <c>true</c> if the two instances are equal.</summary>
    public static bool operator ==(VisitedPlacesCacheOptions? left, VisitedPlacesCacheOptions? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Returns <c>true</c> if the two instances are not equal.</summary>
    public static bool operator !=(VisitedPlacesCacheOptions? left, VisitedPlacesCacheOptions? right) =>
        !(left == right);
}
