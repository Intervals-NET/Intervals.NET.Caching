# Glossary — VisitedPlaces Cache

VisitedPlaces-specific term definitions. Shared terms — `IRangeCache`, `IDataSource`, `RangeResult`, `RangeChunk`, `CacheInteraction`, `WaitForIdleAsync`, `GetDataAndWaitForIdleAsync`, `LayeredRangeCache` — are defined in `docs/shared/glossary.md`.

---

## Core Terms

**RequestedRange** — A bounded range submitted by the user via `GetDataAsync`. The User Path serves exactly this range (subject to boundary semantics). See Invariant VPC.A.9.

**CachedSegments** — The internal collection of non-contiguous `CachedSegment` objects maintained by the configured Storage Strategy. Gaps between segments are permitted (Invariant VPC.C.1). The User Path reads from this collection; only the Background Path writes to it (Invariant VPC.A.1).

**Segment** — A single contiguous range with its associated data, stored as an entry in `CachedSegments`. Represented by `CachedSegment<TRange, TData>`. Each segment is independently fetchable, independently evictable, and carries per-segment `EvictionMetadata` owned by the Eviction Selector.

**CacheNormalizationRequest** — A message published by the User Path to the Background Path after every `GetDataAsync` call. Carries:
- `UsedSegments` — references to segments that contributed to the response
- `FetchedData` — newly fetched data from `IDataSource` (null for full cache hits)
- `RequestedRange` — the original user request

**True Gap** — A sub-range within `RequestedRange` that is not covered by any segment in `CachedSegments`. Each true gap is fetched synchronously from `IDataSource` on the User Path before the response is assembled (Invariant VPC.F.1, VPC.C.5).

---

## Eviction Terms

**EvictionMetadata** — Per-segment metadata owned by the configured Eviction Selector (`IEvictionMetadata?` on each `CachedSegment`). Selector-specific: `LruMetadata { LastAccessedAt }`, `FifoMetadata { CreatedAt }`, `SmallestFirstMetadata { Span }`. See `docs/visited-places/eviction.md` for the full metadata ownership model and lifecycle.

**EvictionPolicy** — Determines whether eviction should run after each storage step. Evaluates the current `CachedSegments` state and produces an `IEvictionPressure` object. Eviction triggers when ANY configured policy fires (OR semantics, Invariant VPC.E.1a). Built-in: `MaxSegmentCountPolicy`, `MaxTotalSpanPolicy`.

**EvictionPressure** — A constraint tracker produced by an `IEvictionPolicy` when its limit is exceeded. The executor repeatedly calls `Reduce(candidate)` until `IsExceeded` becomes `false`. See `docs/visited-places/eviction.md` for the full pressure model.

**EvictionSelector** — Defines, creates, and updates per-segment eviction metadata. Selects the single worst eviction candidate from a random sample of segments via `TrySelectCandidate` (O(SampleSize), controlled by `EvictionSamplingOptions.SampleSize`). Built-in: `LruEvictionSelector`, `FifoEvictionSelector`, `SmallestFirstEvictionSelector`.

**EvictionEngine** — Internal facade encapsulating the full eviction subsystem. Exposed to `CacheNormalizationExecutor` as its sole eviction dependency. Orchestrates selector metadata management, policy evaluation, and the constraint satisfaction loop. See `docs/visited-places/eviction.md`.

**EvictionExecutor** — Internal component of `EvictionEngine` that runs the constraint satisfaction loop until all policy pressures are satisfied or no eligible candidates remain. See `docs/visited-places/eviction.md`.

**Just-Stored Segment Immunity** — The segment(s) stored in step 2 of the current background event are always excluded from the eviction candidate set (Invariant VPC.E.3). Prevents an infinite fetch-store-evict loop on every new cache miss.

---

## TTL Terms

**SegmentTtl** — An optional `TimeSpan` configured on `VisitedPlacesCacheOptions`. When set, a `TtlExpirationWorkItem` is scheduled immediately after each segment is stored. When null (default), no TTL is applied and segments are only removed by eviction.

**TtlEngine** — Internal facade encapsulating the full TTL subsystem: `TtlExpirationExecutor`, `ConcurrentWorkScheduler`, dedicated `AsyncActivityCounter`, and disposal `CancellationTokenSource`. Exposed to `CacheNormalizationExecutor` as its sole TTL dependency. See Invariant VPC.T.4.

**TtlExpirationWorkItem** — Carries a segment reference and expiry timestamp. Scheduled on a `ConcurrentWorkScheduler`; each work item awaits `Task.Delay` independently on the thread pool (fire-and-forget).

**Idempotent Removal** — The coordination mechanism between TTL expiration and eviction. `CachedSegment.MarkAsRemoved()` ensures only the first caller performs storage removal; concurrent callers are no-ops. See Invariant VPC.T.1 and `docs/visited-places/architecture.md` — Single-Writer Details.

---

## Concurrency Terms

**Background Storage Loop** — The single background thread that dequeues and processes `CacheNormalizationRequest`s in FIFO order. Sole writer of `CachedSegments` and segment `EvictionMetadata` via `CacheNormalizationExecutor`. Invariant VPC.D.3.

**TTL Loop** — Independent background work dispatched fire-and-forget on the thread pool via `ConcurrentWorkScheduler`. Awaits TTL delays and removes expired segments directly via `ISegmentStorage`. Only present when `SegmentTtl` is configured. Runs concurrently with the Background Storage Loop; uses `CachedSegment.MarkAsRemoved()` for coordination.

**FIFO Event Processing** — Unlike `SlidingWindowCache` (latest-intent-wins), VPC processes every `CacheNormalizationRequest` in the exact order it was enqueued — no supersession. See `docs/visited-places/architecture.md` — FIFO vs. Latest-Intent-Wins for the rationale. Invariant VPC.B.1, VPC.B.1a.

---

## Storage Terms

**SnapshotAppendBufferStorage** — Default VPC storage strategy. Maintains a sorted snapshot of segments plus an unsorted append buffer. The User Path reads from the snapshot (safe, no locks needed); the Background Path appends to the buffer and periodically normalizes it into the snapshot. Suitable for caches with up to a few hundred segments.

**LinkedListStrideIndexStorage** — Alternative VPC storage strategy. Maintains a doubly-linked list of segments with a fixed-stride index for O(SampleSize + log N) range queries. Better suited for caches with thousands of segments or high query rates. No append buffer — insertions are immediate.

---

## Configuration Terms

**VisitedPlacesCacheOptions** — Main configuration record. Fields: `StorageStrategy` (required), `SegmentTtl` (optional), `EventChannelCapacity` (optional, for bounded background queue).

**EvictionSamplingOptions** — Configures random sampling for eviction: `SampleSize` (number of segments sampled per `TrySelectCandidate` call). Smaller = faster eviction, less accuracy. Larger = more accurate candidate selection, higher per-eviction cost.

---

## See Also

- `docs/shared/glossary.md` — shared terms: `IRangeCache`, `IDataSource`, `RangeResult`, `CacheInteraction`, `WaitForIdleAsync`, `LayeredRangeCache`
- `docs/visited-places/actors.md` — actor catalog (who does what)
- `docs/visited-places/scenarios.md` — temporal scenario walkthroughs (how terms interact at runtime)
- `docs/visited-places/eviction.md` — full eviction architecture (policy-pressure-selector model, strategy catalog, metadata lifecycle)
- `docs/visited-places/invariants.md` — formal invariants
