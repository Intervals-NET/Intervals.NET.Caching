# Invariant Tests — VisitedPlaces Cache

Automated tests that verify the behavioral invariants of `VisitedPlacesCache` via the public API. Each test method is named after its invariant ID from `docs/visited-places/invariants.md`.

Only **behavioral** invariants are tested here — those observable through the public API. Architectural and concurrency-model invariants are enforced by code structure and are not reflected in this suite.

## Run

```bash
dotnet test tests/Intervals.NET.Caching.VisitedPlaces.Invariants.Tests/Intervals.NET.Caching.VisitedPlaces.Invariants.Tests.csproj
```

## Invariants Covered

| Test method                                                                | Invariant | What is verified                                                                              |
|----------------------------------------------------------------------------|-----------|-----------------------------------------------------------------------------------------------|
| `Invariant_VPC_A_3_UserPathAlwaysServesRequests`                           | VPC.A.3   | 10 parallel requests all return correct data regardless of background state                   |
| `Invariant_VPC_A_4_UserPathNeverWaitsForBackground`                        | VPC.A.4   | `GetDataAsync` completes before a slow data source (200 ms) would affect timing               |
| `Invariant_VPC_A_9_UserAlwaysReceivesDataForRequestedRange`                | VPC.A.9   | Correct data length and values for FullMiss, FullHit, PartialHit (both storage strategies)    |
| `Invariant_VPC_A_9a_CacheInteractionClassifiedCorrectly`                   | VPC.A.9a  | `FullMiss → FullHit → PartialHit` sequence matches `CacheInteraction` values                  |
| `Invariant_VPC_B_3_BackgroundEventProcessedInFourStepSequence`             | VPC.B.3   | Diagnostics counters confirm all four Background Path steps fire for a full-miss event        |
| `Invariant_VPC_B_3b_EvictionNotEvaluatedForFullCacheHit`                   | VPC.B.3b  | Stats-only events do not trigger eviction evaluation                                          |
| `Invariant_VPC_C_1_NonContiguousSegmentsArePermitted`                      | VPC.C.1   | Two non-overlapping segments coexist; gap remains a full miss                                 |
| `Invariant_VPC_E_3_JustStoredSegmentIsImmuneFromEviction`                  | VPC.E.3   | At capacity=1, second stored segment survives and is returned as FullHit                      |
| `Invariant_VPC_E_3a_OnlySegmentIsImmuneEvenWhenOverLimit`                  | VPC.E.3a  | First store at capacity=1 does not trigger eviction (count not exceeded)                      |
| `Invariant_VPC_F_1_DataSourceCalledOnlyForGaps`                            | VPC.F.1   | No data source call on FullHit; spy records zero fetches                                      |
| `Invariant_VPC_S_H_BackgroundEventLifecycleConsistency`                    | S.H       | `Received == Processed + Failed` across FullMiss/FullHit/PartialHit (both storage strategies) |
| `Invariant_VPC_S_J_GetDataAsyncAfterDispose_ThrowsObjectDisposedException` | S.J       | `ObjectDisposedException` thrown after `DisposeAsync`                                         |
| `Invariant_VPC_S_J_DisposeAsyncIsIdempotent`                               | S.J       | Second `DisposeAsync` does not throw                                                          |
| `Invariant_VPC_BothStrategies_BehaviorallyEquivalent`                      | —         | Both storage strategies produce identical FullMiss/FullHit behavior and correct data          |
| `Invariant_VPC_T_1_TtlExpirationIsIdempotent`                              | VPC.T.1   | Eviction-before-TTL: `MarkAsRemoved` returns false; only one `TtlSegmentExpired`; no failures |
| `Invariant_VPC_T_2_TtlDoesNotBlockUserPath`                                | VPC.T.2   | 10 requests complete in under 2 s with 1 ms TTL active                                        |
| `Invariant_VPC_S_R_1_UnboundedRangeThrowsArgumentException`                | S.R.1     | Infinite range throws `ArgumentException` before any cache logic runs                         |

## Key Infrastructure

- `EventCounterCacheDiagnostics` — counts all 16 diagnostic events; `Reset()` isolates phases within a test
- `TestHelpers.CreateCacheWithSimpleSource` — standard cache factory used for most invariant tests
- `SpyDataSource` — used in `VPC.F.1` to assert no data-source call on a full hit
- `WaitForIdleAsync` / `GetDataAndWaitForIdleAsync` — drive the cache to a quiescent state before asserting
- `StorageStrategyTestData` — `[MemberData]` source supplying both storage strategies for parametrized tests

## See Also

- `docs/visited-places/invariants.md` — formal invariant definitions
- `docs/visited-places/scenarios.md` — scenario walkthroughs referenced by test descriptions
