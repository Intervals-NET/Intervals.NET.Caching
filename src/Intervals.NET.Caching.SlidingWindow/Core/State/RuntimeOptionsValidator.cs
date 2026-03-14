using Intervals.NET.Caching.SlidingWindow.Public.Configuration;

namespace Intervals.NET.Caching.SlidingWindow.Core.State;

/// <summary>
/// Provides shared validation logic for runtime-updatable cache option values. See docs/sliding-window/ for design details.
/// </summary>
internal static class RuntimeOptionsValidator
{
    /// <summary>
    /// Validates cache size and threshold values that are shared between
    /// <see cref="RuntimeCacheOptions"/> and
    /// <see cref="SlidingWindowCacheOptions"/>.
    /// </summary>
    /// <param name="leftCacheSize">Must be ≥ 0.</param>
    /// <param name="rightCacheSize">Must be ≥ 0.</param>
    /// <param name="leftThreshold">Must be in [0, 1] when not null.</param>
    /// <param name="rightThreshold">Must be in [0, 1] when not null.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any size or threshold value is outside its valid range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when both thresholds are specified and their sum exceeds 1.0.
    /// </exception>
    internal static void ValidateCacheSizesAndThresholds(
        double leftCacheSize,
        double rightCacheSize,
        double? leftThreshold,
        double? rightThreshold)
    {
        // NaN comparisons always return false in IEEE 754, so NaN would silently pass
        // all subsequent range checks and corrupt geometry calculations. Guard explicitly.
        if (double.IsNaN(leftCacheSize))
        {
            throw new ArgumentOutOfRangeException(nameof(leftCacheSize),
                "LeftCacheSize must not be NaN.");
        }

        if (double.IsNaN(rightCacheSize))
        {
            throw new ArgumentOutOfRangeException(nameof(rightCacheSize),
                "RightCacheSize must not be NaN.");
        }

        if (leftCacheSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leftCacheSize),
                "LeftCacheSize must be greater than or equal to 0.");
        }

        if (rightCacheSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rightCacheSize),
                "RightCacheSize must be greater than or equal to 0.");
        }

        if (leftThreshold.HasValue && double.IsNaN(leftThreshold.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(leftThreshold),
                "LeftThreshold must not be NaN.");
        }

        if (rightThreshold.HasValue && double.IsNaN(rightThreshold.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(rightThreshold),
                "RightThreshold must not be NaN.");
        }

        if (leftThreshold is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leftThreshold),
                "LeftThreshold must be greater than or equal to 0.");
        }

        if (rightThreshold is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rightThreshold),
                "RightThreshold must be greater than or equal to 0.");
        }

        if (leftThreshold is > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(leftThreshold),
                "LeftThreshold must not exceed 1.0.");
        }

        if (rightThreshold is > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(rightThreshold),
                "RightThreshold must not exceed 1.0.");
        }

        // Validate that thresholds don't overlap (sum must not exceed 1.0)
        if (leftThreshold.HasValue && rightThreshold.HasValue &&
            (leftThreshold.Value + rightThreshold.Value) > 1.0)
        {
            throw new ArgumentException(
                $"The sum of LeftThreshold ({leftThreshold.Value:F6}) and RightThreshold ({rightThreshold.Value:F6}) " +
                $"must not exceed 1.0 (actual sum: {leftThreshold.Value + rightThreshold.Value:F6}). " +
                "Thresholds represent percentages of the total cache window that are shrunk from each side. " +
                "When their sum exceeds 1.0, the shrinkage zones would overlap, creating an invalid configuration.");
        }
    }
}
