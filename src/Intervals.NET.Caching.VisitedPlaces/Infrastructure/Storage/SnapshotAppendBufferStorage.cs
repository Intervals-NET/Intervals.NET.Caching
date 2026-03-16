using Intervals.NET.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Segment storage backed by a volatile snapshot array and a small fixed-size append buffer.
/// Optimised for small caches (&lt;85 KB total data, &lt;~50 segments).
/// See docs/visited-places/ for design details.
/// </summary>
/// <remarks>
/// This class implements only the data-structure mechanics of the snapshot + append-buffer
/// pattern. All invariant enforcement (VPC.C.3 overlap check, VPC.T.1 idempotent removal,
/// normalization threshold check, retry/filter for random sampling) is handled by the base
/// class <see cref="SegmentStorageBase{TRange,TData}"/>.
/// </remarks>
internal sealed class SnapshotAppendBufferStorage<TRange, TData> : SegmentStorageBase<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly int _appendBufferSize;
    private readonly TimeProvider _timeProvider;

    // Guards the atomic read/write pair of (_snapshot, _appendCount) during normalization.
    // Held only during Normalize() writes and at the start of FindIntersecting() to capture
    // a consistent snapshot of both fields. NOT held during the actual search work.
    private readonly object _normalizeLock = new();

    // Sorted snapshot — mutated only inside _normalizeLock during normalization.
    // User Path reads the reference inside _normalizeLock (captures a local copy, then searches lock-free).
    private CachedSegment<TRange, TData>[] _snapshot = [];

    // Small fixed-size append buffer for recently-added segments (Background Path only).
    // Size is determined by the appendBufferSize constructor parameter.
    private readonly CachedSegment<TRange, TData>[] _appendBuffer;

    // Written by AddCore() via Volatile.Write (non-normalizing path) and inside _normalizeLock (NormalizeCore).
    // Read by FindIntersecting() inside _normalizeLock to form a consistent pair with _snapshot.
    private int _appendCount;

    /// <summary>
    /// Initializes a new <see cref="SnapshotAppendBufferStorage{TRange,TData}"/> with the
    /// specified append buffer size and optional time provider.
    /// </summary>
    internal SnapshotAppendBufferStorage(int appendBufferSize = 8, TimeProvider? timeProvider = null)
    {
        if (appendBufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appendBufferSize),
                "AppendBufferSize must be greater than or equal to 1.");
        }

        _appendBufferSize = appendBufferSize;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _appendBuffer = new CachedSegment<TRange, TData>[appendBufferSize];
    }

    // -------------------------------------------------------------------------
    // FindIntersecting (abstract in base; scan is tightly coupled to snapshot + buffer structure)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public override IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range)
    {
        // Capture (_snapshot, _appendCount) as a consistent pair under the normalize lock.
        // The lock body is two field reads — held for nanoseconds, never contended during
        // normal operation (Normalize fires only every appendBufferSize additions).
        CachedSegment<TRange, TData>[] snapshot;
        int appendCount;
        lock (_normalizeLock)
        {
            snapshot = _snapshot;
            appendCount = _appendCount;
        }

        // Pre-compute the current UTC ticks once for all expiry checks in this call.
        var utcNowTicks = _timeProvider.GetUtcNow().UtcTicks;

        // Lazy-init: only allocate the results list on the first actual match.
        // Full-Miss path (no intersecting segments) returns the static empty array — zero allocation.
        List<CachedSegment<TRange, TData>>? results = null;

        // Binary search: find the rightmost snapshot entry whose Start <= range.Start.
        // That entry is itself the earliest possible intersector: because segments are
        // non-overlapping and sorted by Start (Invariant VPC.C.3), every earlier segment
        // has End < Start[hi] <= range.Start and therefore cannot intersect.
        // No step-back needed — unlike the stride strategy, every element is directly indexed.
        var hi = snapshot.Length > 0
            ? FindLastAtOrBefore(snapshot, range.Start.Value, default(DirectAccessor))
            : -1;

        // Start scanning from hi (the rightmost segment whose Start <= range.Start).
        // If hi == -1 all segments start after range.Start; begin from 0 in case some
        // still have Start <= range.End (i.e. the query range starts before all segments).
        var scanStart = Math.Max(0, hi);

        // Linear scan from scanStart forward
        for (var i = scanStart; i < snapshot.Length; i++)
        {
            var seg = snapshot[i];
            // Short-circuit: if segment starts after range ends, no more candidates
            if (seg.Range.Start.Value.CompareTo(range.End.Value) > 0)
            {
                break;
            }

            // Filter out removed and TTL-expired segments (lazy expiration on read).
            if (!seg.IsRemoved && !seg.IsExpired(utcNowTicks) && seg.Range.Overlaps(range))
            {
                (results ??= []).Add(seg);
            }
        }

        // Scan append buffer (unsorted, small) up to the count captured above.
        for (var i = 0; i < appendCount; i++)
        {
            var seg = _appendBuffer[i];
            if (!seg.IsRemoved && !seg.IsExpired(utcNowTicks) && seg.Range.Overlaps(range))
            {
                (results ??= []).Add(seg);
            }
        }

        return (IReadOnlyList<CachedSegment<TRange, TData>>?)results ?? [];
    }

    // -------------------------------------------------------------------------
    // Abstract primitive implementations (data-structure mechanics only)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Appends the segment to <c>_appendBuffer</c> and increments <c>_appendCount</c>
    /// via <see cref="Volatile.Write"/> to publish the new entry atomically.
    /// VPC.C.3 overlap check is handled by <see cref="SegmentStorageBase{TRange,TData}.TryAdd"/>.
    /// </remarks>
    protected override void AddCore(CachedSegment<TRange, TData> segment)
    {
        _appendBuffer[_appendCount] = segment;
        // Release fence: makes buffer entry visible to readers before count increment is observed.
        Volatile.Write(ref _appendCount, _appendCount + 1);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Bypasses the append buffer: merges the validated sorted segments directly into the
    /// snapshot via <see cref="MergeSorted"/> and publishes atomically via
    /// <see cref="Interlocked.Exchange"/>. The append buffer is left untouched (see class
    /// remarks and VPC.C.7 in docs/visited-places/invariants.md).
    /// VPC.C.3 overlap check is handled by <see cref="SegmentStorageBase{TRange,TData}.TryAddRange"/>.
    /// Does NOT perform normalization or TTL discovery — per the base class contract on
    /// <see cref="SegmentStorageBase{TRange,TData}.AddRangeCore"/>; the executor's subsequent
    /// <see cref="TryNormalize"/> call owns that responsibility.
    /// </remarks>
    protected override void AddRangeCore(CachedSegment<TRange, TData>[] segments)
    {
        var snapshot = Volatile.Read(ref _snapshot);

        // Count live entries in the current snapshot (removes do not affect incoming segments).
        var liveSnapshotCount = 0;
        for (var i = 0; i < snapshot.Length; i++)
        {
            if (!snapshot[i].IsRemoved)
            {
                liveSnapshotCount++;
            }
        }

        // Merge current snapshot (left) with sorted, validated incoming (right) — one allocation.
        // Incoming segments are brand-new and therefore never IsRemoved; pass their full length
        // as both rightLength and liveRightCount.
        var merged = MergeSorted(snapshot, liveSnapshotCount, segments, segments.Length, segments.Length);

        // Atomically replace the snapshot. _appendCount is NOT touched — the lock guards the
        // (_snapshot, _appendCount) pair; since _appendCount is unchanged, Interlocked.Exchange suffices.
        Interlocked.Exchange(ref _snapshot, merged);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Picks a random index from the combined pool of <c>_snapshot</c> and <c>_appendBuffer</c>.
    /// Returns <see langword="null"/> when the pool is empty. Dead-segment filtering is handled
    /// by <see cref="SegmentStorageBase{TRange,TData}.TryGetRandomSegment"/>.
    /// </remarks>
    protected override CachedSegment<TRange, TData>? SampleRandomCore()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        var pool = snapshot.Length + _appendCount;

        if (pool == 0)
        {
            return null;
        }

        var index = Random.Next(pool);

        if (index < snapshot.Length)
        {
            return snapshot[index];
        }

        return _appendBuffer[index - snapshot.Length];
    }

    /// <inheritdoc/>
    protected override bool ShouldNormalize() => _appendCount >= _appendBufferSize;

    /// <inheritdoc/>
    /// <remarks>
    /// Rebuilds the sorted snapshot by merging live entries from snapshot and append buffer.
    /// Expired segments are discovered, marked removed via <see cref="SegmentStorageBase{TRange,TData}.TryRemove"/>,
    /// and collected in <paramref name="expired"/> for the executor to process.
    /// Publishes the new snapshot and resets <c>_appendCount</c> atomically under <c>_normalizeLock</c>.
    /// </remarks>
    protected override void NormalizeCore(long utcNowTicks, ref List<CachedSegment<TRange, TData>>? expired)
    {
        var snapshot = Volatile.Read(ref _snapshot);

        // Count live snapshot entries (skip removed/expired segments) without allocating a List.
        var liveSnapshotCount = 0;
        for (var i = 0; i < snapshot.Length; i++)
        {
            var seg = snapshot[i];
            if (seg.IsRemoved)
            {
                continue;
            }

            if (seg.IsExpired(utcNowTicks))
            {
                TryRemove(seg);
                (expired ??= []).Add(seg);
                continue;
            }

            liveSnapshotCount++;
        }

        // Sort the append buffer in-place (Background Path owns _appendBuffer exclusively).
        // MemoryExtensions.Sort operates on a Span — zero allocation.
        _appendBuffer.AsSpan(0, _appendCount).Sort(
            static (a, b) => a.Range.Start.Value.CompareTo(b.Range.Start.Value));

        // Count live append buffer entries after sorting, discovering TTL-expired segments.
        var liveAppendCount = 0;
        for (var i = 0; i < _appendCount; i++)
        {
            var seg = _appendBuffer[i];
            if (seg.IsRemoved)
            {
                continue;
            }

            if (seg.IsExpired(utcNowTicks))
            {
                TryRemove(seg);
                (expired ??= []).Add(seg);
                continue;
            }

            liveAppendCount++;
        }

        // Merge two sorted sequences directly into the output array — one allocation.
        var merged = MergeSorted(snapshot, liveSnapshotCount, _appendBuffer, _appendCount, liveAppendCount);

        // Atomically publish the new snapshot and reset _appendCount under the normalize lock.
        // FindIntersecting captures both fields under the same lock, so it is guaranteed to see
        // either (old snapshot, old count) or (new snapshot, 0) — never the mixed state that
        // previously caused duplicate segment references to appear in query results (VPC.C.7).
        lock (_normalizeLock)
        {
            _snapshot = merged;
            _appendCount = 0;
        }

        // Intentionally NOT clearing _appendBuffer here.
        //
        // A FindIntersecting call that captured appendCount > 0 under the lock (before the
        // _appendCount = 0 write above) is still iterating _appendBuffer[0..appendCount] lock-free.
        // Array.Clear on the shared buffer while that scan is in progress produces a
        // NullReferenceException when the reader dereferences a nulled slot.
        //
        // Leaving the stale references in place is safe:
        //   (a) Any FindIntersecting entering AFTER the lock update captures appendCount = 0
        //       and skips the buffer scan entirely.
        //   (b) Any FindIntersecting that captured (old snapshot, appendCount = N) before the
        //       lock update sees a consistent pre-normalization view — no duplication is possible
        //       because the same lock prevents the mixed state (new snapshot, old count).
        //   (c) The next Add() call overwrites _appendBuffer[0] before Volatile.Write increments
        //       _appendCount, so the stale reference at slot 0 is never observable to readers.
        //   (d) The merged snapshot already holds references to all live segments; leaving them
        //       in buffer slots until overwritten does not extend their logical lifetime.
    }

    /// <inheritdoc/>
    /// <remarks>
    /// No-op: <see cref="NormalizeCore"/> resets <c>_appendCount</c> to zero inside
    /// <c>_normalizeLock</c> as part of the atomic publish step. The base class calls this
    /// after <see cref="NormalizeCore"/> returns; for this strategy it is already done.
    /// </remarks>
    protected override void ResetNormalizationCounter()
    {
        // Reset is performed atomically inside NormalizeCore under _normalizeLock.
        // Nothing to do here.
    }

    /// <inheritdoc/>
    protected override long GetUtcNowTicks() => _timeProvider.GetUtcNow().UtcTicks;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static CachedSegment<TRange, TData>[] MergeSorted(
        CachedSegment<TRange, TData>[] left,
        int liveLeftCount,
        CachedSegment<TRange, TData>[] right,
        int rightLength,
        int liveRightCount)
    {
        var result = new CachedSegment<TRange, TData>[liveLeftCount + liveRightCount];
        int i = 0, j = 0, k = 0;

        // Advance i to the next live left entry.
        while (i < left.Length && left[i].IsRemoved)
        {
            i++;
        }

        // Advance j to the next live right entry.
        while (j < rightLength && right[j].IsRemoved)
        {
            j++;
        }

        while (i < left.Length && j < rightLength)
        {
            var cmp = left[i].Range.Start.Value.CompareTo(right[j].Range.Start.Value);
            if (cmp <= 0)
            {
                result[k++] = left[i++];
                while (i < left.Length && left[i].IsRemoved)
                {
                    i++;
                }
            }
            else
            {
                result[k++] = right[j++];
                while (j < rightLength && right[j].IsRemoved)
                {
                    j++;
                }
            }
        }

        while (i < left.Length)
        {
            if (!left[i].IsRemoved)
            {
                result[k++] = left[i];
            }

            i++;
        }

        while (j < rightLength)
        {
            if (!right[j].IsRemoved)
            {
                result[k++] = right[j];
            }

            j++;
        }

        // k == result.Length: TTL expiry runs exclusively on the Background Path (single writer)
        // inside NormalizeCore(), so no concurrent writer can mark additional segments as removed
        // between the counting pass and this merge pass.

        return result;
    }

    /// <summary>
    /// Zero-allocation accessor for extracting <c>Range.Start.Value</c> from a segment.
    /// </summary>
    private readonly struct DirectAccessor : ISegmentAccessor<CachedSegment<TRange, TData>>
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public TRange GetStartValue(CachedSegment<TRange, TData> element) =>
            element.Range.Start.Value;
    }
}
