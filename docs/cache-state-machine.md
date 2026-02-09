# Sliding Window Cache — Cache State Machine

This document defines the formal state machine for the Sliding Window Cache, clarifying state transitions, mutation ownership, and concurrency control.

---

## States

The cache exists in one of three states:

### 1. **Uninitialized**
- **Definition:** Cache has no data and no range defined
- **Characteristics:**
  - `CurrentCacheRange == null`
  - `CacheData == null`
  - `LastRequestedRange == null`
  - `NoRebalanceRange == null`

### 2. **Initialized**
- **Definition:** Cache contains valid data corresponding to a defined range
- **Characteristics:**
  - `CurrentCacheRange != null`
  - `CacheData != null`
  - `CacheData` is consistent with `CurrentCacheRange` (Invariant 11)
  - Cache is contiguous (no gaps, Invariant 9a)
  - System is ready to serve user requests

### 3. **Rebalancing**
- **Definition:** Background normalization is in progress
- **Characteristics:**
  - Cache remains in `Initialized` state from external perspective
  - User Path continues to serve requests normally
  - Rebalance Execution is mutating cache asynchronously
  - Rebalance can be cancelled at any time by User Path

---

## State Transitions

```
┌─────────────────┐
│  Uninitialized  │
└────────┬────────┘
         │
         │ U1: First User Request
         │ (User Path populates cache)
         ▼
┌─────────────────┐
│   Initialized   │◄──────────┐
└────────┬────────┘           │
         │                    │
         │ Any User Request   │
         │ triggers rebalance │
         ▼                    │
┌─────────────────┐           │
│  Rebalancing    │           │
└────────┬────────┘           │
         │                    │
         │ Rebalance          │
         │ completes          │
         └────────────────────┘
         
         (User Request during Rebalancing)
         ┌────────────────────┐
         │ Cancel Rebalance   │
         │ Return to          │
         │ Initialized        │
         └────────────────────┘
```

---

## Transition Details

### T1: Uninitialized → Initialized (Cold Start)
- **Trigger:** First user request (Scenario U1)
- **Actor:** User Path
- **Mutation:**
  - Fetch `RequestedRange` from IDataSource
  - Set `CacheData` = fetched data
  - Set `CurrentCacheRange` = `RequestedRange`
  - Set `LastRequestedRange` = `RequestedRange`
- **Atomicity:** Changes applied atomically (Invariant 12)
- **Postcondition:** Cache enters `Initialized` state, rebalance is triggered (fire-and-forget)

### T2: Initialized → Rebalancing (Normal Operation)
- **Trigger:** User request that requires rebalancing (Scenarios U2–U5, Decision D3)
- **Actor:** User Path (triggers), Rebalance Executor (executes)
- **Sequence:**
  1. User Path serves request (may mutate cache per A.3 rules)
  2. User Path updates `LastRequestedRange`
  3. User Path triggers rebalance asynchronously
  4. Cache logically enters `Rebalancing` state (background process active)
- **Concurrency:** User Path and Rebalance Execution never mutate concurrently (Invariant -1)

### T3: Rebalancing → Initialized (Rebalance Completion)
- **Trigger:** Rebalance execution completes successfully
- **Actor:** Rebalance Executor
- **Mutation:**
  - Fetch missing data for `DesiredCacheRange`
  - Merge with existing data (expansion)
  - Trim excess data (normalization)
  - Set `CurrentCacheRange` = `DesiredCacheRange`
  - Recompute `NoRebalanceRange`
- **Atomicity:** Changes applied atomically (Invariant 12)
- **Postcondition:** Cache returns to stable `Initialized` state

### T4: Rebalancing → Initialized (User Request Cancels Rebalance)
- **Trigger:** User request arrives during rebalance execution (Scenarios C1, C2)
- **Actor:** User Path (cancels), Cache State Manager (coordinates)
- **Sequence:**
  1. **User Path cancels ongoing/pending rebalance** (Invariant 0a)
  2. User Path waits for exclusive cache access
  3. User Path performs its cache mutation (expansion or replacement)
  4. User Path triggers new rebalance intent
  5. Cache returns to `Initialized` state with new rebalance pending
- **Critical Rule:** User Path and Rebalance Execution never mutate cache concurrently (Invariant -1)
- **Priority:** User Path always has priority (Invariant 0)

---

## Mutation Ownership Matrix

| State          | User Path Mutations                                                                                                 | Rebalance Execution Mutations                     |
|----------------|---------------------------------------------------------------------------------------------------------------------|---------------------------------------------------|
| Uninitialized  | ✅ Initial population (full cache replacement)                                                                      | ❌ Not active                                      |
| Initialized    | ✅ Expansion (if intersection)<br>✅ Full replacement (if no intersection)<br>❌ Never removes during expansion      | ❌ Not active                                      |
| Rebalancing    | ✅ Expansion (if intersection)<br>✅ Full replacement (if no intersection)<br>⚠️ MUST cancel rebalance first         | ✅ Expand to DesiredCacheRange<br>✅ Trim excess<br>✅ Recompute NoRebalanceRange<br>⚠️ MUST yield on cancellation |

### Mutation Rules Summary

**User Path may mutate cache for (Invariant 8):**
1. Initial cache population (cold start)
2. Cache expansion when `RequestedRange ∩ CurrentCacheRange ≠ ∅`
3. Full cache replacement when `RequestedRange ∩ CurrentCacheRange = ∅`

**Rebalance Execution may mutate cache for (Invariant 35a):**
1. Expanding cache to `DesiredCacheRange`
2. Trimming excess data outside `DesiredCacheRange`
3. Recomputing `NoRebalanceRange`

**Mutual Exclusion (Invariant -1):**
- User Path and Rebalance Execution **NEVER mutate cache concurrently**
- User Path **ALWAYS cancels** rebalance before mutating (Invariant 0a)
- Rebalance Execution **MUST yield** immediately on cancellation (Invariant 34a)

---

## Concurrency Semantics

### Cancellation Protocol

When a User Request arrives during `Rebalancing` state:

1. **Pre-mutation cancellation:** User Path invokes cancellation on active rebalance
2. **Synchronization:** User Path acquires exclusive cache access
3. **Rebalance yields:** Rebalance Execution:
   - Stops fetching data as soon as possible
   - Discards partial results if mutation not yet applied
   - Releases cache access
4. **User Path proceeds:** Performs its cache mutation safely
5. **New intent issued:** User Path triggers new rebalance with updated `LastRequestedRange`

### Cancellation Guarantees (Invariants 34, 34a, 34b)

- Rebalance Execution **MUST support cancellation** at all stages
- Rebalance Execution **MUST yield** to User Path immediately
- Cancelled execution **MUST NOT leave cache inconsistent**

### State Safety

- **Atomicity:** All cache mutations are atomic (Invariant 12)
- **Consistency:** `CacheData ↔ CurrentCacheRange` always consistent (Invariant 11)
- **Contiguity:** Cache data never contains gaps (Invariant 9a)
- **Idempotence:** Multiple cancellations are safe

---

## State Invariants by State

### In Uninitialized State:
- ✅ All range and data fields are null
- ✅ User Path may mutate via initial population
- ✅ Rebalance Execution is not active

### In Initialized State:
- ✅ `CacheData ↔ CurrentCacheRange` consistent (Invariant 11)
- ✅ Cache is contiguous (Invariant 9a)
- ✅ User Path may mutate per expansion/replacement rules (Invariant 8)
- ✅ Rebalance Execution is not active

### In Rebalancing State:
- ✅ `CacheData ↔ CurrentCacheRange` remain consistent (Invariant 11)
- ✅ Cache is contiguous (Invariant 9a)
- ✅ User Path may cancel and mutate (Invariants 0, 0a)
- ✅ Rebalance Execution is active but cancellable (Invariant 34)
- ✅ **No concurrent mutations** (Invariant -1)

---

## Examples

### Example 1: Cold Start → Initialized
```
State: Uninitialized
User requests [100, 200]
→ User Path fetches [100, 200]
→ Sets CacheData, CurrentCacheRange = [100, 200]
→ Triggers rebalance (fire-and-forget)
State: Initialized
```

### Example 2: Expansion During Rebalancing
```
State: Initialized
CurrentCacheRange = [100, 200]

User requests [150, 250]
→ Triggers rebalance R1 for DesiredCacheRange = [50, 300]
State: Rebalancing (R1 executing)

User requests [200, 300] (before R1 completes)
→ CANCELS R1 (Invariant 0a)
→ Expands cache: [100, 300] (intersection exists)
→ Triggers rebalance R2 for new DesiredCacheRange
State: Rebalancing (R2 executing)
```

### Example 3: Full Replacement During Rebalancing
```
State: Rebalancing
CurrentCacheRange = [100, 200]
Rebalance R1 executing for DesiredCacheRange = [50, 250]

User requests [500, 600] (no intersection)
→ CANCELS R1 (Invariant 0a)
→ Replaces cache: CacheData, CurrentCacheRange = [500, 600] (Invariant 9b)
→ Triggers rebalance R2 for new DesiredCacheRange = [450, 650]
State: Rebalancing (R2 executing)
```

---

## Architectural Summary

This state machine enforces three critical architectural constraints:

1. **Cache Contiguity:** Non-intersecting requests fully replace cache (Invariant 9b)
2. **User Priority:** User requests always cancel rebalance before mutation (Invariants 0, 0a)
3. **Mutation Ownership:** Both paths mutate cache, but never concurrently (Invariant -1)

The state machine guarantees:
- Fast, non-blocking user access (Invariants 1, 2)
- Eventual convergence to optimal cache shape (Invariant 23)
- Atomic, consistent cache state (Invariants 11, 12)
- Safe cancellation at any time (Invariants 34, 34a, 34b)
