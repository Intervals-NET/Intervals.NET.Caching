using Intervals.NET.Extensions;
using Intervals.NET.Caching.Dto;

namespace Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;

/// <summary>
/// A test IDataSource that simulates a bounded data source with physical limits.
/// Only returns data for ranges within [MinId, MaxId] boundaries.
/// Used for testing boundary handling, partial fulfillment, and out-of-bounds scenarios.
/// </summary>
public sealed class BoundedDataSource : IDataSource<int, int>
{
    private const int MinId = 1000;
    private const int MaxId = 9999;

    /// <summary>Gets the minimum available ID (inclusive).</summary>
    public int MinimumId => MinId;

    /// <summary>Gets the maximum available ID (inclusive).</summary>
    public int MaximumId => MaxId;

    /// <summary>
    /// Fetches data for a single range, respecting physical boundaries.
    /// Returns only data within [MinId, MaxId]. Returns null Range when no data is available.
    /// </summary>
    public Task<RangeChunk<int, int>> FetchAsync(Range<int> requested, CancellationToken cancellationToken)
    {
        var availableRange = Factories.Range.Closed<int>(MinId, MaxId);
        var fulfillable = requested.Intersect(availableRange);

        if (fulfillable == null)
        {
            return Task.FromResult(new RangeChunk<int, int>(null, Array.Empty<int>()));
        }

        var data = DataGenerationHelpers.GenerateDataForRange(fulfillable.Value);
        return Task.FromResult(new RangeChunk<int, int>(fulfillable.Value, data));
    }
}
