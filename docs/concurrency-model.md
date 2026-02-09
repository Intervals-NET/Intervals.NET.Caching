# Concurrency Model

## Core Principle

This library is built around a **single logical consumer per cache instance**.

A cache instance:
- is **not thread-safe**
- is **not designed for concurrent access**
- assumes a single, coherent access pattern

This is an **ideological requirement**, not merely an architectural or technical limitation.

The architecture of the library reflects and enforces this principle.

---

## Single Cache Instance = Single Consumer

A sliding window cache models the behavior of **one observer moving through data**.

Each cache instance represents:
- one user
- one access trajectory
- one temporal sequence of requests

Attempting to share a single cache instance across multiple users or threads
violates this fundamental assumption.

---

## Why This Is a Requirement (Not a Limitation)

### 1. Sliding Window Requires a Unified Access Pattern

The cache continuously adapts its window based on observed access.

If multiple consumers request unrelated ranges:
- there is no single `DesiredCacheRange`
- the window oscillates or becomes unstable
- cache efficiency collapses

This is not a concurrency bug — it is a **model mismatch**.

---

### 2. Rebalance Logic Depends on a Single Timeline

Rebalance behavior relies on:
- ordered intents
- cancellation of obsolete work
- "latest access wins" semantics
- eventual stabilization

These guarantees require a **single temporal sequence of access events**.

Multiple consumers introduce conflicting timelines that cannot be meaningfully
merged without fundamentally changing the model.

---

### 3. Architecture Reflects the Ideology

The system architecture:
- enforces single-thread access
- isolates rebalance logic from user code
- assumes coherent access intent

These choices do not define the constraint —  
they **exist to preserve it**.

---

## How to Use This Library in Multi-User Environments

### ✅ Correct Approach

If your system has multiple users or concurrent consumers:

> **Create one cache instance per user (or per logical consumer).**

Each cache instance:
- operates independently
- maintains its own sliding window
- runs its own rebalance lifecycle

This preserves correctness, performance, and predictability.

---

### ❌ Incorrect Approach

Do **not**:
- share a cache instance across threads
- multiplex multiple users through a single cache
- attempt to synchronize access externally

External synchronization does not solve the underlying model conflict and will
result in inefficient or unstable behavior.

---

## What Is Supported

- Single-threaded access per cache instance
- Background asynchronous rebalance
- Cancellation and debouncing of rebalance execution
- High-frequency access from one logical consumer

---

## What Is Explicitly Not Supported

- Multiple concurrent consumers per cache instance
- Thread-safe shared access
- Cross-user sliding window arbitration

---

## Design Philosophy

This library prioritizes:
- conceptual clarity
- predictable behavior
- cache efficiency
- correctness of temporal and spatial logic

Instead of providing superficial thread safety,
it enforces a model that remains stable, explainable, and performant.
