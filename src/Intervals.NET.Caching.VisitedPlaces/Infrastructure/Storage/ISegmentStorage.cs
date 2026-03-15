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
    /// Adds a new segment to the storage (Background Path only).
    /// </summary>
    void Add(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Adds multiple pre-validated, pre-sorted segments to the storage in a single bulk operation
    /// (Background Path only). Reduces normalization overhead from O(count/bufferSize) normalizations
    /// to a single pass — beneficial when a multi-gap partial-hit request produces many new segments.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for ensuring all segments in <paramref name="segments"/> are
    /// non-overlapping and sorted by range start (Invariant VPC.C.3). Each segment must already
    /// have passed the overlap pre-check against current storage contents.
    /// </remarks>
    void AddRange(CachedSegment<TRange, TData>[] segments);

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
