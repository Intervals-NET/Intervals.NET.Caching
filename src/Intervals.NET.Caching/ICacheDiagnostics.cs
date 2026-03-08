namespace Intervals.NET.Caching;

/// <summary>
/// Shared base diagnostics interface for all range cache implementations.
/// Defines the common observable events that apply to every cache package
/// (<c>Intervals.NET.Caching.SlidingWindow</c>, <c>Intervals.NET.Caching.VisitedPlaces</c>, etc.).
/// All methods are fire-and-forget; implementations must never throw.
/// </summary>
/// <remarks>
/// <para>
/// Each package extends this interface with its own package-specific events:
/// <list type="bullet">
/// <item><description><c>ISlidingWindowCacheDiagnostics</c> — SlidingWindow-specific rebalance lifecycle events</description></item>
/// <item><description><c>IVisitedPlacesCacheDiagnostics</c> — VisitedPlaces-specific normalization and eviction events</description></item>
/// </list>
/// </para>
/// <para>
/// The default no-op implementation is <see cref="NoOpCacheDiagnostics"/>.
/// </para>
/// </remarks>
public interface ICacheDiagnostics
{
    // ============================================================================
    // USER PATH COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a completed user request served by the User Path.
    /// Called at the end of <c>UserRequestHandler.HandleRequestAsync</c> for all successful requests.
    /// </summary>
    void UserRequestServed();

    /// <summary>
    /// Records a full cache hit where all requested data is available in the cache
    /// without fetching from <c>IDataSource</c>.
    /// </summary>
    void UserRequestFullCacheHit();

    /// <summary>
    /// Records a partial cache hit where the requested range intersects the cache
    /// but is not fully covered; missing segments are fetched from <c>IDataSource</c>.
    /// </summary>
    void UserRequestPartialCacheHit();

    /// <summary>
    /// Records a full cache miss requiring a complete fetch from <c>IDataSource</c>.
    /// Occurs on cold start or when the requested range has no intersection with cached data.
    /// </summary>
    void UserRequestFullCacheMiss();

    // ============================================================================
    // ERROR REPORTING
    // ============================================================================

    /// <summary>
    /// Records an unhandled exception that occurred during a background operation
    /// (e.g., rebalance execution or normalization request processing).
    /// The background loop swallows the exception after reporting it here to prevent application crashes.
    /// </summary>
    /// <param name="ex">The exception that was thrown.</param>
    /// <remarks>
    /// <para><strong>CRITICAL: Applications MUST handle this event.</strong></para>
    /// <para>
    /// Background operations execute in fire-and-forget tasks. When an exception occurs,
    /// the task catches it, records this event, and silently swallows the exception to prevent
    /// application crashes from unhandled task exceptions.
    /// </para>
    /// <para><strong>Consequences of ignoring this event:</strong></para>
    /// <list type="bullet">
    /// <item><description>Silent failures in background operations</description></item>
    /// <item><description>Cache may stop rebalancing/normalizing without any visible indication</description></item>
    /// <item><description>Degraded performance with no diagnostics</description></item>
    /// <item><description>Data source errors may go unnoticed</description></item>
    /// </list>
    /// <para><strong>Recommended implementation:</strong></para>
    /// <para>
    /// At minimum, log all <c>BackgroundOperationFailed</c> events with full exception details.
    /// Consider also implementing:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Structured logging with context (requested range, cache state)</description></item>
    /// <item><description>Alerting for repeated failures (circuit breaker pattern)</description></item>
    /// <item><description>Metrics tracking failure rate and exception types</description></item>
    /// <item><description>Graceful degradation strategies (e.g., disable background work after N failures)</description></item>
    /// </list>
    /// </remarks>
    void BackgroundOperationFailed(Exception ex);
}
