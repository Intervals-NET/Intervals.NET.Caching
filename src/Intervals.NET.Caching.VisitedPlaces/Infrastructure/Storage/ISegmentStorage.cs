using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Internal storage contract for the non-contiguous segment collection.
/// See docs/visited-places/ for design details.
/// </summary>
internal interface ISegmentStorage<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Returns the current number of live segments in the storage.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Returns all non-removed segments whose ranges intersect <paramref name="range"/>.
    /// </summary>
    IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range);

    /// <summary>
    /// Attempts to add a new segment to the storage (Background Path only).
    /// Enforces Invariant VPC.C.3: the segment is not stored if it overlaps any existing segment.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the segment was stored;
    /// <see langword="false"/> if it was skipped due to an overlap with an existing segment.
    /// </returns>
    bool TryAdd(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Attempts to add multiple segments to the storage in a single bulk operation
    /// (Background Path only). Reduces normalization overhead from O(count/bufferSize) normalizations
    /// to a single pass — beneficial when a multi-gap partial-hit request produces many new segments.
    /// Enforces Invariant VPC.C.3: each segment is checked for overlap against the current storage
    /// state (including segments inserted earlier in the same call) before being stored.
    /// </summary>
    /// <returns>
    /// The segments that were actually stored. Segments that overlap an existing segment are skipped.
    /// Returns an empty array if no segments were stored.
    /// </returns>
    CachedSegment<TRange, TData>[] TryAddRange(CachedSegment<TRange, TData>[] segments);

    /// <summary>
    /// Marks a segment as removed and decrements the live count.
    /// Idempotent: returns <see langword="false"/> (no-op) if the segment has already been removed.
    /// The caller must ensure the segment belongs to this storage instance.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the segment was live and is now marked removed;
    /// <see langword="false"/> if it was already removed.
    /// </returns>
    bool TryRemove(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Returns a single randomly-selected live segment, or <see langword="null"/> if none available.
    /// </summary>
    CachedSegment<TRange, TData>? TryGetRandomSegment();

    /// <summary>
    /// Performs a normalization pass if the internal threshold has been reached.
    /// During normalization, any segments whose TTL has expired are discovered,
    /// marked as removed via <c>MarkAsRemoved</c>, physically removed from storage,
    /// and returned via <paramref name="expiredSegments"/>.
    /// </summary>
    /// <param name="expiredSegments">
    /// When normalization runs and at least one segment expired, receives the list of
    /// newly-expired segments discovered during this pass.
    /// <see langword="null"/> when normalization did not run or no segments expired.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if normalization was performed; <see langword="false"/> if the
    /// threshold was not yet reached and no normalization took place.
    /// </returns>
    bool TryNormalize(out IReadOnlyList<CachedSegment<TRange, TData>>? expiredSegments);
}
