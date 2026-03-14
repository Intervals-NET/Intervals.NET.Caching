using Intervals.NET.Caching.Infrastructure.Concurrency;
using Intervals.NET.Caching.Infrastructure.Diagnostics;

namespace Intervals.NET.Caching.Infrastructure.Scheduling.Base;

/// <summary>
/// Abstract base class providing the shared execution pipeline for all work scheduler implementations.
/// Handles debounce, cancellation check, executor call, diagnostics, and cleanup.
/// See docs/shared/components/infrastructure.md for design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal abstract class WorkSchedulerBase<TWorkItem> : IWorkScheduler<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
    /// <summary>Delegate that executes the actual work for a given work item.</summary>
    private protected readonly Func<TWorkItem, CancellationToken, Task> Executor;

    /// <summary>Returns the current debounce delay; snapshotted at the start of each execution ("next cycle" semantics).</summary>
    private protected readonly Func<TimeSpan> DebounceProvider;

    /// <summary>Diagnostics for scheduler-level lifecycle events.</summary>
    private protected readonly IWorkSchedulerDiagnostics Diagnostics;

    /// <summary>Activity counter for tracking active operations.</summary>
    private protected readonly AsyncActivityCounter ActivityCounter;

    /// <summary>Time provider used for debounce delays. Enables deterministic testing.</summary>
    private protected readonly TimeProvider TimeProvider;

    // Disposal state: 0 = not disposed, 1 = disposed (lock-free via Interlocked)
    private int _disposeState;

    /// <summary>
    /// Initializes the shared fields.
    /// </summary>
    private protected WorkSchedulerBase(
        Func<TWorkItem, CancellationToken, Task> executor,
        Func<TimeSpan> debounceProvider,
        IWorkSchedulerDiagnostics diagnostics,
        AsyncActivityCounter activityCounter,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(debounceProvider);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(activityCounter);

        Executor = executor;
        DebounceProvider = debounceProvider;
        Diagnostics = diagnostics;
        ActivityCounter = activityCounter;
        TimeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public abstract ValueTask PublishWorkItemAsync(TWorkItem workItem, CancellationToken loopCancellationToken);

    /// <summary>
    /// Executes a single work item: debounce, cancellation check, executor call, diagnostics, cleanup.
    /// </summary>
    private protected async Task ExecuteWorkItemCoreAsync(TWorkItem workItem)
    {
        try
        {
            // Step 0: Signal work-started and snapshot configuration.
            // These are inside the try so that any unexpected throw does not bypass the
            // finally block — keeping the activity counter balanced (Invariant S.H.2).
            Diagnostics.WorkStarted();

            // The work item owns its CancellationTokenSource and exposes the derived token.
            var cancellationToken = workItem.CancellationToken;

            // Snapshot debounce delay at execution time — picks up any runtime updates
            // published since this work item was enqueued ("next cycle" semantics).
            var debounceDelay = DebounceProvider();

            // Step 1: Apply debounce delay — allows superseded work items to be cancelled.
            // Skipped entirely when debounce is zero (e.g. VPC event processing) to avoid
            // unnecessary task allocation. ConfigureAwait(false) ensures continuation on thread pool.
            if (debounceDelay > TimeSpan.Zero)
            {
                await Task.Delay(debounceDelay, TimeProvider, cancellationToken)
                    .ConfigureAwait(false);

                // Step 2: Check cancellation after debounce.
                // NOTE: Task.Delay can complete normally just as cancellation is signalled (a race),
                // so we may reach here with cancellation requested but no exception thrown.
                // This explicit check provides a clean diagnostic path (WorkCancelled) for that case.
                if (cancellationToken.IsCancellationRequested)
                {
                    Diagnostics.WorkCancelled();
                    return;
                }
            }

            // Step 3: Execute the work item.
            await Executor(workItem, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Diagnostics.WorkCancelled();
        }
        catch (Exception ex)
        {
            Diagnostics.WorkFailed(ex);
        }
        finally
        {
            // Nested try/finally ensures DecrementActivity() fires even if Dispose() throws
            // (Invariant S.H.2). A throwing Dispose() would otherwise skip the decrement,
            // leaving the counter permanently incremented and hanging WaitForIdleAsync forever.
            try
            {
                // Dispose the work item (releases its CancellationTokenSource etc.)
                // This is the canonical disposal site — every work item is disposed here,
                // so no separate dispose step is needed during scheduler disposal.
                workItem.Dispose();
            }
            finally
            {
                // Decrement activity counter — ALWAYS happens after execution completes/cancels/fails.
                ActivityCounter.DecrementActivity();
            }
        }
    }

    /// <summary>
    /// Performs strategy-specific teardown during disposal.
    /// Called by <see cref="DisposeAsync"/> after the disposal guard has fired.
    /// </summary>
    private protected abstract ValueTask DisposeAsyncCore();

    /// <summary>
    /// Returns whether the scheduler has been disposed.
    /// Subclasses use this to guard <see cref="PublishWorkItemAsync"/>.
    /// </summary>
    private protected bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Idempotent guard using lock-free Interlocked.CompareExchange
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
        {
            return; // Already disposed
        }

        // Strategy-specific teardown.
        // Serial subclasses (SerialWorkSchedulerBase) also cancel the last work item here,
        // allowing early exit from debounce / I/O before awaiting the task chain or loop.
        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log via diagnostics but don't throw — best-effort disposal.
            // Follows "Background Path Exceptions" pattern from AGENTS.md.
            Diagnostics.WorkFailed(ex);
        }
    }
}
