using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Public.Instrumentation;

/// <summary>
/// Unit tests for <see cref="NoOpDiagnostics"/> that verify it never throws exceptions.
/// This is critical because diagnostic failures must never break cache functionality.
/// </summary>
public sealed class NoOpDiagnosticsTests
{
    [Fact]
    public void AllMethods_WhenCalled_DoNotThrowExceptions()
    {
        // ARRANGE
        var diagnostics = NoOpDiagnostics.Instance;
        var testException = new InvalidOperationException("Test exception");

        // ACT & ASSERT — call every method and verify none throw
        var exception = Record.Exception(() =>
        {
            // Shared base (NoOpCacheDiagnostics)
            diagnostics.BackgroundOperationFailed(testException);
            diagnostics.UserRequestServed();
            diagnostics.UserRequestFullCacheHit();
            diagnostics.UserRequestPartialCacheHit();
            diagnostics.UserRequestFullCacheMiss();

            // VPC-specific
            diagnostics.DataSourceFetchGap();
            diagnostics.NormalizationRequestReceived();
            diagnostics.NormalizationRequestProcessed();
            diagnostics.BackgroundStatisticsUpdated();
            diagnostics.BackgroundSegmentStored();
            diagnostics.EvictionEvaluated();
            diagnostics.EvictionTriggered();
            diagnostics.EvictionExecuted();
            diagnostics.EvictionSegmentRemoved();
            diagnostics.TtlSegmentExpired();
        });

        Assert.Null(exception);
    }
}
