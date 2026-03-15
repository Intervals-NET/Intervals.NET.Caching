using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
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
    private readonly TimeSpan? _segmentTtl;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="CacheNormalizationExecutor{TRange,TData,TDomain}"/>.
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
    /// Stores a single chunk via <see cref="ISegmentStorage{TRange,TData}.Add"/>.
    /// Used when exactly one chunk was fetched (constant-span or single-gap requests).
    /// Returns a single-element list if the chunk was stored, or <see langword="null"/> if it
    /// had no valid range or overlapped an existing segment.
    /// </summary>
    private List<CachedSegment<TRange, TData>>? StoreSingle(RangeChunk<TRange, TData> chunk)
    {
        if (!chunk.Range.HasValue)
        {
            return null;
        }

        // VPC.C.3: skip if an overlapping segment already exists in storage.
        var overlapping = _storage.FindIntersecting(chunk.Range.Value);
        if (overlapping.Count > 0)
        {
            return null;
        }

        var data = new ReadOnlyMemory<TData>(chunk.Data.ToArray());
        var segment = new CachedSegment<TRange, TData>(chunk.Range.Value, data)
        {
            ExpiresAt = ComputeExpiresAt()
        };

        _storage.Add(segment);
        _evictionEngine.InitializeSegment(segment);
        _diagnostics.BackgroundSegmentStored();

        return [segment];
    }

    /// <summary>
    /// Validates all chunks, builds the segment array, stores them in a single bulk call via
    /// <see cref="ISegmentStorage{TRange,TData}.AddRange"/>, then initialises metadata for each.
    /// Used when there are two or more fetched chunks.
    /// Returns the list of stored segments, or <see langword="null"/> if none were stored.
    /// </summary>
    private List<CachedSegment<TRange, TData>>? StoreBulk(
        IReadOnlyList<RangeChunk<TRange, TData>> chunks)
    {
        // ValidateChunks is a lazy enumerator — materialise to an array before calling AddRange
        // so all overlap checks are done against the pre-bulk-add storage state (single-writer
        // guarantee means no concurrent writes can occur between the checks and the bulk add).
        var validated = ValidateChunks(chunks).ToArray();

        if (validated.Length == 0)
        {
            return null;
        }

        // Bulk-add: a single normalization pass for all incoming segments.
        _storage.AddRange(validated);

        // Metadata init has no dependency on storage internals —
        // it operates only on the segment objects themselves.
        var justStored = new List<CachedSegment<TRange, TData>>(validated.Length);
        foreach (var segment in validated)
        {
            _evictionEngine.InitializeSegment(segment);
            _diagnostics.BackgroundSegmentStored();
            justStored.Add(segment);
        }

        return justStored;
    }

    /// <summary>
    /// Lazy enumerator that yields a <see cref="CachedSegment{TRange,TData}"/> for each chunk
    /// that has a valid range and does not overlap an existing segment in storage (VPC.C.3).
    /// Materialise with <c>.ToArray()</c> before the bulk add so all checks run against the
    /// consistent pre-add storage state.
    /// </summary>
    private IEnumerable<CachedSegment<TRange, TData>> ValidateChunks(
        IReadOnlyList<RangeChunk<TRange, TData>> chunks)
    {
        var expiresAt = ComputeExpiresAt();

        foreach (var chunk in chunks)
        {
            if (!chunk.Range.HasValue)
            {
                continue;
            }

            var overlapping = _storage.FindIntersecting(chunk.Range.Value);
            if (overlapping.Count > 0)
            {
                continue;
            }

            var data = new ReadOnlyMemory<TData>(chunk.Data.ToArray());
            yield return new CachedSegment<TRange, TData>(chunk.Range.Value, data)
            {
                ExpiresAt = expiresAt
            };
        }
    }

    /// <summary>
    /// Computes the absolute UTC tick expiry for a newly stored segment, or <see langword="null"/>
    /// when TTL is not configured.
    /// </summary>
    private long? ComputeExpiresAt() => _segmentTtl.HasValue
        ? _timeProvider.GetUtcNow().UtcTicks + _segmentTtl.Value.Ticks
        : null;
}
