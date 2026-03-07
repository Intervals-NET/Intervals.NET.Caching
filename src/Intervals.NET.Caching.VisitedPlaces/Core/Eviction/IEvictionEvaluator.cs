namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Determines whether the cache has exceeded a configured policy limit and
/// computes how many segments must be removed to return to within-policy state.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> Background Path (single writer thread)</para>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>Inspects the current segment collection after each storage step</description></item>
/// <item><description>Returns the number of segments to remove (0 when the policy limit has not been exceeded)</description></item>
/// </list>
/// <para><strong>OR Semantics (Invariant VPC.E.1a):</strong></para>
/// <para>
/// Multiple evaluators may be active simultaneously. Eviction is triggered when ANY evaluator fires.
/// The <see cref="IEvictionExecutor{TRange,TData}"/> receives the maximum removal count across all
/// fired evaluators and satisfies all their constraints in a single pass (Invariant VPC.E.2a).
/// </para>
/// </remarks>
public interface IEvictionEvaluator<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Evaluates whether eviction should run and returns the number of segments to remove.
    /// Returns 0 when the policy limit has not been exceeded (no eviction needed).
    /// </summary>
    /// <param name="count">The current number of segments in storage.</param>
    /// <param name="allSegments">All currently stored segments.</param>
    /// <returns>
    /// The number of segments that must be removed to satisfy this evaluator's constraint,
    /// or 0 if eviction is not needed.
    /// </returns>
    int ComputeEvictionCount(int count, IReadOnlyList<CachedSegment<TRange, TData>> allSegments);
}
