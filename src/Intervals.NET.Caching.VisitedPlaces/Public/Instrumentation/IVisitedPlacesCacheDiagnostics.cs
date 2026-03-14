using Intervals.NET.Caching.Infrastructure.Diagnostics;

namespace Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

/// <summary>
/// Diagnostics interface for tracking behavioral events in
/// <see cref="Cache.VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// Extends <see cref="ICacheDiagnostics"/> with VisitedPlaces-specific normalization and eviction events.
/// All methods are fire-and-forget; implementations must never throw.
/// </summary>
public interface IVisitedPlacesCacheDiagnostics : ICacheDiagnostics
{
    // ============================================================================
    // DATA SOURCE ACCESS COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a data source fetch for a single gap range (partial-hit gap or full-miss).
    /// Called once per gap in the User Path.
    /// </summary>
    void DataSourceFetchGap();

    // ============================================================================
    // BACKGROUND PROCESSING COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a normalization request received and started processing by the Background Path.
    /// </summary>
    void NormalizationRequestReceived();

    /// <summary>
    /// Records a normalization request fully processed by the Background Path.
    /// </summary>
    void NormalizationRequestProcessed();

    /// <summary>
    /// Records statistics updated for used segments (Background Path step 1).
    /// </summary>
    void BackgroundStatisticsUpdated();

    /// <summary>
    /// Records a new segment stored in the cache (Background Path step 2).
    /// </summary>
    void BackgroundSegmentStored();

    // ============================================================================
    // EVICTION COUNTERS
    // ============================================================================

    /// <summary>
    /// Records an eviction evaluation pass (Background Path step 3).
    /// Called once per storage step, regardless of whether any policy fired.
    /// </summary>
    void EvictionEvaluated();

    /// <summary>
    /// Records that at least one eviction policy fired and eviction will be executed.
    /// </summary>
    void EvictionTriggered();

    /// <summary>
    /// Records a completed eviction execution pass (Background Path step 4).
    /// </summary>
    void EvictionExecuted();

    /// <summary>
    /// Records a single segment removed from the cache during eviction.
    /// Called once per segment actually removed.
    /// </summary>
    void EvictionSegmentRemoved();

    // ============================================================================
    // TTL COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a segment that was successfully expired and removed by the TTL actor.
    /// Only actual removals fire this event; idempotent no-ops do not.
    /// </summary>
    void TtlSegmentExpired();

    /// <summary>
    /// Records a TTL expiration work item scheduled for a newly stored segment.
    /// Called once per segment stored when TTL is enabled.
    /// </summary>
    void TtlWorkItemScheduled();
}
