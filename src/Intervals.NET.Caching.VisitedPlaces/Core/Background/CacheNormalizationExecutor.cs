using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Background;

/// <summary>
/// Processes cache normalization requests on the Background Storage Loop (single writer).
/// See docs/visited-places/ for design details.
/// </summary>
internal sealed class CacheNormalizationExecutor<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly ISegmentStorage<TRange, TData> _storage;
    private readonly EvictionEngine<TRange, TData> _evictionEngine;
    private readonly IVisitedPlacesCacheDiagnostics _diagnostics;
    private readonly TimeSpan? _segmentTtl;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="CacheNormalizationExecutor{TRange,TData}"/>.
    /// </summary>
    public CacheNormalizationExecutor(
        ISegmentStorage<TRange, TData> storage,
        EvictionEngine<TRange, TData> evictionEngine,
        IVisitedPlacesCacheDiagnostics diagnostics,
        TimeSpan? segmentTtl = null,
        TimeProvider? timeProvider = null)
    {
        _storage = storage;
        _evictionEngine = evictionEngine;
        _diagnostics = diagnostics;
        _segmentTtl = segmentTtl;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Executes a single cache normalization request through the four-step sequence.
    /// </summary>
    /// <remarks>
    /// This method is currently fully synchronous and returns <see cref="Task.CompletedTask"/>.
    /// The <c>Task</c> return type is required by the scheduler's delegate contract.
    /// TODO: If this method remains synchronous, consider refactoring to <c>void Execute(...)</c>
    /// and adapting the scheduler call site to wrap it: <c>(evt, ct) =&gt; { Execute(evt, ct); return Task.CompletedTask; }</c>.
    /// </remarks>
    public Task ExecuteAsync(CacheNormalizationRequest<TRange, TData> request, CancellationToken _)
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
                // Choose between bulk and single-add paths based on chunk count.
                //
                // Constant-span access patterns (each request fetches at most one range) never
                // benefit from bulk storage: there is at most one gap per request, so the
                // single-add path is used.
                //
                // Variable-span access patterns can produce many gaps in a single request
                // (one per cached sub-range not covering the requested span). With the
                // single-add path each chunk triggers a normalization every AppendBufferSize
                // additions — O(gaps/bufferSize) normalizations, each rebuilding an
                // increasingly large data structure: O(gaps x totalSegments) overall.
                // The bulk path reduces this to a single O(totalSegments) normalization.
                if (request.FetchedChunks.Count > 1)
                {
                    justStoredSegments = StoreBulk(request.FetchedChunks);
                }
                else
                {
                    justStoredSegments = StoreSingle(request.FetchedChunks[0]);
                }
            }

            // Step 2b: TryNormalize — called unconditionally after every store step.
            // The storage decides internally whether the threshold is met.
            // Expired segments discovered here are removed from eviction policy aggregates
            // and reported via diagnostics (lazy TTL expiration, Invariant VPC.T.1).
            if (_storage.TryNormalize(out var expiredSegments) && expiredSegments != null)
            {
                foreach (var expired in expiredSegments)
                {
                    _evictionEngine.OnSegmentRemoved(expired);
                    _diagnostics.TtlSegmentExpired();
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
                    // Eviction candidates are sampled from live storage (TryGetRandomSegment
                    // filters IsRemoved and IsExpired). TryNormalize physically removes expired
                    // segments before this loop runs — so the candidate is always live at this
                    // point. TryRemove guards against the degenerate case: if the segment was
                    // already removed, OnSegmentRemoved is skipped to prevent a double-decrement
                    // of policy aggregates.
                    if (!_storage.TryRemove(segment))
                    {
                        continue;
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
        catch (Exception ex)
        {
            _diagnostics.BackgroundOperationFailed(ex);
            // Swallow: the background loop must survive individual request failures.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores a single chunk via <see cref="ISegmentStorage{TRange,TData}.TryAdd"/>.
    /// Used when exactly one chunk was fetched (constant-span or single-gap requests).
    /// Returns a single-element list if the chunk was stored, or <see langword="null"/> if it
    /// had no valid range or was skipped due to an overlap with an existing segment (VPC.C.3).
    /// </summary>
    private List<CachedSegment<TRange, TData>>? StoreSingle(RangeChunk<TRange, TData> chunk)
    {
        if (!chunk.Range.HasValue)
        {
            return null;
        }

        var data = new ReadOnlyMemory<TData>(chunk.Data.ToArray());
        var segment = new CachedSegment<TRange, TData>(chunk.Range.Value, data)
        {
            ExpiresAt = ComputeExpiresAt()
        };

        // VPC.C.3: TryAdd skips the segment if it overlaps an existing one.
        if (!_storage.TryAdd(segment))
        {
            return null;
        }

        _evictionEngine.InitializeSegment(segment);
        _diagnostics.BackgroundSegmentStored();

        return [segment];
    }

    /// <summary>
    /// Builds a segment array, stores the non-overlapping subset in a single bulk call via
    /// <see cref="ISegmentStorage{TRange,TData}.TryAddRange"/>, then initialises metadata for each.
    /// Used when there are two or more fetched chunks.
    /// Returns the list of stored segments, or <see langword="null"/> if none were stored.
    /// </summary>
    private List<CachedSegment<TRange, TData>>? StoreBulk(
        IReadOnlyList<RangeChunk<TRange, TData>> chunks)
    {
        // Build a segment for every chunk that has a valid range.
        // TryAddRange performs the VPC.C.3 overlap check internally.
        var candidates = BuildSegments(chunks);

        if (candidates.Length == 0)
        {
            return null;
        }

        // Bulk-add: a single normalization pass for all stored segments.
        // TryAddRange returns only the segments that were actually stored.
        var stored = _storage.TryAddRange(candidates);

        if (stored.Length == 0)
        {
            return null;
        }

        // Metadata init has no dependency on storage internals —
        // it operates only on the segment objects themselves.
        var justStored = new List<CachedSegment<TRange, TData>>(stored.Length);
        foreach (var segment in stored)
        {
            _evictionEngine.InitializeSegment(segment);
            _diagnostics.BackgroundSegmentStored();
            justStored.Add(segment);
        }

        return justStored;
    }

    /// <summary>
    /// Builds a <see cref="CachedSegment{TRange,TData}"/> array from chunks that have a valid range.
    /// Chunks without a valid range are skipped. No overlap check is performed here — that
    /// responsibility belongs to the storage operations (Invariant VPC.C.3).
    /// </summary>
    private CachedSegment<TRange, TData>[] BuildSegments(
        IReadOnlyList<RangeChunk<TRange, TData>> chunks)
    {
        var expiresAt = ComputeExpiresAt();
        List<CachedSegment<TRange, TData>>? result = null;

        foreach (var chunk in chunks)
        {
            if (!chunk.Range.HasValue)
            {
                continue;
            }

            var data = new ReadOnlyMemory<TData>(chunk.Data.ToArray());
            (result ??= []).Add(new CachedSegment<TRange, TData>(chunk.Range.Value, data)
            {
                ExpiresAt = expiresAt
            });
        }

        return result?.ToArray() ?? [];
    }

    /// <summary>
    /// Computes the absolute UTC tick expiry for a newly stored segment, or <see langword="null"/>
    /// when TTL is not configured.
    /// </summary>
    private long? ComputeExpiresAt() => _segmentTtl.HasValue
        ? _timeProvider.GetUtcNow().UtcTicks + _segmentTtl.Value.Ticks
        : null;
}
