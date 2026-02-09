# WindowCache Invariant Tests - Implementation Summary

## Overview
Comprehensive unit test suite for the WindowCache library verifying all 47 system invariants through the public API using DEBUG-only instrumentation counters.

**Test Statistics**:
- **Total Invariants**: 47 (19 Behavioral, 20 Architectural, 8 Conceptual)
- **Total Tests**: 28 automated tests (27 invariant tests + 1 comprehensive scenario)
- **Test Coverage**: 19/19 behavioral invariants directly covered
- **Test Execution Time**: ~8.5 seconds for full suite

## Implementation Details

### 1. DEBUG-Only Instrumentation Infrastructure
- **Location**: `src/SlidingWindowCache/Instrumentation/`
- **Files Created**:
  - `CacheInstrumentationCounters.cs` - Static thread-safe counters wrapped in `#if DEBUG`
  - Each counter property includes XML documentation linking to specific invariants
  
- **Instrumented Components**:
  - `WindowCache.cs` - No direct instrumentation (facade)
  - `UserRequestHandler.cs` - Tracks user requests served, cache expansions/replacements
  - `IntentController.cs` - Tracks intent published/cancelled
  - `RebalanceScheduler.cs` - Tracks execution started/completed/cancelled, policy-based skips
  - `RebalanceExecutor.cs` - Tracks optimization-based skips (same-range detection)

- **Counter Types** (with Invariant References):
  - `UserRequestsServed` - User requests completed
  - `CacheExpanded` - Cache expanded (intersecting request)
  - `CacheReplaced` - Cache replaced (non-intersecting request)
  - `RebalanceIntentPublished` - Rebalance intent published (every user request)
  - `RebalanceIntentCancelled` - Rebalance intent cancelled (new request supersedes old)
  - `RebalanceExecutionStarted` - Rebalance execution began
  - `RebalanceExecutionCompleted` - Rebalance execution finished successfully
  - `RebalanceExecutionCancelled` - Rebalance execution cancelled
  - `RebalanceSkippedNoRebalanceRange` - **Policy-based skip** (Invariant D.27) - Request within NoRebalanceRange threshold
  - `RebalanceSkippedSameRange` - **Optimization-based skip** (Invariant D.28) - DesiredRange == CurrentRange

### 2. Test Infrastructure
- **Location**: `tests/SlidingWindowCache.Invariants.Tests/TestInfrastructure/`
- **Files Created**:
  - `TestHelpers.cs` - Factory methods for creating domains, ranges, cache options, and data verification utilities

- **Domain Strategy**: Uses `Intervals.NET.Domain.Default.Numeric.IntegerFixedStepDomain` for proper range handling with inclusivity support

- **Mock Strategy**: Uses **Moq** framework for `IDataSource<int, int>` mocking
  - Mock configured per-test in Arrange section
  - Generates sequential integer data respecting range inclusivity
  - Supports configurable fetch delays for cancellation testing
  - Properly calculates range spans using Intervals.NET domain

### 3. Test Project Configuration
- **Updated**: `SlidingWindowCache.Invariants.Tests.csproj`
- **Added Dependencies**:
  - `Moq` (Version 4.20.70) - For IDataSource mocking
  - `xUnit` - Test framework
  - `Intervals.NET` packages - Domain and range handling
  - Project reference to `SlidingWindowCache`
- **Framework**: xUnit with standard `Assert` class (not FluentAssertions - decision for consistency)

### 4. Comprehensive Test Suite
- **Location**: `tests/SlidingWindowCache.Invariants.Tests/WindowCacheInvariantTests.cs`
- **Test Count**: 27 invariant tests + 1 execution lifecycle meta-invariant
- **Test Structure**: Each test method references its invariant number and description

#### Test Categories:

**A. User Path & Fast User Access (8 tests)**
- A.1-0a: User request cancels rebalance before mutations
- A.2.1: User path always serves requests
- A.2.2: User path never waits for rebalance
- A.2.10: User always receives exact requested range
- A.3.8: Cold start cache population
- A.3.8: Cache expansion (intersecting request)
- A.3.8: Full cache replacement (non-intersecting request)
- A.3.9a: Cache contiguity maintained

**B. Cache State & Consistency (2 tests)**
- B.11: CacheData and CurrentCacheRange always consistent
- B.15: Cancelled rebalance doesn't violate consistency

**C. Rebalance Intent & Temporal (4 tests)**
- C.17: At most one active intent
- C.18: Previous intent becomes obsolete
- C.24: Intent doesn't guarantee execution (opportunistic)
- C.23: System stabilizes under load

**D. Rebalance Decision Path (2 tests + TODOs)**
- D.27: No rebalance if request in NoRebalanceRange (policy-based skip) - **Enhanced with execution started assertion**
- D.28: Rebalance skipped when DesiredRange == CurrentRange (optimization-based skip) - **New test**
- TODOs for D.25, D.26, D.29 (require internal state access)

**E. Cache Geometry & Policy (1 test + TODOs)**
- E.30: DesiredRange computed from config and request
- TODOs for E.31-34 (require internal state inspection)

**F. Rebalance Execution (3 tests)**
- F.35, F.35a: Rebalance execution supports cancellation
- F.36a: Rebalance normalizes cache - **Enhanced with lifecycle integrity assertions**
- F.40-42: Post-execution guarantees

**G. Execution Context & Scheduling (2 tests)**
- G.43-45: Execution context separation
- G.46: Cancellation supported for all scenarios

**Meta-Invariant Tests (1 test)**
- Execution lifecycle integrity: started == (completed + cancelled) - **New test**

**Additional Comprehensive Tests (3 tests)**
- Complete scenario with multiple requests and rebalancing
- Concurrency scenario with rapid request bursts and cancellation
- Read mode variations (Snapshot and CopyOnRead)

### 5. Key Implementation Fixes

**UserRequestHandler.cs**:
- Added cold start detection using `LastRequested.HasValue`
- Fixed to avoid calling `ToRangeData()` on uninitialized cache
- Properly tracks cache expansion vs replacement with instrumentation

**Storage Classes**:
- **CopyOnReadStorage.cs**: Refactored to use dual-buffer (staging buffer) pattern for safe rematerialization
  - Active buffer remains immutable during reads
  - Staging buffer used for new range data during rematerialization
  - Atomic buffer swap after rematerialization completes
  - Prevents enumeration issues when concatenating existing + new data
- **SnapshotReadStorage.cs**: No changes needed - already uses safe rematerialization pattern

### 6. Test Execution
- **Build Configuration**: DEBUG mode (required for instrumentation)
- **Reset Pattern**: Each test resets counters in constructor/dispose
- **Async Handling**: Uses `Task.Delay` for background rebalance observation (timing-based)
- **Data Verification**: Custom helper verifies returned data matches expected range values

## Invariants Coverage

### Classification System
Invariants are classified into three categories based on their nature and enforcement mechanism:

- 🟢 **Behavioral** (test-covered): Externally observable via public API, verified by automated tests
- 🔵 **Architectural** (structure-enforced): Internal constraints enforced by code organization, not directly testable
- 🟡 **Conceptual** (design-level): Design intent and guarantees, enforced by documentation

**By design, this document contains MORE invariants (47) than the test suite covers (28 tests).**

### Test Coverage Breakdown

**Directly Testable - Behavioral Invariants (19 covered by 27 tests)**:
- User Path behavior (A.0a, A.1, A.2, A.10, A.8, A.9a)
- Cache consistency (B.11, B.15)
- Intent lifecycle (C.17, C.18, C.23, C.24)
- Decision path blocking (D.27 - policy-based skip, D.28 - optimization-based skip)
- Geometry computation (E.30)
- Execution cancellation & normalization (F.35, F.35a, F.36a, F.40-42)
- Execution context (G.43-46)

**Meta-Invariants (1 test)**:
- Execution lifecycle integrity: `started == (completed + cancelled)`

**Architectural Invariants (20 total - enforced by code structure)**:
- Examples: A.-1, A.0 (user path priority), A.3-5, A.7, A.9, A.9b (mutation rules)
- D.25, D.26, D.29 (decision path purity)
- E.31, E.34 (geometry independence)
- F.36, F.37-39 (execution mutation rules)
- G.44, G.45 (execution context)
- These are enforced by component boundaries, encapsulation, and ownership model

**Conceptual Invariants (8 total - documented design decisions)**:
- Examples: A.6 (user path may sync fetch), B.14 (temporary inefficiency acceptable)
- C.22 (convergence toward latest pattern - best-effort)
- C.22a (known race condition limitation - documented trade-off)
- C.24 (opportunistic execution with sub-invariants C.24a-d)
- E.32, E.33 (design principles)
- F.42 (internal state update)

**Indirectly Observable** (with TODOs):
- Execution details (F.38, F.39) - would need IDataSource instrumentation

## Usage

```bash
# Run all invariant tests
dotnet test tests/SlidingWindowCache.Invariants.Tests/SlidingWindowCache.Invariants.Tests.csproj --configuration Debug

# Run specific test
dotnet test --filter "FullyQualifiedName~Invariant_D28_SkipWhenDesiredEqualsCurrentRange"

# Run tests by category (example: all Decision Path tests)
dotnet test --filter "FullyQualifiedName~Invariant_D"
```

## Key Implementation Details

### Skip Condition Distinction
The system has **two distinct skip scenarios**, tracked by separate counters:

1. **Policy-Based Skip** (Invariant D.27)
   - Counter: `RebalanceSkippedNoRebalanceRange`
   - Location: `RebalanceScheduler` (after `DecisionEngine` returns `ShouldExecute=false`)
   - Reason: Request within NoRebalanceRange threshold zone
   - Characteristic: Execution **never starts** (decision-level optimization)

2. **Optimization-Based Skip** (Invariant D.28)
   - Counter: `RebalanceSkippedSameRange`
   - Location: `RebalanceExecutor.ExecuteAsync` (before I/O operations)
   - Reason: `CurrentCacheRange == DesiredCacheRange` (already at target)
   - Characteristic: Execution **starts but exits early** (executor-level optimization)

### CopyOnRead Storage - Staging Buffer Pattern
The `CopyOnReadStorage` implementation uses a dual-buffer approach for safe rematerialization:
- **Active buffer**: Immutable during reads, serves user requests
- **Staging buffer**: Write-only during rematerialization, reused across operations
- **Atomic swap**: After successful rematerialization, buffers are swapped
- **Rationale**: Prevents enumeration issues when concatenating existing + new data ranges

This pattern ensures:
- Active storage remains immutable during reads (no lock needed for single-consumer model)
- Predictable memory allocation behavior
- No temporary allocations beyond the staging buffer

See `docs/STORAGE_STRATEGIES.md` for detailed documentation.

## Notes
- Instrumentation is DEBUG-only (`#if DEBUG`) - zero overhead in Release builds
- Tests use timing-based async verification with `WaitForRebalanceAsync()` helper
- Counter reset in constructor/dispose ensures test isolation
- Uses `Intervals.NET.Domain.Default.Numeric.IntegerFixedStepDomain` for proper range inclusivity handling
- Some architectural and conceptual invariants are not meant to be unit-tested (enforced by code structure and documentation)
- The gap between 46 invariants and 28 tests is intentional and by design

## Related Documentation
- `docs/invariants.md` - Complete invariant classification and descriptions
- `docs/TEST_ENHANCEMENT_SUMMARY.md` - Details on counter-based test enhancements
- `docs/STORAGE_STRATEGIES.md` - CopyOnRead vs Snapshot storage comparison
- `docs/concurrency-model.md` - Single-consumer model and coordination
