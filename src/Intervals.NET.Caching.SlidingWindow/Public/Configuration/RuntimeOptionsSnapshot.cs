namespace Intervals.NET.Caching.SlidingWindow.Public.Configuration;

/// <summary>
/// A read-only snapshot of the current runtime-updatable cache option values.
/// </summary>
/// <remarks>
/// Obtained via <see cref="ISlidingWindowCache{TRange,TData,TDomain}.CurrentRuntimeOptions"/>.
/// Captures values at the moment the property was read; not updated by subsequent calls to
/// <see cref="ISlidingWindowCache{TRange,TData,TDomain}.UpdateRuntimeOptions"/>.
/// </remarks>
public sealed class RuntimeOptionsSnapshot
{
    internal RuntimeOptionsSnapshot(
        double leftCacheSize,
        double rightCacheSize,
        double? leftThreshold,
        double? rightThreshold,
        TimeSpan debounceDelay)
    {
        LeftCacheSize = leftCacheSize;
        RightCacheSize = rightCacheSize;
        LeftThreshold = leftThreshold;
        RightThreshold = rightThreshold;
        DebounceDelay = debounceDelay;
    }

    /// <summary>
    /// The coefficient for the left cache size relative to the requested range.
    /// </summary>
    public double LeftCacheSize { get; }

    /// <summary>
    /// The coefficient for the right cache size relative to the requested range.
    /// </summary>
    public double RightCacheSize { get; }

    /// <summary>
    /// The left no-rebalance threshold percentage, or <c>null</c> if the left threshold is disabled.
    /// </summary>
    public double? LeftThreshold { get; }

    /// <summary>
    /// The right no-rebalance threshold percentage, or <c>null</c> if the right threshold is disabled.
    /// </summary>
    public double? RightThreshold { get; }

    /// <summary>
    /// The debounce delay applied before executing a rebalance.
    /// </summary>
    public TimeSpan DebounceDelay { get; }
}
