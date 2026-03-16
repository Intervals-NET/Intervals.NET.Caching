using Intervals.NET.Caching.Dto;

namespace Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;

/// <summary>
/// A configurable IDataSource that delegates fetch calls through a user-supplied callback,
/// allowing individual tests to inject faults (exceptions) or control returned data on a per-call basis.
/// Intended for exception-handling tests only. For boundary/null-Range scenarios use BoundedDataSource.
/// </summary>
/// <typeparam name="TRange">The range boundary type.</typeparam>
/// <typeparam name="TData">The data type.</typeparam>
public sealed class FaultyDataSource<TRange, TData> : IDataSource<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly Func<Range<TRange>, IReadOnlyList<TData>> _fetchCallback;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="fetchCallback">
    /// Callback invoked for every fetch. May throw to simulate failures,
    /// or return any <see cref="IReadOnlyList{T}"/> to control the returned data.
    /// The <see cref="RangeChunk{TRange,TData}.Range"/> in the result is always set to
    /// the requested range — this class does not support returning a null Range.
    /// </param>
    public FaultyDataSource(Func<Range<TRange>, IReadOnlyList<TData>> fetchCallback)
    {
        _fetchCallback = fetchCallback;
    }

    /// <inheritdoc />
    public Task<RangeChunk<TRange, TData>> FetchAsync(Range<TRange> range, CancellationToken cancellationToken)
    {
        var data = _fetchCallback(range);
        return Task.FromResult(new RangeChunk<TRange, TData>(range, data));
    }
}
