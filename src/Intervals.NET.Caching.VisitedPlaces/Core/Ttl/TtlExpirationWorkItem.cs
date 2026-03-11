using Intervals.NET.Caching.Infrastructure.Scheduling;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Ttl;

/// <summary>
/// A work item carrying the information needed for a single TTL expiration event:
/// a reference to the segment to remove and the absolute time at which it expires.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Lifecycle:</strong></para>
/// <para>
/// One <see cref="TtlExpirationWorkItem{TRange,TData}"/> is created per stored segment when
/// TTL is enabled. It is published to <c>TtlExpirationExecutor</c>'s scheduler immediately
/// after the segment is stored in the Background Storage Loop (Step 2 of
/// <c>CacheNormalizationExecutor</c>).
/// </para>
/// <para><strong>Ownership of <see cref="ExpiresAt"/>:</strong></para>
/// <para>
/// <see cref="ExpiresAt"/> is computed at creation time as
/// <c>DateTimeOffset.UtcNow + SegmentTtl</c>. The executor delays until this absolute
/// timestamp to account for any scheduling latency between creation and execution.
/// </para>
/// <para><strong>Cancellation:</strong></para>
/// <para>
/// The <see cref="CancellationToken"/> is cancelled by the scheduler on disposal (cache teardown).
/// This causes the executor's <c>Task.Delay</c> to throw <see cref="OperationCanceledException"/>,
/// cleanly aborting pending TTL expirations without removing segments.
/// </para>
/// <para>Alignment: Invariant VPC.T.1 (TTL expirations are idempotent).</para>
/// </remarks>
internal sealed class TtlExpirationWorkItem<TRange, TData> : ISchedulableWorkItem
    where TRange : IComparable<TRange>
{
    // todo: cts is redundant here and just adds allocation cost here on every new added segment.
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Initializes a new <see cref="TtlExpirationWorkItem{TRange,TData}"/>.
    /// </summary>
    /// <param name="segment">The segment to expire.</param>
    /// <param name="expiresAt">The absolute UTC time at which the segment expires.</param>
    public TtlExpirationWorkItem(
        CachedSegment<TRange, TData> segment,
        DateTimeOffset expiresAt)
    {
        Segment = segment;
        ExpiresAt = expiresAt;
    }

    /// <summary>The segment that will be removed when this work item is executed.</summary>
    public CachedSegment<TRange, TData> Segment { get; }

    /// <summary>The absolute UTC time at which this segment's TTL expires.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken => _cts.Token;

    /// <inheritdoc/>
    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Safe to ignore — already disposed.
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts.Dispose();
    }
}
