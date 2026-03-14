using Intervals.NET.Caching.Infrastructure.Diagnostics;
using Intervals.NET.Caching.SlidingWindow.Public.Instrumentation;

namespace Intervals.NET.Caching.SlidingWindow.Infrastructure.Adapters;

/// <summary>
/// Adapts <see cref="ISlidingWindowCacheDiagnostics"/> to the <see cref="IWorkSchedulerDiagnostics"/> interface.
/// </summary>
internal sealed class SlidingWindowWorkSchedulerDiagnostics : IWorkSchedulerDiagnostics
{
    private readonly ISlidingWindowCacheDiagnostics _inner;

    public SlidingWindowWorkSchedulerDiagnostics(ISlidingWindowCacheDiagnostics inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    public void WorkStarted() => _inner.RebalanceExecutionStarted();

    /// <inheritdoc/>
    public void WorkCancelled() => _inner.RebalanceExecutionCancelled();

    /// <inheritdoc/>
    public void WorkFailed(Exception ex) => _inner.BackgroundOperationFailed(ex);
}
