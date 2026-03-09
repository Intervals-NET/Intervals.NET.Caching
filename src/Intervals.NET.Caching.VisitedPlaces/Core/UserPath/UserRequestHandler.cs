using Intervals.NET.Extensions;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.Extensions;
using Intervals.NET.Caching.Infrastructure;
using Intervals.NET.Caching.Infrastructure.Scheduling;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;
using Intervals.NET.Data;
using Intervals.NET.Data.Extensions;

namespace Intervals.NET.Caching.VisitedPlaces.Core.UserPath;

/// <summary>
/// Handles user requests on the User Path: reads cached segments, computes gaps, fetches missing
/// data from <c>IDataSource</c>, assembles the result, and publishes a
/// <see cref="CacheNormalizationRequest{TRange,TData}"/> (fire-and-forget) for the Background Storage Loop.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the range domain.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> User Thread</para>
/// <para><strong>Critical Contract — User Path is READ-ONLY (Invariant VPC.A.10):</strong></para>
/// <para>
/// This handler NEVER mutates <see cref="ISegmentStorage{TRange,TData}"/>. All cache writes are
/// performed exclusively by the Background Storage Loop (single writer).
/// </para>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>Read intersecting segments from storage</description></item>
/// <item><description>Compute coverage gaps within the requested range</description></item>
/// <item><description>Fetch gap data from <c>IDataSource</c> (User Path — inline, synchronous w.r.t. the request)</description></item>
/// <item><description>Assemble and return a <see cref="RangeResult{TRange,TData}"/></description></item>
/// <item><description>Publish a <see cref="CacheNormalizationRequest{TRange,TData}"/> (fire-and-forget)</description></item>
/// </list>
/// </remarks>
internal sealed class UserRequestHandler<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly ISegmentStorage<TRange, TData> _storage;
    private readonly IDataSource<TRange, TData> _dataSource;
    private readonly IWorkScheduler<CacheNormalizationRequest<TRange, TData>> _scheduler;
    private readonly IVisitedPlacesCacheDiagnostics _diagnostics;
    private readonly TDomain _domain;

    // Disposal state: 0 = active, 1 = disposed
    private int _disposeState;

    /// <summary>
    /// Initializes a new <see cref="UserRequestHandler{TRange,TData,TDomain}"/>.
    /// </summary>
    public UserRequestHandler(
        ISegmentStorage<TRange, TData> storage,
        IDataSource<TRange, TData> dataSource,
        IWorkScheduler<CacheNormalizationRequest<TRange, TData>> scheduler,
        IVisitedPlacesCacheDiagnostics diagnostics,
        TDomain domain)
    {
        _storage = storage;
        _dataSource = dataSource;
        _scheduler = scheduler;
        _diagnostics = diagnostics;
        _domain = domain;
    }

    /// <summary>
    /// Handles a user request for the specified range.
    /// </summary>
    /// <param name="requestedRange">The range requested by the user.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{T}"/> containing the assembled <see cref="RangeResult{TRange,TData}"/>.
    /// </returns>
    /// <remarks>
    /// <para><strong>Algorithm:</strong></para>
    /// <list type="number">
    /// <item><description>Find intersecting segments via <c>storage.FindIntersecting</c></description></item>
    /// <item><description>Map segments to <see cref="RangeData{TRangeType,TDataType,TRangeDomain}"/> (zero-copy via <see cref="ReadOnlyMemoryEnumerable{T}"/>)</description></item>
    /// <item><description>Compute gaps (sub-ranges not covered by any hitting segment)</description></item>
    /// <item><description>Determine scenario: FullHit (no gaps), FullMiss (no segments hit), or PartialHit (some gaps)</description></item>
    /// <item><description>Fetch gap data from IDataSource (FullMiss / PartialHit)</description></item>
    /// <item><description>Assemble result data from <see cref="RangeData{TRangeType,TDataType,TRangeDomain}"/> sources</description></item>
    /// <item><description>Publish CacheNormalizationRequest (fire-and-forget)</description></item>
    /// <item><description>Return RangeResult immediately</description></item>
    /// </list>
    /// </remarks>
    public async ValueTask<RangeResult<TRange, TData>> HandleRequestAsync(
        Range<TRange> requestedRange,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(
                nameof(UserRequestHandler<TRange, TData, TDomain>),
                "Cannot handle request on a disposed handler.");
        }

        // Step 1: Read intersecting segments (read-only, Invariant VPC.A.10).
        var hittingSegments = _storage.FindIntersecting(requestedRange);

        // Step 2: Map segments to RangeData — zero-copy via ReadOnlyMemoryEnumerable.
        var hittingRangeData = hittingSegments
            .Select(s => SegmentToRangeData(s, _domain))
            .ToList();

        // Step 3: Compute coverage gaps.
        var gaps = ComputeGaps(requestedRange, hittingSegments);

        CacheInteraction cacheInteraction;
        IReadOnlyList<RangeChunk<TRange, TData>>? fetchedChunks;
        ReadOnlyMemory<TData> resultData;
        Range<TRange>? actualRange;

        if (gaps.Count == 0 && hittingRangeData.Count > 0)
        {
            // Full Hit: entire requested range is covered by cached segments.
            cacheInteraction = CacheInteraction.FullHit;
            _diagnostics.UserRequestFullCacheHit();

            (resultData, actualRange) = Assemble(requestedRange, hittingRangeData);
            fetchedChunks = null; // Signal to background: no new data to store
        }
        else if (hittingRangeData.Count == 0)
        {
            // Full Miss: no cached data at all for this range.
            cacheInteraction = CacheInteraction.FullMiss;
            _diagnostics.UserRequestFullCacheMiss();

            var chunk = await _dataSource.FetchAsync(requestedRange, cancellationToken)
                .ConfigureAwait(false);

            _diagnostics.DataSourceFetchGap();

            fetchedChunks = [chunk];
            actualRange = chunk.Range;
            resultData = chunk.Range.HasValue
                ? MaterialiseData(chunk.Data)
                : ReadOnlyMemory<TData>.Empty;
        }
        else
        {
            // Partial Hit: some cached data, some gaps to fill.
            cacheInteraction = CacheInteraction.PartialHit;
            _diagnostics.UserRequestPartialCacheHit();

            // Fetch all gaps from IDataSource.
            var chunks = await _dataSource.FetchAsync(gaps, cancellationToken)
                .ConfigureAwait(false);

            fetchedChunks = [.. chunks];

            // Fire one diagnostic event per gap fetched.
            for (var i = 0; i < gaps.Count; i++)
            {
                _diagnostics.DataSourceFetchGap();
            }

            // Map fetched chunks to RangeData and merge with hitting segments.
            var chunkRangeData = fetchedChunks
                .Where(c => c.Range.HasValue)
                .Select(c => c.Data.ToRangeData(c.Range!.Value, _domain));

            // Assemble result from all RangeData sources (segments + fetched chunks).
            (resultData, actualRange) = Assemble(requestedRange, [.. hittingRangeData, .. chunkRangeData]);
        }

        // Step 7: Publish CacheNormalizationRequest and await the enqueue (preserves activity counter correctness).
        // Awaiting PublishWorkItemAsync only waits for the channel enqueue — not background processing —
        // so fire-and-forget semantics are preserved. The background loop handles processing asynchronously.
        var request = new CacheNormalizationRequest<TRange, TData>(
            requestedRange,
            hittingSegments,
            fetchedChunks);

        await _scheduler.PublishWorkItemAsync(request, cancellationToken)
            .ConfigureAwait(false);

        _diagnostics.UserRequestServed();

        return new RangeResult<TRange, TData>(actualRange, resultData, cacheInteraction);
    }

    /// <summary>
    /// Disposes the handler and shuts down the background scheduler.
    /// </summary>
    internal async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
        {
            return; // Already disposed
        }

        await _scheduler.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the gaps in <paramref name="requestedRange"/> not covered by
    /// <paramref name="hittingSegments"/>.
    /// </summary>
    private static IReadOnlyList<Range<TRange>> ComputeGaps(
        Range<TRange> requestedRange,
        IReadOnlyList<CachedSegment<TRange, TData>> hittingSegments)
    {
        if (hittingSegments.Count == 0)
        {
            return [requestedRange];
        }

        IEnumerable<Range<TRange>> remaining = [requestedRange];

        // Iteratively subtract each hitting segment's range from the remaining uncovered ranges.
        // The complexity is O(n*m) where n is the number of hitting segments
        // and m is the number of remaining ranges at each step,
        // but in practice m should be small (often 1) due to the nature of typical cache hits.
        foreach (var seg in hittingSegments)
        {
            var segRange = seg.Range;
            remaining = remaining.SelectMany(r =>
            {
                var intersection = r.Intersect(segRange);
                return intersection.HasValue ? r.Except(intersection.Value) : [r];
            });
        }

        return [..remaining];
    }

    /// <summary>
    /// Assembles result data from a list of <see cref="RangeData{TRangeType,TDataType,TRangeDomain}"/>
    /// sources (cached segments and/or fetched chunks) clipped to <paramref name="requestedRange"/>.
    /// </summary>
    /// <param name="requestedRange">The range to assemble data for.</param>
    /// <param name="sources">Domain-aware data sources, in any order.</param>
    /// <returns>
    /// The assembled <see cref="ReadOnlyMemory{T}"/> and the actual available range
    /// (<see langword="null"/> when no source intersects <paramref name="requestedRange"/>).
    /// </returns>
    /// <remarks>
    /// Each source is intersected with <paramref name="requestedRange"/>, sliced lazily in domain
    /// space via the <see cref="RangeData{TRangeType,TDataType,TRangeDomain}"/> indexer, and then
    /// materialized. Only the intersection portion is ever allocated — no oversized backing arrays.
    /// </remarks>
    private static (ReadOnlyMemory<TData> Data, Range<TRange>? ActualRange) Assemble(
        Range<TRange> requestedRange,
        IReadOnlyList<RangeData<TRange, TData, TDomain>> sources)
    {
        var pieces = new List<(TRange Start, ReadOnlyMemory<TData> Data)>(sources.Count);

        foreach (var source in sources)
        {
            var intersectionRange = source.Range.Intersect(requestedRange);
            if (!intersectionRange.HasValue)
            {
                continue;
            }

            // Slice lazily in domain space, then materialize only the intersection portion.
            var slicedData = MaterialiseData(source[intersectionRange.Value].Data);
            pieces.Add((intersectionRange.Value.Start.Value, slicedData));
        }

        if (pieces.Count == 0)
        {
            return (ReadOnlyMemory<TData>.Empty, null);
        }

        // Sort pieces by start and concatenate.
        pieces.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        var totalLength = pieces.Sum(p => p.Data.Length);
        var assembled = ConcatenateMemory(pieces.Select(p => p.Data), totalLength);

        // Determine actual range: from requestedRange.Start to requestedRange.End
        // (bounded by what we actually assembled — use requestedRange as approximation).
        return (assembled, requestedRange);
    }

    /// <summary>
    /// Converts a <see cref="CachedSegment{TRange,TData}"/> to a
    /// <see cref="RangeData{TRangeType,TDataType,TRangeDomain}"/> using a zero-copy
    /// <see cref="ReadOnlyMemoryEnumerable{T}"/> wrapper — no array allocation or data copy occurs.
    /// </summary>
    private static RangeData<TRange, TData, TDomain> SegmentToRangeData(
        CachedSegment<TRange, TData> segment,
        TDomain domain
    ) => new ReadOnlyMemoryEnumerable<TData>(segment.Data).ToRangeData(segment.Range, domain);

    private static ReadOnlyMemory<TData> MaterialiseData(IEnumerable<TData> data)
        => new(data.ToArray());

    private static ReadOnlyMemory<TData> ConcatenateMemory(
        IEnumerable<ReadOnlyMemory<TData>> pieces,
        int totalLength)
    {
        using var enumerator = pieces.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return ReadOnlyMemory<TData>.Empty;
        }

        var first = enumerator.Current;

        if (!enumerator.MoveNext())
        {
            return first;
        }

        var result = new TData[totalLength];
        var offset = 0;

        first.Span.CopyTo(result.AsSpan(offset));
        offset += first.Length;

        do
        {
            var piece = enumerator.Current;
            piece.Span.CopyTo(result.AsSpan(offset));
            offset += piece.Length;
        } while (enumerator.MoveNext());

        return result;
    }
}