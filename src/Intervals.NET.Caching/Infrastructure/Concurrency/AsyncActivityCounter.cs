namespace Intervals.NET.Caching.Infrastructure.Concurrency;

/// <summary>
/// Lock-free, thread-safe activity counter that provides awaitable idle state notification.
/// Tracks active operations using atomic counter and signals completion via TaskCompletionSource.
/// See docs/shared/components/infrastructure.md for design details and invariant references.
/// </summary>
internal sealed class AsyncActivityCounter
{
    // Activity counter - incremented when work starts, decremented when work finishes
    // Atomic operations via Interlocked.Increment/Decrement
    private int _activityCount;

    // Current TaskCompletionSource - signaled when counter reaches 0
    // Access via Volatile.Read/Write for proper memory barriers
    // Published via Volatile.Write on 0>1 transition, observed via Volatile.Read on N>0 transition and WaitForIdleAsync
    private TaskCompletionSource<bool> _idleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncActivityCounter"/> class.
    /// Counter starts at 0 (idle state) with a pre-completed TCS.
    /// </summary>
    public AsyncActivityCounter()
    {
        // Start in idle state with completed TCS
        _idleTcs.TrySetResult(true);
    }

    /// <summary>
    /// Increments the activity counter atomically.
    /// If this is a transition from idle (0) to busy (1), creates a new TaskCompletionSource.
    /// Must be called BEFORE making work visible (invariant S.H.1).
    /// </summary>
    public void IncrementActivity()
    {
        var newCount = Interlocked.Increment(ref _activityCount);

        // Check if this is a transition from idle (0) to busy (1)
        if (newCount == 1)
        {
            // Create new TCS for this busy period
            var newTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Publish new TCS with release fence (Volatile.Write)
            // Ensures TCS construction completes before reference becomes visible
            Volatile.Write(ref _idleTcs, newTcs);
        }
    }

    /// <summary>
    /// Decrements the activity counter atomically.
    /// If this is a transition from busy to idle (counter reaches 0), signals the TaskCompletionSource.
    /// Must be called in a finally block (invariant S.H.2).
    /// </summary>
    public void DecrementActivity()
    {
        var newCount = Interlocked.Decrement(ref _activityCount);

        // Sanity check - counter should never go negative
        if (newCount < 0)
        {
            // This indicates a bug: a DecrementActivity() call without a matching IncrementActivity().
            // Restore to 0 so the counter doesn't remain invalid, then throw unconditionally.
            // Intentionally supersedes any in-flight exception: counter underflow is an unrecoverable
            // logic fault and the root cause MUST be surfaced, even inside finally blocks or catch handlers.
            Interlocked.CompareExchange(ref _activityCount, 0, newCount);
            throw new InvalidOperationException(
                $"AsyncActivityCounter decremented below zero. This indicates unbalanced IncrementActivity/DecrementActivity calls.");
        }

        // Check if this is a transition to idle (counter reached 0)
        if (newCount == 0)
        {
            // Read current TCS with acquire fence (Volatile.Read)
            // Ensures we observe TCS published by Volatile.Write in IncrementActivity
            var tcs = Volatile.Read(ref _idleTcs);

            // Signal idle state - TrySetResult is thread-safe and idempotent
            // Multiple threads might see count=0 simultaneously, but only first TrySetResult succeeds
            tcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// Returns a Task that completes when the activity counter reaches zero (idle state).
    /// Completes immediately if already idle. Uses "was idle" semantics (invariant S.H.3).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the wait operation.</param>
    /// <returns>A Task that completes when counter reaches 0, or throws OperationCanceledException if cancelled.</returns>
    public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot current TCS with acquire fence (Volatile.Read)
        // Ensures we observe TCS published by Volatile.Write in IncrementActivity
        var tcs = Volatile.Read(ref _idleTcs);

        // Use Task.WaitAsync for simplified cancellation (available in .NET 6+)
        // If already completed, returns immediately
        // If pending, waits until signaled or cancellation token fires
        return tcs.Task.WaitAsync(cancellationToken);
    }
}
