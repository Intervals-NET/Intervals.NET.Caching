using Intervals.NET.Caching.Infrastructure.Diagnostics;
using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Infrastructure.Adapters;

/// <summary>
/// Bridges <see cref="IVisitedPlacesCacheDiagnostics"/> to <see cref="IWorkSchedulerDiagnostics"/>
/// for the VisitedPlacesCache background scheduler. See docs/visited-places/ for design details.
/// </summary>
internal sealed class VisitedPlacesWorkSchedulerDiagnostics : IWorkSchedulerDiagnostics
{
    private readonly IVisitedPlacesCacheDiagnostics _inner;

    /// <summary>
    /// Initializes a new instance of <see cref="VisitedPlacesWorkSchedulerDiagnostics"/>.
    /// </summary>
    public VisitedPlacesWorkSchedulerDiagnostics(IVisitedPlacesCacheDiagnostics inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    public void WorkStarted() => _inner.NormalizationRequestReceived();

    /// <inheritdoc/>
    public void WorkCancelled() { }

    /// <inheritdoc/>
    public void WorkFailed(Exception ex) => _inner.BackgroundOperationFailed(ex);
}
