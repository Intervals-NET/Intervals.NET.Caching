using Intervals.NET.Extensions;

namespace Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Decision;

/// <summary>
/// Evaluates whether rebalancing should occur based on no-rebalance range containment.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
internal readonly struct NoRebalanceSatisfactionPolicy<TRange>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Determines whether rebalancing should occur based on whether the requested range
    /// is contained within the no-rebalance zone.
    /// </summary>
    /// <param name="noRebalanceRange">The stability zone within which rebalancing is suppressed.</param>
    /// <param name="requested">The range requested by the user.</param>
    /// <returns>True if rebalancing should occur (request is outside no-rebalance zone); otherwise false.</returns>
    public bool ShouldRebalance(Range<TRange> noRebalanceRange, Range<TRange> requested) =>
        !noRebalanceRange.Contains(requested);
}
