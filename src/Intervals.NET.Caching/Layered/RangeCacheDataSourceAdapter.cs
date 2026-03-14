using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.Infrastructure;
using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.Layered;

/// <summary>
/// Adapts an <see cref="IRangeCache{TRange,TData,TDomain}"/> instance to the
/// <see cref="IDataSource{TRange,TData}"/> interface, enabling any cache to serve as the
/// data source for another cache layer.
/// </summary>
/// <typeparam name="TRange">
/// The type representing range boundaries. Must implement <see cref="IComparable{T}"/>.
/// </typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">
/// The type representing the domain of the ranges. Must implement <see cref="IRangeDomain{TRange}"/>.
/// </typeparam>
public sealed class RangeCacheDataSourceAdapter<TRange, TData, TDomain>
    : IDataSource<TRange, TData>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly IRangeCache<TRange, TData, TDomain> _innerCache;

    /// <summary>
    /// Initializes a new instance of <see cref="RangeCacheDataSourceAdapter{TRange,TData,TDomain}"/>.
    /// </summary>
    /// <param name="innerCache">
    /// The cache instance to adapt as a data source. Must not be null.
    /// The adapter does not take ownership; the caller is responsible for disposal.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="innerCache"/> is null.
    /// </exception>
    public RangeCacheDataSourceAdapter(IRangeCache<TRange, TData, TDomain> innerCache)
    {
        _innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
    }

    /// <summary>
    /// Fetches data for the specified range from the inner cache.
    /// </summary>
    /// <param name="range">The range for which to fetch data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="RangeChunk{TRange,TData}"/> containing the data available in the inner cache
    /// for the requested range.
    /// </returns>
    public async Task<RangeChunk<TRange, TData>> FetchAsync(
        Range<TRange> range,
        CancellationToken cancellationToken)
    {
        var result = await _innerCache.GetDataAsync(range, cancellationToken).ConfigureAwait(false);
        return new RangeChunk<TRange, TData>(result.Range, new ReadOnlyMemoryEnumerable<TData>(result.Data));
    }
}
