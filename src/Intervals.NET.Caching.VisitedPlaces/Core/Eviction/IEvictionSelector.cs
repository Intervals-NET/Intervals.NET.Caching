using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Extends <see cref="IEvictionSelector{TRange,TData}"/> with post-construction storage injection.
/// See docs/visited-places/ for design details.
/// </summary>
internal interface IStorageAwareEvictionSelector<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Injects the storage instance into this selector. Must be called exactly once before use.
    /// </summary>
    /// <param name="storage">The segment storage used to obtain random samples.</param>
    void Initialize(ISegmentStorage<TRange, TData> storage);
}

/// <summary>
/// Selects a single eviction candidate from the current segment pool using a
/// strategy-specific sampling approach, and owns the per-segment metadata required
/// to implement that strategy.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// Selectors use random sampling (O(SampleSize)) rather than sorting all segments.
/// Each selector defines its own <see cref="IEvictionMetadata"/> for per-segment state.
/// </remarks>
public interface IEvictionSelector<TRange, TData>
    where TRange : IComparable<TRange>
{    /// <summary>
     /// Selects a single eviction candidate by randomly sampling segments from storage
     /// and returning the worst according to this selector's strategy.
     /// </summary>
     /// <param name="immuneSegments">
     /// Segments that must not be selected (just-stored and already-selected segments).
     /// </param>
     /// <param name="candidate">
     /// When this method returns <see langword="true"/>, contains the selected eviction candidate.
     /// When this method returns <see langword="false"/>, this parameter is undefined.
     /// </param>
     /// <returns>
     /// <see langword="true"/> if a candidate was found; <see langword="false"/> if no eligible
     /// candidate exists.
     /// </returns>
    bool TrySelectCandidate(
        IReadOnlySet<CachedSegment<TRange, TData>> immuneSegments,
        out CachedSegment<TRange, TData> candidate);

    /// <summary>
    /// Attaches selector-specific metadata to a newly stored segment.
    /// </summary>
    /// <param name="segment">The newly stored segment to initialize metadata for.</param>
    void InitializeMetadata(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Updates selector-specific metadata on segments that were accessed on the User Path.
    /// </summary>
    /// <param name="usedSegments">The segments that were read during the User Path request.</param>
    void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments);
}
