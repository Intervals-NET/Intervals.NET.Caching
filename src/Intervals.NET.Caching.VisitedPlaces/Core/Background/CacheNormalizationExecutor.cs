using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Core.Ttl;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Background;

/// <summary>
/// Processes cache normalization requests on the Background Storage Loop (single writer).
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class CacheNormalizationExecutor<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly ISegmentStorage<TRange, TData> _storage;
    private readonly EvictionEngine<TRange, TData> _evictionEngine;
    private readonly IVisitedPlacesCacheDiagnostics _diagnostics;
    private readonly TtlEngine<TRange, TData>? _ttlEngine;

    /// <summary>
    /// Initializes a new <see cref="CacheNormalizationExecutor{TRange,TData,TDomain}"/>.
    /// </summary>
    public CacheNormalizationExecutor(
        ISegmentStorage<TRange, TData> storage,
        EvictionEngine<TRange, TData> evictionEngine,
        IVisitedPlacesCacheDiagnostics diagnostics,
        TtlEngine<TRange, TData>? ttlEngine = null)
    {
        _storage = storage;
        _evictionEngine = evictionEngine;
        _diagnostics = diagnostics;
        _ttlEngine = ttlEngine;
    }

    /// <summary>
    /// Executes a single cache normalization request through the four-step sequence.
    /// </summary>
    public async Task ExecuteAsync(CacheNormalizationRequest<TRange, TData> request, CancellationToken _)
    {
        try
        {
            // Step 1: Update selector metadata for segments read on the User Path.
            _evictionEngine.UpdateMetadata(request.UsedSegments);
            _diagnostics.BackgroundStatisticsUpdated();

            // Step 2: Store freshly fetched data (null FetchedChunks means full cache hit — skip).
            // Track ALL segments stored in this request cycle for just-stored immunity (Invariant VPC.E.3).
            // Lazy-init: list is only allocated when at least one segment is actually stored,
            // so the full-hit path (FetchedChunks == null) pays zero allocation here.
            List<CachedSegment<TRange, TData>>? justStoredSegments = null;

            if (request.FetchedChunks != null)
            {
                foreach (var chunk in request.FetchedChunks)
                {
                    if (!chunk.Range.HasValue)
                    {
                        continue;
                    }

                    // VPC.C.3: Enforce no-overlap invariant before storing. If a segment covering
                    // any part of this chunk's range already exists (e.g., from a concurrent
                    // in-flight request for the same range), skip storing to prevent duplicates.
                    var overlapping = _storage.FindIntersecting(chunk.Range.Value);
                    if (overlapping.Count > 0)
                    {
                        continue;
                    }

                    var data = new ReadOnlyMemory<TData>(chunk.Data.ToArray());
                    var segment = new CachedSegment<TRange, TData>(chunk.Range.Value, data);

                    _storage.Add(segment);
                    _evictionEngine.InitializeSegment(segment);
                    _diagnostics.BackgroundSegmentStored();

                    // TTL: if enabled, delegate scheduling to the engine facade.
                    if (_ttlEngine != null)
                    {
                        await _ttlEngine.ScheduleExpirationAsync(segment).ConfigureAwait(false);
                    }

                    (justStoredSegments ??= []).Add(segment);
                }
            }

            // Steps 3 & 4: Evaluate and execute eviction only when new data was stored.
            if (justStoredSegments != null)
            {
                // Step 3+4: Evaluate policies and iterate candidates to remove (Invariant VPC.E.2a).
                // The selector samples directly from its injected storage.
                // EvictionEvaluated and EvictionTriggered diagnostics are fired by the engine.
                // EvictionExecuted is fired here after the full enumeration completes.
                var evicted = false;
                foreach (var segment in _evictionEngine.EvaluateAndExecute(justStoredSegments))
                {
                    if (!_storage.TryRemove(segment))
                    {
                        continue; // TTL actor already claimed this segment — skip.
                    }

                    _evictionEngine.OnSegmentRemoved(segment);
                    _diagnostics.EvictionSegmentRemoved();
                    evicted = true;
                }

                if (evicted)
                {
                    _diagnostics.EvictionExecuted();
                }
            }

            _diagnostics.NormalizationRequestProcessed();
        }
        catch (OperationCanceledException)
        {
            // Cancellation (e.g. from TtlEngine disposal CTS) must propagate so the
            // scheduler's execution pipeline can fire WorkCancelled instead of WorkFailed.
            throw;
        }
        catch (Exception ex)
        {
            _diagnostics.BackgroundOperationFailed(ex);
            // Swallow: the background loop must survive individual request failures.
        }
    }
}
