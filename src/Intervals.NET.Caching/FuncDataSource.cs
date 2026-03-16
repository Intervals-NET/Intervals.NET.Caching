using Intervals.NET.Caching.Dto;

namespace Intervals.NET.Caching;

/// <summary>
/// An <see cref="IDataSource{TRange,TData}"/> implementation that delegates fetching to a caller-supplied
/// async function, enabling inline data sources without a dedicated class.
/// Batch fetching falls through to the default <see cref="IDataSource{TRange,TData}"/> implementation (<c>Parallel.ForEachAsync</c>).
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries. Must implement <see cref="IComparable{T}"/>.</typeparam>
/// <typeparam name="TData">The type of data being fetched.</typeparam>
public sealed class FuncDataSource<TRange, TData> : IDataSource<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly Func<Range<TRange>, CancellationToken, Task<RangeChunk<TRange, TData>>> _fetchFunc;

    /// <summary>Initializes a new <see cref="FuncDataSource{TRange,TData}"/> with the specified fetch delegate.</summary>
    /// <param name="fetchFunc">The async function invoked for every single-range fetch. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fetchFunc"/> is <see langword="null"/>.</exception>
    public FuncDataSource(
        Func<Range<TRange>, CancellationToken, Task<RangeChunk<TRange, TData>>> fetchFunc)
    {
        ArgumentNullException.ThrowIfNull(fetchFunc);
        _fetchFunc = fetchFunc;
    }

    /// <inheritdoc />
    public Task<RangeChunk<TRange, TData>> FetchAsync(
        Range<TRange> range,
        CancellationToken cancellationToken)
        => _fetchFunc(range, cancellationToken);
}
