using Intervals.NET;
using Intervals.NET.Domain.Abstractions;
using SlidingWindowCache.Public.Dto;

namespace SlidingWindowCache.Public;

/// <summary>
/// Extension methods for <see cref="IWindowCache{TRange, TData, TDomain}"/> providing
/// opt-in strong consistency mode on top of the default eventual consistency model.
/// </summary>
public static class WindowCacheExtensions
{
    /// <summary>
    /// Retrieves data for the specified range and waits for the cache to reach an idle
    /// state before returning, providing strong consistency semantics.
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
    /// <see cref="IWindowCache{TRange, TData, TDomain}.GetDataAsync"/> and
    /// <see cref="IWindowCache{TRange, TData, TDomain}.WaitForIdleAsync"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a
    /// <see cref="RangeResult{TRange, TData}"/> with the actual available range and data,
    /// identical to what <see cref="IWindowCache{TRange, TData, TDomain}.GetDataAsync"/> returns.
    /// The task completes only after the cache has reached an idle state (no pending intent,
    /// no executing rebalance).
    /// </returns>
    /// <remarks>
    /// <para><strong>Default vs. Strong Consistency:</strong></para>
    /// <para>
    /// By default, <see cref="IWindowCache{TRange, TData, TDomain}.GetDataAsync"/> returns data
    /// immediately under an eventual consistency model: the user always receives correct data,
    /// but the cache window may still be converging toward its optimal configuration in the background.
    /// </para>
    /// <para>
    /// This method extends that with a wait: it calls <c>GetDataAsync</c> first (user data returned
    /// immediately from cache or <c>IDataSource</c>), then awaits
    /// <see cref="IWindowCache{TRange, TData, TDomain}.WaitForIdleAsync"/> before returning.
    /// The caller receives the same <see cref="RangeResult{TRange, TData}"/> as <c>GetDataAsync</c>
    /// would return, but the method does not complete until the cache has converged.
    /// </para>
    /// <para><strong>Composition:</strong></para>
    /// <code>
    /// // Equivalent to:
    /// var result = await cache.GetDataAsync(requestedRange, cancellationToken);
    /// await cache.WaitForIdleAsync(cancellationToken);
    /// return result;
    /// </code>
    /// <para><strong>When to Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>
    /// When the caller needs to assert or inspect the cache geometry after the request
    /// (e.g., verifying that a rebalance occurred or that the window has shifted).
    /// </description></item>
    /// <item><description>
    /// Cold start synchronization: waiting for the initial rebalance to complete before
    /// proceeding with subsequent operations.
    /// </description></item>
    /// <item><description>
    /// Integration tests that need deterministic cache state before making assertions.
    /// </description></item>
    /// </list>
    /// <para><strong>When NOT to Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>
    /// Hot paths: the idle wait adds latency proportional to the rebalance execution time
    /// (debounce delay + data fetching + cache update). For normal usage, prefer the default
    /// eventual consistency model via <see cref="IWindowCache{TRange, TData, TDomain}.GetDataAsync"/>.
    /// </description></item>
    /// <item><description>
    /// Rapid sequential requests: calling this method back-to-back means each call waits
    /// for the prior rebalance to complete, eliminating the debounce and work-avoidance
    /// benefits of the cache.
    /// </description></item>
    /// </list>
    /// <para><strong>Idle Semantics (Invariant H.49):</strong></para>
    /// <para>
    /// The idle wait uses "was idle at some point" semantics inherited from
    /// <see cref="IWindowCache{TRange, TData, TDomain}.WaitForIdleAsync"/>. This is sufficient
    /// for the strong consistency use cases above: after the await, the cache has converged at
    /// least once since the request. New activity may begin immediately after, but the
    /// cache state observed at the idle point reflects the completed rebalance.
    /// </para>
    /// <para><strong>Exception Propagation:</strong></para>
    /// <list type="bullet">
    /// <item><description>
    /// If <c>GetDataAsync</c> throws (e.g., <see cref="ObjectDisposedException"/>,
    /// <see cref="OperationCanceledException"/>), the exception propagates immediately and
    /// <c>WaitForIdleAsync</c> is never called.
    /// </description></item>
    /// <item><description>
    /// If <c>WaitForIdleAsync</c> throws (e.g., <see cref="OperationCanceledException"/> via
    /// <paramref name="cancellationToken"/>), the exception propagates. The data returned by
    /// <c>GetDataAsync</c> is discarded.
    /// </description></item>
    /// </list>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// // Strong consistency: returns only after cache has converged
    /// var result = await cache.GetDataAndWaitForIdleAsync(
    ///     Range.Closed(100, 200),
    ///     cancellationToken);
    ///
    /// // Cache geometry is now fully converged — safe to inspect or assert
    /// if (result.Range.HasValue)
    ///     ProcessData(result.Data);
    /// </code>
    /// </remarks>
    public static async ValueTask<RangeResult<TRange, TData>> GetDataAndWaitForIdleAsync<TRange, TData, TDomain>(
        this IWindowCache<TRange, TData, TDomain> cache,
        Range<TRange> requestedRange,
        CancellationToken cancellationToken = default)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
    {
        var result = await cache.GetDataAsync(requestedRange, cancellationToken);
        await cache.WaitForIdleAsync(cancellationToken);
        return result;
    }
}
