# Integration Tests — VisitedPlaces Cache

End-to-end tests that wire `VisitedPlacesCache` to real data sources and verify observable behavior across the full User Path → Background Path cycle. Uses `WaitForIdleAsync` to drive the cache to a deterministic state before asserting.

## Run

```bash
dotnet test tests/Intervals.NET.Caching.VisitedPlaces.Integration.Tests/Intervals.NET.Caching.VisitedPlaces.Integration.Tests.csproj
```

## Test Files

### `CacheDataSourceInteractionTests.cs`

Validates the request/response cycle, diagnostics counters, and both storage strategies.

| Group                   | What is tested                                                                              |
|-------------------------|---------------------------------------------------------------------------------------------|
| Cache Miss              | Cold-start full miss, data source called, correct data returned, diagnostics counters       |
| Cache Hit               | Full hit after caching, data source NOT called, correct data, diagnostics counters          |
| Partial Hit             | Gap fetch: only missing portion fetched, data assembled correctly, diagnostics counters     |
| Multiple Requests       | Non-overlapping ranges all served; repeated identical requests use cached data              |
| Eviction Integration    | MaxSegmentCount exceeded → eviction triggered                                               |
| Both Storage Strategies | `SnapshotAppendBufferStorage` and `LinkedListStrideIndexStorage` produce identical behavior |
| Diagnostics Lifecycle   | `Received == Processed + Failed` holds across all three interaction types                   |
| Disposal                | `GetDataAsync` after dispose throws `ObjectDisposedException`; double-dispose is a no-op    |

### `TtlExpirationTests.cs`

Validates the end-to-end TTL expiration path including interaction with eviction.

| Group                           | What is tested                                                                                                        |
|---------------------------------|-----------------------------------------------------------------------------------------------------------------------|
| TTL Disabled                    | No TTL work items scheduled; segment persists indefinitely                                                            |
| TTL Enabled — single segment    | Segment expires after TTL; `TtlSegmentExpired` fires once                                                             |
| TTL Enabled — multiple segments | All segments expire; counter matches stored count                                                                     |
| After Expiry                    | Subsequent request is a full miss (segment gone); re-fetch and re-store occurs                                        |
| TTL + Eviction idempotency      | Segment evicted before TTL fires → `MarkAsRemoved` returns `false`; no double-removal, no `BackgroundOperationFailed` |
| Disposal                        | Pending TTL delays cancelled on `DisposeAsync`; `TtlSegmentExpired` does not fire                                     |
| Diagnostics                     | `TtlWorkItemScheduled == BackgroundSegmentStored` when TTL is enabled                                                 |

## Key Infrastructure

- `EventCounterCacheDiagnostics` — counts all 16 diagnostic events; `Reset()` isolates phases within a test
- `SpyDataSource` — records fetch calls; `WasRangeCovered` / `TotalFetchCount` for assertions
- `SimpleTestDataSource` — zero-setup data source for tests that do not need spy behavior
- `TestHelpers.CreateCache` / `CreateCacheWithSimpleSource` — standard cache factory with `MaxSegmentCount` + LRU
- `WaitForIdleAsync` — awaits background convergence before asserting on cache state
