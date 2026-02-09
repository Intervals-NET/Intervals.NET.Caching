using Intervals.NET;
using Intervals.NET.Domain.Abstractions;
using SlidingWindowCache.CacheRebalance.Policy;

namespace SlidingWindowCache.CacheRebalance.Executor;

/// <summary>
/// Executes rebalance operations by fetching missing data, merging with existing cache,
/// and trimming to the desired range. This is the sole component responsible for cache normalization.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> Background / ThreadPool</para>
/// <para><strong>Characteristics:</strong> Asynchronous, cancellable, heavyweight</para>
/// <para><strong>Responsibility:</strong> Cache normalization (expand, trim, recompute NoRebalanceRange)</para>
/// </remarks>
internal sealed class RebalanceExecutor<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly CacheState<TRange, TData, TDomain> _state;
    private readonly CacheDataFetcher<TRange, TData, TDomain> _cacheFetcher;
    private readonly ThresholdRebalancePolicy<TRange, TDomain> _rebalancePolicy;

    public RebalanceExecutor(
        CacheState<TRange, TData, TDomain> state,
        CacheDataFetcher<TRange, TData, TDomain> cacheFetcher,
        ThresholdRebalancePolicy<TRange, TDomain> rebalancePolicy)
    {
        _state = state;
        _cacheFetcher = cacheFetcher;
        _rebalancePolicy = rebalancePolicy;
    }

    /// <summary>
    /// Executes rebalance by normalizing the cache to the desired range.
    /// </summary>
    /// <param name="desiredRange">The target cache range to normalize to.</param>
    /// <param name="cancellationToken">Cancellation token to support cancellation at all stages.</param>
    /// <returns>A task representing the asynchronous rebalance operation.</returns>
    public async Task ExecuteAsync(Range<TRange> desiredRange, CancellationToken cancellationToken)
    {
        // Get current cache data snapshot
        var rangeData = _state.Cache.ToRangeData();

        // Check if desired range equals current range (Decision Path D2)
        // This is a final check before expensive I/O operations
        if (rangeData.Range == desiredRange)
        {
#if DEBUG
            Instrumentation.CacheInstrumentationCounters.OnRebalanceSkippedSameRange();
#endif
            return; // No-op, cache already at desired state
        }

        // Cancellation check after decision but before expensive I/O
        // Satisfies Invariant 34a: "Rebalance Execution MUST yield to User Path requests immediately"
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 1: Extend cache to cover desired range (fetch missing data)
        // This operation is cancellable and will throw OperationCanceledException if cancelled
        var extended = await _cacheFetcher.ExtendCacheAsync(rangeData, desiredRange, cancellationToken);

        // Cancellation check after I/O but before mutation
        // If User Path cancelled us, don't apply the rebalance result
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 2: Trim to desired range (rebalancing-specific: discard data outside desired range)
        var rebalanced = extended[desiredRange];

        // Final cancellation check before applying mutation
        // Ensures we don't apply obsolete rebalance results
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 3: Update the cache with the rebalanced data (atomic mutation)
        _state.Cache.Rematerialize(rebalanced);

        // Phase 4: Update the no-rebalance range to prevent unnecessary rebalancing
        _state.NoRebalanceRange = _rebalancePolicy.GetNoRebalanceRange(_state.Cache.Range);
    }
}