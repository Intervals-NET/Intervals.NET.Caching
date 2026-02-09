using Intervals.NET;
using Intervals.NET.Data;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Extensions;
using SlidingWindowCache.CacheRebalance;
using SlidingWindowCache.CacheRebalance.Executor;

namespace SlidingWindowCache.UserPath;

/// <summary>
/// Handles user requests synchronously, serving data from cache or data source.
/// This is the Fast Path Actor that operates in the User Thread.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> User Thread</para>
/// <para><strong>Critical Contract:</strong></para>
/// <para>
/// Every user access produces a rebalance intent.
/// The UserRequestHandler NEVER invokes decision logic.
/// </para>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>Handles user requests synchronously</description></item>
/// <item><description>Decides how to serve RequestedRange (from cache, from IDataSource, or mixed)</description></item>
/// <item><description>Updates LastRequestedRange and CacheData/CurrentCacheRange only to cover RequestedRange</description></item>
/// <item><description>Triggers rebalance intent (fire-and-forget)</description></item>
/// <item><description>Never blocks on rebalance</description></item>
/// </list>
/// <para><strong>Explicit Non-Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>❌ NEVER checks NoRebalanceRange (belongs to DecisionEngine)</description></item>
/// <item><description>❌ NEVER computes DesiredCacheRange (belongs to GeometryPolicy)</description></item>
/// <item><description>❌ NEVER decides whether to rebalance (belongs to DecisionEngine)</description></item>
/// <item><description>❌ No cache normalization</description></item>
/// <item><description>❌ No trimming or shrinking</description></item>
/// </list>
/// </remarks>
internal sealed class UserRequestHandler<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly CacheState<TRange, TData, TDomain> _state;
    private readonly CacheDataFetcher<TRange, TData, TDomain> _cacheFetcher;
    private readonly IntentController<TRange, TData, TDomain> _intentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRequestHandler{TRange,TData,TDomain}"/> class.
    /// </summary>
    /// <param name="state">The cache state.</param>
    /// <param name="cacheFetcher">The cache data fetcher for extending cache coverage.</param>
    /// <param name="intentManager">The intent controller for publishing rebalance intents.</param>
    public UserRequestHandler(
        CacheState<TRange, TData, TDomain> state,
        CacheDataFetcher<TRange, TData, TDomain> cacheFetcher,
        IntentController<TRange, TData, TDomain> intentManager)
    {
        _state = state;
        _cacheFetcher = cacheFetcher;
        _intentManager = intentManager;
    }

    /// <summary>
    /// Handles a user request for the specified range.
    /// </summary>
    /// <param name="requestedRange">The range requested by the user.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <see cref="ReadOnlyMemory{T}"/>
    /// of data for the specified range from the materialized cache.
    /// </returns>
    /// <remarks>
    /// <para>This method implements the User Path logic:</para>
    /// <list type="number">
    /// <item><description>Cancel any pending/ongoing rebalance (Invariant A.1-0a: User Path priority)</description></item>
    /// <item><description>Check if requested range is fully covered by cache</description></item>
    /// <item><description>If not, extend cache to cover requested range (User Path mutation allowed for expansion)</description></item>
    /// <item><description>Update LastRequestedRange</description></item>
    /// <item><description>Publish rebalance intent (fire-and-forget, NEVER invokes decision logic)</description></item>
    /// <item><description>Return data for requested range from materialized cache</description></item>
    /// </list>
    /// </remarks>
    public async ValueTask<ReadOnlyMemory<TData>> HandleRequestAsync(
        Range<TRange> requestedRange,
        CancellationToken cancellationToken)
    {
        // CRITICAL: Cancel any pending/ongoing rebalance FIRST, before any cache access
        // This satisfies Invariant A.1-0a: "Every User Request MUST cancel any ongoing or pending
        // Rebalance Execution before performing cache mutations"
        // This also implements State Machine Transition T4: User Path cancels rebalance before mutations
        _intentManager.CancelPendingRebalance();

        // Check if cache is cold (never used)
        var isColdStart = !_state.LastRequested.HasValue;

        // User Path: Check if the requested range is fully covered by the cache
        if (isColdStart || !_state.Cache.Range.Contains(requestedRange))
        {
            RangeData<TRange, TData, TDomain> newCacheData;
            bool isExpansion;

            if (isColdStart)
            {
                // Scenario 1: Cold Start (Invariant A.3.8)
                // Initial cache population - fetch data ONLY for requested range
                isExpansion = false;
                newCacheData = await _cacheFetcher.FetchDataAsync(requestedRange, cancellationToken);
            }
            else
            {
                var currentCacheData = _state.Cache.ToRangeData();
                var hasIntersection = currentCacheData.Range.Intersect(requestedRange).HasValue;

                if (hasIntersection)
                {
                    // Scenario 2: Cache Expansion (Invariant A.3.8)
                    // RequestedRange intersects CurrentCacheRange - extend cache to cover requested range
                    // This preserves all existing data and only fetches missing parts
                    isExpansion = true;
                    newCacheData =
                        await _cacheFetcher.ExtendCacheAsync(currentCacheData, requestedRange, cancellationToken);
                }
                else
                {
                    // Scenario 3: Full Cache Replacement (Invariant A.3.8 & A.3.9b)
                    // RequestedRange does NOT intersect CurrentCacheRange
                    // MUST fully replace cache - fetch ONLY the requested range, discard old cache
                    // Per Invariant A.3.9b: "If RequestedRange does NOT intersect CurrentCacheRange,
                    // the User Path MUST fully replace both CacheData and CurrentCacheRange"
                    isExpansion = false;
                    newCacheData = await _cacheFetcher.FetchDataAsync(requestedRange, cancellationToken);
                }
            }

            // Materialize the new cache data (atomic update)
            _state.Cache.Rematerialize(newCacheData);

#if DEBUG
            // Track cache mutation type
            if (isExpansion)
            {
                Instrumentation.CacheInstrumentationCounters.OnCacheExpanded();
            }
            else
            {
                Instrumentation.CacheInstrumentationCounters.OnCacheReplaced();
            }
#endif
        }

        // CRITICAL: Read from cache IMMEDIATELY after ensuring it contains the requested range
        // This minimizes the window for race conditions in concurrent scenarios
        var result = _state.Cache.Read(requestedRange);

        // Update the last requested range
        _state.LastRequested = requestedRange;

        // Publish rebalance intent (fire-and-forget)
        // UserRequestHandler NEVER invokes decision logic - it only publishes intents
        _intentManager.PublishIntent(requestedRange);

#if DEBUG
        Instrumentation.CacheInstrumentationCounters.OnUserRequestServed();
#endif

        // Return the data
        return result;
    }
}