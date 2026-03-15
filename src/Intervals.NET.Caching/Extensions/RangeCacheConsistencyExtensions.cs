using Intervals.NET.Caching.Dto;
using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.Extensions;

/// <summary>
/// Extension methods for <see cref="IRangeCache{TRange,TData,TDomain}"/> providing
/// strong consistency mode on top of the default eventual consistency model.
/// </summary>
public static class RangeCacheConsistencyExtensions
{
    /// <summary>
    /// Retrieves data for the specified range and unconditionally waits for the cache to reach
    /// an idle state before returning, providing strong consistency semantics.
    /// Degrades gracefully on cancellation during idle wait by returning the already-obtained result.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <typeparam name="TDomain">The type representing the domain of the ranges. Must implement <see cref="IRangeDomain{TRange}"/>.</typeparam>
    /// <param name="cache">The cache instance to retrieve data from.</param>
    /// <param name="requestedRange">The range for which to retrieve data.</param>
    /// <param name="cancellationToken">A cancellation token passed to both <c>GetDataAsync</c> and <c>WaitForIdleAsync</c>.</param>
    /// <returns>A task that completes only after the cache has reached an idle state.</returns>
    public static async ValueTask<RangeResult<TRange, TData>> GetDataAndWaitForIdleAsync<TRange, TData, TDomain>(
        this IRangeCache<TRange, TData, TDomain> cache,
        Range<TRange> requestedRange,
        CancellationToken cancellationToken = default)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
    {
        var result = await cache.GetDataAsync(requestedRange, cancellationToken).ConfigureAwait(false);

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

        return result;
    }
}
