using Intervals.NET.Caching.VisitedPlaces.Public.Instrumentation;

namespace Intervals.NET.Caching.VisitedPlaces.Core.Eviction;

/// <summary>
/// Facade that encapsulates the full eviction subsystem: selector metadata management,
/// policy evaluation, and execution of the candidate-removal loop.
/// See docs/visited-places/ for design details.
/// </summary>
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
    /// </summary>
    public void UpdateMetadata(IReadOnlyList<CachedSegment<TRange, TData>> usedSegments)
    {
        _selector.UpdateMetadata(usedSegments);
    }

    /// <summary>
    /// Initializes selector metadata and notifies stateful policies for a newly stored segment.
    /// </summary>
    public void InitializeSegment(CachedSegment<TRange, TData> segment)
    {
        _selector.InitializeMetadata(segment);
        _policyEvaluator.OnSegmentAdded(segment);
    }

    /// <summary>
    /// Evaluates all policies and, if any constraint is exceeded, executes the candidate-removal loop.
    /// </summary>
    public IEnumerable<CachedSegment<TRange, TData>> EvaluateAndExecute(
        IReadOnlyList<CachedSegment<TRange, TData>> justStoredSegments)
    {
        var pressure = _policyEvaluator.Evaluate();
        _diagnostics.EvictionEvaluated();

        if (!pressure.IsExceeded)
        {
            return [];
        }

        _diagnostics.EvictionTriggered();

        return _executor.Execute(pressure, justStoredSegments);
    }

    /// <summary>
    /// Notifies stateful policies that a single segment has been removed from storage.
    /// </summary>
    public void OnSegmentRemoved(CachedSegment<TRange, TData> segment)
    {
        _policyEvaluator.OnSegmentRemoved(segment);
    }
}
