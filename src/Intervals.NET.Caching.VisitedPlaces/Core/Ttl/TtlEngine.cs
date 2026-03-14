using Intervals.NET.Caching.Infrastructure.Concurrency;
using Intervals.NET.Caching.Infrastructure.Diagnostics;
using Intervals.NET.Caching.Infrastructure.Scheduling;
using Intervals.NET.Caching.Infrastructure.Scheduling.Concurrent;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Ttl;

/// <summary>
/// Facade that encapsulates the full TTL subsystem: scheduling, activity tracking, and coordinated disposal.
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class TtlEngine<TRange, TData> : IAsyncDisposable
    where TRange : IComparable<TRange>
{
    private readonly TimeSpan _segmentTtl;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkScheduler<TtlExpirationWorkItem<TRange, TData>> _scheduler;
    private readonly AsyncActivityCounter _activityCounter;
    private readonly CancellationTokenSource _disposalCts;
    private readonly IVisitedPlacesCacheDiagnostics _diagnostics;

    /// <summary>
    /// Initializes a new <see cref="TtlEngine{TRange,TData}"/> and wires all internal TTL infrastructure.
    /// </summary>
    public TtlEngine(
        TimeSpan segmentTtl,
        ISegmentStorage<TRange, TData> storage,
        EvictionEngine<TRange, TData> evictionEngine,
        IVisitedPlacesCacheDiagnostics diagnostics,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(evictionEngine);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _segmentTtl = segmentTtl;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _diagnostics = diagnostics;
        _disposalCts = new CancellationTokenSource();
        _activityCounter = new AsyncActivityCounter();

        var executor = new TtlExpirationExecutor<TRange, TData>(storage, evictionEngine, diagnostics, _timeProvider);

        _scheduler = new ConcurrentWorkScheduler<TtlExpirationWorkItem<TRange, TData>>(
            executor: (workItem, ct) => executor.ExecuteAsync(workItem, ct),
            debounceProvider: static () => TimeSpan.Zero,
            diagnostics: NoOpWorkSchedulerDiagnostics.Instance,
            activityCounter: _activityCounter);
    }

    /// <summary>
    /// Schedules a TTL expiration work item for the given segment.
    /// </summary>
    /// <param name="segment">The segment that was just added to storage.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the work item has been enqueued.</returns>
    public async ValueTask ScheduleExpirationAsync(CachedSegment<TRange, TData> segment)
    {
        var workItem = new TtlExpirationWorkItem<TRange, TData>(
            segment,
            expiresAt: _timeProvider.GetUtcNow() + _segmentTtl,
            _disposalCts.Token);

        await _scheduler.PublishWorkItemAsync(workItem, CancellationToken.None)
            .ConfigureAwait(false);

        _diagnostics.TtlWorkItemScheduled();
    }

    /// <summary>
    /// Asynchronously disposes the TTL engine: cancel token, stop scheduler, drain activity, release CTS.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Cancel the shared disposal token — simultaneously aborts all pending
        // Task.Delay calls across every in-flight TTL work item.
        await _disposalCts.CancelAsync().ConfigureAwait(false);

        // Stop accepting new TTL work items.
        await _scheduler.DisposeAsync().ConfigureAwait(false);

        // Drain all in-flight TTL work items. Each item responds to cancellation
        // by swallowing OperationCanceledException and decrementing the counter,
        // so this completes quickly after the token has been cancelled above.
        await _activityCounter.WaitForIdleAsync().ConfigureAwait(false);

        _disposalCts.Dispose();
    }
}