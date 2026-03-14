namespace Intervals.NET.Caching.Infrastructure.Concurrency;

/// <summary>
/// Encapsulates the three-state disposal pattern used by public cache classes.
/// Provides idempotent, concurrent-safe <c>DisposeAsync</c> orchestration and a disposal guard.
/// </summary>
/// <remarks>
/// The owning class holds a single <see cref="DisposalState"/> instance and delegates all
/// disposal logic here. This eliminates copy-pasted boilerplate without requiring inheritance.
///
/// <para><b>Three disposal states</b></para>
/// <list type="bullet">
///   <item><description>0 — active</description></item>
///   <item><description>1 — disposing (winner thread is performing disposal)</description></item>
///   <item><description>2 — disposed</description></item>
/// </list>
///
/// <para><b>Invariants satisfied</b></para>
/// <list type="bullet">
///   <item><description>S.J.1 — post-disposal guard on public methods</description></item>
///   <item><description>S.J.2 — idempotent disposal (multiple calls return after the first completes)</description></item>
///   <item><description>S.J.3 — concurrent callers wait for the winner without CPU burn</description></item>
/// </list>
/// </remarks>
internal sealed class DisposalState
{
    // 0 = active, 1 = disposing, 2 = disposed
    private int _state;

    // Published by the winner thread via Volatile.Write so loser threads can await it.
    private TaskCompletionSource? _completionSource;

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> when this instance has entered any
    /// disposal state (disposing or disposed).
    /// </summary>
    /// <param name="typeName">
    /// The name to use in the <see cref="ObjectDisposedException"/> message.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown when <c>_state</c> is non-zero.</exception>
    internal void ThrowIfDisposed(string typeName)
    {
        if (Volatile.Read(ref _state) != 0)
        {
            throw new ObjectDisposedException(typeName);
        }
    }

    /// <summary>
    /// Performs three-state CAS-based disposal, ensuring exactly one caller executes
    /// <paramref name="disposeCore"/> while all concurrent callers await the same result.
    /// </summary>
    /// <param name="disposeCore">
    /// The actual disposal logic (class-specific). Only the winner thread executes this delegate.
    /// </param>
    /// <returns>A <see cref="ValueTask"/> that completes when disposal is fully finished.</returns>
    /// <remarks>
    /// Winner thread (CAS 0→1): creates the <see cref="TaskCompletionSource"/>, publishes it via
    /// <c>Volatile.Write</c>, calls <paramref name="disposeCore"/>, and signals the TCS.
    /// Transitions to state 2 in a <c>finally</c> block.
    ///
    /// Loser threads (previous state == 1): spin-wait until the TCS is published (CPU-only,
    /// nanoseconds), then <c>await tcs.Task</c> without CPU burn. If the winner threw, the
    /// same exception is re-observed here.
    ///
    /// Already-disposed callers (previous state == 2): return immediately (idempotent).
    /// </remarks>
    internal async ValueTask DisposeAsync(Func<ValueTask> disposeCore)
    {
        var previousState = Interlocked.CompareExchange(ref _state, 1, 0);

        if (previousState == 0)
        {
            // Winner thread: publish TCS first so loser threads have somewhere to wait.
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _completionSource, tcs);

            try
            {
                await disposeCore().ConfigureAwait(false);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
            finally
            {
                // Transition to state 2 regardless of success or failure.
                Volatile.Write(ref _state, 2);
            }
        }
        else if (previousState == 1)
        {
            // Loser thread: spin-wait for TCS publication (CPU-only, very brief).
            TaskCompletionSource? tcs;
            var spinWait = new SpinWait();

            while ((tcs = Volatile.Read(ref _completionSource)) == null)
            {
                spinWait.SpinOnce();
            }

            // Await without CPU burn; re-throws winner's exception if disposal failed.
            await tcs.Task.ConfigureAwait(false);
        }
        // previousState == 2: already disposed — return immediately (idempotent).
    }
}
