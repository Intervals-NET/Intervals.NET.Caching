namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Immutable configuration options for <see cref="Cache.VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// All properties are validated in the constructor and are immutable after construction.
/// </summary>
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
    /// to use unbounded task-chaining scheduling instead (the default).
    /// Must be &gt;= 1 when non-null.
    /// </summary>
    public int? EventChannelCapacity { get; }

    /// <summary>
    /// The time-to-live for each cached segment after it is stored, or <see langword="null"/>
    /// to disable TTL-based expiration (the default).
    /// Must be &gt; <see cref="TimeSpan.Zero"/> when non-null.
    /// </summary>
    public TimeSpan? SegmentTtl { get; }

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
    /// <param name="segmentTtl">
    /// The time-to-live for each cached segment, or <see langword="null"/> (default) to disable
    /// TTL expiration. Must be &gt; <see cref="TimeSpan.Zero"/> when non-null.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="eventChannelCapacity"/> is non-null and less than 1,
    /// or when <paramref name="segmentTtl"/> is non-null and &lt;= <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public VisitedPlacesCacheOptions(
        StorageStrategyOptions<TRange, TData>? storageStrategy = null,
        int? eventChannelCapacity = null,
        TimeSpan? segmentTtl = null)
    {
        if (eventChannelCapacity is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventChannelCapacity),
                "EventChannelCapacity must be greater than or equal to 1 when specified.");
        }

        if (segmentTtl is { } ttl && ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(segmentTtl),
                "SegmentTtl must be greater than TimeSpan.Zero when specified.");
        }

        StorageStrategy = storageStrategy ?? SnapshotAppendBufferStorageOptions<TRange, TData>.Default;
        EventChannelCapacity = eventChannelCapacity;
        SegmentTtl = segmentTtl;
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
               && EventChannelCapacity == other.EventChannelCapacity
               && SegmentTtl == other.SegmentTtl;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is VisitedPlacesCacheOptions<TRange, TData> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(StorageStrategy, EventChannelCapacity, SegmentTtl);

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
