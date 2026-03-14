using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.VisitedPlaces.Public;

/// <summary>
/// Represents a visited places cache that stores and retrieves data for arbitrary,
/// non-contiguous ranges with pluggable eviction.
/// </summary>
/// <remarks>
/// Stores independently-fetched segments as non-contiguous entries (gaps are permitted, no merging).
/// Uses eventual consistency: <see cref="IRangeCache{TRange,TData,TDomain}.GetDataAsync"/> returns
/// immediately; storage and eviction happen asynchronously in the background.
/// Always dispose via <c>await using</c> to release background resources.
/// </remarks>
public interface IVisitedPlacesCache<TRange, TData, TDomain> : IRangeCache<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
}
