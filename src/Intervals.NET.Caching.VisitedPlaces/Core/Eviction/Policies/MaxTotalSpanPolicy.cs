using Intervals.NET.Caching.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Pressure;
using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Policies;

/// <summary>
/// An <see cref="IEvictionPolicy{TRange,TData}"/> that fires when the sum of all cached
/// segment spans (total domain coverage) exceeds a configured maximum.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The range domain type used to compute spans.</typeparam>
/// <remarks>
/// <para><strong>Firing Condition:</strong>
/// <c>sum(segment.Range.Span(domain) for segment in allSegments) &gt; MaxTotalSpan</c></para>
/// <para><strong>Pressure Produced:</strong> <see cref="TotalSpanPressure"/>
/// with the computed total span, the configured maximum, and the domain for per-segment span
/// computation during <see cref="IEvictionPressure{TRange,TData}.Reduce"/>.</para>
/// <para>
/// This policy limits the total cached domain coverage regardless of how many segments it is
/// split into. More meaningful than segment count when segments vary significantly in span.
/// </para>
/// <para><strong>Key improvement over <c>MaxTotalSpanEvaluator</c>:</strong></para>
/// <para>
/// The old evaluator had to estimate removal counts using a greedy algorithm (sort by span
/// descending, count until excess is covered). This estimate could mismatch the actual executor
/// order (LRU, FIFO, etc.), leading to under-eviction. The new design avoids this entirely:
/// the pressure object tracks actual span reduction as segments are removed, regardless of order.
/// </para>
/// <para><strong>Span Computation:</strong> Uses <typeparamref name="TDomain"/> to compute each
/// segment's span at evaluation time. The domain is captured at construction and passed to the
/// pressure object for use during <see cref="IEvictionPressure{TRange,TData}.Reduce"/>.</para>
/// </remarks>
internal sealed class MaxTotalSpanPolicy<TRange, TData, TDomain> : IEvictionPolicy<TRange, TData>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly TDomain _domain;

    /// <summary>
    /// The maximum total span allowed across all cached segments before eviction is triggered.
    /// </summary>
    public int MaxTotalSpan { get; }

    /// <summary>
    /// Initializes a new <see cref="MaxTotalSpanPolicy{TRange,TData,TDomain}"/> with the
    /// specified maximum total span and domain.
    /// </summary>
    /// <param name="maxTotalSpan">
    /// The maximum total span (in domain units). Must be &gt;= 1.
    /// </param>
    /// <param name="domain">The range domain used to compute segment spans.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxTotalSpan"/> is less than 1.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="domain"/> is <see langword="null"/>.
    /// </exception>
    public MaxTotalSpanPolicy(int maxTotalSpan, TDomain domain)
    {
        if (maxTotalSpan < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTotalSpan),
                "MaxTotalSpan must be greater than or equal to 1.");
        }

        if (domain is null)
        {
            throw new ArgumentNullException(nameof(domain));
        }

        MaxTotalSpan = maxTotalSpan;
        _domain = domain;
    }

    /// <inheritdoc/>
    public IEvictionPressure<TRange, TData> Evaluate(IReadOnlyList<CachedSegment<TRange, TData>> allSegments)
    {
        var totalSpan = allSegments.Sum(s => s.Range.Span(_domain).Value);

        if (totalSpan <= MaxTotalSpan)
        {
            return NoPressure<TRange, TData>.Instance;
        }

        return new TotalSpanPressure(totalSpan, MaxTotalSpan, _domain);
    }

    /// <summary>
    /// An <see cref="IEvictionPressure{TRange,TData}"/> that tracks whether the total span
    /// (sum of all segment spans) exceeds a configured maximum. Each <see cref="Reduce"/> call
    /// subtracts the removed segment's span from the tracked total.
    /// </summary>
    /// <remarks>
    /// <para><strong>Constraint:</strong> <c>currentTotalSpan &gt; maxTotalSpan</c></para>
    /// <para><strong>Reduce behavior:</strong> Subtracts the removed segment's span from <c>currentTotalSpan</c>.
    /// This is the key improvement over the old <c>MaxTotalSpanEvaluator</c> which had to estimate
    /// removal counts using a greedy algorithm that could mismatch the actual executor order.</para>
    /// <para><strong>TDomain capture:</strong> The <typeparamref name="TDomain"/> is captured internally
    /// so that the <see cref="IEvictionPressure{TRange,TData}"/> interface stays generic only on
    /// <c>&lt;TRange, TData&gt;</c>.</para>
    /// </remarks>
    internal sealed class TotalSpanPressure : IEvictionPressure<TRange, TData>
    {
        private long _currentTotalSpan;
        private readonly int _maxTotalSpan;
        private readonly TDomain _domain;

        /// <summary>
        /// Initializes a new <see cref="TotalSpanPressure"/>.
        /// </summary>
        /// <param name="currentTotalSpan">The current total span across all segments.</param>
        /// <param name="maxTotalSpan">The maximum allowed total span.</param>
        /// <param name="domain">The range domain used to compute individual segment spans during <see cref="Reduce"/>.</param>
        internal TotalSpanPressure(long currentTotalSpan, int maxTotalSpan, TDomain domain)
        {
            _currentTotalSpan = currentTotalSpan;
            _maxTotalSpan = maxTotalSpan;
            _domain = domain;
        }

        /// <inheritdoc/>
        public bool IsExceeded => _currentTotalSpan > _maxTotalSpan;

        /// <inheritdoc/>
        /// <remarks>Subtracts the removed segment's span from the tracked total.</remarks>
        public void Reduce(CachedSegment<TRange, TData> removedSegment)
        {
            _currentTotalSpan -= removedSegment.Range.Span(_domain).Value;
        }
    }
}
