using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Pressure;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Policies;

/// <summary>
/// An <see cref="IEvictionPolicy{TRange,TData}"/> that fires when the number of cached
/// segments exceeds a configured maximum count.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Firing Condition:</strong> <c>allSegments.Count &gt; MaxCount</c></para>
/// <para><strong>Pressure Produced:</strong> <see cref="SegmentCountPressure"/>
/// with <c>currentCount = allSegments.Count</c> and <c>maxCount = MaxCount</c>.</para>
/// <para>
/// This is the simplest policy: it limits the total number of independently-cached segments
/// regardless of their span or data size. Count-based eviction is order-independent —
/// removing any segment equally satisfies the constraint.
/// </para>
/// </remarks>
internal sealed class MaxSegmentCountPolicy<TRange, TData> : IEvictionPolicy<TRange, TData>
    where TRange : IComparable<TRange>
{
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
    public IEvictionPressure<TRange, TData> Evaluate(IReadOnlyList<CachedSegment<TRange, TData>> allSegments)
    {
        var count = allSegments.Count;

        if (count <= MaxCount)
        {
            return NoPressure<TRange, TData>.Instance;
        }

        return new SegmentCountPressure(count, MaxCount);
    }

    /// <summary>
    /// An <see cref="IEvictionPressure{TRange,TData}"/> that tracks whether the segment count
    /// exceeds a configured maximum. Each <see cref="Reduce"/> call decrements the tracked count.
    /// </summary>
    /// <remarks>
    /// <para><strong>Constraint:</strong> <c>currentCount &gt; maxCount</c></para>
    /// <para><strong>Reduce behavior:</strong> Decrements <c>currentCount</c> by 1 (count-based eviction
    /// is order-independent — every segment removal equally satisfies the constraint).</para>
    /// </remarks>
    internal sealed class SegmentCountPressure : IEvictionPressure<TRange, TData>
    {
        private int _currentCount;
        private readonly int _maxCount;

        /// <summary>
        /// Initializes a new <see cref="SegmentCountPressure"/>.
        /// </summary>
        /// <param name="currentCount">The current number of segments in storage.</param>
        /// <param name="maxCount">The maximum allowed segment count.</param>
        internal SegmentCountPressure(int currentCount, int maxCount)
        {
            _currentCount = currentCount;
            _maxCount = maxCount;
        }

        /// <inheritdoc/>
        public bool IsExceeded => _currentCount > _maxCount;

        /// <inheritdoc/>
        /// <remarks>Decrements the tracked segment count by 1.</remarks>
        public void Reduce(CachedSegment<TRange, TData> removedSegment)
        {
            _currentCount--;
        }
    }
}
