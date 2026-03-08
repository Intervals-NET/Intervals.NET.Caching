namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Immutable configuration options for <see cref="Cache.VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// All properties are validated in the constructor and are immutable after construction.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>All options are construction-time only.</strong> There are no runtime-updatable
/// options on the visited places cache. Construct a new cache instance to change configuration.</para>
/// <para><strong>Storage strategy</strong> is specified by passing a typed options object
/// (e.g., <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}"/> or
/// <see cref="LinkedListStrideIndexStorageOptions{TRange,TData}"/>) via
/// <see cref="StorageStrategy"/>. The options object carries both the tuning parameters and
/// the responsibility for constructing the storage implementation.</para>
/// <para><strong>Eviction configuration</strong> is supplied separately via
/// <see cref="Cache.VisitedPlacesCacheBuilder{TRange,TData,TDomain}.WithEviction"/>, not here.
/// This keeps storage strategy and eviction concerns cleanly separated.</para>
/// </remarks>
public sealed class VisitedPlacesCacheOptions<TRange, TData> : IEquatable<VisitedPlacesCacheOptions<TRange, TData>>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// The storage strategy used for the internal segment collection.
    /// Defaults to <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}.Default"/>.
    /// </summary>
    public StorageStrategyOptions<TRange, TData> StorageStrategy { get; }

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
    /// Initializes a new <see cref="VisitedPlacesCacheOptions{TRange,TData}"/> with the specified values.
    /// </summary>
    /// <param name="storageStrategy">
    /// The storage strategy options object. When <see langword="null"/>, defaults to
    /// <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}.Default"/>.
    /// </param>
    /// <param name="eventChannelCapacity">
    /// The background event channel capacity, or <see langword="null"/> (default) to use
    /// unbounded task-chaining scheduling. Must be &gt;= 1 when non-null.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="eventChannelCapacity"/> is non-null and less than 1.
    /// </exception>
    public VisitedPlacesCacheOptions(
        StorageStrategyOptions<TRange, TData>? storageStrategy = null,
        int? eventChannelCapacity = null)
    {
        if (eventChannelCapacity is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventChannelCapacity),
                "EventChannelCapacity must be greater than or equal to 1 when specified.");
        }

        StorageStrategy = storageStrategy ?? SnapshotAppendBufferStorageOptions<TRange, TData>.Default;
        EventChannelCapacity = eventChannelCapacity;
    }

    /// <inheritdoc/>
    public bool Equals(VisitedPlacesCacheOptions<TRange, TData>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return StorageStrategy.Equals(other.StorageStrategy)
               && EventChannelCapacity == other.EventChannelCapacity;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is VisitedPlacesCacheOptions<TRange, TData> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(StorageStrategy, EventChannelCapacity);

    /// <summary>Returns <c>true</c> if the two instances are equal.</summary>
    public static bool operator ==(
        VisitedPlacesCacheOptions<TRange, TData>? left,
        VisitedPlacesCacheOptions<TRange, TData>? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Returns <c>true</c> if the two instances are not equal.</summary>
    public static bool operator !=(
        VisitedPlacesCacheOptions<TRange, TData>? left,
        VisitedPlacesCacheOptions<TRange, TData>? right) =>
        !(left == right);
}
