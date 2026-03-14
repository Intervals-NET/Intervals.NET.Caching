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
/// Maintains a running total span via <see cref="OnSegmentAdded"/>/<see cref="OnSegmentRemoved"/>
/// using atomic operations for thread safety. Evaluation is O(1).
/// </remarks>
/// <summary>
/// Non-generic factory companion for <see cref="MaxTotalSpanPolicy{TRange,TData,TDomain}"/>.
/// Enables type inference at the call site: <c>MaxTotalSpanPolicy.Create&lt;int, MyData, MyDomain&gt;(1000, domain)</c>.
/// </summary>
public static class MaxTotalSpanPolicy
{
    /// <summary>
    /// Creates a new <see cref="MaxTotalSpanPolicy{TRange,TData,TDomain}"/> with the specified maximum total span.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <typeparam name="TDomain">The range domain type used to compute spans.</typeparam>
    /// <param name="maxTotalSpan">The maximum total span (in domain units). Must be &gt;= 1.</param>
    /// <param name="domain">The range domain used to compute segment spans.</param>
    /// <returns>A new <see cref="MaxTotalSpanPolicy{TRange,TData,TDomain}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxTotalSpan"/> is less than 1.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="domain"/> is <see langword="null"/>.
    /// </exception>
    public static MaxTotalSpanPolicy<TRange, TData, TDomain> Create<TRange, TData, TDomain>(
        int maxTotalSpan,
        TDomain domain)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
        => new(maxTotalSpan, domain);
}

/// <inheritdoc cref="MaxTotalSpanPolicy{TRange,TData,TDomain}"/>
public sealed class MaxTotalSpanPolicy<TRange, TData, TDomain> : IEvictionPolicy<TRange, TData>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly TDomain _domain;
    private long _totalSpan;

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
    public void OnSegmentAdded(CachedSegment<TRange, TData> segment)
    {
        var span = segment.Range.Span(_domain);
        if (!span.IsFinite)
        {
            return;
        }

        Interlocked.Add(ref _totalSpan, span.Value);
    }

    /// <inheritdoc/>
    public void OnSegmentRemoved(CachedSegment<TRange, TData> segment)
    {
        var span = segment.Range.Span(_domain);
        if (!span.IsFinite)
        {
            return;
        }

        Interlocked.Add(ref _totalSpan, -span.Value);
    }

    /// <inheritdoc/>
    public IEvictionPressure<TRange, TData> Evaluate()
    {
        var currentSpan = Volatile.Read(ref _totalSpan);

        if (currentSpan <= MaxTotalSpan)
        {
            return NoPressure<TRange, TData>.Instance;
        }

        return new TotalSpanPressure(currentSpan, MaxTotalSpan, _domain);
    }

    /// <summary>
    /// Tracks whether the total span exceeds a configured maximum.
    /// </summary>
    internal sealed class TotalSpanPressure : IEvictionPressure<TRange, TData>
    {
        private long _currentTotalSpan;
        private readonly int _maxTotalSpan;
        private readonly TDomain _domain;

        /// <summary>
        /// Initializes a new <see cref="TotalSpanPressure"/>.
        /// </summary>
        internal TotalSpanPressure(long currentTotalSpan, int maxTotalSpan, TDomain domain)
        {
            _currentTotalSpan = currentTotalSpan;
            _maxTotalSpan = maxTotalSpan;
            _domain = domain;
        }

        /// <inheritdoc/>
        public bool IsExceeded => _currentTotalSpan > _maxTotalSpan;

        /// <inheritdoc/>
        public void Reduce(CachedSegment<TRange, TData> removedSegment)
        {
            var span = removedSegment.Range.Span(_domain);
            if (!span.IsFinite)
            {
                return;
            }

            _currentTotalSpan -= span.Value;
        }
    }
}
