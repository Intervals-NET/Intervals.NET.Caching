using Intervals.NET.Caching.SlidingWindow.Core.State;

namespace Intervals.NET.Caching.SlidingWindow.Public.Configuration;

/// <summary>
/// Options for configuring the sliding window cache. See docs/sliding-window/components/public-api.md for parameter details.
/// </summary>
public sealed class SlidingWindowCacheOptions : IEquatable<SlidingWindowCacheOptions>
{
    /// <summary>
    /// Initializes a new instance of <see cref="SlidingWindowCacheOptions"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when LeftCacheSize, RightCacheSize, LeftThreshold, RightThreshold is less than 0,
    /// when DebounceDelay is negative, or when RebalanceQueueCapacity is less than or equal to 0.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the sum of LeftThreshold and RightThreshold exceeds 1.0.
    /// </exception>
    public SlidingWindowCacheOptions(
        double leftCacheSize,
        double rightCacheSize,
        UserCacheReadMode readMode,
        double? leftThreshold = null,
        double? rightThreshold = null,
        TimeSpan? debounceDelay = null,
        int? rebalanceQueueCapacity = null
    )
    {
        RuntimeOptionsValidator.ValidateCacheSizesAndThresholds(
            leftCacheSize, rightCacheSize, leftThreshold, rightThreshold);

        if (rebalanceQueueCapacity is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rebalanceQueueCapacity),
                "RebalanceQueueCapacity must be greater than 0 or null.");
        }

        if (debounceDelay.HasValue && debounceDelay.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceDelay),
                "DebounceDelay must be non-negative.");
        }

        LeftCacheSize = leftCacheSize;
        RightCacheSize = rightCacheSize;
        ReadMode = readMode;
        LeftThreshold = leftThreshold;
        RightThreshold = rightThreshold;
        DebounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(100);
        RebalanceQueueCapacity = rebalanceQueueCapacity;
    }

    /// <summary>Left cache size coefficient (multiplied by requested range size). Must be >= 0.</summary>
    public double LeftCacheSize { get; }

    /// <summary>Right cache size coefficient (multiplied by requested range size). Must be >= 0.</summary>
    public double RightCacheSize { get; }

    /// <summary>Left threshold as a fraction of total cache size; triggers rebalance when exceeded. Null disables left threshold.</summary>
    public double? LeftThreshold { get; }

    /// <summary>Right threshold as a fraction of total cache size; triggers rebalance when exceeded. Null disables right threshold.</summary>
    public double? RightThreshold { get; }

    /// <summary>Debounce delay before a rebalance is executed. Defaults to 100 ms.</summary>
    public TimeSpan DebounceDelay { get; }

    /// <summary>
    /// The read mode that determines how materialized cache data is exposed to users.
    /// </summary>
    public UserCacheReadMode ReadMode { get; }

    /// <summary>Controls the rebalance execution strategy: null = unbounded task-based, >= 1 = bounded channel-based with backpressure.</summary>
    public int? RebalanceQueueCapacity { get; }

    /// <inheritdoc/>
    public bool Equals(SlidingWindowCacheOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return LeftCacheSize.Equals(other.LeftCacheSize)
               && RightCacheSize.Equals(other.RightCacheSize)
               && ReadMode == other.ReadMode
               && Nullable.Equals(LeftThreshold, other.LeftThreshold)
               && Nullable.Equals(RightThreshold, other.RightThreshold)
               && DebounceDelay == other.DebounceDelay
               && RebalanceQueueCapacity == other.RebalanceQueueCapacity;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SlidingWindowCacheOptions);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(LeftCacheSize, RightCacheSize, ReadMode, LeftThreshold, RightThreshold, DebounceDelay, RebalanceQueueCapacity);

    /// <summary>Determines whether two <see cref="SlidingWindowCacheOptions"/> instances are equal.</summary>
    public static bool operator ==(SlidingWindowCacheOptions? left, SlidingWindowCacheOptions? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Determines whether two <see cref="SlidingWindowCacheOptions"/> instances are not equal.</summary>
    public static bool operator !=(SlidingWindowCacheOptions? left, SlidingWindowCacheOptions? right) => !(left == right);
}
