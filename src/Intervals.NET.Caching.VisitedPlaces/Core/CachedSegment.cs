using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

namespace Intervals.NET.Caching.VisitedPlaces.Core;

/// <summary>
/// Represents a single contiguous cached segment: a range, its data, and optional eviction metadata.
/// See docs/visited-places/ for design details.
/// </summary>
public sealed class CachedSegment<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>The range covered by this segment.</summary>
    public Range<TRange> Range { get; }

    /// <summary>The data stored for this segment.</summary>
    public ReadOnlyMemory<TData> Data { get; }

    /// <summary>
    /// Optional selector-owned eviction metadata. Set and interpreted exclusively by the
    /// configured <see cref="IEvictionSelector{TRange,TData}"/>. <see langword="null"/> when
    /// the selector requires no metadata.
    /// </summary>
    public IEvictionMetadata? EvictionMetadata { get; internal set; }

    // Removal state: 0 = live, 1 = removed.
    // Written via Volatile.Write (MarkAsRemoved) on the Background Path.
    // Read via Volatile.Read (IsRemoved) on both paths.
    private int _isRemoved;

    /// <summary>
    /// Indicates whether this segment has been logically removed from the cache (monotonic flag).
    /// Written on the Background Path via <see cref="MarkAsRemoved"/>; read on both paths.
    /// </summary>
    internal bool IsRemoved => Volatile.Read(ref _isRemoved) != 0;

    /// <summary>
    /// Optional TTL deadline expressed as UTC ticks. <see langword="null"/> means the segment
    /// has no TTL and never expires passively. Set once at creation time by
    /// <c>CacheNormalizationExecutor</c> before the segment is added to storage.
    /// </summary>
    internal long? ExpiresAt { get; init; }

    /// <summary>
    /// Returns <see langword="true"/> when this segment has a TTL and the deadline has passed.
    /// </summary>
    /// <param name="utcNowTicks">Current UTC time as ticks (from <see cref="TimeProvider"/>).</param>
    internal bool IsExpired(long utcNowTicks) => ExpiresAt.HasValue && utcNowTicks >= ExpiresAt.Value;

    /// <summary>
    /// Marks this segment as removed. Called exclusively on the Background Path (single writer) —
    /// either during TTL expiry in <c>TryNormalize</c>, or during eviction in
    /// <c>SegmentStorageBase.Remove</c>. Uses <see cref="Volatile.Write"/> to ensure
    /// the flag is immediately visible to User Path readers.
    /// </summary>
    internal void MarkAsRemoved() =>
        Volatile.Write(ref _isRemoved, 1);

    internal CachedSegment(Range<TRange> range, ReadOnlyMemory<TData> data)
    {
        Range = range;
        Data = data;
    }
}
