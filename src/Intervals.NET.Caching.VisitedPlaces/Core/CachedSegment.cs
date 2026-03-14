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
    // Accessed atomically via Interlocked.CompareExchange (TryMarkAsRemoved) and Volatile.Read (IsRemoved).
    private int _isRemoved;

    /// <summary>
    /// Indicates whether this segment has been logically removed from the cache (monotonic flag).
    /// </summary>
    internal bool IsRemoved => Volatile.Read(ref _isRemoved) != 0;

    /// <summary>
    /// Attempts to atomically transition this segment from live to removed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this call performed the transition;
    /// <see langword="false"/> if the segment was already removed.
    /// </returns>
    internal bool TryMarkAsRemoved() =>
        Interlocked.CompareExchange(ref _isRemoved, 1, 0) == 0;

    internal CachedSegment(Range<TRange> range, ReadOnlyMemory<TData> data)
    {
        Range = range;
        Data = data;
    }
}
