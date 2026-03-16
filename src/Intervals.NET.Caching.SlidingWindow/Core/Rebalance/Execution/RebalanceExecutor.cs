using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Intent;
using Intervals.NET.Caching.SlidingWindow.Core.State;
using Intervals.NET.Caching.SlidingWindow.Public.Instrumentation;

namespace Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Execution;

/// <summary>
/// Executes rebalance operations by fetching missing data, merging with existing cache,
/// and trimming to the desired range. See docs/sliding-window/ for design details.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
internal sealed class RebalanceExecutor<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly CacheState<TRange, TData, TDomain> _state;
    private readonly CacheDataExtender<TRange, TData, TDomain> _cacheExtensionService;
    private readonly ISlidingWindowCacheDiagnostics _cacheDiagnostics;

    public RebalanceExecutor(
        CacheState<TRange, TData, TDomain> state,
        CacheDataExtender<TRange, TData, TDomain> cacheExtensionService,
        ISlidingWindowCacheDiagnostics cacheDiagnostics
    )
    {
        _state = state;
        _cacheExtensionService = cacheExtensionService;
        _cacheDiagnostics = cacheDiagnostics;
    }

    /// <summary>
    /// Executes rebalance by normalizing the cache to the desired range.
    /// This is the ONLY component that mutates cache state (single-writer architecture).
    /// </summary>
    /// <param name="intent">The intent with data that was actually assembled in UserPath and the requested range.</param>
    /// <param name="desiredRange">The target cache range to normalize to.</param>
    /// <param name="desiredNoRebalanceRange">The no-rebalance range for the target cache state.</param>
    /// <param name="cancellationToken">Cancellation token to support cancellation at all stages.</param>
    /// <returns>A task representing the asynchronous rebalance operation.</returns>
    public async Task ExecuteAsync(
        Intent<TRange, TData, TDomain> intent,
        Range<TRange> desiredRange,
        Range<TRange>? desiredNoRebalanceRange,
        CancellationToken cancellationToken)
    {
        // Use delivered data as the base - this is what the user received
        var baseRangeData = intent.AssembledRangeData;

        // Cancellation check before expensive I/O
        // Satisfies SWC.F.1a: "Rebalance Execution MUST yield to User Path requests immediately"
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 1: Extend delivered data to cover desired range (fetch only truly missing data)
        // Use delivered data as base instead of current cache to ensure consistency
        var extended = await _cacheExtensionService.ExtendCacheAsync(baseRangeData, desiredRange, cancellationToken)
            .ConfigureAwait(false);

        // Cancellation check after I/O but before mutation
        // If User Path cancelled us, don't apply the rebalance result
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 2: Trim to desired range (rebalancing-specific: discard data outside desired range)
        var normalizedData = extended[desiredRange];

        // Final cancellation check before applying mutation
        // Ensures we don't apply obsolete rebalance results
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 3: Apply cache state mutations (single writer � all fields updated atomically)
        _state.UpdateCacheState(normalizedData, desiredNoRebalanceRange);

        _cacheDiagnostics.RebalanceExecutionCompleted();
    }
}