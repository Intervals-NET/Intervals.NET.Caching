using Intervals.NET.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Core;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

/// <summary>
/// Segment storage backed by a sorted doubly-linked list with a volatile stride index for
/// accelerated range lookup. Optimised for larger caches (&gt;85 KB total data, &gt;50 segments)
/// where LOH pressure from large snapshot arrays must be avoided.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Data Structure:</strong></para>
/// <list type="bullet">
/// <item><description><c>_list</c> — doubly-linked list sorted by segment range start; mutated on Background Path only</description></item>
/// <item><description><c>_strideIndex</c> — array of every Nth node ("stride anchors"); published via <c>Volatile.Write</c></description></item>
/// <item><description><c>_strideAppendBuffer</c> — fixed-size buffer collecting newly-added segments before stride normalization</description></item>
/// </list>
/// <para><strong>Soft-delete via <see cref="CachedSegment{TRange,TData}.IsRemoved"/>:</strong></para>
/// <para>
/// Rather than maintaining a separate <c>_softDeleted</c> collection, this implementation uses
/// <see cref="CachedSegment{TRange,TData}.IsRemoved"/> as the primary soft-delete filter.
/// The flag is set atomically by <see cref="CachedSegment{TRange,TData}.MarkAsRemoved"/>.
/// Removed nodes are physically unlinked from <c>_list</c> during <see cref="NormalizeStrideIndex"/>.
/// All read paths skip segments whose <c>IsRemoved</c> flag is set without needing a shared collection.
/// </para>
/// <para><strong>RCU semantics (Invariant VPC.B.5):</strong>
/// User Path threads read a stable stride index via <c>Volatile.Read</c>. New stride index arrays
/// are published atomically via <c>Volatile.Write</c> during normalization.</para>
/// <para><strong>Threading:</strong>
/// <see cref="ISegmentStorage{TRange,TData}.FindIntersecting"/> is called on the User Path (concurrent reads safe).
/// All other methods are Background-Path-only (single writer).</para>
/// <para>Alignment: Invariants VPC.A.10, VPC.B.5, VPC.C.2, VPC.C.3, S.H.4.</para>
/// </remarks>
internal sealed class LinkedListStrideIndexStorage<TRange, TData> : ISegmentStorage<TRange, TData>
    where TRange : IComparable<TRange>
{
    private const int DefaultStride = 16;
    private const int DefaultAppendBufferSize = 8;

    private readonly int _stride;
    private readonly int _strideAppendBufferSize;

    // Sorted linked list — mutated on Background Path only.
    private readonly LinkedList<CachedSegment<TRange, TData>> _list = [];

    // Stride index: every Nth node in the sorted list as a navigation anchor.
    // Published atomically via Volatile.Write; read via Volatile.Read on the User Path.
    private CachedSegment<TRange, TData>[] _strideIndex = [];

    // Maps each segment to its linked list node for O(1) removal.
    // Maintained on Background Path only.
    private readonly Dictionary<CachedSegment<TRange, TData>, LinkedListNode<CachedSegment<TRange, TData>>>
        _nodeMap = new(ReferenceEqualityComparer.Instance);

    // Stride append buffer: newly-added segments not yet reflected in the stride index.
    private readonly CachedSegment<TRange, TData>[] _strideAppendBuffer;
    private int _strideAppendCount;

    // Total count of live (non-removed) segments.
    // Decremented by Remove (which may be called from the TTL thread) via Interlocked.Decrement.
    // Incremented only on the Background Path via Interlocked.Increment.
    private int _count;

    /// <summary>
    /// Initializes a new <see cref="LinkedListStrideIndexStorage{TRange,TData}"/> with optional
    /// append buffer size and stride values.
    /// </summary>
    /// <param name="appendBufferSize">
    /// Number of segments accumulated in the stride append buffer before stride index
    /// normalization is triggered. Must be &gt;= 1. Default: 8.
    /// </param>
    /// <param name="stride">
    /// Distance between stride anchors (default 16). Must be &gt;= 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="appendBufferSize"/> or <paramref name="stride"/> is less than 1.
    /// </exception>
    public LinkedListStrideIndexStorage(int appendBufferSize = DefaultAppendBufferSize, int stride = DefaultStride)
    {
        if (appendBufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(appendBufferSize),
                "AppendBufferSize must be greater than or equal to 1.");
        }

        if (stride < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stride),
                "Stride must be greater than or equal to 1.");
        }

        _strideAppendBufferSize = appendBufferSize;
        _strideAppendBuffer = new CachedSegment<TRange, TData>[appendBufferSize];
        _stride = stride;
    }

    /// <inheritdoc/>
    public int Count => Volatile.Read(ref _count);

    /// <inheritdoc/>
    /// <remarks>
    /// <para><strong>Algorithm (O(log(n/N) + k + N + m)):</strong></para>
    /// <list type="number">
    /// <item><description>Acquire stable stride index via <c>Volatile.Read</c></description></item>
    /// <item><description>Binary-search stride index for the anchor just before <paramref name="range"/>.Start</description></item>
    /// <item><description>Walk the list forward from the anchor, collecting intersecting non-removed segments (checked via <see cref="CachedSegment{TRange,TData}.IsRemoved"/>)</description></item>
    /// </list>
    /// </remarks>
    public IReadOnlyList<CachedSegment<TRange, TData>> FindIntersecting(Range<TRange> range)
    {
        var strideIndex = Volatile.Read(ref _strideIndex);

        var results = new List<CachedSegment<TRange, TData>>();

        // todo try to deduplicate search mechanism
        // Binary search stride index: find the last anchor whose Start <= range.End
        // (the anchor just before or at the query range).
        // We want the rightmost anchor whose Start.Value <= range.End.Value.
        LinkedListNode<CachedSegment<TRange, TData>>? startNode = null;

        if (strideIndex.Length > 0)
        {
            var lo = 0;
            var hi = strideIndex.Length - 1;

            // Find the rightmost anchor where Start.Value <= range.End.Value.
            // Because the stride index is sorted ascending by Start.Value, we binary-search for
            // the largest index where anchor.Start.Value <= range.End.Value.
            while (lo <= hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (strideIndex[mid].Range.Start.Value.CompareTo(range.End.Value) <= 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            // hi is now the rightmost anchor with Start <= range.End.
            // Step back one more to ensure we start at or just before range.Start
            // (the anchor may cover part of range).
            var anchorIdx = hi > 0 ? hi - 1 : 0;
            if (hi >= 0)
            {
                // Look up the anchor segment in the node map to get the linked-list node.
                var anchorSeg = strideIndex[anchorIdx];
                if (_nodeMap.TryGetValue(anchorSeg, out var anchorNode))
                {
                    startNode = anchorNode;
                }
            }
        }

        // Walk linked list from the start node (or from head if no anchor found).
        var node = startNode ?? _list.First;

        while (node != null)
        {
            var seg = node.Value;

            // Short-circuit: if segment starts after range ends, no more candidates.
            if (seg.Range.Start.Value.CompareTo(range.End.Value) > 0)
            {
                break;
            }

            // Use IsRemoved flag as the primary soft-delete filter (no shared collection needed).
            if (!seg.IsRemoved && seg.Range.Overlaps(range))
            {
                results.Add(seg);
            }

            node = node.Next;
        }

        // NOTE: The stride append buffer does NOT need to be scanned separately.
        // All segments added via Add() are inserted into _list immediately (InsertSorted).
        // The stride append buffer only tracks which list entries haven't been reflected
        // in the stride index yet — they are already covered by the list walk above.

        return results;
    }

    /// <inheritdoc/>
    public void Add(CachedSegment<TRange, TData> segment)
    {
        // Insert into sorted position in the linked list.
        InsertSorted(segment);

        // Write to stride append buffer.
        _strideAppendBuffer[_strideAppendCount] = segment;
        _strideAppendCount++;
        Interlocked.Increment(ref _count);

        if (_strideAppendCount == _strideAppendBufferSize)
        {
            NormalizeStrideIndex();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Calls <see cref="CachedSegment{TRange,TData}.MarkAsRemoved"/> to atomically transition
    /// the segment to the removed state. If this is the first removal of the segment, <c>_count</c>
    /// is decremented and <see langword="true"/> is returned. Subsequent calls are no-ops
    /// (idempotent) and return <see langword="false"/>.
    /// </para>
    /// <para>
    /// The node is NOT physically unlinked immediately; it remains in <c>_list</c> until the next
    /// <see cref="NormalizeStrideIndex"/> pass. All read paths skip removed segments via the
    /// <see cref="CachedSegment{TRange,TData}.IsRemoved"/> flag.
    /// </para>
    /// <para><strong>Thread safety:</strong> Safe to call concurrently from the Background Path
    /// (eviction) and the TTL thread. <see cref="CachedSegment{TRange,TData}.MarkAsRemoved"/>
    /// uses <c>Interlocked.CompareExchange</c>; <c>_count</c> uses <c>Interlocked.Decrement</c>.
    /// </para>
    /// </remarks>
    public bool Remove(CachedSegment<TRange, TData> segment)
    {
        if (segment.MarkAsRemoved())
        {
            Interlocked.Decrement(ref _count);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public IReadOnlyList<CachedSegment<TRange, TData>> GetAllSegments()
    {
        var results = new List<CachedSegment<TRange, TData>>(_count);

        var node = _list.First;
        while (node != null)
        {
            if (!node.Value.IsRemoved)
            {
                results.Add(node.Value);
            }

            node = node.Next;
        }

        // Also include segments currently in the stride append buffer that are not in the list yet.
        // Note: InsertSorted already adds to _list, so all segments are in _list. The stride
        // append buffer just tracks which are not yet reflected in the stride index.
        // GetAllSegments returns live list segments (already done above).

        return results;
    }

    /// <summary>
    /// Inserts a segment into the linked list in sorted order by range start value.
    /// Also registers the node in <see cref="_nodeMap"/> for O(1) lookup.
    /// </summary>
    private void InsertSorted(CachedSegment<TRange, TData> segment)
    {
        if (_list.Count == 0)
        {
            var node = _list.AddFirst(segment);
            _nodeMap[segment] = node;
            return;
        }

        // Use stride index to find a close insertion point (O(log(n/N)) search + O(N) walk).
        var strideIndex = Volatile.Read(ref _strideIndex);
        LinkedListNode<CachedSegment<TRange, TData>>? insertAfter = null;

        if (strideIndex.Length > 0)
        {
            // Binary search: find last anchor with Start.Value <= segment.Range.Start.Value.
            var lo = 0;
            var hi = strideIndex.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (strideIndex[mid].Range.Start.Value.CompareTo(segment.Range.Start.Value) <= 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (hi >= 0 && _nodeMap.TryGetValue(strideIndex[hi], out var anchorNode))
            {
                insertAfter = anchorNode;
            }
        }

        // Walk forward from anchor (or from head) to find insertion position.
        var current = insertAfter ?? _list.First;

        // If insertAfter is set, we start walking from that node.
        // Walk until we find the first node with Start > segment.Range.Start.
        if (insertAfter != null)
        {
            // Walk forward while next node starts before or at our value.
            while (current!.Next != null &&
                   current.Next.Value.Range.Start.Value.CompareTo(segment.Range.Start.Value) <= 0)
            {
                current = current.Next;
            }

            // Now insert after current.
            var newNode = _list.AddAfter(current, segment);
            _nodeMap[segment] = newNode;
        }
        else
        {
            // No anchor, walk from head.
            if (current != null &&
                current.Value.Range.Start.Value.CompareTo(segment.Range.Start.Value) > 0)
            {
                // Insert before the first node.
                var newNode = _list.AddBefore(current, segment);
                _nodeMap[segment] = newNode;
            }
            else
            {
                // Walk forward to find insertion position.
                while (current!.Next != null &&
                       current.Next.Value.Range.Start.Value.CompareTo(segment.Range.Start.Value) <= 0)
                {
                    current = current.Next;
                }

                var newNode = _list.AddAfter(current, segment);
                _nodeMap[segment] = newNode;
            }
        }
    }

    /// <summary>
    /// Rebuilds the stride index by walking the live linked list, physically removing nodes
    /// whose <see cref="CachedSegment{TRange,TData}.IsRemoved"/> flag is set, collecting every
    /// Nth live node as a stride anchor, and atomically publishing the new stride index via
    /// <c>Volatile.Write</c>.
    /// </summary>
    /// <remarks>
    /// <para><strong>Algorithm:</strong> O(n) list traversal + O(n/N) stride array allocation.</para>
    /// <para>
    /// Resets <c>_strideAppendCount</c> to 0 and publishes the new stride index atomically.
    /// Removed segments are physically unlinked from <c>_list</c> and evicted from <c>_nodeMap</c>
    /// during this pass, reclaiming memory.
    /// </para>
    /// </remarks>
    private void NormalizeStrideIndex()
    {
        // First pass: physically unlink removed nodes from the list.
        var node = _list.First;
        while (node != null)
        {
            var next = node.Next;
            if (node.Value.IsRemoved)
            {
                _nodeMap.Remove(node.Value);
                _list.Remove(node);
            }

            node = next;
        }

        // Second pass: walk live list and collect every Nth node as a stride anchor.
        var liveCount = _list.Count;
        var anchorCount = liveCount == 0 ? 0 : (liveCount + _stride - 1) / _stride;
        var newStrideIndex = new CachedSegment<TRange, TData>[anchorCount];

        var current = _list.First;
        var nodeIdx = 0;
        var anchorIdx = 0;

        while (current != null)
        {
            if (nodeIdx % _stride == 0 && anchorIdx < anchorCount)
            {
                newStrideIndex[anchorIdx++] = current.Value;
            }

            current = current.Next;
            nodeIdx++;
        }

        // Reset stride append buffer.
        Array.Clear(_strideAppendBuffer, 0, _strideAppendBufferSize);
        _strideAppendCount = 0;

        // Atomically publish new stride index (release fence — User Path reads with acquire fence).
        Volatile.Write(ref _strideIndex, newStrideIndex);
    }
}
