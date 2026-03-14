namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Pressure;

/// <summary>
/// Aggregates multiple <see cref="IEvictionPressure{TRange,TData}"/> instances into a single
/// composite pressure. Exceeded when ANY child pressure is exceeded.
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class CompositePressure<TRange, TData> : IEvictionPressure<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly IEvictionPressure<TRange, TData>[] _pressures;

    /// <summary>
    /// Initializes a new <see cref="CompositePressure{TRange,TData}"/>.
    /// </summary>
    internal CompositePressure(IEvictionPressure<TRange, TData>[] pressures)
    {
        _pressures = pressures;
    }

    /// <inheritdoc/>
    public bool IsExceeded
    {
        get
        {
            foreach (var pressure in _pressures)
            {
                if (pressure.IsExceeded)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public void Reduce(CachedSegment<TRange, TData> removedSegment)
    {
        foreach (var pressure in _pressures)
        {
            pressure.Reduce(removedSegment);
        }
    }
}
