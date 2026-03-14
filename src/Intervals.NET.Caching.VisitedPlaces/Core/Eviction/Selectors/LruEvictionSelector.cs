using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;

/// <summary>
/// An <see cref="IEvictionSelector{TRange,TData}"/> that selects eviction candidates using
/// the Least Recently Used (LRU) strategy: the least recently accessed segment is evicted first.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// Uses random sampling with O(SampleSize) per candidate selection. Metadata tracks last
/// access time and is updated when segments are used on the User Path.
/// </remarks>
/// <summary>
/// Non-generic factory companion for <see cref="LruEvictionSelector{TRange,TData}"/>.
/// Enables type inference at the call site: <c>LruEvictionSelector.Create&lt;int, MyData&gt;()</c>.
/// </summary>
public static class LruEvictionSelector
{
    /// <summary>
    /// Creates a new <see cref="LruEvictionSelector{TRange,TData}"/>.
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
    /// <returns>A new <see cref="LruEvictionSelector{TRange,TData}"/> instance.</returns>
    public static LruEvictionSelector<TRange, TData> Create<TRange, TData>(
        EvictionSamplingOptions? samplingOptions = null,
        TimeProvider? timeProvider = null)
        where TRange : IComparable<TRange>
        => new(samplingOptions, timeProvider);
}

/// <inheritdoc cref="LruEvictionSelector{TRange,TData}"/>
public sealed class LruEvictionSelector<TRange, TData> : SamplingEvictionSelector<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Selector-specific metadata for <see cref="LruEvictionSelector{TRange,TData}"/>.
    /// Tracks the most recent access time for a cached segment.
    /// </summary>
    internal sealed class LruMetadata : IEvictionMetadata
    {
        /// <summary>
        /// The UTC timestamp of the last access to the segment on the User Path.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Initializes a new <see cref="LruMetadata"/> with the given access timestamp.
        /// </summary>
        /// <param name="lastAccessedAt">The initial last-accessed timestamp (typically the creation time).</param>
        public LruMetadata(DateTime lastAccessedAt)
        {
            LastAccessedAt = lastAccessedAt;
        }
    }

    /// <summary>
    /// Initializes a new <see cref="LruEvictionSelector{TRange,TData}"/>.
    /// </summary>
    /// <param name="samplingOptions">
    /// Optional sampling configuration. When <see langword="null"/>,
    /// <see cref="EvictionSamplingOptions.Default"/> is used (SampleSize = 32).
    /// </param>
    /// <param name="timeProvider">
    /// Optional time provider used to obtain the current UTC timestamp for metadata creation
    /// and updates. When <see langword="null"/>, <see cref="TimeProvider.System"/> is used.
    /// </param>
    public LruEvictionSelector(
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
        var candidateTime = ((LruMetadata)candidate.EvictionMetadata!).LastAccessedAt;
        var currentTime = ((LruMetadata)current.EvictionMetadata!).LastAccessedAt;

        return candidateTime < currentTime;
    }

    /// <inheritdoc/>
    protected override void EnsureMetadata(CachedSegment<TRange, TData> segment)
    {
        if (segment.EvictionMetadata is not LruMetadata)
        {
            segment.EvictionMetadata = new LruMetadata(TimeProvider.GetUtcNow().UtcDateTime);
        }
    }

    /// <inheritdoc/>
    public override void InitializeMetadata(CachedSegment<TRange, TData> segment)
    {
        segment.EvictionMetadata = new LruMetadata(TimeProvider.GetUtcNow().UtcDateTime);
    }

    /// <inheritdoc/>
    public override void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;

        foreach (var segment in usedSegments)
        {
            if (segment.EvictionMetadata is not LruMetadata meta)
            {
                meta = new LruMetadata(now);
                segment.EvictionMetadata = meta;
            }
            else
            {
                meta.LastAccessedAt = now;
            }
        }
    }
}
