using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Background;

/// <summary>
/// Processes <see cref="BackgroundEvent{TRange,TData}"/> items on the Background Storage Loop
/// (the single writer). Executes the four-step Background Path sequence per event:
/// (1) update metadata, (2) store fetched data, (3) evaluate eviction, (4) execute eviction.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The range domain type; used by domain-aware eviction policies.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> Background Storage Loop (single writer thread)</para>
/// <para><strong>Critical Contract — Background Path is the SINGLE WRITER (Invariant VPC.A.10):</strong></para>
/// <para>
/// All mutations to <see cref="ISegmentStorage{TRange,TData}"/> (<c>Add</c> and <c>Remove</c>)
/// are made exclusively here. Neither the User Path nor the
/// <see cref="EvictionEngine{TRange,TData}"/> touches storage.
/// </para>
/// <para><strong>Four-step sequence per event (Invariant VPC.B.3):</strong></para>
/// <list type="number">
/// <item><description>
///   Metadata update — <see cref="EvictionEngine{TRange,TData}.UpdateMetadata"/> updates
///   selector metadata for segments that were read on the User Path (e.g., LRU timestamps).
/// </description></item>
/// <item><description>
///   Store data — each chunk in <see cref="BackgroundEvent{TRange,TData}.FetchedChunks"/> with
///   a non-null Range is added to storage as a new <see cref="CachedSegment{TRange,TData}"/>,
///   followed immediately by <see cref="EvictionEngine{TRange,TData}.InitializeSegment"/> to
///   set up selector metadata and notify stateful policies.
///   Skipped when <c>FetchedChunks</c> is null (full cache hit).
/// </description></item>
/// <item><description>
///   Evaluate and execute eviction — <see cref="EvictionEngine{TRange,TData}.EvaluateAndExecute"/>
///   queries all policies and, if any constraint is exceeded, runs the candidate-removal loop.
///   Returns the list of segments to remove. Only runs when step 2 stored at least one segment.
/// </description></item>
/// <item><description>
///   Remove evicted segments — the processor removes each returned segment from storage and
///   calls <see cref="EvictionEngine{TRange,TData}.OnSegmentsRemoved"/> to notify stateful
///   policies in bulk.
/// </description></item>
/// </list>
/// <para><strong>Activity counter (Invariant S.H.1):</strong></para>
/// <para>
/// The activity counter was incremented by the User Path before publishing the event.
/// It is decremented by <see cref="WorkSchedulerBase{TWorkItem}.ExecuteWorkItemCoreAsync"/>'s
/// <c>finally</c> block, NOT by this processor. This processor must not touch the counter.
/// </para>
/// <para><strong>Exception handling:</strong></para>
/// <para>
/// Exceptions are caught, reported via <see cref="ICacheDiagnostics.BackgroundEventProcessingFailed"/>,
/// and swallowed so that the background loop survives individual event failures.
/// </para>
/// </remarks>
internal sealed class BackgroundEventProcessor<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly ISegmentStorage<TRange, TData> _storage;
    private readonly EvictionEngine<TRange, TData> _evictionEngine;
    private readonly ICacheDiagnostics _diagnostics;

    /// <summary>
    /// Initializes a new <see cref="BackgroundEventProcessor{TRange,TData,TDomain}"/>.
    /// </summary>
    /// <param name="storage">The segment storage (single writer — only mutated here).</param>
    /// <param name="evictionEngine">
    /// The eviction engine facade; encapsulates selector metadata, policy evaluation,
    /// execution, and eviction diagnostics.
    /// </param>
    /// <param name="diagnostics">Diagnostics sink; must never throw.</param>
    public BackgroundEventProcessor(
        ISegmentStorage<TRange, TData> storage,
        EvictionEngine<TRange, TData> evictionEngine,
        ICacheDiagnostics diagnostics)
    {
        _storage = storage;
        _evictionEngine = evictionEngine;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Processes a single <see cref="BackgroundEvent{TRange,TData}"/> through the four-step sequence.
    /// </summary>
    /// <param name="backgroundEvent">The event to process.</param>
    /// <param name="_">Unused cancellation token (BackgroundEvents never cancel).</param>
    /// <returns>A <see cref="Task"/> that completes when processing is done.</returns>
    /// <remarks>
    /// <para>
    /// The activity counter is managed by the caller (<see cref="WorkSchedulerBase{TWorkItem}"/>),
    /// which decrements it in its own <c>finally</c> block after this method returns.
    /// This processor must NOT touch the activity counter.
    /// </para>
    /// <para>
    /// Note: <c>BackgroundEventReceived()</c> is called by the scheduler adapter
    /// (<c>VisitedPlacesWorkSchedulerDiagnostics.WorkStarted()</c>) before this method is invoked.
    /// </para>
    /// </remarks>
    public Task ProcessEventAsync(BackgroundEvent<TRange, TData> backgroundEvent, CancellationToken _)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Step 1: Update selector metadata for segments read on the User Path.
            _evictionEngine.UpdateMetadata(backgroundEvent.UsedSegments, now);
            _diagnostics.BackgroundStatisticsUpdated();

            // Step 2: Store freshly fetched data (null FetchedChunks means full cache hit — skip).
            // Track ALL segments stored in this event cycle for just-stored immunity (Invariant VPC.E.3).
            var justStoredSegments = new List<CachedSegment<TRange, TData>>();

            if (backgroundEvent.FetchedChunks != null)
            {
                foreach (var chunk in backgroundEvent.FetchedChunks)
                {
                    if (!chunk.Range.HasValue)
                    {
                        continue;
                    }

                    var data = new ReadOnlyMemory<TData>(chunk.Data.ToArray());
                    var segment = new CachedSegment<TRange, TData>(chunk.Range.Value, data);

                    _storage.Add(segment);
                    _evictionEngine.InitializeSegment(segment, now);
                    _diagnostics.BackgroundSegmentStored();

                    justStoredSegments.Add(segment);
                }
            }

            // Steps 3 & 4: Evaluate and execute eviction only when new data was stored.
            if (justStoredSegments.Count > 0)
            {
                // Step 3+4: Evaluate policies and get candidates to remove (Invariant VPC.E.2a).
                // Eviction diagnostics (EvictionEvaluated, EvictionTriggered, EvictionExecuted)
                // are fired internally by the engine.
                var allSegments = _storage.GetAllSegments();
                var toRemove = _evictionEngine.EvaluateAndExecute(allSegments, justStoredSegments);

                // Step 4 (storage): Remove evicted segments; processor is the sole storage writer.
                foreach (var segment in toRemove)
                {
                    _storage.Remove(segment);
                }

                _evictionEngine.OnSegmentsRemoved(toRemove);
            }

            _diagnostics.BackgroundEventProcessed();
        }
        catch (Exception ex)
        {
            _diagnostics.BackgroundEventProcessingFailed(ex);
            // Swallow: the background loop must survive individual event failures.
        }

        // todo: check how this actually sync method works with the task based scheduler. I afraid that it can be executed on the user path, because there is no any awaiting of the not completed task inside, so there is no freeing the thread.
        return Task.CompletedTask;
    }
}
