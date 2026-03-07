using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;

/// <summary>
/// An <see cref="IEvictionSelector{TRange,TData}"/> that orders eviction candidates using the
/// Smallest-First strategy: segments with the narrowest range span are evicted first.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The range domain type used to compute segment spans.</typeparam>
/// <remarks>
/// <para><strong>Strategy:</strong> Orders candidates ascending by span
/// (stored in <see cref="SmallestFirstMetadata.Span"/>) — the narrowest segment is first
/// (highest eviction priority).</para>
/// <para><strong>Execution Context:</strong> Background Path (single writer thread)</para>
/// <para>
/// Smallest-First optimizes for total domain coverage: wide segments (covering more of the domain)
/// are retained over narrow ones. Best for workloads where wider segments are more valuable
/// because they are more likely to be re-used.
/// </para>
/// <para><strong>Metadata:</strong> Uses <see cref="SmallestFirstMetadata"/> stored on
/// <see cref="CachedSegment{TRange,TData}.EvictionMetadata"/>. The span is computed once at
/// initialization from <c>segment.Range.Span(domain).Value</c> and cached — segments are
/// immutable so the span never changes, and pre-computing it avoids redundant computation
/// during every <see cref="OrderCandidates"/> call. <c>UpdateMetadata</c> is a no-op because
/// span is unaffected by access patterns.</para>
/// </remarks>
internal sealed class SmallestFirstEvictionSelector<TRange, TData, TDomain> : IEvictionSelector<TRange, TData>
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
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="domain"/> is <see langword="null"/>.
    /// </exception>
    public SmallestFirstEvictionSelector(TDomain domain)
    {
        if (domain is null)
        {
            throw new ArgumentNullException(nameof(domain));
        }

        _domain = domain;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Computes <c>segment.Range.Span(domain).Value</c> once and stores it as a
    /// <see cref="SmallestFirstMetadata"/> instance on the segment. Because segment ranges
    /// are immutable, this value never needs to be recomputed.
    /// </remarks>
    public void InitializeMetadata(CachedSegment<TRange, TData> segment, DateTime now)
    {
        segment.EvictionMetadata = new SmallestFirstMetadata(segment.Range.Span(_domain).Value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// No-op — SmallestFirst ordering is based on span, which is immutable after segment creation.
    /// Access patterns do not affect eviction priority.
    /// </remarks>
    public void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments, DateTime now)
    {
        // SmallestFirst derives ordering from segment span — no metadata to update.
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sorts candidates ascending by <see cref="SmallestFirstMetadata.Span"/>.
    /// The narrowest segment is first in the returned list.
    /// If a segment has no <see cref="SmallestFirstMetadata"/> (e.g., metadata was never initialized),
    /// the span is computed live from <c>segment.Range.Span(domain).Value</c> as a fallback.
    /// </remarks>
    public IReadOnlyList<CachedSegment<TRange, TData>> OrderCandidates(
        IReadOnlyList<CachedSegment<TRange, TData>> candidates)
    {
        return candidates
            .OrderBy(s => s.EvictionMetadata is SmallestFirstMetadata meta
                ? meta.Span
                : s.Range.Span(_domain).Value)
            .ToList();
    }
}
