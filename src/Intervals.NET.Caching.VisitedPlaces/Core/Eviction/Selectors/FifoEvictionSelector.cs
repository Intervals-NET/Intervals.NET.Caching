using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;

/// <summary>
/// An <see cref="IEvictionSelector{TRange,TData}"/> that selects eviction candidates using
/// the First In, First Out (FIFO) strategy: the oldest segment is evicted first.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// Uses random sampling with O(SampleSize) per candidate selection. Metadata tracks creation
/// time and is immutable after initialization — access patterns do not affect ordering.
/// </remarks>
/// <summary>
/// Non-generic factory companion for <see cref="FifoEvictionSelector{TRange,TData}"/>.
/// Enables type inference at the call site: <c>FifoEvictionSelector.Create&lt;int, MyData&gt;()</c>.
/// </summary>
public static class FifoEvictionSelector
{
    /// <summary>
    /// Creates a new <see cref="FifoEvictionSelector{TRange,TData}"/>.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <param name="samplingOptions">
    /// Optional sampling configuration. When <see langword="null"/>,
    /// <see cref="EvictionSamplingOptions.Default"/> is used (SampleSize = 32).
    /// </param>
    /// <param name="timeProvider">
    /// Optional time provider. When <see langword="null"/>, <see cref="TimeProvider.System"/> is used.
    /// </param>
    /// <returns>A new <see cref="FifoEvictionSelector{TRange,TData}"/> instance.</returns>
    public static FifoEvictionSelector<TRange, TData> Create<TRange, TData>(
        EvictionSamplingOptions? samplingOptions = null,
        TimeProvider? timeProvider = null)
        where TRange : IComparable<TRange>
        => new(samplingOptions, timeProvider);
}

/// <inheritdoc cref="FifoEvictionSelector{TRange,TData}"/>
public sealed class FifoEvictionSelector<TRange, TData> : SamplingEvictionSelector<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Selector-specific metadata for <see cref="FifoEvictionSelector{TRange,TData}"/>.
    /// Records when the segment was first stored in the cache.
    /// </summary>
    internal sealed class FifoMetadata : IEvictionMetadata
    {
        /// <summary>
        /// The UTC timestamp at which the segment was added to the cache.
        /// Immutable — FIFO ordering is determined solely by insertion time.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Initializes a new <see cref="FifoMetadata"/> with the given creation timestamp.
        /// </summary>
        /// <param name="createdAt">The UTC timestamp at which the segment was stored.</param>
        public FifoMetadata(DateTime createdAt)
        {
            CreatedAt = createdAt;
        }
    }

    /// <summary>
    /// Initializes a new <see cref="FifoEvictionSelector{TRange,TData}"/>.
    /// </summary>
    /// <param name="samplingOptions">
    /// Optional sampling configuration. When <see langword="null"/>,
    /// <see cref="EvictionSamplingOptions.Default"/> is used (SampleSize = 32).
    /// </param>
    /// <param name="timeProvider">
    /// Optional time provider used to obtain the current UTC timestamp for metadata creation.
    /// When <see langword="null"/>, <see cref="TimeProvider.System"/> is used.
    /// </param>
    public FifoEvictionSelector(
        EvictionSamplingOptions? samplingOptions = null,
        TimeProvider? timeProvider = null)
        : base(samplingOptions, timeProvider)
    {
    }

    /// <inheritdoc/>
    protected override bool IsWorse(
        CachedSegment<TRange, TData> candidate,
        CachedSegment<TRange, TData> current)
    {
        var candidateTime = ((FifoMetadata)candidate.EvictionMetadata!).CreatedAt;
        var currentTime = ((FifoMetadata)current.EvictionMetadata!).CreatedAt;

        return candidateTime < currentTime;
    }

    /// <inheritdoc/>
    protected override void EnsureMetadata(CachedSegment<TRange, TData> segment)
    {
        if (segment.EvictionMetadata is not FifoMetadata)
        {
            segment.EvictionMetadata = new FifoMetadata(TimeProvider.GetUtcNow().UtcDateTime);
        }
    }

    /// <inheritdoc/>
    public override void InitializeMetadata(CachedSegment<TRange, TData> segment)
    {
        segment.EvictionMetadata = new FifoMetadata(TimeProvider.GetUtcNow().UtcDateTime);
    }

    /// <inheritdoc/>
    public override void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments)
    {
        // FIFO metadata is immutable after creation — nothing to update.
    }
}
