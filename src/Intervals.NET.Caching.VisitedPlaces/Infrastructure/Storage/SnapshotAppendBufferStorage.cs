using Intervals.NET.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Segment storage backed by a volatile snapshot array and a small fixed-size append buffer.
/// Optimised for small caches (&lt;85 KB total data, &lt;~50 segments).
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class SnapshotAppendBufferStorage<TRange, TData> : SegmentStorageBase<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly int _appendBufferSize;

    // Sorted snapshot — published atomically via Volatile.Write on normalization.
    // User Path reads via Volatile.Read.
    private CachedSegment<TRange, TData>[] _snapshot = [];

    // Small fixed-size append buffer for recently-added segments (Background Path only).
    // Size is determined by the appendBufferSize constructor parameter.
    private readonly CachedSegment<TRange, TData>[] _appendBuffer;
    private int _appendCount;

    /// <summary>
    /// Initializes a new <see cref="SnapshotAppendBufferStorage{TRange,TData}"/> with the
    /// specified append buffer size.
    /// </summary>
    internal SnapshotAppendBufferStorage(int appendBufferSize = 8)
    {
        if (appendBufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appendBufferSize),
                "AppendBufferSize must be greater than or equal to 1.");
        }

        _appendBufferSize = appendBufferSize;
        _appendBuffer = new CachedSegment<TRange, TData>[appendBufferSize];
    }

    /// <inheritdoc/>
    public override IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range)
    {
        var snapshot = Volatile.Read(ref _snapshot);

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

            // Use IsRemoved flag as the primary soft-delete filter (no shared collection needed).
            if (!seg.IsRemoved && seg.Range.Overlaps(range))
            {
                (results ??= []).Add(seg);
            }
        }

        // Scan append buffer (unsorted, small)
        var appendCount = Volatile.Read(ref _appendCount); // Acquire fence: ensures visibility of append buffer entries written before this count was published
        for (var i = 0; i < appendCount; i++)
        {
            var seg = _appendBuffer[i];
            if (!seg.IsRemoved && seg.Range.Overlaps(range))
            {
                (results ??= []).Add(seg);
            }
        }

        return (IReadOnlyList<CachedSegment<TRange, TData>>?)results ?? [];
    }

    /// <inheritdoc/>
    public override void Add(CachedSegment<TRange, TData> segment)
    {
        _appendBuffer[_appendCount] = segment;
        Volatile.Write(ref _appendCount, _appendCount + 1); // Release fence: ensures buffer entry is visible before count increment
        IncrementCount();

        if (_appendCount == _appendBufferSize)
        {
            Normalize();
        }
    }

    /// <inheritdoc/>
    public override CachedSegment<TRange, TData>? TryGetRandomSegment()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        var pool = snapshot.Length + _appendCount;

        if (pool == 0)
        {
            return null;
        }

        for (var attempt = 0; attempt < RandomRetryLimit; attempt++)
        {
            var index = Random.Next(pool);
            CachedSegment<TRange, TData> seg;

            if (index < snapshot.Length)
            {
                seg = snapshot[index];
            }
            else
            {
                seg = _appendBuffer[index - snapshot.Length];
            }

            if (!seg.IsRemoved)
            {
                return seg;
            }
        }

        return null;
    }

    /// <summary>
    /// Rebuilds the sorted snapshot by merging live entries from snapshot and append buffer.
    /// </summary>
    private void Normalize()
    {
        var snapshot = Volatile.Read(ref _snapshot);

        // Count live snapshot entries (skip removed segments) without allocating a List.
        var liveSnapshotCount = 0;
        for (var i = 0; i < snapshot.Length; i++)
        {
            var seg = snapshot[i];
            if (!seg.IsRemoved)
            {
                liveSnapshotCount++;
            }
        }

        // Sort the append buffer in-place (Background Path owns _appendBuffer exclusively).
        // MemoryExtensions.Sort operates on a Span — zero allocation.
        _appendBuffer.AsSpan(0, _appendCount).Sort(
            static (a, b) => a.Range.Start.Value.CompareTo(b.Range.Start.Value));

        // Count live append buffer entries after sorting.
        var liveAppendCount = 0;
        for (var i = 0; i < _appendCount; i++)
        {
            if (!_appendBuffer[i].IsRemoved)
            {
                liveAppendCount++;
            }
        }

        // Merge two sorted sequences directly into the output array — one allocation.
        var merged = MergeSorted(snapshot, liveSnapshotCount, _appendBuffer, _appendCount, liveAppendCount);

        // Atomically publish the new snapshot FIRST (release fence — User Path reads with acquire fence)
        // Must happen before resetting _appendCount so User Path never sees count==0 with the old snapshot.
        // NOTE: There is a brief window between publishing the snapshot and resetting _appendCount
        // where a concurrent User Path could read the new snapshot but also count the same newly-appended
        // segments via the append buffer (i.e. see them twice). This is an accepted design tradeoff:
        // over-counting is harmless (TryGetRandomSegment skips IsRemoved segments), and the window
        // closes as soon as _appendCount is reset below.
        Volatile.Write(ref _snapshot, merged);

        // Reset append buffer — after snapshot publication
        Volatile.Write(ref _appendCount, 0);
        // Clear stale references in append buffer
        Array.Clear(_appendBuffer, 0, _appendBufferSize);
    }

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
