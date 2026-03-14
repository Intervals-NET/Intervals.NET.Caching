using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.Dto;

namespace Intervals.NET.Caching.SlidingWindow.Public.Extensions;

/// <summary>
/// Extension methods for <see cref="ISlidingWindowCache{TRange, TData, TDomain}"/> providing
/// opt-in consistency modes on top of the default eventual consistency model.
/// </summary>
public static class SlidingWindowCacheConsistencyExtensions
{
    /// <summary>
    /// Retrieves data for the specified range and — if the request resulted in a cache miss or
    /// partial cache hit — waits for the cache to reach an idle state before returning.
    /// This provides <em>hybrid consistency</em> semantics.
    /// </summary>
    /// <typeparam name="TRange">
    /// The type representing the range boundaries. Must implement <see cref="IComparable{T}"/>.
    /// </typeparam>
    /// <typeparam name="TData">
    /// The type of data being cached.
    /// </typeparam>
    /// <typeparam name="TDomain">
    /// The type representing the domain of the ranges. Must implement <see cref="IRangeDomain{TRange}"/>.
    /// </typeparam>
    /// <param name="cache">
    /// The cache instance to retrieve data from.
    /// </param>
    /// <param name="requestedRange">
    /// The range for which to retrieve data.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the operation. Passed to both
    /// <see cref="ISlidingWindowCache{TRange, TData, TDomain}.GetDataAsync"/> and, when applicable,
    /// <see cref="ISlidingWindowCache{TRange, TData, TDomain}.WaitForIdleAsync"/>.
    /// Cancelling the token during the idle wait stops the <em>wait</em> and causes the method
    /// to return the already-obtained <see cref="RangeResult{TRange,TData}"/> gracefully
    /// (eventual consistency degradation). The background rebalance continues to completion.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a
    /// <see cref="RangeResult{TRange, TData}"/> with the actual available range, data, and
    /// <see cref="RangeResult{TRange,TData}.CacheInteraction"/>, identical to what
    /// <see cref="ISlidingWindowCache{TRange, TData, TDomain}.GetDataAsync"/> returns directly.
    /// The task completes immediately on a full cache hit; on a partial hit or full miss the
    /// task completes only after the cache has reached an idle state (or immediately if the
    /// idle wait is cancelled).
    /// </returns>
    /// <remarks>
    /// On a <see cref="CacheInteraction.FullHit"/>, returns immediately. On a
    /// <see cref="CacheInteraction.PartialHit"/> or <see cref="CacheInteraction.FullMiss"/>,
    /// waits for idle so the cache is warm around the new position before returning.
    /// If the idle wait is cancelled, the already-obtained result is returned gracefully.
    /// </remarks>
    public static async ValueTask<RangeResult<TRange, TData>> GetDataAndWaitOnMissAsync<TRange, TData, TDomain>(
        this ISlidingWindowCache<TRange, TData, TDomain> cache,
        Range<TRange> requestedRange,
        CancellationToken cancellationToken = default)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
    {
        var result = await cache.GetDataAsync(requestedRange, cancellationToken).ConfigureAwait(false);

        // Wait for idle only on cache miss scenarios (full miss or partial hit) to ensure
        // the cache is rebalanced around the new position before returning.
        // Full cache hits return immediately — the cache is already correctly positioned.
        // If the idle wait is cancelled, return the already-obtained result gracefully
        // (degrade to eventual consistency) rather than discarding valid data.
        if (result.CacheInteraction != CacheInteraction.FullHit)
        {
            try
            {
                await cache.WaitForIdleAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Graceful degradation: cancellation during the idle wait does not
                // discard the data already obtained from GetDataAsync. The background
                // rebalance continues; we simply stop waiting for it.
            }
        }

        return result;
    }
}
