using Intervals.NET.Data;
using Intervals.NET.Data.Extensions;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Extensions;
using Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Execution;
using Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Intent;
using Intervals.NET.Caching.SlidingWindow.Core.State;
using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.SlidingWindow.Public.Instrumentation;

namespace Intervals.NET.Caching.SlidingWindow.Core.UserPath;

/// <summary>
/// Handles user requests synchronously, serving data from cache or data source. See docs/sliding-window/ for design details.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
internal sealed class UserRequestHandler<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly CacheState<TRange, TData, TDomain> _state;
    private readonly CacheDataExtender<TRange, TData, TDomain> _cacheExtensionService;
    private readonly IntentController<TRange, TData, TDomain> _intentController;
    private readonly IDataSource<TRange, TData> _dataSource;
    private readonly ISlidingWindowCacheDiagnostics _cacheDiagnostics;

    // Disposal state tracking (lock-free using Interlocked)
    // 0 = not disposed, 1 = disposed
    private int _disposeState;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRequestHandler{TRange,TData,TDomain}"/> class.
    /// </summary>
    /// <param name="state">The cache state.</param>
    /// <param name="cacheExtensionService">The cache data extender for extending cache coverage.</param>
    /// <param name="intentController">The intent controller for publishing rebalance intents.</param>
    /// <param name="dataSource">The data source to request missing data from.</param>
    /// <param name="cacheDiagnostics">The diagnostics interface for recording cache metrics and events.</param>
    public UserRequestHandler(CacheState<TRange, TData, TDomain> state,
        CacheDataExtender<TRange, TData, TDomain> cacheExtensionService,
        IntentController<TRange, TData, TDomain> intentController,
        IDataSource<TRange, TData> dataSource,
        ISlidingWindowCacheDiagnostics cacheDiagnostics
    )
    {
        _state = state;
        _cacheExtensionService = cacheExtensionService;
        _intentController = intentController;
        _dataSource = dataSource;
        _cacheDiagnostics = cacheDiagnostics;
    }

    /// <summary>
    /// Handles a user request for the specified range.
    /// </summary>
    /// <param name="requestedRange">The range requested by the user.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a 
    /// <see cref="RangeResult{TRange, TData}"/> with the actual available range and data.
    /// The Range may be null if no data is available, or a subset of requestedRange if truncated at boundaries.
    /// </returns>
    public async ValueTask<RangeResult<TRange, TData>> HandleRequestAsync(
        Range<TRange> requestedRange,
        CancellationToken cancellationToken)
    {
        // Check disposal state using Volatile.Read (lock-free)
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(
                nameof(UserRequestHandler<TRange, TData, TDomain>),
                "Cannot handle request on a disposed handler.");
        }

        var cacheStorage = _state.Storage;
        // Bare Range reads without lock/volatile fence are intentional.
        // CacheState follows an eventual-consistency model on the user path: IsInitialized,
        // Storage.Range, and the storage contents are all written by the single rebalance writer.
        // A slightly stale Range observation here at most causes the user path to take a
        // suboptimal branch (e.g., treating a full hit as a partial hit), but the intent
        // published at the end of this method will drive the system back to the correct state.
        var fullyInCache = _state.IsInitialized && cacheStorage.Range.Contains(requestedRange);
        var hasOverlap = _state.IsInitialized && !fullyInCache && cacheStorage.Range.Overlaps(requestedRange);

        RangeData<TRange, TData, TDomain>? assembledData;
        Range<TRange>? actualRange;
        ReadOnlyMemory<TData> resultData;
        CacheInteraction cacheInteraction;

        if (!fullyInCache && !hasOverlap)
        {
            // Scenario 1 (Cold Start) & Scenario 4 (Full Cache Miss / Non-intersecting Jump):
            // Cache is uninitialised or RequestedRange does not overlap CurrentCacheRange.
            // Fetch ONLY the requested range from IDataSource.
            (assembledData, actualRange, resultData) =
                await FetchSingleRangeAsync(requestedRange, cancellationToken).ConfigureAwait(false);
            cacheInteraction = CacheInteraction.FullMiss;
            _cacheDiagnostics.UserRequestFullCacheMiss();
        }
        else if (fullyInCache)
        {
            // Scenario 2: Full Cache Hit
            // All requested data is available in cache - read directly (no IDataSource call).
            assembledData = cacheStorage.ToRangeData();
            actualRange = requestedRange; // Fully in cache, so actual == requested
            resultData = cacheStorage.Read(requestedRange);
            cacheInteraction = CacheInteraction.FullHit;
            _cacheDiagnostics.UserRequestFullCacheHit();
        }
        else
        {
            // Scenario 3: Partial Cache Hit
            // RequestedRange intersects CurrentCacheRange - read from cache and fetch missing parts.
            // NOTE: storage.Read cannot be used here because we need a contiguous range that may
            // require concatenating multiple segments (cached + fetched).
            assembledData = await _cacheExtensionService.ExtendCacheAsync(
                cacheStorage.ToRangeData(),
                requestedRange,
                cancellationToken
            ).ConfigureAwait(false);

            cacheInteraction = CacheInteraction.PartialHit;
            _cacheDiagnostics.UserRequestPartialCacheHit();

            // Compute actual available range (intersection of requested and assembled).
            // assembledData.Range may not fully cover requestedRange if DataSource returned
            // truncated/null chunks (e.g., bounded source where some segments are unavailable).
            actualRange = assembledData.Range.Intersect(requestedRange);

            if (actualRange.HasValue)
            {
                // Slice to the actual available range (may be smaller than requestedRange).
                resultData = MaterialiseData(assembledData[actualRange.Value]);
            }
            else
            {
                // No actual intersection after extension (defensive fallback).
                assembledData = null;
                resultData = ReadOnlyMemory<TData>.Empty;
            }
        }

        // Publish intent only when there was a physical data hit (assembledData is not null).
        // Full vacuum (out-of-physical-bounds) requests produce no intent — there is no
        // meaningful cache shift to signal to the rebalance pipeline (see Invariant SWC.C.8e).
        if (assembledData is not null)
        {
            _intentController.PublishIntent(new Intent<TRange, TData, TDomain>(requestedRange, assembledData));
        }

        // UserRequestServed fires for ALL successful completions, including boundary misses
        // where assembledData == null (full vacuum / out-of-physical-bounds).
        _cacheDiagnostics.UserRequestServed();

        return new RangeResult<TRange, TData>(actualRange, resultData, cacheInteraction);
    }

    /// <summary>
    /// Disposes the user request handler, shutting down the intent controller.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal operation.</returns>
    internal async ValueTask DisposeAsync()
    {
        // Idempotent check using lock-free Interlocked.CompareExchange
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
        {
            return; // Already disposed
        }

        // Dispose intent controller (cascades to execution controller)
        await _intentController.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches data for a single range directly from the data source, without involving the cache.
    /// </summary>
    /// <param name="requestedRange">The range to fetch.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A named tuple of (AssembledData, ActualRange, ResultData). <c>AssembledData</c> is null and
    /// <c>ActualRange</c> is null when the data source reports no data is available for the range.
    /// </returns>
    private async ValueTask<(RangeData<TRange, TData, TDomain>? AssembledData, Range<TRange>? ActualRange, ReadOnlyMemory<TData> ResultData)>
        FetchSingleRangeAsync(Range<TRange> requestedRange, CancellationToken cancellationToken)
    {
        _cacheDiagnostics.DataSourceFetchSingleRange();
        var fetchedChunk = await _dataSource.FetchAsync(requestedRange, cancellationToken)
            .ConfigureAwait(false);

        // Handle boundary: chunk.Range may be null when the requested range lies entirely
        // outside the physical bounds of the data source.
        if (!fetchedChunk.Range.HasValue)
        {
            return (AssembledData: null, ActualRange: null, ResultData: ReadOnlyMemory<TData>.Empty);
        }

        var assembledData = fetchedChunk.Data.ToRangeData(fetchedChunk.Range.Value, _state.Domain);
        return (AssembledData: assembledData, ActualRange: fetchedChunk.Range.Value, ResultData: MaterialiseData(assembledData));
    }

    /// <summary>
    /// Materialises the data of a <see cref="RangeData{TRange,TData,TDomain}"/> into a
    /// <see cref="ReadOnlyMemory{T}"/> buffer.
    /// </summary>
    private static ReadOnlyMemory<TData> MaterialiseData(RangeData<TRange, TData, TDomain> data)
        => new(data.Data.ToArray());
}