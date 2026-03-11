namespace Intervals.NET.Caching.Infrastructure.Scheduling;

/// <summary>
/// A no-op implementation of <see cref="IWorkSchedulerDiagnostics"/> that silently discards all events.
/// </summary>
/// <remarks>
/// <para><strong>Usage:</strong></para>
/// <para>
/// Use when a work scheduler is needed but its lifecycle events (started, cancelled, failed) do not
/// map to any meaningful diagnostics surface. For example, the TTL scheduler in
/// <c>VisitedPlacesCache</c> uses this because TTL work items have their own diagnostics
/// (<c>TtlSegmentExpired</c>, <c>TtlWorkItemScheduled</c>) that are fired directly from the executor
/// and the <c>CacheNormalizationExecutor</c> rather than via the scheduler lifecycle.
/// </para>
/// <para>
/// Exceptions fired via <see cref="WorkFailed"/> are silently swallowed. Callers that need
/// exception surfacing should supply a concrete implementation.
/// </para>
/// </remarks>
internal sealed class NoOpWorkSchedulerDiagnostics : IWorkSchedulerDiagnostics
{
    /// <summary>The singleton no-op instance.</summary>
    public static readonly NoOpWorkSchedulerDiagnostics Instance = new();

    private NoOpWorkSchedulerDiagnostics() { }

    /// <inheritdoc/>
    public void WorkStarted() { }

    /// <inheritdoc/>
    public void WorkCancelled() { }

    /// <inheritdoc/>
    public void WorkFailed(Exception ex) { }
}
