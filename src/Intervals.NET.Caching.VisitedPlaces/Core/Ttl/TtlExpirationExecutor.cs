using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Ttl;

/// <summary>
/// Executes TTL expiration work items: waits until expiry, then removes the segment from storage.
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class TtlExpirationExecutor<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly ISegmentStorage<TRange, TData> _storage;
    private readonly EvictionEngine<TRange, TData> _evictionEngine;
    private readonly IVisitedPlacesCacheDiagnostics _diagnostics;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="TtlExpirationExecutor{TRange,TData}"/>.
    /// </summary>
    public TtlExpirationExecutor(
        ISegmentStorage<TRange, TData> storage,
        EvictionEngine<TRange, TData> evictionEngine,
        IVisitedPlacesCacheDiagnostics diagnostics,
        TimeProvider? timeProvider = null)
    {
        _storage = storage;
        _evictionEngine = evictionEngine;
        _diagnostics = diagnostics;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Waits until the work item's expiration time, then removes the segment if it is still live.
    /// </summary>
    public async Task ExecuteAsync(
        TtlExpirationWorkItem<TRange, TData> workItem,
        CancellationToken cancellationToken)
    {
        // Compute remaining delay from now to expiry.
        // If already past expiry, delay is zero and we proceed immediately.
        var remaining = workItem.ExpiresAt - _timeProvider.GetUtcNow();

        if (remaining > TimeSpan.Zero)
        {
            // Await expiry. OperationCanceledException propagates on cache disposal —
            // handled by the scheduler pipeline (not caught here).
            await Task.Delay(remaining, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        // Delegate removal to storage, which atomically claims ownership via TryMarkAsRemoved()
        // and returns true only for the first caller. If the segment was already evicted by
        // the Background Storage Loop, this returns false and we fire only the diagnostic.
        if (!_storage.TryRemove(workItem.Segment))
        {
            // Already removed by eviction — idempotent no-op. Diagnostic is NOT fired;
            // TtlSegmentExpired counts only actual TTL-driven removals.
            return;
        }

        // Notify stateful policies (e.g. decrements MaxTotalSpanPolicy._totalSpan atomically).
        // Single-segment overload avoids any intermediate collection allocation.
        _evictionEngine.OnSegmentRemoved(workItem.Segment);

        _diagnostics.TtlSegmentExpired();
    }
}