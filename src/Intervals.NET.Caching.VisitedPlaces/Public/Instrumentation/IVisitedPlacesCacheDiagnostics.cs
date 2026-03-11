namespace Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

/// <summary>
/// Diagnostics interface for tracking behavioral events in
/// <see cref="Cache.VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// Extends <see cref="ICacheDiagnostics"/> with VisitedPlaces-specific normalization and eviction events.
/// All methods are fire-and-forget; implementations must never throw.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is <see cref="NoOpDiagnostics"/>, which silently discards all events.
/// For testing and observability, provide a custom implementation or use
/// <c>EventCounterCacheDiagnostics</c> from the test infrastructure package.
/// </para>
/// <para><strong>Execution Context Summary</strong></para>
/// <para>
/// Each method fires synchronously on the thread that triggers the event.
/// See the individual method's <c>Context:</c> annotation for details.
/// </para>
/// <list type="table">
/// <listheader><term>Method</term><term>Thread Context</term></listheader>
/// <item><term><see cref="DataSourceFetchGap"/></term><term>User Thread</term></item>
/// <item><term><see cref="NormalizationRequestReceived"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="NormalizationRequestProcessed"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="BackgroundStatisticsUpdated"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="BackgroundSegmentStored"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="EvictionEvaluated"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="EvictionTriggered"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="EvictionExecuted"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="EvictionSegmentRemoved"/></term><term>Background Thread (Normalization Loop)</term></item>
/// <item><term><see cref="TtlSegmentExpired"/></term><term>Background Thread (TTL / Fire-and-forget)</term></item>
/// <item><term><see cref="TtlWorkItemScheduled"/></term><term>Background Thread (Normalization Loop)</term></item>
/// </list>
/// <para>
/// Inherited from <see cref="ICacheDiagnostics"/>: <c>UserRequestServed</c>,
/// <c>UserRequestFullCacheHit</c>, <c>UserRequestPartialCacheHit</c>,
/// <c>UserRequestFullCacheMiss</c> — all User Thread.
/// <c>BackgroundOperationFailed</c> — Background Thread (Normalization Loop).
/// </para>
/// </remarks>
public interface IVisitedPlacesCacheDiagnostics : ICacheDiagnostics
{
    // ============================================================================
    // DATA SOURCE ACCESS COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a data source fetch for a single gap range (partial-hit gap or full-miss).
    /// Called once per gap in the User Path.
    /// Location: UserRequestHandler.HandleRequestAsync
    /// Related: Invariant VPC.F.1
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> User Thread</para>
    /// </remarks>
    void DataSourceFetchGap();

    // ============================================================================
    // BACKGROUND PROCESSING COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a normalization request received and started processing by the Background Path.
    /// Location: CacheNormalizationExecutor.ExecuteAsync (entry)
    /// Related: Invariant VPC.B.2
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void NormalizationRequestReceived();

    /// <summary>
    /// Records a normalization request fully processed by the Background Path (all 4 steps completed).
    /// Location: CacheNormalizationExecutor.ExecuteAsync (exit)
    /// Related: Invariant VPC.B.3
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void NormalizationRequestProcessed();

    /// <summary>
    /// Records statistics updated for used segments (Background Path step 1).
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 1)
    /// Related: Invariant VPC.E.4b
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void BackgroundStatisticsUpdated();

    /// <summary>
    /// Records a new segment stored in the cache (Background Path step 2).
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 2)
    /// Related: Invariant VPC.B.3, VPC.C.1
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void BackgroundSegmentStored();

    // ============================================================================
    // EVICTION COUNTERS
    // ============================================================================

    /// <summary>
    /// Records an eviction evaluation pass (Background Path step 3).
    /// Called once per storage step, regardless of whether any evaluator fired.
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 3)
    /// Related: Invariant VPC.E.1a
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void EvictionEvaluated();

    /// <summary>
    /// Records that at least one eviction evaluator fired and eviction will be executed.
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 3, at least one evaluator fired)
    /// Related: Invariant VPC.E.1a, VPC.E.2a
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void EvictionTriggered();

    /// <summary>
    /// Records a completed eviction execution pass (Background Path step 4).
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 4)
    /// Related: Invariant VPC.E.2a
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void EvictionExecuted();

    /// <summary>
    /// Records a single segment removed from the cache during eviction.
    /// Called once per segment actually removed (segments already claimed by the TTL actor are skipped).
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 4 — per-segment removal loop)
    /// Related: Invariant VPC.E.6
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void EvictionSegmentRemoved();

    // ============================================================================
    // TTL COUNTERS
    // ============================================================================

    /// <summary>
    /// Records a segment that was successfully expired and removed by the TTL actor.
    /// Called once per segment removed due to TTL expiration (idempotent removal is a no-op
    /// and does NOT fire this event — only actual removals are counted).
    /// Location: TtlExpirationExecutor.ExecuteAsync
    /// Related: Invariant VPC.T.1
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (TTL / Fire-and-forget)</para>
    /// <para>
    /// TTL work items are executed on ThreadPool threads via <see cref="M:System.Threading.ThreadPool.QueueUserWorkItem"/>
    /// (fire-and-forget, without serialization). Multiple TTL work items may execute concurrently.
    /// </para>
    /// </remarks>
    void TtlSegmentExpired();

    /// <summary>
    /// Records a TTL expiration work item that was scheduled for a newly stored segment.
    /// Called once per segment stored when TTL is enabled.
    /// Location: CacheNormalizationExecutor.ExecuteAsync (step 2, after storage)
    /// Related: Invariant VPC.T.2
    /// </summary>
    /// <remarks>
    /// <para><strong>Context:</strong> Background Thread (Normalization Loop)</para>
    /// </remarks>
    void TtlWorkItemScheduled();
}
