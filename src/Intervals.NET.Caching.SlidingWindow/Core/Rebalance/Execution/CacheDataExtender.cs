using Intervals.NET.Data;
using Intervals.NET.Data.Extensions;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Extensions;
using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.SlidingWindow.Public.Instrumentation;

namespace Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Execution;

/// <summary>
/// Fetches missing data from the data source to extend the cache.
/// Does not perform trimming - that's the responsibility of the caller based on their context.
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
internal sealed class CacheDataExtender<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly IDataSource<TRange, TData> _dataSource;
    private readonly TDomain _domain;
    private readonly ISlidingWindowCacheDiagnostics _cacheDiagnostics;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheDataExtender{TRange,TData,TDomain}"/> class.
    /// </summary>
    /// <param name="dataSource">
    /// The data source from which to fetch data.
    /// </param>
    /// <param name="domain">
    /// The domain defining the range characteristics.
    /// </param>
    /// <param name="cacheDiagnostics">
    /// The diagnostics interface for recording cache operation metrics and events.
    /// </param>
    public CacheDataExtender(
        IDataSource<TRange, TData> dataSource,
        TDomain domain,
        ISlidingWindowCacheDiagnostics cacheDiagnostics
    )
    {
        _dataSource = dataSource;
        _domain = domain;
        _cacheDiagnostics = cacheDiagnostics;
    }

    /// <summary>
    /// Extends the cache to cover the requested range by fetching only missing data segments.
    /// Preserves all existing cached data without trimming.
    /// </summary>
    /// <param name="currentCache">The current cached data.</param>
    /// <param name="requested">The requested range that needs to be covered by the cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Extended cache containing all existing data plus newly fetched data to cover the requested range.
    /// </returns>
    public async Task<RangeData<TRange, TData, TDomain>> ExtendCacheAsync(
        RangeData<TRange, TData, TDomain> currentCache,
        Range<TRange> requested,
        CancellationToken cancellationToken
    )
    {
        _cacheDiagnostics.DataSourceFetchMissingSegments();

        // Step 1: Calculate which ranges are missing (and record the expansion/replacement diagnostic)
        var missingRanges = CalculateMissingRanges(currentCache.Range, requested, out var isCacheExpanded);

        // Step 2: Record the diagnostic event here (caller context), not inside the pure helper
        if (isCacheExpanded)
        {
            _cacheDiagnostics.CacheExpanded();
        }
        else
        {
            _cacheDiagnostics.CacheReplaced();
        }

        // Step 3: Fetch the missing data from data source
        var fetchedResults = await _dataSource.FetchAsync(missingRanges, cancellationToken)
            .ConfigureAwait(false);

        // Step 4: Union fetched data with current cache (UnionAll will filter null ranges)
        return UnionAll(currentCache, fetchedResults);
    }

    /// <summary>
    /// Calculates which ranges are missing from the current cache to cover the requested range.
    /// Uses range intersection and subtraction to determine gaps.
    /// </summary>
    /// <param name="currentRange">The range currently covered by the cache.</param>
    /// <param name="requestedRange">The range that needs to be covered.</param>
    /// <param name="isCacheExpanded">
    /// Set to <see langword="true"/> when the existing cache overlaps with the requested range
    /// (expansion case); <see langword="false"/> when there is no overlap (replacement case).
    /// </param>
    /// <returns>
    /// An enumerable of missing ranges that need to be fetched, or the full requested range
    /// when there is no intersection (meaning the entire requested range needs to be fetched).
    /// </returns>
    private static IEnumerable<Range<TRange>> CalculateMissingRanges(
        Range<TRange> currentRange,
        Range<TRange> requestedRange,
        out bool isCacheExpanded
    )
    {
        var intersection = currentRange.Intersect(requestedRange);

        if (intersection.HasValue)
        {
            isCacheExpanded = true;
            // Calculate the missing segments using range subtraction
            return requestedRange.Except(intersection.Value);
        }

        isCacheExpanded = false;
        // No overlap - indicate that entire requested range is missing
        // This signals to fetch the whole requested range without trying to calculate missing segments, as they are all missing.
        return [requestedRange];
    }

    /// <summary>
    /// Combines the existing cached data with the newly fetched data.
    /// </summary>
    private RangeData<TRange, TData, TDomain> UnionAll(
        RangeData<TRange, TData, TDomain> current,
        IEnumerable<RangeChunk<TRange, TData>> rangeChunks
    )
    {
        // Combine existing data with fetched data
        foreach (var chunk in rangeChunks)
        {
            // Filter out segments with null ranges (unavailable data)
            // This preserves cache contiguity - only available data is stored
            if (!chunk.Range.HasValue)
            {
                _cacheDiagnostics.DataSegmentUnavailable();
                continue;
            }

            // It is important to call Union on the current range data to overwrite outdated
            // intersected segments with the newly fetched data, ensuring that the most up-to-date
            // information is retained in the cache.
            current = current.Union(chunk.Data.ToRangeData(chunk.Range!.Value, _domain))!;
        }

        return current;
    }
}