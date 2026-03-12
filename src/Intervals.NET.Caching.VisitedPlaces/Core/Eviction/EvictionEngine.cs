using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Facade that encapsulates the full eviction subsystem: selector metadata management,
/// policy evaluation, and execution of the candidate-removal loop.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Execution Context:</strong> Background Path (single writer thread)</para>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>
///   Delegates selector metadata operations (<see cref="UpdateMetadata"/>,
///   <see cref="InitializeSegment"/>) to the <see cref="IEvictionSelector{TRange,TData}"/>.
/// </description></item>
/// <item><description>
///   Notifies the <see cref="EvictionPolicyEvaluator{TRange,TData}"/> of segment lifecycle
///   events via <see cref="InitializeSegment"/>, <see cref="OnSegmentRemoved"/>, and
///   <see cref="OnSegmentsRemoved"/>, keeping stateful policy aggregates consistent with
///   storage state.
/// </description></item>
/// <item><description>
///   Evaluates all policies and executes the constraint satisfaction loop via
///   <see cref="EvaluateAndExecute"/>. Returns the list of segments the processor must remove
///   from storage, firing eviction-specific diagnostics internally.
/// </description></item>
/// </list>
/// <para><strong>Storage ownership:</strong></para>
/// <para>
/// The engine holds no reference to <c>ISegmentStorage</c>. All storage mutations
/// (<c>Add</c>, <c>Remove</c>) remain exclusively in
/// <see cref="Background.CacheNormalizationExecutor{TRange,TData,TDomain}"/> (Invariant VPC.A.10).
/// </para>
/// <para><strong>Diagnostics split:</strong></para>
/// <para>
/// The engine fires eviction-specific diagnostics:
/// <see cref="IVisitedPlacesCacheDiagnostics.EvictionEvaluated"/>,
/// <see cref="IVisitedPlacesCacheDiagnostics.EvictionTriggered"/>,
/// <see cref="IVisitedPlacesCacheDiagnostics.EvictionExecuted"/>.
/// The processor retains ownership of storage-level diagnostics
/// (<c>BackgroundSegmentStored</c>, <c>BackgroundStatisticsUpdated</c>, etc.).
/// </para>
/// <para><strong>Internal components (hidden from processor):</strong></para>
/// <list type="bullet">
/// <item><description>
///   <see cref="EvictionPolicyEvaluator{TRange,TData}"/> — stateful policy lifecycle
///   and multi-policy pressure aggregation.
/// </description></item>
/// <item><description>
///   <see cref="EvictionExecutor{TRange,TData}"/> — constraint satisfaction loop.
/// </description></item>
/// </list>
/// </remarks>
internal sealed class EvictionEngine<TRange, TData>
    where TRange : IComparable<TRange>
{
    private readonly IEvictionSelector<TRange, TData> _selector;
    private readonly EvictionPolicyEvaluator<TRange, TData> _policyEvaluator;
    private readonly EvictionExecutor<TRange, TData> _executor;
    private readonly IVisitedPlacesCacheDiagnostics _diagnostics;

    /// <summary>
    /// Initializes a new <see cref="EvictionEngine{TRange,TData}"/>.
    /// </summary>
    /// <param name="policies">
    /// One or more eviction policies. Eviction is triggered when ANY produces an exceeded
    /// pressure (OR semantics, Invariant VPC.E.1a). All policies receive lifecycle notifications
    /// (<c>OnSegmentAdded</c>, <c>OnSegmentRemoved</c>) for O(1) evaluation.
    /// </param>
    /// <param name="selector">
    /// Eviction selector; determines candidate ordering and owns per-segment metadata.
    /// </param>
    /// <param name="diagnostics">
    /// Diagnostics sink. Must never throw. The engine fires eviction-specific events;
    /// the caller retains storage-level diagnostics.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="policies"/>, <paramref name="selector"/>, or
    /// <paramref name="diagnostics"/> is <see langword="null"/>.
    /// </exception>
    public EvictionEngine(
        IReadOnlyList<IEvictionPolicy<TRange, TData>> policies,
        IEvictionSelector<TRange, TData> selector,
        IVisitedPlacesCacheDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(policies);

        ArgumentNullException.ThrowIfNull(selector);

        ArgumentNullException.ThrowIfNull(diagnostics);

        _selector = selector;
        _policyEvaluator = new EvictionPolicyEvaluator<TRange, TData>(policies);
        _executor = new EvictionExecutor<TRange, TData>(selector);
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Updates selector metadata for segments that were accessed on the User Path.
    /// Called by the processor in Step 1 of the Background Path sequence.
    /// </summary>
    /// <param name="usedSegments">The segments that were read during the User Path request.</param>
    public void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments)
    {
        _selector.UpdateMetadata(usedSegments);
    }

    /// <summary>
    /// Initializes selector metadata and notifies stateful policies for a newly stored segment.
    /// Called by the processor in Step 2 immediately after each segment is added to storage.
    /// </summary>
    /// <param name="segment">The segment that was just added to storage.</param>
    public void InitializeSegment(CachedSegment<TRange, TData> segment)
    {
        _selector.InitializeMetadata(segment);
        _policyEvaluator.OnSegmentAdded(segment);
    }

    /// <summary>
    /// Evaluates all policies against the current segment collection and, if any constraint
    /// is exceeded, executes the candidate-removal loop.
    /// </summary>
    /// <param name="justStoredSegments">
    /// All segments stored during the current event cycle. These are immune from eviction
    /// (Invariant VPC.E.3) and cannot be returned as candidates.
    /// </param>
    /// <returns>
    /// The segments that the processor must remove from storage, in selection order.
    /// Empty when no policy constraint is exceeded or all candidates are immune
    /// (Invariant VPC.E.3a).
    /// </returns>
    /// <remarks>
    /// Fires <see cref="IVisitedPlacesCacheDiagnostics.EvictionEvaluated"/> unconditionally,
    /// <see cref="IVisitedPlacesCacheDiagnostics.EvictionTriggered"/> when at least one policy fires, and
    /// <see cref="IVisitedPlacesCacheDiagnostics.EvictionExecuted"/> after the removal loop completes.
    /// </remarks>
    public IReadOnlyList<CachedSegment<TRange, TData>> EvaluateAndExecute(
        IReadOnlyList<CachedSegment<TRange, TData>> justStoredSegments)
    {
        var pressure = _policyEvaluator.Evaluate();
        _diagnostics.EvictionEvaluated();

        if (!pressure.IsExceeded)
        {
            return [];
        }

        _diagnostics.EvictionTriggered();

        var toRemove = _executor.Execute(pressure, justStoredSegments);

        _diagnostics.EvictionExecuted();

        return toRemove;
    }

    /// <summary>
    /// Notifies stateful policies that a batch of segments has been removed from storage.
    /// Called by the processor in Step 4 after all <c>storage.Remove</c> calls complete.
    /// </summary>
    /// <param name="removedSegments">
    /// The segments that were just removed from storage. Must be the same list returned by
    /// <see cref="EvaluateAndExecute"/> in the same event cycle.
    /// </param>
    public void OnSegmentsRemoved(IReadOnlyList<CachedSegment<TRange, TData>> removedSegments)
    {
        foreach (var segment in removedSegments)
        {
            _policyEvaluator.OnSegmentRemoved(segment);
        }
    }

    /// <summary>
    /// Notifies stateful policies that a single segment has been removed from storage.
    /// Prefer this overload over <see cref="OnSegmentsRemoved"/> when only one segment is
    /// removed per call site to avoid allocating a temporary collection.
    /// </summary>
    /// <param name="segment">The segment that was just removed from storage.</param>
    /// <remarks>
    /// Called by <c>TtlExpirationExecutor</c> after a single TTL expiration, and by
    /// <c>CacheNormalizationExecutor</c> inside the per-segment eviction loop (Step 4).
    /// Using this overload eliminates the intermediate <c>List&lt;CachedSegment&gt;</c>
    /// allocation that the batch variant would require in those call sites.
    /// </remarks>
    public void OnSegmentRemoved(CachedSegment<TRange, TData> segment)
    {
        _policyEvaluator.OnSegmentRemoved(segment);
    }
}
