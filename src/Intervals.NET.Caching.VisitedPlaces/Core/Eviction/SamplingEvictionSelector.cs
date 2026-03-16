using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Abstract base class for sampling-based eviction selectors.
/// Implements <see cref="IEvictionSelector{TRange,TData}.TrySelectCandidate"/> using random
/// sampling, delegating only the comparison logic to derived classes.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// Samples up to <c>SampleSize</c> random segments, skipping immune ones, and returns the
/// worst candidate according to <see cref="IsWorse"/>. <see cref="EnsureMetadata"/> guarantees
/// valid metadata before each comparison.
/// </remarks>
public abstract class SamplingEvictionSelector<TRange, TData>
    : IEvictionSelector<TRange, TData>, IStorageAwareEvictionSelector<TRange, TData>
    where TRange : IComparable<TRange>
{
    private ISegmentStorage<TRange, TData>? _storage;

    /// <summary>
    /// The number of segments randomly examined per <see cref="TrySelectCandidate"/> call.
    /// </summary>
    protected int SampleSize { get; }

    /// <summary>
    /// Provides the current UTC time for time-aware selectors (e.g., LRU, FIFO).
    /// Time-agnostic selectors (e.g., SmallestFirst) may ignore this.
    /// </summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>
    /// Initializes a new <see cref="SamplingEvictionSelector{TRange,TData}"/>.
    /// </summary>
    /// <param name="samplingOptions">
    /// Optional sampling configuration. When <see langword="null"/>,
    /// <see cref="EvictionSamplingOptions.Default"/> is used (SampleSize = 32).
    /// </param>
    /// <param name="timeProvider">
    /// Optional time provider. When <see langword="null"/>,
    /// <see cref="TimeProvider.System"/> is used.
    /// </param>
    protected SamplingEvictionSelector(
        EvictionSamplingOptions? samplingOptions = null,
        TimeProvider? timeProvider = null)
    {
        var options = samplingOptions ?? EvictionSamplingOptions.Default;
        SampleSize = options.SampleSize;
        TimeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    void IStorageAwareEvictionSelector<TRange, TData>.Initialize(ISegmentStorage<TRange, TData> storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    /// <inheritdoc/>
    public bool TrySelectCandidate(
        IReadOnlySet<CachedSegment<TRange, TData>> immuneSegments,
        out CachedSegment<TRange, TData> candidate)
    {
        var storage = _storage!; // initialized before first use

        CachedSegment<TRange, TData>? worst = null;

        for (var i = 0; i < SampleSize; i++)
        {
            var segment = storage.TryGetRandomSegment();

            if (segment is null)
            {
                // Storage empty or retries exhausted for this slot — skip.
                continue;
            }

            // Skip immune segments (just-stored + already selected in this eviction pass).
            if (immuneSegments.Contains(segment))
            {
                continue;
            }

            // Guarantee valid metadata before comparison so IsWorse can stay pure.
            EnsureMetadata(segment);

            if (worst is null)
            {
                worst = segment;
            }
            else
            {
                // EnsureMetadata has already been called on worst when it was first selected.
                if (IsWorse(segment, worst))
                {
                    worst = segment;
                }
            }
        }

        if (worst is null)
        {
            // All sampled segments were immune or pool exhausted — no candidate found.
            candidate = default!;
            return false;
        }

        candidate = worst;
        return true;
    }

    /// <summary>
    /// Ensures the segment carries valid selector-specific metadata before comparison.
    /// Creates and attaches the correct metadata if missing or from a different selector type.
    /// </summary>
    /// <param name="segment">The segment to validate and, if necessary, repair.</param>
    protected abstract void EnsureMetadata(CachedSegment<TRange, TData> segment);

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is a worse eviction choice than
    /// <paramref name="current"/> — i.e., should be preferred for eviction.
    /// </summary>
    /// <param name="candidate">The newly sampled segment to evaluate.</param>
    /// <param name="current">The current worst candidate found so far.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="candidate"/> is more eviction-worthy than
    /// <paramref name="current"/>; <see langword="false"/> otherwise.
    /// </returns>
    protected abstract bool IsWorse(
        CachedSegment<TRange, TData> candidate,
        CachedSegment<TRange, TData> current);

    /// <inheritdoc/>
    public abstract void InitializeMetadata(CachedSegment<TRange, TData> segment);

    /// <inheritdoc/>
    public abstract void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments);
}
