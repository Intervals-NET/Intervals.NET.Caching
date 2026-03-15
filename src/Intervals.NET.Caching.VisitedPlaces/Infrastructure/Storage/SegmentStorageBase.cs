using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Abstract base class for segment storage; owns all invariant enforcement (VPC.C.3, VPC.T.1).
/// See docs/visited-places/storage-strategies.md for design details.
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

    // -------------------------------------------------------------------------
    // ISegmentStorage concrete implementations (invariant-enforcement layer)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public abstract IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range);

    /// <inheritdoc/>
    /// <remarks>
    /// Enforces Invariant VPC.C.3: calls <see cref="FindIntersecting"/> before delegating to
    /// <see cref="AddCore"/>. If an overlapping segment already exists, the segment is not stored
    /// and <see langword="false"/> is returned.
    /// </remarks>
    public bool TryAdd(CachedSegment<TRange, TData> segment)
    {
        // VPC.C.3: skip if an overlapping segment already exists in storage.
        if (FindIntersecting(segment.Range).Count > 0)
        {
            return false;
        }

        AddCore(segment);
        _count++;
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Enforces Invariant VPC.C.3 for each segment individually: sorts the input, then calls
    /// <see cref="FindIntersecting"/> for each segment (including against peers inserted earlier
    /// in the same call). Only non-overlapping segments are passed to <see cref="AddRangeCore"/>
    /// in a single bulk call.
    /// </remarks>
    public CachedSegment<TRange, TData>[] TryAddRange(CachedSegment<TRange, TData>[] segments)
    {
        if (segments.Length == 0)
        {
            return [];
        }

        // Sort incoming segments by range start (Background Path owns the array exclusively).
        segments.AsSpan().Sort(static (a, b) => a.Range.Start.Value.CompareTo(b.Range.Start.Value));

        // Filter to non-overlapping segments only (VPC.C.3). Because prior peers are inserted
        // via AddRangeCore only after all validation, intra-batch overlap detection relies on
        // the sort order: sorted non-overlapping incoming segments cannot overlap each other.
        // Overlap with already-stored segments is detected via FindIntersecting.
        // For strategies like SnapshotAppendBufferStorage that bypass the append buffer in
        // AddRangeCore, peers from this same call are NOT yet in FindIntersecting's view —
        // this is safe because the sort guarantees incoming segments are processed in ascending
        // order, and each accepted segment will be the new rightmost, so subsequent segments
        // to its right cannot overlap it by VPC.C.3's strict-inequality contract.
        List<CachedSegment<TRange, TData>>? validated = null;

        foreach (var segment in segments)
        {
            // VPC.C.3: check against current live storage.
            if (FindIntersecting(segment.Range).Count > 0)
            {
                continue;
            }

            (validated ??= []).Add(segment);
        }

        if (validated == null)
        {
            return [];
        }

        var validatedArray = validated.ToArray();
        AddRangeCore(validatedArray);
        _count += validatedArray.Length;
        return validatedArray;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Enforces Invariant VPC.T.1 (idempotent removal): checks <see cref="CachedSegment{TRange,TData}.IsRemoved"/>
    /// before calling <see cref="CachedSegment{TRange,TData}.MarkAsRemoved"/> and decrementing <see cref="_count"/>.
    /// Safe without a lock because the Background Path is the sole writer (VPC.A.1).
    /// </remarks>
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
    /// <remarks>
    /// Retries up to <see cref="RandomRetryLimit"/> times, delegating each attempt to
    /// <see cref="SampleRandomCore"/>. Dead segments (removed or expired) are filtered here;
    /// concrete strategies do not need to repeat this logic in their sampling implementation.
    /// </remarks>
    public CachedSegment<TRange, TData>? TryGetRandomSegment()
    {
        // Pre-compute UTC ticks once for all expiry checks in this sampling pass.
        var utcNowTicks = GetUtcNowTicks();

        for (var attempt = 0; attempt < RandomRetryLimit; attempt++)
        {
            var seg = SampleRandomCore();

            if (seg == null)
            {
                // Underlying store is empty — no point retrying.
                return null;
            }

            if (!seg.IsRemoved && !seg.IsExpired(utcNowTicks))
            {
                return seg;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Checks the normalization threshold via <see cref="ShouldNormalize"/>. When triggered,
    /// delegates the structural rebuild to <see cref="NormalizeCore"/> (which also discovers
    /// TTL-expired segments and calls <see cref="TryRemove"/> on them), then resets the counter
    /// via <see cref="ResetNormalizationCounter"/>.
    /// </remarks>
    public bool TryNormalize(out IReadOnlyList<CachedSegment<TRange, TData>>? expiredSegments)
    {
        if (!ShouldNormalize())
        {
            expiredSegments = null;
            return false;
        }

        List<CachedSegment<TRange, TData>>? expired = null;
        NormalizeCore(GetUtcNowTicks(), ref expired);
        ResetNormalizationCounter();

        expiredSegments = expired;
        return true;
    }

    // -------------------------------------------------------------------------
    // Abstract primitives — implemented by each concrete strategy
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inserts a single segment into the underlying data structure.
    /// Precondition: VPC.C.3 has already been verified by the caller (<see cref="TryAdd"/>).
    /// Must increment any internal add counter used by <see cref="ShouldNormalize"/>.
    /// </summary>
    protected abstract void AddCore(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Inserts a batch of validated, sorted segments into the underlying data structure.
    /// Precondition: each segment in <paramref name="segments"/> has already been verified
    /// against VPC.C.3 by <see cref="TryAddRange"/>. The array is sorted by range start.
    /// Must increment any internal add counter by the number of segments inserted.
    /// </summary>
    /// <remarks>
    /// Must NOT call normalization — <see cref="TryAddRange"/> returns to the executor which calls
    /// <see cref="TryNormalize"/> immediately after. Normalization here would silently drop TTL-expired
    /// segments and permanently break the normalization cadence.
    /// </remarks>
    protected abstract void AddRangeCore(CachedSegment<TRange, TData>[] segments);

    /// <summary>
    /// Returns a single candidate segment from the underlying data structure for random
    /// sampling, or <see langword="null"/> if the store is empty.
    /// The returned segment may be removed or TTL-expired — <see cref="TryGetRandomSegment"/>
    /// filters those out after calling this method.
    /// </summary>
    protected abstract CachedSegment<TRange, TData>? SampleRandomCore();

    /// <summary>
    /// Returns <see langword="true"/> when the internal add counter has reached the
    /// normalization threshold and <see cref="NormalizeCore"/> should run.
    /// </summary>
    protected abstract bool ShouldNormalize();

    /// <summary>
    /// Performs the structural rebuild (e.g., merge snapshot + append buffer, rebuild stride
    /// index) and discovers TTL-expired segments.
    /// </summary>
    /// <param name="utcNowTicks">
    /// Pre-computed current UTC ticks for expiry comparisons. Passed in from the base to avoid
    /// multiple <see cref="GetUtcNowTicks"/> calls across the normalization pass.
    /// </param>
    /// <param name="expired">
    /// Mutable list that this method populates with newly-expired segments.
    /// For each segment whose TTL has elapsed, call <see cref="TryRemove"/> to mark it removed
    /// and add it to this list. The list is lazily initialised; pass <see langword="null"/>
    /// and the method will allocate only when at least one segment expires.
    /// </param>
    protected abstract void NormalizeCore(
        long utcNowTicks,
        ref List<CachedSegment<TRange, TData>>? expired);

    /// <summary>
    /// Resets the internal add counter to zero after a normalization pass completes.
    /// Called by <see cref="TryNormalize"/> after <see cref="NormalizeCore"/> returns
    /// successfully. If <see cref="NormalizeCore"/> throws, this method is NOT called —
    /// implementations that must reset the counter unconditionally (e.g., on exception)
    /// should do so inside a <c>finally</c> block within <see cref="NormalizeCore"/> and
    /// leave this as a no-op.
    /// </summary>
    protected abstract void ResetNormalizationCounter();

    /// <summary>
    /// Returns the current UTC time as ticks. Injected by concrete strategies via the
    /// <see cref="TimeProvider"/> they hold; the base class calls this helper to avoid
    /// coupling itself to a specific time provider instance.
    /// </summary>
    protected abstract long GetUtcNowTicks();

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
