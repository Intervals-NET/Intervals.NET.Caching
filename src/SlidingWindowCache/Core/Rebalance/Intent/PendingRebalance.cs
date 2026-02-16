using Intervals.NET;

namespace SlidingWindowCache.Core.Rebalance.Intent;

/// <summary>
/// Represents an immutable snapshot of a pending rebalance operation's target state.
/// Used by the decision engine to evaluate stability without coupling to execution details.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <remarks>
/// <para><strong>Architectural Role:</strong></para>
/// <para>
/// This class provides a stable, immutable view of a scheduled rebalance's intended outcome,
/// allowing the decision engine to perform Stage 2 anti-thrashing validation (pending desired
/// cache stability check) without creating dependencies on scheduler or executor internals.
/// </para>
/// <para><strong>Lifetime:</strong></para>
/// <para>
/// Created when a rebalance is scheduled, captured atomically by IntentController,
/// and passed to DecisionEngine for subsequent decision evaluations.
/// </para>
/// </remarks>
/// todo add ct here in ordr to call .Cancel() on this object - cancels actually pending rebalance. I guess it will be more DDD like
/// todo also define rebalance execution task property here, so using it we can wait for idle in blocking rebalance scenarious.
internal sealed class PendingRebalance<TRange>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Gets the desired cache range that the pending rebalance will establish.
    /// </summary>
    public Range<TRange> DesiredRange { get; }

    /// <summary>
    /// Gets the no-rebalance range that will be active after the pending rebalance completes.
    /// May be null if not yet computed or if rebalance was skipped.
    /// </summary>
    public Range<TRange>? DesiredNoRebalanceRange { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingRebalance{TRange}"/> class.
    /// </summary>
    /// <param name="desiredRange">The desired cache range for the pending rebalance.</param>
    /// <param name="desiredNoRebalanceRange">The no-rebalance range for the target state.</param>
    public PendingRebalance(Range<TRange> desiredRange, Range<TRange>? desiredNoRebalanceRange)
    {
        DesiredRange = desiredRange;
        DesiredNoRebalanceRange = desiredNoRebalanceRange;
    }
}
