namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Evaluates cache state and produces an <see cref="IEvictionPressure{TRange,TData}"/> object
/// representing whether a configured constraint has been violated.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> Background Path (single writer thread)</para>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>Maintains incremental internal state via <see cref="OnSegmentAdded"/> and <see cref="OnSegmentRemoved"/></description></item>
/// <item><description>Returns an <see cref="IEvictionPressure{TRange,TData}"/> that tracks constraint satisfaction</description></item>
/// <item><description>Returns <see cref="Pressure.NoPressure{TRange,TData}.Instance"/> when the constraint is not violated</description></item>
/// </list>
/// <para><strong>Architectural Invariant — Policies must NOT:</strong></para>
/// <list type="bullet">
/// <item><description>Know about eviction strategy (selector order)</description></item>
/// <item><description>Estimate how many segments to remove</description></item>
/// <item><description>Make assumptions about which segments will be removed</description></item>
/// </list>
/// <para><strong>OR Semantics (Invariant VPC.E.1a):</strong></para>
/// <para>
/// Multiple policies may be active simultaneously. Eviction is triggered when ANY policy
/// produces a pressure with <see cref="IEvictionPressure{TRange,TData}.IsExceeded"/> = <c>true</c>.
/// The executor removes segments until ALL pressures are satisfied (Invariant VPC.E.2a).
/// </para>
/// <para><strong>Lifecycle contract:</strong></para>
/// <para>
/// <see cref="OnSegmentAdded"/> and <see cref="OnSegmentRemoved"/> are called by
/// <see cref="EvictionPolicyEvaluator{TRange,TData}"/> on the Background Path. Implementations
/// use these to maintain a running aggregate so that <see cref="Evaluate"/> runs in O(1).
/// Both methods may also be called from the TTL actor concurrently;
/// implementations must use atomic operations (e.g., <see cref="System.Threading.Interlocked"/>)
/// where cross-thread safety is required.
/// </para>
/// </remarks>
public interface IEvictionPolicy<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Notifies this policy that a new segment has been added to storage.
    /// Implementations should update their internal running aggregate to include
    /// the contribution of <paramref name="segment"/>.
    /// </summary>
    /// <param name="segment">The segment that was just added to storage.</param>
    /// <remarks>
    /// Called by <see cref="EvictionPolicyEvaluator{TRange,TData}"/> immediately after each
    /// segment is added to storage. Runs on the Background Path; may also be called from the
    /// TTL actor concurrently. Must be allocation-free and lightweight.
    /// </remarks>
    void OnSegmentAdded(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Notifies this policy that a segment has been removed from storage.
    /// Implementations should update their internal running aggregate to exclude
    /// the contribution of <paramref name="segment"/>.
    /// </summary>
    /// <param name="segment">The segment that was just removed from storage.</param>
    /// <remarks>
    /// Called by <see cref="EvictionPolicyEvaluator{TRange,TData}"/> immediately after each
    /// segment is removed from storage. Runs on the Background Path or TTL thread.
    /// Must be allocation-free and lightweight.
    /// </remarks>
    void OnSegmentRemoved(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Evaluates whether the configured constraint is violated and returns a pressure object
    /// that tracks constraint satisfaction as segments are removed.
    /// </summary>
    /// <returns>
    /// An <see cref="IEvictionPressure{TRange,TData}"/> whose <see cref="IEvictionPressure{TRange,TData}.IsExceeded"/>
    /// indicates whether eviction is needed. Returns <see cref="Pressure.NoPressure{TRange,TData}.Instance"/>
    /// when the constraint is not violated.
    /// </returns>
    /// <remarks>
    /// O(1): implementations read their internally maintained running aggregate rather than
    /// iterating the segment collection.
    /// </remarks>
    IEvictionPressure<TRange, TData> Evaluate();
}
