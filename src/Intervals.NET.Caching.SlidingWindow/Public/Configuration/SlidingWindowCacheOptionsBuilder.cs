namespace Intervals.NET.Caching.SlidingWindow.Public.Configuration;

/// <summary>
/// Fluent builder for constructing <see cref="SlidingWindowCacheOptions"/> instances.
/// See docs/sliding-window/components/public-api.md for parameter descriptions.
/// </summary>
/// <remarks>
/// <see cref="WithLeftCacheSize"/> and <see cref="WithRightCacheSize"/> (or <see cref="WithCacheSize(double)"/>)
/// must be called before <see cref="Build"/>. All other fields have sensible defaults.
/// </remarks>
public sealed class SlidingWindowCacheOptionsBuilder
{
    private double? _leftCacheSize;
    private double? _rightCacheSize;
    private UserCacheReadMode _readMode = UserCacheReadMode.Snapshot;
    private double? _leftThreshold;
    private double? _rightThreshold;
    private bool _leftThresholdSet;
    private bool _rightThresholdSet;
    private TimeSpan? _debounceDelay;
    private int? _rebalanceQueueCapacity;

    /// <summary>Initializes a new instance of the <see cref="SlidingWindowCacheOptionsBuilder"/> class.</summary>
    public SlidingWindowCacheOptionsBuilder() { }

    /// <summary>Sets the left cache size coefficient (must be &gt;= 0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithLeftCacheSize(double value)
    {
        _leftCacheSize = value;
        return this;
    }

    /// <summary>Sets the right cache size coefficient (must be &gt;= 0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithRightCacheSize(double value)
    {
        _rightCacheSize = value;
        return this;
    }

    /// <summary>Sets both left and right cache size coefficients to the same value (must be &gt;= 0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithCacheSize(double value)
    {
        _leftCacheSize = value;
        _rightCacheSize = value;
        return this;
    }

    /// <summary>Sets left and right cache size coefficients to different values (both must be &gt;= 0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithCacheSize(double left, double right)
    {
        _leftCacheSize = left;
        _rightCacheSize = right;
        return this;
    }

    /// <summary>
    /// Sets the read mode that determines how materialized cache data is exposed to users.
    /// Default is <see cref="UserCacheReadMode.Snapshot"/>.
    /// </summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithReadMode(UserCacheReadMode value)
    {
        _readMode = value;
        return this;
    }

    /// <summary>Sets the left no-rebalance threshold percentage (must be &gt;= 0; sum with right must not exceed 1.0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithLeftThreshold(double value)
    {
        _leftThresholdSet = true;
        _leftThreshold = value;
        return this;
    }

    /// <summary>Sets the right no-rebalance threshold percentage (must be &gt;= 0; sum with left must not exceed 1.0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithRightThreshold(double value)
    {
        _rightThresholdSet = true;
        _rightThreshold = value;
        return this;
    }

    /// <summary>Sets both left and right no-rebalance threshold percentages to the same value (combined sum must not exceed 1.0).</summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithThresholds(double value)
    {
        _leftThresholdSet = true;
        _leftThreshold = value;
        _rightThresholdSet = true;
        _rightThreshold = value;
        return this;
    }

    /// <summary>
    /// Sets the debounce delay applied before executing a rebalance.
    /// Default is 100 ms. <see cref="TimeSpan.Zero"/> disables debouncing.
    /// </summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithDebounceDelay(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value),
                "DebounceDelay must be non-negative.");
        }

        _debounceDelay = value;
        return this;
    }

    /// <summary>
    /// Sets the rebalance execution queue capacity, selecting the bounded channel-based strategy.
    /// Default is <c>null</c> (unbounded task-based serialization).
    /// </summary>
    /// <returns>This builder instance, for fluent chaining.</returns>
    public SlidingWindowCacheOptionsBuilder WithRebalanceQueueCapacity(int value)
    {
        _rebalanceQueueCapacity = value;
        return this;
    }

    /// <summary>
    /// Builds a <see cref="SlidingWindowCacheOptions"/> instance from the configured values.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither <see cref="WithLeftCacheSize"/>/<see cref="WithRightCacheSize"/> nor
    /// a <see cref="WithCacheSize(double)"/> overload has been called.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any value fails validation (negative sizes, thresholds, or queue capacity &lt;= 0).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the sum of left and right thresholds exceeds 1.0.
    /// </exception>
    public SlidingWindowCacheOptions Build()
    {
        if (_leftCacheSize is null || _rightCacheSize is null)
        {
            throw new InvalidOperationException(
                "LeftCacheSize and RightCacheSize must be configured. " +
                "Use WithLeftCacheSize()/WithRightCacheSize() or WithCacheSize() to set them.");
        }

        return new SlidingWindowCacheOptions(
            _leftCacheSize.Value,
            _rightCacheSize.Value,
            _readMode,
            _leftThresholdSet ? _leftThreshold : null,
            _rightThresholdSet ? _rightThreshold : null,
            _debounceDelay,
            _rebalanceQueueCapacity
        );
    }
}
