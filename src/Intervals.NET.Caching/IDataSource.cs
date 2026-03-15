using Intervals.NET.Caching.Dto;

namespace Intervals.NET.Caching;

/// <summary>
/// Contract for data sources used in range-based caches. See docs/shared/boundary-handling.md for usage and boundary handling contract.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries. Must implement <see cref="IComparable{T}"/>.</typeparam>
/// <typeparam name="TData">The type of data being fetched.</typeparam>
public interface IDataSource<TRange, TData> where TRange : IComparable<TRange>
{
    /// <summary>
    /// Fetches data for the specified range. Must return <c>null</c> range (not throw) for out-of-bounds requests.
    /// See docs/shared/boundary-handling.md for the full boundary contract.
    /// </summary>
    /// <param name="range">The range for which to fetch data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    Task<RangeChunk<TRange, TData>> FetchAsync(
        Range<TRange> range,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Fetches data for multiple ranges. Default implementation parallelizes single-range calls up to <see cref="Environment.ProcessorCount"/>;
    /// override for true batch optimization (e.g., a single bulk query).
    /// </summary>
    /// <param name="ranges">The ranges for which to fetch data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    async Task<IEnumerable<RangeChunk<TRange, TData>>> FetchAsync(
        IEnumerable<Range<TRange>> ranges,
        CancellationToken cancellationToken
    )
    {
        var rangeList = ranges.ToList();
        var results = new RangeChunk<TRange, TData>[rangeList.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, rangeList.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            },
            async (index, ct) =>
            {
                results[index] = await FetchAsync(rangeList[index], ct);
            });

        return results;
    }
}
