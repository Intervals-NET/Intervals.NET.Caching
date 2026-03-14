using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Pressure;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Policies;

/// <summary>
/// An <see cref="IEvictionPolicy{TRange,TData}"/> that fires when the number of cached
/// segments exceeds a configured maximum count.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// Maintains a running count via <see cref="OnSegmentAdded"/>/<see cref="OnSegmentRemoved"/>
/// using atomic operations for thread safety. Evaluation is O(1).
/// </remarks>
/// <summary>
/// Non-generic factory companion for <see cref="MaxSegmentCountPolicy{TRange,TData}"/>.
/// Enables type inference at the call site: <c>MaxSegmentCountPolicy.Create&lt;int, MyData&gt;(50)</c>.
/// </summary>
public static class MaxSegmentCountPolicy
{
    /// <summary>
    /// Creates a new <see cref="MaxSegmentCountPolicy{TRange,TData}"/> with the specified maximum segment count.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <param name="maxCount">The maximum number of segments. Must be &gt;= 1.</param>
    /// <returns>A new <see cref="MaxSegmentCountPolicy{TRange,TData}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxCount"/> is less than 1.
    /// </exception>
    public static MaxSegmentCountPolicy<TRange, TData> Create<TRange, TData>(int maxCount)
        where TRange : IComparable<TRange>
        => new(maxCount);
}

/// <inheritdoc cref="MaxSegmentCountPolicy{TRange,TData}"/>
public sealed class MaxSegmentCountPolicy<TRange, TData> : IEvictionPolicy<TRange, TData>
    where TRange : IComparable<TRange>
{
    private int _count;

    /// <summary>
    /// The maximum number of segments allowed in the cache before eviction is triggered.
    /// </summary>
    public int MaxCount { get; }

    /// <summary>
    /// Initializes a new <see cref="MaxSegmentCountPolicy{TRange,TData}"/> with the specified maximum segment count.
    /// </summary>
    /// <param name="maxCount">
    /// The maximum number of segments. Must be &gt;= 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxCount"/> is less than 1.
    /// </exception>
    public MaxSegmentCountPolicy(int maxCount)
    {
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount),
                "MaxCount must be greater than or equal to 1.");
        }

        MaxCount = maxCount;
    }

    /// <inheritdoc/>
    public void OnSegmentAdded(CachedSegment<TRange, TData> segment)
    {
        Interlocked.Increment(ref _count);
    }

    /// <inheritdoc/>
    public void OnSegmentRemoved(CachedSegment<TRange, TData> segment)
    {
        Interlocked.Decrement(ref _count);
    }

    /// <inheritdoc/>
    public IEvictionPressure<TRange, TData> Evaluate()
    {
        var count = Volatile.Read(ref _count);

        if (count <= MaxCount)
        {
            return NoPressure<TRange, TData>.Instance;
        }

        return new SegmentCountPressure(count, MaxCount);
    }

    /// <summary>
    /// Tracks whether the segment count exceeds a configured maximum.
    /// </summary>
    internal sealed class SegmentCountPressure : IEvictionPressure<TRange, TData>
    {
        private int _currentCount;
        private readonly int _maxCount;

        /// <summary>
        /// Initializes a new <see cref="SegmentCountPressure"/>.
        /// </summary>
        internal SegmentCountPressure(int currentCount, int maxCount)
        {
            _currentCount = currentCount;
            _maxCount = maxCount;
        }

        /// <inheritdoc/>
        public bool IsExceeded => _currentCount > _maxCount;

        /// <inheritdoc/>
        public void Reduce(CachedSegment<TRange, TData> removedSegment)
        {
            _currentCount--;
        }
    }
}
