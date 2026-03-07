namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Performs eviction of segments from the cache and maintains per-segment statistics.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> Background Path (single writer thread)</para>
/// <para><strong>Responsibilities (Invariant VPC.E.2):</strong></para>
/// <list type="bullet">
/// <item><description>Determines which segments to evict based on the configured strategy</description></item>
/// <item><description>Returns the segments to remove (the caller performs actual removal from storage)</description></item>
/// <item><description>Maintains per-segment statistics (<c>HitCount</c>, <c>LastAccessedAt</c>)</description></item>
/// </list>
/// <para><strong>Single-pass eviction (Invariant VPC.E.2a):</strong></para>
/// <para>
/// The executor runs at most once per background event, regardless of how many evaluators fired.
/// A single invocation must satisfy ALL fired evaluator constraints simultaneously.
/// </para>
/// <para><strong>Just-stored immunity (Invariant VPC.E.3):</strong></para>
/// <para>
/// All segments in <paramref name="justStoredSegments"/> must be excluded from the returned
/// eviction set. This covers every segment stored within the current event processing cycle.
/// </para>
/// </remarks>
public interface IEvictionExecutor<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Updates per-segment statistics for all segments in <paramref name="usedSegments"/>.
    /// Called as Background Path step 1 (statistics update).
    /// </summary>
    /// <param name="usedSegments">The segments that were accessed by the User Path.</param>
    /// <param name="now">The current timestamp to assign to <c>LastAccessedAt</c>.</param>
    /// <remarks>
    /// For each segment in <paramref name="usedSegments"/>:
    /// <list type="bullet">
    /// <item><description><c>HitCount</c> is incremented</description></item>
    /// <item><description><c>LastAccessedAt</c> is set to <paramref name="now"/></description></item>
    /// </list>
    /// </remarks>
    void UpdateStatistics(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments, DateTime now);

    /// <summary>
    /// Selects which segments to evict to satisfy all fired evaluator constraints.
    /// Called as Background Path step 4 (eviction execution) only when at least one evaluator fired.
    /// The caller is responsible for removing the returned segments from storage.
    /// </summary>
    /// <param name="allSegments">All currently stored segments (the full candidate pool).</param>
    /// <param name="justStoredSegments">
    /// All segments stored during the current event processing cycle (immune from eviction per
    /// Invariant VPC.E.3). Empty when no segments were stored in this cycle.
    /// </param>
    /// <param name="removalCount">
    /// The maximum number of segments to remove, computed as the maximum across all fired evaluators.
    /// Always greater than 0 when this method is called.
    /// </param>
    /// <returns>The segments that should be removed from storage. May be empty.</returns>
    IReadOnlyList<CachedSegment<TRange, TData>> SelectForEviction(
        IReadOnlyList<CachedSegment<TRange, TData>> allSegments,
        IReadOnlyList<CachedSegment<TRange, TData>> justStoredSegments,
        int removalCount);
}
