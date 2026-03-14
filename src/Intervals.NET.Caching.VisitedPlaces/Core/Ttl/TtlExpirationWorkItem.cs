using Intervals.NET.Caching.Infrastructure.Scheduling;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Ttl;

/// <summary>
/// A work item carrying a segment reference and its absolute expiration time for a single TTL event.
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class TtlExpirationWorkItem<TRange, TData> : ISchedulableWorkItem
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// Initializes a new <see cref="TtlExpirationWorkItem{TRange,TData}"/>.
    /// </summary>
    public TtlExpirationWorkItem(
        CachedSegment<TRange, TData> segment,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        Segment = segment;
        ExpiresAt = expiresAt;
        CancellationToken = cancellationToken;
    }

    /// <summary>The segment that will be removed when this work item is executed.</summary>
    public CachedSegment<TRange, TData> Segment { get; }

    /// <summary>The absolute UTC time at which this segment's TTL expires.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public void Cancel() { }

    /// <inheritdoc/>
    public void Dispose() { }
}
