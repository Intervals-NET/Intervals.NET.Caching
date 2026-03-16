using Intervals.NET.Data;
using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.SlidingWindow.Infrastructure.Storage;

/// <summary>
/// Internal strategy interface for handling user cache read operations.
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
internal interface ICacheStorage<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    /// <summary>
    /// Gets the current range of data stored in internal storage.
    /// </summary>
    Range<TRange> Range { get; }

    /// <summary>
    /// Rematerializes internal storage from the provided range data.
    /// </summary>
    /// <param name="rangeData">
    /// The range data to materialize into internal storage.
    /// </param>
    void Rematerialize(RangeData<TRange, TData, TDomain> rangeData);

    /// <summary>
    /// Reads data for the specified range from internal storage.
    /// </summary>
    /// <param name="range">
    /// The range for which to retrieve data.
    /// </param>
    /// <returns>
    /// A <see cref="ReadOnlyMemory{T}"/> containing the data for the specified range.
    /// </returns>
    ReadOnlyMemory<TData> Read(Range<TRange> range);

    /// <summary>
    /// Converts the current internal storage state into a <see cref="RangeData{TRange,TData,TDomain}"/> representation.
    /// </summary>
    /// <returns>
    /// A <see cref="RangeData{TRange,TData,TDomain}"/> representing the current state of internal storage.
    /// </returns>
    RangeData<TRange, TData, TDomain> ToRangeData();
}