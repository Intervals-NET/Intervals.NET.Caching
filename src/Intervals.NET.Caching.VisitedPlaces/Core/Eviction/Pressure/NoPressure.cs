namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Pressure;

/// <summary>
/// A singleton <see cref="IEvictionPressure{TRange,TData}"/> representing no constraint violation.
/// See docs/visited-places/ for design details.
/// </summary>
public sealed class NoPressure<TRange, TData> : IEvictionPressure<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// The shared singleton instance. Use this instead of creating new instances.
    /// </summary>
    public static readonly NoPressure<TRange, TData> Instance = new();

    private NoPressure() { }

    /// <inheritdoc/>
    public bool IsExceeded => false;

    /// <inheritdoc/>
    public void Reduce(CachedSegment<TRange, TData> removedSegment) { }
}
