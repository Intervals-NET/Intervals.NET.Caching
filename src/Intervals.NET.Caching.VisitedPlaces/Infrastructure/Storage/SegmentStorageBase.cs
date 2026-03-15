using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Abstract base class for segment storage implementations, providing shared concurrency
/// primitives and binary search infrastructure. See docs/visited-places/ for design details.
/// </summary>
internal abstract class SegmentStorageBase<TRange, TData> : ISegmentStorage<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Maximum number of retry attempts when sampling a random live segment
    /// before giving up. Used when all candidates within the retry budget are soft-deleted.
    /// </summary>
    protected const int RandomRetryLimit = 8;

    /// <summary>
    /// Per-instance random number generator for <see cref="TryGetRandomSegment"/>.
    /// Background-Path-only — no synchronization required.
    /// </summary>
    protected readonly Random Random = new();

    // Total count of live (non-removed) segments.
    // All mutations (Add, AddRange, Remove, TryNormalize) occur exclusively on the
    // Background Path (single writer), so plain reads/writes are safe — no Interlocked needed.
    protected int _count;

    /// <inheritdoc/>
    public int Count => _count;

    /// <inheritdoc/>
    public abstract IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range);

    /// <inheritdoc/>
    public abstract void Add(CachedSegment<TRange, TData> segment);

    /// <inheritdoc/>
    public abstract void AddRange(CachedSegment<TRange, TData>[] segments);

    /// <inheritdoc/>
    public bool TryRemove(CachedSegment<TRange, TData> segment)
    {
        if (segment.IsRemoved)
        {
            return false;
        }
        segment.MarkAsRemoved();
        _count--;
        return true;
    }

    /// <inheritdoc/>
    public abstract CachedSegment<TRange, TData>? TryGetRandomSegment();

    /// <inheritdoc/>
    public abstract bool TryNormalize(out IReadOnlyList<CachedSegment<TRange, TData>>? expiredSegments);

    // -------------------------------------------------------------------------
    // Shared binary search infrastructure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Zero-allocation accessor for extracting <c>Range.Start.Value</c> from an array element.
    /// </summary>
    /// <typeparam name="TElement">The array element type.</typeparam>
    protected interface ISegmentAccessor<in TElement>
    {
        /// <summary>Returns the <c>Range.Start.Value</c> of <paramref name="element"/>.</summary>
        TRange GetStartValue(TElement element);
    }

    /// <summary>
    /// Binary-searches <paramref name="array"/> for the rightmost element whose
    /// <c>Range.Start.Value</c> is less than or equal to <paramref name="value"/>.
    /// </summary>
    protected static int FindLastAtOrBefore<TElement, TAccessor>(
        TElement[] array,
        TRange value,
        TAccessor accessor = default)
        where TAccessor : struct, ISegmentAccessor<TElement>
    {
        var lo = 0;
        var hi = array.Length - 1;

        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (accessor.GetStartValue(array[mid]).CompareTo(value) <= 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // hi is the rightmost index where Start.Value <= value, or -1 if none.
        return hi;
    }
}
