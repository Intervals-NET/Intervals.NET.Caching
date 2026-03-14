namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Executes eviction by repeatedly asking the selector for a candidate until all eviction
/// pressures are satisfied or no more eligible candidates exist.
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class EvictionExecutor<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly IEvictionSelector<TRange, TData> _selector;

    /// <summary>
    /// Initializes a new <see cref="EvictionExecutor{TRange,TData}"/>.
    /// </summary>
    internal EvictionExecutor(IEvictionSelector<TRange, TData> selector)
    {
        _selector = selector;
    }

    /// <summary>
    /// Executes the constraint satisfaction eviction loop.
    /// </summary>
    internal IEnumerable<CachedSegment<TRange, TData>> Execute(
        IEvictionPressure<TRange, TData> pressure,
        IReadOnlyList<CachedSegment<TRange, TData>> justStoredSegments)
    {
        // Lazy-init: only build the HashSet if pressure is actually exceeded.
        // When no policy fires (NoPressure or all constraints satisfied up-front),
        // the HashSet is never allocated — zero cost on the common no-eviction path.
        HashSet<CachedSegment<TRange, TData>>? immune = null;

        while (pressure.IsExceeded)
        {
            // Build the immune set on first use (first eviction iteration).
            // justStoredSegments immunity (Invariant VPC.E.3) + already-selected candidates
            // are both tracked here. Constructed from justStoredSegments so all just-stored
            // entries are immune from the first selection attempt.
            immune ??= [.. justStoredSegments];

            if (!_selector.TrySelectCandidate(immune, out var candidate))
            {
                // No eligible candidates remain (all immune or pool exhausted).
                yield break;
            }

            immune.Add(candidate);   // Prevent re-selecting this segment in the same pass.
            pressure.Reduce(candidate);
            yield return candidate;
        }
    }
}
