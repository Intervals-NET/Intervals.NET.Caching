using Intervals.NET.Caching.Infrastructure.Diagnostics;

namespace Intervals.NET.Caching.SlidingWindow.Public.Instrumentation;

/// <summary>
/// Diagnostics interface for tracking cache behavioral events in
/// <see cref="Cache.SlidingWindowCache{TRange,TData,TDomain}"/>.
/// Extends <see cref="ICacheDiagnostics"/> with SlidingWindow-specific rebalance lifecycle events.
/// All methods are fire-and-forget; implementations must never throw.
/// </summary>
public interface ISlidingWindowCacheDiagnostics : ICacheDiagnostics
{
    // ============================================================================
    // CACHE MUTATION COUNTERS
    // ============================================================================

    /// <summary>
    /// Records when cache extension analysis determines that expansion is needed (intersection exists).
    /// </summary>
    void CacheExpanded();

    /// <summary>
    /// Records when cache extension analysis determines that full replacement is needed (no intersection).
    /// </summary>
    void CacheReplaced();

    // ============================================================================
    // DATA SOURCE ACCESS COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a single-range fetch from IDataSource for a complete range (cold start or non-intersecting jump).
    /// </summary>
    void DataSourceFetchSingleRange();

    /// <summary>
    /// Records a missing-segments fetch from IDataSource during cache extension.
    /// </summary>
    void DataSourceFetchMissingSegments();

    /// <summary>
    /// Called when a data segment is unavailable because the DataSource returned a null Range
    /// (e.g., physical boundaries such as database min/max IDs or time-series limits).
    /// </summary>
    void DataSegmentUnavailable();

    // ============================================================================
    // REBALANCE INTENT LIFECYCLE COUNTERS
    // ============================================================================

    /// <summary>
    /// Records publication of a rebalance intent by the User Path.
    /// </summary>
    void RebalanceIntentPublished();

    // ============================================================================
    // REBALANCE EXECUTION LIFECYCLE COUNTERS
    // ============================================================================

    /// <summary>
    /// Records the start of rebalance execution after the decision engine approves it.
    /// </summary>
    void RebalanceExecutionStarted();

    /// <summary>
    /// Records successful completion of rebalance execution.
    /// </summary>
    void RebalanceExecutionCompleted();

    /// <summary>
    /// Records cancellation of rebalance execution due to supersession by a newer request.
    /// </summary>
    void RebalanceExecutionCancelled();

    // ============================================================================
    // REBALANCE SKIP OPTIMIZATION COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a rebalance skipped because the requested range is within the current cache's no-rebalance range (Stage 1).
    /// </summary>
    void RebalanceSkippedCurrentNoRebalanceRange();

    /// <summary>
    /// Records a rebalance skipped because the requested range is within the pending rebalance's desired no-rebalance range (Stage 2).
    /// </summary>
    void RebalanceSkippedPendingNoRebalanceRange();

    /// <summary>
    /// Records a rebalance skipped because the current cache range already matches the desired range.
    /// </summary>
    void RebalanceSkippedSameRange();

    /// <summary>
    /// Records that a rebalance was scheduled for execution after passing all decision pipeline stages.
    /// </summary>
    void RebalanceScheduled();
}