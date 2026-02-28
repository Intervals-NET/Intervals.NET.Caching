# Scenarios

## Overview

This document describes the temporal behavior of SlidingWindowCache: what happens over time when user requests occur, decisions are evaluated, and background executions run.

## Motivation

Component maps describe "what exists"; scenarios describe "what happens". Scenarios are the fastest way to debug behavior because they connect public API calls to background convergence.

## Base Definitions

The following terms are used consistently across all scenarios:

- **RequestedRange** — A range requested by the user.
- **IsInitialized** — Whether the cache has been initialized (Rebalance Execution has written to the cache at least once).
- **CurrentCacheRange** — The range of data currently stored in the cache.
- **CacheData** — The data corresponding to `CurrentCacheRange`.
- **DesiredCacheRange** — The target cache range computed from `RequestedRange` and cache configuration (left/right expansion sizes, thresholds).
- **NoRebalanceRange** — A range inside which cache rebalance is not required (stability zone).
- **IDataSource** — A sequential, range-based data source.

Canonical definitions: `docs/glossary.md`.

## Design

Scenarios are grouped by path:

1. **User Path** (user thread)
2. **Decision Path** (background intent loop)
3. **Execution Path** (background execution)

---

## I. User Path Scenarios

### U1 — Cold Cache Request

**Preconditions**:
- `IsInitialized == false`
- `CurrentCacheRange == null`
- `CacheData == null`

**Action Sequence**:
1. User requests `RequestedRange`
2. Cache detects it is not initialized
3. Cache requests `RequestedRange` from `IDataSource` in the user thread (unavoidable — user request must be served immediately)
4. A rebalance intent is published (fire-and-forget) with the fetched data
5. Data is returned to the user immediately
6. Rebalance Execution (background) stores the data as `CacheData`, sets `CurrentCacheRange = RequestedRange`, sets `IsInitialized = true`

**Note**: The User Path does not expand the cache beyond `RequestedRange`. Cache expansion to `DesiredCacheRange` is performed exclusively by Rebalance Execution.

---

### U2 — Full Cache Hit (Within NoRebalanceRange)

**Preconditions**:
- `IsInitialized == true`
- `CurrentCacheRange.Contains(RequestedRange) == true`
- `NoRebalanceRange.Contains(RequestedRange) == true`

**Action Sequence**:
1. User requests `RequestedRange`
2. Cache detects a full cache hit
3. Data is read from `CacheData`
4. Rebalance intent is published; Decision Engine rejects execution at Stage 1 (NoRebalanceRange containment)
5. Data is returned to the user

---

### U3 — Full Cache Hit (Outside NoRebalanceRange)

**Preconditions**:
- `IsInitialized == true`
- `CurrentCacheRange.Contains(RequestedRange) == true`
- `NoRebalanceRange.Contains(RequestedRange) == false`

**Action Sequence**:
1. User requests `RequestedRange`
2. Cache detects all requested data is available
3. Subrange is read from `CacheData`
4. Rebalance intent is published; Decision Engine proceeds through validation
5. Data is returned to the user
6. Rebalance executes asynchronously to shift the window

---

### U4 — Partial Cache Hit

**Preconditions**:
- `IsInitialized == true`
- `CurrentCacheRange.Intersects(RequestedRange) == true`
- `CurrentCacheRange.Contains(RequestedRange) == false`

**Action Sequence**:
1. User requests `RequestedRange`
2. Cache computes intersection with `CurrentCacheRange`
3. Missing part is synchronously requested from `IDataSource`
4. Cache:
   - merges cached and newly fetched data **locally** (in-memory assembly, not stored to cache)
   - does **not** trim excess data
   - does **not** update `CurrentCacheRange` (User Path is read-only with respect to cache state)
5. Rebalance intent is published; rebalance executes asynchronously
6. `RequestedRange` data is returned to the user

**Note**: Cache expansion is permitted because `RequestedRange` intersects `CurrentCacheRange`, preserving cache contiguity. Excess data may temporarily remain in `CacheData` for reuse during Rebalance.

---

### U5 — Full Cache Miss (Jump)

**Preconditions**:
- `IsInitialized == true`
- `CurrentCacheRange.Intersects(RequestedRange) == false`

**Action Sequence**:
1. User requests `RequestedRange`
2. Cache determines that `RequestedRange` does NOT intersect `CurrentCacheRange`
3. **Cache contiguity enforcement**: Cached data cannot be preserved — merging would create gaps
4. `RequestedRange` is synchronously requested from `IDataSource`
5. Cache:
   - **fully replaces** `CacheData` with new data
   - **fully replaces** `CurrentCacheRange` with `RequestedRange`
6. Rebalance intent is published; rebalance executes asynchronously
7. Data is returned to the user

**Critical**: Partial cache expansion is FORBIDDEN in this case — it would create logical gaps and violate the Cache Contiguity Rule (Invariant A.9a). The cache MUST remain contiguous at all times.

---

## II. Decision Path Scenarios

**Core principle**: Rebalance necessity is determined by multi-stage analytical validation, not by intent existence. Publishing an intent does NOT guarantee execution. The Decision Engine is the sole authority for necessity determination.

The validation pipeline:
1. **Stage 1**: Current Cache `NoRebalanceRange` validation (fast-path rejection)
2. **Stage 2**: Pending Desired Cache `NoRebalanceRange` validation (anti-thrashing)
3. **Stage 3**: Compute `DesiredCacheRange` from `RequestedRange` + configuration
4. **Stage 4**: `DesiredCacheRange` vs `CurrentCacheRange` equality check (no-op prevention)

Execution occurs **only if ALL validation stages confirm necessity**.

---

### D1 — Rebalance Blocked by NoRebalanceRange (Stage 1)

**Condition**:
- `NoRebalanceRange.Contains(RequestedRange) == true`

**Sequence**:
1. Intent arrives; Stage 1 validation begins
2. `NoRebalanceRange` computed from `CurrentCacheRange` is checked
3. `RequestedRange` is fully contained within `NoRebalanceRange`
4. Validation rejects: current cache provides sufficient buffer
5. Fast return — rebalance is skipped; Execution Path is not started

**Rationale**: Current cache already provides adequate coverage around the requested range. No I/O or cache mutation needed.

---

### D1b — Rebalance Blocked by Pending Desired Cache (Stage 2, Anti-Thrashing)

**Condition**:
- Stage 1 passed: `NoRebalanceRange(CurrentCacheRange).Contains(RequestedRange) == false`
- Pending rebalance exists with `PendingDesiredCacheRange`
- `NoRebalanceRange(PendingDesiredCacheRange).Contains(RequestedRange) == true`

**Sequence**:
1. Intent arrives; Stage 1 passes
2. Stage 2: pending rebalance exists — compute `NoRebalanceRange` from `PendingDesiredCacheRange`
3. `RequestedRange` is fully contained within pending `NoRebalanceRange`
4. Validation rejects: pending execution will already satisfy this request
5. Fast return — existing pending rebalance continues undisturbed

**Purpose**: Anti-thrashing mechanism preventing oscillating cache geometry.

**Rationale**: A rebalance is already scheduled that will position the cache optimally for this request. Starting a new rebalance would cancel the pending one, potentially causing thrashing. Better to let the pending rebalance complete.

---

### D2 — Rebalance Blocked by No-Op Geometry (Stage 4)

**Condition**:
- Stage 1 passed: `NoRebalanceRange.Contains(RequestedRange) == false`
- `DesiredCacheRange == CurrentCacheRange`

**Sequence**:
1. Intent arrives; Stages 1–3 pass
2. Stage 3: `DesiredCacheRange` is computed from `RequestedRange` + config
3. Stage 4: `DesiredCacheRange == CurrentCacheRange` — cache already in optimal configuration
4. Validation rejects: no geometry change needed
5. Fast return — rebalance is skipped; Execution Path is not started

**Rationale**: Cache is already sized and positioned optimally. No I/O or cache mutation needed.

---

### D3 — Rebalance Required (All Validation Stages Passed)

**Condition**:
- Stage 1 passed: `NoRebalanceRange.Contains(RequestedRange) == false`
- Stage 2 passed (if applicable): Pending coverage does not satisfy request
- Stage 4 passed: `DesiredCacheRange != CurrentCacheRange`

**Sequence**:
1. Intent arrives; all validation stages pass
2. Stage 3: `DesiredCacheRange` computed
3. Stage 4 confirms: cache geometry change required
4. Validation confirms necessity
5. Prior pending execution is cancelled (if any)
6. New execution is scheduled

**Rationale**: ALL validation stages confirm that cache requires rebalancing. Rebalance Execution will normalize cache to `DesiredCacheRange` using delivered data as authoritative source.

---

## III. Execution Path Scenarios

### R1 — Build from Scratch

**Preconditions**:
- `CurrentCacheRange == null`

OR:
- `DesiredCacheRange.Intersects(CurrentCacheRange) == false`

**Sequence**:
1. `DesiredCacheRange` is requested from `IDataSource`
2. `CacheData` is fully replaced
3. `CurrentCacheRange` is set to `DesiredCacheRange`
4. `NoRebalanceRange` is computed

---

### R2 — Expand Cache (Partial Overlap)

**Preconditions**:
- `DesiredCacheRange.Intersects(CurrentCacheRange) == true`
- `DesiredCacheRange != CurrentCacheRange`

**Sequence**:
1. Missing subranges are computed (`DesiredCacheRange \ CurrentCacheRange`)
2. Missing data is requested from `IDataSource`
3. Data is merged with existing `CacheData`
4. `CacheData` is normalized to `DesiredCacheRange`
5. `NoRebalanceRange` is updated

---

### R3 — Shrink / Normalize Cache

**Preconditions**:
- `CurrentCacheRange.Contains(DesiredCacheRange) == true`

**Sequence**:
1. `CacheData` is trimmed to `DesiredCacheRange`
2. `CurrentCacheRange` is updated
3. `NoRebalanceRange` is recomputed

---

## IV. Concurrency and Cancellation Scenarios

### Concurrency Principles

1. User Path is never blocked by rebalance logic.
2. Multiple rebalance triggers may overlap in time.
3. Only the **latest validated rebalance intent** is executed.
4. Obsolete rebalance work must be cancelled or abandoned.
5. Rebalance execution must support cancellation at all stages.
6. Cache state may be temporarily non-optimal but must always be consistent.

---

### C1 — New Request While Rebalance Is Pending

**Situation**:
- User request U₁ triggers rebalance R₁ (fire-and-forget)
- R₁ has not started execution yet (queued or debouncing)
- User request U₂ arrives before R₁ executes

**Expected Behavior**:
1. New intent from U₂ supersedes R₁; Decision Engine validates necessity
2. User Path for U₂ executes normally and immediately
3. If validation confirms: R₁ is cancelled; new rebalance R₂ is scheduled
4. If validation rejects: R₁ continues (anti-thrashing, Stage 2 validation)
5. Only R₂ is allowed to execute (if scheduled)

**Outcome**: No rebalance work executes based on outdated intent. User Path always has priority.

---

### C2 — New Request While Rebalance Is Executing

**Situation**:
- User request U₁ triggers rebalance R₁
- R₁ has already started execution (I/O or merge in progress)
- User request U₂ arrives and triggers rebalance R₂

**Expected Behavior**:
1. New intent from U₂ supersedes R₁; Decision Engine validates necessity
2. User Path for U₂ executes normally and immediately
3. If validation confirms: R₁ receives cancellation signal
4. R₁ stops as early as possible or completes but discards its results
5. R₂ proceeds with fresh `DesiredCacheRange`

**Outcome**: Cache normalization reflects the most recent validated access pattern. User Path and Rebalance Execution never mutate cache concurrently.

---

### C3 — Multiple Rapid User Requests (Spike)

**Situation**:
- User produces a burst of requests: U₁, U₂, U₃, ..., Uₙ
- Each request publishes an intent; rebalance execution cannot keep up

**Expected Behavior**:
1. User Path serves all requests independently
2. Intents are superseded ("latest wins")
3. At most one rebalance execution is active at any time
4. Only the final validated intent is executed
5. All intermediate rebalance work is cancelled or skipped via decision validation

**Outcome**: System remains responsive and converges to a stable cache state once user activity slows.

---

### Cancellation and State Safety Guarantees

For concurrency correctness, the following guarantees hold:

- Rebalance execution is cancellable at all stages (before I/O, after I/O, before mutation)
- Cache mutations are atomic — no partial state is ever visible
- Partial rebalance results must not corrupt cache state (cancelled execution discards results)
- Final rebalance always produces a fully normalized, consistent cache

Temporary non-optimal cache geometry is acceptable. Permanent inconsistency is not.

## Invariants

Scenarios must be consistent with:

- User Path invariants: `docs/invariants.md` (Section A)
- Decision Path invariants: `docs/invariants.md` (Section D)
- Execution invariants: `docs/invariants.md` (Section F)
- Cache state invariants: `docs/invariants.md` (Section B)

## Usage

Use scenarios as a debugging checklist:

1. What did the user call?
2. What was delivered?
3. What intent was published?
4. Did the decision validate execution? If not, which stage rejected?
5. Did execution run, debounce, and mutate atomically?
6. Was there a concurrent cancellation? Did the cache remain consistent?

## Examples

Diagnostics examples in `docs/diagnostics.md` show how to observe these scenario transitions in production.

## Edge Cases

- A cache can be "temporarily non-optimal"; eventual convergence is expected.
- `WaitForIdleAsync` indicates the system was idle at some point, not that it remains idle.
- In Scenario D1b, the pending rebalance may already be in execution; it continues undisturbed if validation confirms it will satisfy the new request.

## Limitations

- Scenarios are behavioral descriptions, not an exhaustive proof; invariants are the normative source.
