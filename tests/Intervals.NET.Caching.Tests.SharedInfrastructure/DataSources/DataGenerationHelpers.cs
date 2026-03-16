namespace Intervals.NET.Caching.Tests.SharedInfrastructure.DataSources;

/// <summary>
/// Shared data generation logic for test data sources across all packages.
/// Encapsulates the range-to-integer-data mapping used by <see cref="SpyDataSource"/>
/// implementations, eliminating duplication across test infrastructure projects.
/// </summary>
public static class DataGenerationHelpers
{
    /// <summary>
    /// Generates sequential integer data for an integer range, respecting boundary inclusivity.
    /// </summary>
    /// <param name="range">The range to generate data for.</param>
    /// <returns>A list of sequential integers corresponding to the range.</returns>
    public static List<int> GenerateDataForRange(Range<int> range)
    {
        var data = new List<int>();
        var start = (int)range.Start;
        var end = (int)range.End;

        switch (range)
        {
            case { IsStartInclusive: true, IsEndInclusive: true }:
                // [start, end]
                for (var i = start; i <= end; i++)
                {
                    data.Add(i);
                }

                break;
            case { IsStartInclusive: true, IsEndInclusive: false }:
                // [start, end)
                for (var i = start; i < end; i++)
                {
                    data.Add(i);
                }

                break;
            case { IsStartInclusive: false, IsEndInclusive: true }:
                // (start, end]
                for (var i = start + 1; i <= end; i++)
                {
                    data.Add(i);
                }

                break;
            default:
                // (start, end)
                for (var i = start + 1; i < end; i++)
                {
                    data.Add(i);
                }

                break;
        }

        return data;
    }
}
