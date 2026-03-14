using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.SlidingWindow.Core.Planning;

namespace Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Decision;

/// <summary>
/// Evaluates whether rebalance execution is required based on cache geometry policy. See docs/sliding-window/ for design details.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
internal sealed class RebalanceDecisionEngine<TRange, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly NoRebalanceSatisfactionPolicy<TRange> _policy;
    private readonly ProportionalRangePlanner<TRange, TDomain> _planner;
    private readonly NoRebalanceRangePlanner<TRange, TDomain> _noRebalancePlanner;

    public RebalanceDecisionEngine(
        NoRebalanceSatisfactionPolicy<TRange> policy,
        ProportionalRangePlanner<TRange, TDomain> planner,
        NoRebalanceRangePlanner<TRange, TDomain> noRebalancePlanner)
    {
        _policy = policy;
        _planner = planner;
        _noRebalancePlanner = noRebalancePlanner;
    }

    /// <summary>
    /// Evaluates whether rebalance execution should proceed based on multi-stage validation.
    /// </summary>
    /// <param name="requestedRange">The range requested by the user.</param>
    /// <param name="currentNoRebalanceRange">The no-rebalance range of the current cache state, or null if none.</param>
    /// <param name="currentCacheRange">The range currently covered by the cache.</param>
    /// <param name="pendingNoRebalanceRange">The desired no-rebalance range of the last pending execution request, or null if none.</param>
    /// <returns>A decision indicating whether to schedule rebalance with explicit reasoning.</returns>
    public RebalanceDecision<TRange> Evaluate(
        Range<TRange> requestedRange,
        Range<TRange>? currentNoRebalanceRange,
        Range<TRange> currentCacheRange,
        Range<TRange>? pendingNoRebalanceRange)
    {
        // Stage 1: Current Cache Stability Check (fast path)
        // If requested range is fully contained within current NoRebalanceRange, skip rebalancing
        if (currentNoRebalanceRange.HasValue &&
            !_policy.ShouldRebalance(currentNoRebalanceRange.Value, requestedRange))
        {
            return RebalanceDecision<TRange>.Skip(RebalanceReason.WithinCurrentNoRebalanceRange);
        }

        // Stage 2: Pending Rebalance Stability Check (anti-thrashing)
        // If there's a pending rebalance AND requested range will be covered by its NoRebalanceRange,
        // skip scheduling a new rebalance to avoid cancellation storms
        if (pendingNoRebalanceRange.HasValue &&
            !_policy.ShouldRebalance(pendingNoRebalanceRange.Value, requestedRange))
        {
            return RebalanceDecision<TRange>.Skip(RebalanceReason.WithinPendingNoRebalanceRange);
        }

        // Stage 3: Desired Range Computation
        // Compute the target cache geometry using policy
        var desiredCacheRange = _planner.Plan(requestedRange);
        var desiredNoRebalanceRange = _noRebalancePlanner.Plan(desiredCacheRange);

        // Stage 4: Equality Short Circuit
        // If desired range matches current cache range, no mutation needed
        if (desiredCacheRange.Equals(currentCacheRange))
        {
            return RebalanceDecision<TRange>.Skip(RebalanceReason.DesiredEqualsCurrent);
        }

        // Stage 5: Rebalance Required
        // All validation stages passed - rebalance is necessary
        return RebalanceDecision<TRange>.Execute(desiredCacheRange, desiredNoRebalanceRange);
    }
}
