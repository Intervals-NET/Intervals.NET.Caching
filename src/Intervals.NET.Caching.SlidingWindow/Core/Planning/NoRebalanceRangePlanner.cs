using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.SlidingWindow.Core.State;

namespace Intervals.NET.Caching.SlidingWindow.Core.Planning;

/// <summary>
/// Plans the no-rebalance range by shrinking the cache range using threshold ratios. See docs/sliding-window/ for design details.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
internal sealed class NoRebalanceRangePlanner<TRange, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly RuntimeCacheOptionsHolder _optionsHolder;
    private readonly TDomain _domain;

    /// <summary>
    /// Initializes a new instance of <see cref="NoRebalanceRangePlanner{TRange, TDomain}"/>.
    /// </summary>
    /// <param name="optionsHolder">Shared holder for the current runtime options snapshot.</param>
    /// <param name="domain">Domain implementation used for range arithmetic and span calculations.</param>
    public NoRebalanceRangePlanner(RuntimeCacheOptionsHolder optionsHolder, TDomain domain)
    {
        _optionsHolder = optionsHolder;
        _domain = domain;
    }

    /// <summary>
    /// Computes the no-rebalance range by shrinking the cache range using the current threshold ratios.
    /// </summary>
    /// <param name="cacheRange">The current cache range to compute thresholds from.</param>
    /// <returns>
    /// The no-rebalance range, or null if thresholds would result in an invalid range.
    /// </returns>
    public Range<TRange>? Plan(Range<TRange> cacheRange)
    {
        // Snapshot current options once for consistency within this invocation
        var options = _optionsHolder.Current;

        var leftThreshold = options.LeftThreshold ?? 0;
        var rightThreshold = options.RightThreshold ?? 0;
        var sum = leftThreshold + rightThreshold;

        if (sum >= 1)
        {
            // Means that there is no NoRebalanceRange, the shrinkage shrink the whole cache range
            return null;
        }

        return cacheRange.ExpandByRatio(
            domain: _domain,
            leftRatio: -leftThreshold, // Negate to shrink
            rightRatio: -rightThreshold // Negate to shrink
        );
    }
}
