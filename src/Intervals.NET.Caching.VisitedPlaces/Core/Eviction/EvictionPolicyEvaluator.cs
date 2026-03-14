using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Pressure;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Encapsulates the full eviction policy pipeline: segment lifecycle notifications,
/// multi-policy evaluation, and composite pressure construction.
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class EvictionPolicyEvaluator<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly IReadOnlyList<IEvictionPolicy<TRange, TData>> _policies;

    /// <summary>
    /// Initializes a new <see cref="EvictionPolicyEvaluator{TRange,TData}"/>.
    /// </summary>
    public EvictionPolicyEvaluator(IReadOnlyList<IEvictionPolicy<TRange, TData>> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        _policies = policies;
    }

    /// <summary>
    /// Notifies all policies that a new segment has been added to storage.
    /// </summary>
    public void OnSegmentAdded(CachedSegment<TRange, TData> segment)
    {
        foreach (var policy in _policies)
        {
            policy.OnSegmentAdded(segment);
        }
    }

    /// <summary>
    /// Notifies all policies that a segment has been removed from storage.
    /// </summary>
    public void OnSegmentRemoved(CachedSegment<TRange, TData> segment)
    {
        foreach (var policy in _policies)
        {
            policy.OnSegmentRemoved(segment);
        }
    }

    /// <summary>
    /// Evaluates all registered policies and returns a combined pressure representing all violated constraints.
    /// </summary>
    public IEvictionPressure<TRange, TData> Evaluate()
    {
        // Collect exceeded pressures without allocating unless at least one policy fires.
        // Common case: no policy fires → return singleton NoPressure without any allocation.
        IEvictionPressure<TRange, TData>? singleExceeded = null;
        List<IEvictionPressure<TRange, TData>>? multipleExceeded = null;

        foreach (var policy in _policies)
        {
            var pressure = policy.Evaluate();

            if (!pressure.IsExceeded)
            {
                continue;
            }

            if (singleExceeded is null)
            {
                singleExceeded = pressure;
            }
            else
            {
                multipleExceeded ??= [singleExceeded];
                multipleExceeded.Add(pressure);
            }
        }

        if (multipleExceeded is not null)
        {
            return new CompositePressure<TRange, TData>([.. multipleExceeded]);
        }

        return singleExceeded ?? NoPressure<TRange, TData>.Instance;
    }
}
