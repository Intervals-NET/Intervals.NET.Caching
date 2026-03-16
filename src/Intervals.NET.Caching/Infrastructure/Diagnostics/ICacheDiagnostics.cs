namespace Intervals.NET.Caching.Infrastructure.Diagnostics;

/// <summary>
/// Shared base diagnostics interface for all range cache implementations.
/// All methods are fire-and-forget; implementations must never throw.
/// </summary>
/// <remarks>
/// Diagnostic hooks are invoked synchronously on internal library threads.
/// Keep implementations lightweight (logging, metrics) and never throw — exceptions
/// from a hook will crash internal threads.
/// </remarks>
public interface ICacheDiagnostics
{
    // ============================================================================
    // USER PATH COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a completed user request served by the User Path.
    /// </summary>
    void UserRequestServed();

    /// <summary>
    /// Records a full cache hit where all requested data is available in the cache.
    /// </summary>
    void UserRequestFullCacheHit();

    /// <summary>
    /// Records a partial cache hit where the requested range intersects the cache
    /// but is not fully covered.
    /// </summary>
    void UserRequestPartialCacheHit();

    /// <summary>
    /// Records a full cache miss requiring a complete fetch from <c>IDataSource</c>.
    /// </summary>
    void UserRequestFullCacheMiss();

    // ============================================================================
    // ERROR REPORTING
    // ============================================================================

    /// <summary>
    /// Records an unhandled exception that occurred during a background operation.
    /// The background loop swallows the exception after reporting it here to prevent crashes.
    /// Applications should at minimum log these events — without handling, background failures
    /// (e.g. data source errors) will be completely silent.
    /// </summary>
    /// <param name="ex">The exception that was thrown.</param>
    void BackgroundOperationFailed(Exception ex);
}
