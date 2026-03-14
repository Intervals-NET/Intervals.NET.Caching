# Test Infrastructure — VisitedPlaces Cache

Shared helpers, fakes, and spies used across all three VPC test tiers (unit, integration, invariants). This project is not a test runner — it has no `[Fact]` or `[Theory]` methods. It is referenced by all other VPC test projects.

## Contents

### `EventCounterCacheDiagnostics`

Thread-safe implementation of `IVisitedPlacesCacheDiagnostics` that counts every fired event.

All 16 counters use `Interlocked.Increment` (write) and `Volatile.Read` (read) for safe access from concurrent test threads.

| Counter property                | Event tracked                                   |
|---------------------------------|-------------------------------------------------|
| `UserRequestServed`             | Every `GetDataAsync` call served                |
| `UserRequestFullCacheHit`       | Request fully satisfied from cache              |
| `UserRequestPartialCacheHit`    | Request partially satisfied; gap fetch required |
| `UserRequestFullCacheMiss`      | Request entirely absent from cache              |
| `DataSourceFetchGap`            | Each gap-range fetch issued to `IDataSource`    |
| `NormalizationRequestReceived`  | Event dequeued by Background Path               |
| `NormalizationRequestProcessed` | Event completed all four Background Path steps  |
| `BackgroundStatisticsUpdated`   | Step 1 completed (metadata update)              |
| `BackgroundSegmentStored`       | Step 2 completed (new segment stored)           |
| `EvictionEvaluated`             | Step 3 completed (eviction evaluation pass)     |
| `EvictionTriggered`             | At least one policy fired during evaluation     |
| `EvictionExecuted`              | Step 4 completed (eviction execution pass)      |
| `EvictionSegmentRemoved`        | Individual segment removed during eviction      |
| `BackgroundOperationFailed`     | Unhandled exception in background processing    |
| `TtlSegmentExpired`             | Segment removed via TTL (first caller only)     |
| `TtlWorkItemScheduled`          | TTL work item scheduled after segment storage   |

**Lifecycle invariant**: `NormalizationRequestReceived == NormalizationRequestProcessed + BackgroundOperationFailed`

`Reset()` sets all counters to zero via `Interlocked.Exchange`. Use it between logical phases when a single cache instance is reused across multiple scenarios in one test.

---

### `DataSources/SimpleTestDataSource`

Minimal `IDataSource<int, int>` that generates sequential integer data for any requested range (value at position `i` = range start + `i`). Optional 1 ms async delay to simulate real I/O.

Use this when the test does not need to observe or control data-source calls.

---

### `DataSources/SpyDataSource`

`IDataSource<int, int>` that records every fetch call and exposes inspection methods. Thread-safe via `ConcurrentBag` and `Interlocked`.

| Member                        | Purpose                                            |
|-------------------------------|----------------------------------------------------|
| `TotalFetchCount`             | Number of `FetchAsync` invocations                 |
| `GetAllRequestedRanges()`     | All ranges requested                               |
| `WasRangeCovered(start, end)` | Returns `true` if any fetch covered `[start, end]` |
| `Reset()`                     | Clears all recorded calls                          |

Use this when the test needs to assert that the data source was or was not called, or to inspect which ranges were fetched.

---

### `DataSources/DataGenerationHelpers`

Static helper that generates `ReadOnlyMemory<int>` for a given `Range<int>`, producing sequential integer values starting at the range's inclusive start boundary. Used internally by `SimpleTestDataSource` and `SpyDataSource`.

---

### `Helpers/TestHelpers`

Static factory and assertion helpers used across all three test tiers.

**Range / Domain factories**

```csharp
TestHelpers.CreateIntDomain()           // IntegerFixedStepDomain
TestHelpers.CreateRange(0, 9)           // Factories.Range.Closed<int>(0, 9)
```

**Options factories**

```csharp
TestHelpers.CreateDefaultOptions()
TestHelpers.CreateDefaultOptions(storageStrategy: LinkedListStrideIndexStorageOptions<int,int>.Default)
```

**Cache factories**

```csharp
// With any IDataSource — MaxSegmentCount(100) + LRU by default
TestHelpers.CreateCache(dataSource, domain, options, diagnostics, maxSegmentCount: 100)

// With SimpleTestDataSource — most common in invariant / integration tests
TestHelpers.CreateCacheWithSimpleSource(domain, diagnostics, options, maxSegmentCount: 100)

// With a Moq mock — returns (cache, Mock<IDataSource>) for setup/verify
TestHelpers.CreateCacheWithMock(domain, diagnostics, options, maxSegmentCount, fetchDelay)
```

**Assertion helpers**

| Method                                            | Asserts                                               |
|---------------------------------------------------|-------------------------------------------------------|
| `AssertUserDataCorrect(data, range)`              | Data length matches range span; values are sequential |
| `AssertUserRequestServed(diag, n)`                | `UserRequestServed == n`                              |
| `AssertFullCacheHit(diag, n)`                     | `UserRequestFullCacheHit == n`                        |
| `AssertPartialCacheHit(diag, n)`                  | `UserRequestPartialCacheHit == n`                     |
| `AssertFullCacheMiss(diag, n)`                    | `UserRequestFullCacheMiss == n`                       |
| `AssertNormalizationRequestsProcessed(diag, min)` | `NormalizationRequestProcessed >= min`                |
| `AssertSegmentStored(diag, min)`                  | `BackgroundSegmentStored >= min`                      |
| `AssertEvictionTriggered(diag, min)`              | `EvictionTriggered >= min`                            |
| `AssertSegmentsEvicted(diag, min)`                | `EvictionSegmentRemoved >= min`                       |
| `AssertBackgroundLifecycleIntegrity(diag)`        | `Received == Processed + Failed`                      |
| `AssertNoBackgroundFailures(diag)`                | `BackgroundOperationFailed == 0`                      |
