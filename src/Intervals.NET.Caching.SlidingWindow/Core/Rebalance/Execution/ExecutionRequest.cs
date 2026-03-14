using Intervals.NET.Caching.Infrastructure.Scheduling;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Intent;

namespace Intervals.NET.Caching.SlidingWindow.Core.Rebalance.Execution;

/// <summary>
/// Execution request message sent from IntentController to the supersession work scheduler. See docs/sliding-window/ for design details.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
internal sealed class ExecutionRequest<TRange, TData, TDomain> : ISchedulableWorkItem
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly CancellationTokenSource _cts;

    /// <summary>
    /// The rebalance intent that triggered this execution request.
    /// </summary>
    public Intent<TRange, TData, TDomain> Intent { get; }

    /// <summary>
    /// The desired cache range for this rebalance operation.
    /// </summary>
    public Range<TRange> DesiredRange { get; }

    /// <summary>
    /// The desired no-rebalance range for this rebalance operation, or null if not applicable.
    /// </summary>
    public Range<TRange>? DesiredNoRebalanceRange { get; }

    /// <summary>
    /// The cancellation token for this execution request. Cancelled when superseded or disposed.
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    /// Initializes a new execution request with the specified intent, ranges, and cancellation token source.
    /// </summary>
    /// <param name="intent">The rebalance intent that triggered this request.</param>
    /// <param name="desiredRange">The desired cache range.</param>
    /// <param name="desiredNoRebalanceRange">The desired no-rebalance range, or null.</param>
    /// <param name="cts">The cancellation token source owned by this request.</param>
    public ExecutionRequest(
        Intent<TRange, TData, TDomain> intent,
        Range<TRange> desiredRange,
        Range<TRange>? desiredNoRebalanceRange,
        CancellationTokenSource cts)
    {
        Intent = intent;
        DesiredRange = desiredRange;
        DesiredNoRebalanceRange = desiredNoRebalanceRange;
        _cts = cts;
    }

    /// <summary>
    /// Cancels this execution request. Safe to call multiple times.
    /// </summary>
    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed - cancellation is best-effort
        }
    }

    /// <summary>
    /// Disposes the CancellationTokenSource associated with this execution request. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - best-effort cleanup
        }
    }
}
