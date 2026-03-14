using Intervals.NET.Caching.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;
using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;

/// <summary>
/// An <see cref="IEvictionSelector{TRange,TData}"/> that selects eviction candidates using the
/// Smallest-First strategy: the segment with the narrowest range span is evicted first.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The range domain type used to compute segment spans.</typeparam>
/// <remarks>
/// Uses random sampling with O(SampleSize) per candidate selection. Span is computed once
/// at initialization and cached — segment ranges are immutable. Access patterns do not
/// affect ordering.
/// </remarks>
/// <summary>
/// Non-generic factory companion for <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>.
/// Enables type inference at the call site:
/// <c>SmallestFirstEvictionSelector.Create&lt;int, MyData, MyDomain&gt;(domain)</c>.
/// </summary>
public static class SmallestFirstEvictionSelector
{
    /// <summary>
    /// Creates a new <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <typeparam name="TDomain">The range domain type used to compute segment spans.</typeparam>
    /// <param name="domain">The range domain used to compute segment spans.</param>
    /// <param name="samplingOptions">
    /// Optional sampling configuration. When <see langword="null"/>,
    /// <see cref="EvictionSamplingOptions.Default"/> is used (SampleSize = 32).
    /// </param>
    /// <returns>A new <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="domain"/> is <see langword="null"/>.
    /// </exception>
    public static SmallestFirstEvictionSelector<TRange, TData, TDomain> Create<TRange, TData, TDomain>(
        TDomain domain,
        EvictionSamplingOptions? samplingOptions = null)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
        => new(domain, samplingOptions);
}

/// <inheritdoc cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>
public sealed class SmallestFirstEvictionSelector<TRange, TData, TDomain>
    : SamplingEvictionSelector<TRange, TData>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    /// <summary>
    /// Selector-specific metadata for <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>.
    /// Caches the pre-computed span of the segment's range.
    /// </summary>
    internal sealed class SmallestFirstMetadata : IEvictionMetadata
    {
        /// <summary>
        /// The pre-computed span of the segment's range (in domain steps).
        /// Immutable — segment ranges never change after creation.
        /// </summary>
        public long Span { get; }

        /// <summary>
        /// Initializes a new <see cref="SmallestFirstMetadata"/> with the given span.
        /// </summary>
        /// <param name="span">The pre-computed span of the segment's range.</param>
        public SmallestFirstMetadata(long span)
        {
            Span = span;
        }
    }

    private readonly TDomain _domain;

    /// <summary>
    /// Initializes a new <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>.
    /// </summary>
    /// <param name="domain">The range domain used to compute segment spans.</param>
    /// <param name="samplingOptions">
    /// Optional sampling configuration. When <see langword="null"/>,
    /// <see cref="EvictionSamplingOptions.Default"/> is used (SampleSize = 32).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="domain"/> is <see langword="null"/>.
    /// </exception>
    public SmallestFirstEvictionSelector(
        TDomain domain,
        EvictionSamplingOptions? samplingOptions = null)
        : base(samplingOptions)
    {
        if (domain is null)
        {
            throw new ArgumentNullException(nameof(domain));
        }

        _domain = domain;
    }

    /// <inheritdoc/>
    protected override bool IsWorse(
        CachedSegment<TRange, TData> candidate,
        CachedSegment<TRange, TData> current)
    {
        var candidateSpan = ((SmallestFirstMetadata)candidate.EvictionMetadata!).Span;
        var currentSpan = ((SmallestFirstMetadata)current.EvictionMetadata!).Span;

        return candidateSpan < currentSpan;
    }

    /// <inheritdoc/>
    protected override void EnsureMetadata(CachedSegment<TRange, TData> segment)
    {
        if (segment.EvictionMetadata is SmallestFirstMetadata)
        {
            return;
        }

        var span = segment.Range.Span(_domain);
        segment.EvictionMetadata = new SmallestFirstMetadata(span.IsFinite ? span.Value : 0L);
    }

    /// <inheritdoc/>
    public override void InitializeMetadata(CachedSegment<TRange, TData> segment)
    {
        var span = segment.Range.Span(_domain);
        segment.EvictionMetadata = new SmallestFirstMetadata(span.IsFinite ? span.Value : 0L);
    }

    /// <inheritdoc/>
    public override void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments)
    {
        // SmallestFirst derives ordering from segment span — no metadata to update.
    }
}
