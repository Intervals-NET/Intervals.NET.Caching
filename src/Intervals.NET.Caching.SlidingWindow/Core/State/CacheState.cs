using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.SlidingWindow.Infrastructure.Storage;

namespace Intervals.NET.Caching.SlidingWindow.Core.State;

/// <summary>
/// Encapsulates the mutable state of a window cache. See docs/sliding-window/ for design details.
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
internal sealed class CacheState<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    /// <summary>
    /// The current cached data along with its range.
    /// </summary>
    public ICacheStorage<TRange, TData, TDomain> Storage { get; }

    /// <summary>
    /// Indicates whether the cache has been populated at least once.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// The range within which no rebalancing should occur.
    /// </summary>
    public Range<TRange>? NoRebalanceRange { get; private set; }

    /// <summary>
    /// Gets the domain defining the range characteristics for this cache instance.
    /// </summary>
    public TDomain Domain { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheState{TRange,TData,TDomain}"/> class.
    /// </summary>
    /// <param name="cacheStorage">The cache storage implementation.</param>
    /// <param name="domain">The domain defining the range characteristics.</param>
    public CacheState(ICacheStorage<TRange, TData, TDomain> cacheStorage, TDomain domain)
    {
        Storage = cacheStorage;
        Domain = domain;
    }

    /// <summary>
    /// Applies a complete cache state mutation. Only called from Rebalance Execution context.
    /// </summary>
    /// <param name="normalizedData">The normalized range data to write into storage.</param>
    /// <param name="noRebalanceRange">The pre-computed no-rebalance range for the new state.</param>
    internal void UpdateCacheState(
        Data.RangeData<TRange, TData, TDomain> normalizedData,
        Range<TRange>? noRebalanceRange)
    {
        Storage.Rematerialize(normalizedData);
        IsInitialized = true;
        NoRebalanceRange = noRebalanceRange;
    }
}
