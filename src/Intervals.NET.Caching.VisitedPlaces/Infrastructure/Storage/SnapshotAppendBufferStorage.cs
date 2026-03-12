using Intervals.NET.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Segment storage backed by a volatile snapshot array and a small fixed-size append buffer.
/// Optimised for small caches (&lt;85 KB total data, &lt;~50 segments) with high read-to-write ratios.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Data Structure:</strong></para>
/// <list type="bullet">
/// <item><description><c>_snapshot</c> — sorted array of live segments; published via <c>Volatile.Write</c> (User Path)</description></item>
/// <item><description><c>_appendBuffer</c> — fixed-size buffer for recently-added segments (Background Path only)</description></item>
/// </list>
/// <para><strong>Soft-delete via <see cref="CachedSegment{TRange,TData}.IsRemoved"/>:</strong></para>
/// <para>
/// Rather than maintaining a separate <c>_softDeleted</c> collection (which would require
/// synchronization between the Background Path and the TTL thread), this implementation
/// delegates soft-delete tracking entirely to <see cref="CachedSegment{TRange,TData}.IsRemoved"/>.
/// The flag is set atomically by <see cref="CachedSegment{TRange,TData}.MarkAsRemoved"/> and
/// never reset, so it is safe to read from any thread without a lock.
    /// All read paths (<see cref="FindIntersecting"/>, <see cref="TryGetRandomSegment"/>,
    /// <see cref="Normalize"/>) simply skip segments whose <c>IsRemoved</c> flag is set.
/// </para>
/// <para><strong>RCU semantics (Invariant VPC.B.5):</strong>
/// User Path threads read a stable snapshot via <c>Volatile.Read</c>. New snapshots are published
/// atomically via <c>Volatile.Write</c> during normalization.</para>
/// <para><strong>Threading:</strong>
/// <see cref="ISegmentStorage{TRange,TData}.FindIntersecting"/> is called on the User Path (concurrent reads safe).
/// All other methods are Background-Path-only (single writer).</para>
/// <para>Alignment: Invariants VPC.A.10, VPC.B.5, VPC.C.2, VPC.C.3, S.H.4.</para>
/// </remarks>
internal sealed class SnapshotAppendBufferStorage<TRange, TData> : ISegmentStorage<TRange, TData>
    where TRange : IComparable<TRange>
{
    private const int RandomRetryLimit = 8;

    private readonly int _appendBufferSize;
    private readonly Random _random = new();

    // Sorted snapshot — published atomically via Volatile.Write on normalization.
    // User Path reads via Volatile.Read.
    private CachedSegment<TRange, TData>[] _snapshot = [];

    // Small fixed-size append buffer for recently-added segments (Background Path only).
    // Size is determined by the appendBufferSize constructor parameter.
    private readonly CachedSegment<TRange, TData>[] _appendBuffer;
    private int _appendCount;

    // Total count of live (non-removed) segments.
    // Decremented by Remove (which may be called from the TTL thread) via Interlocked.Decrement.
    // Incremented only on the Background Path via Interlocked.Increment.
    private int _count;

    /// <summary>
    /// Initializes a new <see cref="SnapshotAppendBufferStorage{TRange,TData}"/> with the
    /// specified append buffer size.
    /// </summary>
    /// <param name="appendBufferSize">
    /// Number of segments the append buffer can hold before normalization is triggered.
    /// Must be &gt;= 1. Default: 8.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="appendBufferSize"/> is less than 1.
    /// </exception>
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
    public int Count => Volatile.Read(ref _count);

    /// <inheritdoc/>
    /// <remarks>
    /// <para><strong>Algorithm (O(log n + k + m)):</strong></para>
    /// <list type="number">
    /// <item><description>Acquire stable snapshot via <c>Volatile.Read</c></description></item>
    /// <item><description>Binary-search snapshot for first entry whose range end &gt;= <paramref name="range"/>.Start</description></item>
    /// <item><description>Linear-scan forward collecting intersecting, non-removed entries (checked via <see cref="CachedSegment{TRange,TData}.IsRemoved"/>)</description></item>
    /// <item><description>Linear-scan append buffer for intersecting, non-removed entries</description></item>
    /// </list>
    /// </remarks>
    public IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range)
    {
        var snapshot = Volatile.Read(ref _snapshot);

        var results = new List<CachedSegment<TRange, TData>>();

        // Binary search: find first candidate in snapshot
        var lo = 0;
        var hi = snapshot.Length - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            // A segment intersects range if segment.Range.End.Value >= range.Start.Value
            // We want the first segment where End.Value >= range.Start.Value
            if (snapshot[mid].Range.End.Value.CompareTo(range.Start.Value) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Linear scan from lo forward
        for (var i = lo; i < snapshot.Length; i++)
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
                results.Add(seg);
            }
        }

        // Scan append buffer (unsorted, small)
        var appendCount = _appendCount; // safe: Background Path writes this; User Path reads it
        for (var i = 0; i < appendCount; i++)
        {
            var seg = _appendBuffer[i];
            if (!seg.IsRemoved && seg.Range.Overlaps(range))
            {
                results.Add(seg);
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public void Add(CachedSegment<TRange, TData> segment)
    {
        _appendBuffer[_appendCount] = segment;
        _appendCount++;
        Interlocked.Increment(ref _count);

        if (_appendCount == _appendBufferSize)
        {
            Normalize();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Calls <see cref="CachedSegment{TRange,TData}.TryMarkAsRemoved"/> to atomically transition
    /// the segment to the removed state. If this is the first removal of the segment (the flag
    /// was not already set), <c>_count</c> is decremented and <see langword="true"/> is returned.
    /// Subsequent calls for the same segment are no-ops (idempotent) and return
    /// <see langword="false"/>.
    /// </para>
    /// <para>
    /// The segment remains physically present in the snapshot and append buffer until the next
    /// <see cref="Normalize"/> pass. All read paths skip it immediately via the
    /// <see cref="CachedSegment{TRange,TData}.IsRemoved"/> flag.
    /// </para>
    /// <para><strong>Thread safety:</strong> Safe to call concurrently from the Background Path
    /// (eviction) and the TTL thread. <see cref="CachedSegment{TRange,TData}.TryMarkAsRemoved"/>
    /// uses <c>Interlocked.CompareExchange</c>; <c>_count</c> uses <c>Interlocked.Decrement</c>.
    /// </para>
    /// </remarks>
    public bool TryRemove(CachedSegment<TRange, TData> segment)
    {
        if (segment.TryMarkAsRemoved())
        {
            Interlocked.Decrement(ref _count);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para><strong>Algorithm (O(1) per attempt, bounded retries):</strong></para>
    /// <list type="number">
    /// <item><description>Compute the live pool size: <c>snapshot.Length + _appendCount</c>.</description></item>
    /// <item><description>Pick a random index in that range. Indices in <c>[0, snapshot.Length)</c>
    ///   map to snapshot entries; indices in <c>[snapshot.Length, pool)</c> map to append buffer entries.</description></item>
    /// <item><description>If the selected segment is soft-deleted, retry (bounded by <c>RandomRetryLimit</c>).</description></item>
    /// </list>
    /// </remarks>
    public CachedSegment<TRange, TData>? TryGetRandomSegment()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        var pool = snapshot.Length + _appendCount;

        if (pool == 0)
        {
            return null;
        }

        for (var attempt = 0; attempt < RandomRetryLimit; attempt++)
        {
            var index = _random.Next(pool);
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
    /// Rebuilds the sorted snapshot by merging the current snapshot (excluding removed
    /// entries) with all live append buffer entries, then atomically publishes the new snapshot.
    /// </summary>
    /// <remarks>
    /// <para><strong>Algorithm:</strong> O(n + m) merge of two sorted sequences (snapshot sorted,
    /// append buffer unsorted — sort append buffer entries first).</para>
    /// <para>Resets <c>_appendCount</c> to 0 and publishes via <c>Volatile.Write</c> so User
    /// Path threads atomically see the new snapshot. Removed segments (whose
    /// <see cref="CachedSegment{TRange,TData}.IsRemoved"/> flag is set) are excluded from the
    /// new snapshot and are physically dropped from memory.</para>
    /// </remarks>
    private void Normalize()
    {
        var snapshot = Volatile.Read(ref _snapshot);

        // Collect live snapshot entries (skip removed segments)
        var liveSnapshot = new List<CachedSegment<TRange, TData>>(snapshot.Length);
        foreach (var seg in snapshot)
        {
            if (!seg.IsRemoved)
            {
                liveSnapshot.Add(seg);
            }
        }

        // Collect live append buffer entries and sort them
        var appendEntries = new List<CachedSegment<TRange, TData>>(_appendCount);
        for (var i = 0; i < _appendCount; i++)
        {
            var seg = _appendBuffer[i];
            if (!seg.IsRemoved)
            {
                appendEntries.Add(seg);
            }
        }

        appendEntries.Sort(static (a, b) => a.Range.Start.Value.CompareTo(b.Range.Start.Value));

        // Merge two sorted sequences
        var merged = MergeSorted(liveSnapshot, appendEntries);

        // Reset append buffer
        _appendCount = 0;
        // Clear stale references in append buffer
        Array.Clear(_appendBuffer, 0, _appendBufferSize);

        // Atomically publish the new snapshot (release fence — User Path reads with acquire fence)
        Volatile.Write(ref _snapshot, merged);
    }

    private static CachedSegment<TRange, TData>[] MergeSorted(
        List<CachedSegment<TRange, TData>> left,
        List<CachedSegment<TRange, TData>> right)
    {
        var result = new CachedSegment<TRange, TData>[left.Count + right.Count];
        int i = 0, j = 0, k = 0;

        while (i < left.Count && j < right.Count)
        {
            var cmp = left[i].Range.Start.Value.CompareTo(right[j].Range.Start.Value);
            if (cmp <= 0)
            {
                result[k++] = left[i++];
            }
            else
            {
                result[k++] = right[j++];
            }
        }

        while (i < left.Count) result[k++] = left[i++];
        while (j < right.Count) result[k++] = right[j++];

        return result;
    }
}
