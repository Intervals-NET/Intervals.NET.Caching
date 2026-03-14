using Intervals.NET.Caching.Infrastructure.Concurrency;
using Intervals.NET.Caching.Infrastructure.Diagnostics;

namespace Intervals.NET.Caching.Infrastructure.Scheduling.Supersession;

/// <summary>
/// Serial work scheduler that serializes work item execution using task continuation chaining
/// and implements supersession semantics: each new published item automatically cancels the previous one.
/// See docs/shared/components/infrastructure.md for design details.
/// </summary>
/// <typeparam name="TWorkItem">
/// The type of work item processed by this scheduler.
/// Must implement <see cref="ISchedulableWorkItem"/> so the scheduler can cancel and dispose items.
/// </typeparam>
internal sealed class UnboundedSupersessionWorkScheduler<TWorkItem>
    : SupersessionWorkSchedulerBase<TWorkItem>
    where TWorkItem : class, ISchedulableWorkItem
{
    // Task chaining state (volatile write for single-writer pattern)
    private Task _currentExecutionTask = Task.CompletedTask;

    /// <summary>
    /// Initializes a new instance of <see cref="UnboundedSupersessionWorkScheduler{TWorkItem}"/>.
    /// </summary>
    /// <param name="executor">Delegate that performs the actual work for a given work item.</param>
    /// <param name="debounceProvider">Returns the current debounce delay.</param>
    /// <param name="diagnostics">Diagnostics for work lifecycle events.</param>
    /// <param name="activityCounter">Activity counter for tracking active operations.</param>
    /// <param name="timeProvider">
    /// Time provider for debounce delays. When <see langword="null"/>,
    /// <see cref="TimeProvider.System"/> is used.
    /// </param>
    public UnboundedSupersessionWorkScheduler(
        Func<TWorkItem, CancellationToken, Task> executor,
        Func<TimeSpan> debounceProvider,
        IWorkSchedulerDiagnostics diagnostics,
        AsyncActivityCounter activityCounter,
        TimeProvider? timeProvider = null
    ) : base(executor, debounceProvider, diagnostics, activityCounter, timeProvider)
    {
    }

    /// <summary>
    /// Enqueues the work item by chaining it to the previous execution task.
    /// Returns immediately (fire-and-forget).
    /// </summary>
    /// <param name="workItem">The work item to schedule.</param>
    /// <param name="loopCancellationToken">
    /// Accepted for API consistency; not used by the task-based strategy (never blocks).
    /// </param>
    /// <returns><see cref="ValueTask.CompletedTask"/> — always completes synchronously.</returns>
    private protected override ValueTask EnqueueWorkItemAsync(TWorkItem workItem, CancellationToken loopCancellationToken)
    {
        // Chain execution to previous task (lock-free using volatile write — single-writer context)
        var previousTask = Volatile.Read(ref _currentExecutionTask);
        var newTask = ChainExecutionAsync(previousTask, workItem);
        Volatile.Write(ref _currentExecutionTask, newTask);

        // Return immediately — fire-and-forget execution model
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Chains a new work item to await the previous task's completion before executing.
    /// Ensures sequential execution (single-writer guarantee) and unconditional ThreadPool dispatch.
    /// </summary>
    /// <param name="previousTask">The previous execution task to await.</param>
    /// <param name="workItem">The work item to execute after the previous task completes.</param>
    private async Task ChainExecutionAsync(Task previousTask, TWorkItem workItem)
    {
        // Immediately yield to the ThreadPool so the entire method body runs on a background thread.
        await Task.Yield();

        try
        {
            await previousTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Previous task failed — log but continue with current execution.
            Diagnostics.WorkFailed(ex);
        }

        try
        {
            await ExecuteWorkItemCoreAsync(workItem).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Diagnostics.WorkFailed(ex);
        }
    }

    /// <inheritdoc/>
    private protected override async ValueTask DisposeSerialAsyncCore()
    {
        // Capture current task chain reference (volatile read — no lock needed)
        var currentTask = Volatile.Read(ref _currentExecutionTask);

        // Wait for task chain to complete gracefully
        await currentTask.ConfigureAwait(false);
    }
}
