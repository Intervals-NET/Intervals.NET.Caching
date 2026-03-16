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
    /// <typeparam name="TRange">The type representing the range boundaries. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <typeparam name="TDomain">The type representing the domain of the ranges. Must implement <see cref="IRangeDomain{TRange}"/>.</typeparam>
    /// <param name="cache">The cache instance to retrieve data from.</param>
    /// <param name="requestedRange">The range for which to retrieve data.</param>
    /// <param name="cancellationToken">
    /// A cancellation token passed to both <c>GetDataAsync</c> and, when applicable, <c>WaitForIdleAsync</c>.
    /// Cancelling during idle wait returns the already-obtained result gracefully (eventual consistency degradation).
    /// </param>
    /// <returns>
    /// A task completing immediately on a full cache hit; on a partial hit or full miss, completing only after
    /// the cache reaches idle (or immediately if the idle wait is cancelled).
    /// </returns>
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
