namespace Intervals.NET.Caching.Infrastructure.Diagnostics;

/// <summary>
/// Diagnostics callbacks for a work scheduler's execution lifecycle.
/// </summary>
internal interface IWorkSchedulerDiagnostics
{
    /// <summary>
    /// Called at the start of executing a work item, before the debounce delay.
    /// </summary>
    void WorkStarted();

    /// <summary>
    /// Called when a work item is cancelled (via <see cref="OperationCanceledException"/>
    /// or a post-debounce <see cref="CancellationToken.IsCancellationRequested"/> check).
    /// </summary>
    void WorkCancelled();

    /// <summary>
    /// Called when a work item fails with an unhandled exception.
    /// </summary>
    /// <param name="ex">The exception that caused the failure.</param>
    void WorkFailed(Exception ex);
}
