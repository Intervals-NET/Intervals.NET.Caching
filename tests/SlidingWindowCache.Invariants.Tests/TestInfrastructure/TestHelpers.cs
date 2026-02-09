using Intervals.NET;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Domain.Extensions.Fixed;
using SlidingWindowCache.Configuration;

namespace SlidingWindowCache.Invariants.Tests.TestInfrastructure;

/// <summary>
/// Helper methods for creating test components.
/// Uses Intervals.NET packages for proper range handling and domain calculations.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a standard integer fixed-step domain for testing.
    /// </summary>
    public static IntegerFixedStepDomain CreateIntDomain() => new();

    /// <summary>
    /// Creates a closed range [start, end] (both boundaries inclusive) using Intervals.NET factory.
    /// This is the standard range type used throughout the WindowCache system.
    /// </summary>
    /// <param name="start">The start value (inclusive).</param>
    /// <param name="end">The end value (inclusive).</param>
    /// <returns>A closed range [start, end].</returns>
    public static Range<int> CreateRange(int start, int end) => Intervals.NET.Factories.Range.Closed<int>(start, end);

    /// <summary>
    /// Creates default cache options for testing.
    /// </summary>
    public static WindowCacheOptions CreateDefaultOptions(
        double leftCacheSize = 1.0, // The left cache size equals to the requested range size
        double rightCacheSize = 1.0, // The right cache size equals to the requested range size
        double? leftThreshold = 0.2, // 20% threshold on the left side
        double? rightThreshold = 0.2, // 20% threshold on the right side
        TimeSpan? debounceDelay = null, // Default debounce delay of 50ms
        UserCacheReadMode readMode = UserCacheReadMode.Snapshot
    ) => new(
        leftCacheSize: leftCacheSize,
        rightCacheSize: rightCacheSize,
        readMode: readMode,
        leftThreshold: leftThreshold,
        rightThreshold: rightThreshold,
        debounceDelay: debounceDelay ?? TimeSpan.FromMilliseconds(50)
    );

    /// <summary>
    /// Verifies that the data matches the expected range values using Intervals.NET domain calculations.
    /// Properly handles range inclusivity.
    /// </summary>
    public static void VerifyDataMatchesRange(ReadOnlyMemory<int> data, Range<int> expectedRange)
    {
        var span = data.Span;

        // Use Intervals.NET domain to calculate expected length
        var domain = new IntegerFixedStepDomain();
        var expectedLength = (int)expectedRange.Span(domain);

        Assert.Equal(expectedLength, span.Length);

        // Verify data values match the range
        var start = (int)expectedRange.Start;

        switch (expectedRange)
        {
            // For closed ranges [start, end], data should be sequential from start
            case { IsStartInclusive: true, IsEndInclusive: true }:
            {
                for (var i = 0; i < span.Length; i++)
                {
                    Assert.Equal(start + i, span[i]);
                }

                break;
            }
            case { IsStartInclusive: true, IsEndInclusive: false }:
            {
                // [start, end) - start inclusive, end exclusive
                for (var i = 0; i < span.Length; i++)
                {
                    Assert.Equal(start + i, span[i]);
                }

                break;
            }
            case { IsStartInclusive: false, IsEndInclusive: true }:
            {
                // (start, end] - start exclusive, end inclusive
                for (var i = 0; i < span.Length; i++)
                {
                    Assert.Equal(start + 1 + i, span[i]);
                }

                break;
            }
            default:
            {
                // (start, end) - both exclusive
                for (var i = 0; i < span.Length; i++)
                {
                    Assert.Equal(start + 1 + i, span[i]);
                }

                break;
            }
        }
    }

    /// <summary>
    /// Waits for background rebalance to complete with timeout.
    /// </summary>
    public static async Task WaitForRebalanceAsync(int timeoutMs = 500)
    {
        await Task.Delay(timeoutMs);
    }
}