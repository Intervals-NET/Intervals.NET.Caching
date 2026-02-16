using Intervals.NET.Domain.Abstractions;
using SlidingWindowCache.Core.Rebalance.Decision;
using SlidingWindowCache.Core.Rebalance.Execution;
using SlidingWindowCache.Core.State;
using SlidingWindowCache.Infrastructure.Instrumentation;

namespace SlidingWindowCache.Core.Rebalance.Intent;

/// <summary>
/// Manages the lifecycle of rebalance intents.
/// This is the Intent Controller component within the Rebalance Intent Manager actor.
/// </summary>
/// <typeparam name="TRange">The type representing the range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <typeparam name="TDomain">The type representing the domain of the ranges.</typeparam>
/// <remarks>
/// <para><strong>Architectural Model:</strong></para>
/// <para>
/// The Rebalance Intent Manager is a single logical ACTOR in the system architecture.
/// Internally, it is decomposed into two cooperating components:
/// </para>
/// <list type="number">
/// <item><description><strong>IntentController (this class)</strong> - Intent lifecycle management</description></item>
/// <item><description><strong>RebalanceScheduler</strong> - Timing, debounce, pipeline orchestration</description></item>
/// </list>
/// <para><strong>Intent Controller Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>Receives rebalance intents on every user access</description></item>
/// <item><description>Owns intent identity and versioning (CancellationTokenSource)</description></item>
/// <item><description>Cancels and invalidates obsolete intents</description></item>
/// <item><description>Exposes cancellation interface to User Path</description></item>
/// </list>
/// <para><strong>Explicit Non-Responsibilities:</strong></para>
/// <list type="bullet">
/// <item><description>❌ Does NOT perform scheduling or timing logic (Scheduler's responsibility)</description></item>
/// <item><description>❌ Does NOT decide whether rebalance is logically required (DecisionEngine's job)</description></item>
/// <item><description>❌ Does NOT orchestrate execution pipeline (Scheduler's responsibility)</description></item>
/// </list>
/// <para><strong>Lock-Free Implementation:</strong></para>
/// <list type="bullet">
/// <item><description>✅ Thread-safe using <see cref="System.Threading.Interlocked"/> for atomic operations</description></item>
/// <item><description>✅ No locks, no <c>lock</c> statements, no mutexes</description></item>
/// <item><description>✅ No race conditions - atomic field replacement ensures correctness</description></item>
/// <item><description>✅ Guaranteed progress - non-blocking operations</description></item>
/// <item><description>✅ Validated under concurrent load by ConcurrencyStabilityTests</description></item>
/// </list>
/// </remarks>
internal sealed class IntentController<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly RebalanceScheduler<TRange, TData, TDomain> _scheduler;
    private readonly RebalanceDecisionEngine<TRange, TDomain> _decisionEngine;
    private readonly CacheState<TRange, TData, TDomain> _state;
    private readonly ICacheDiagnostics _cacheDiagnostics;

    /// <summary>
    /// The current rebalance cancellation token source.
    /// Represents the identity and lifecycle of the latest rebalance intent.
    /// </summary>
    private CancellationTokenSource? _currentIntentCts;

    /// <summary>
    /// Snapshot of the pending rebalance's target state, used for Stage 2 stability validation.
    /// Updated atomically when a new rebalance is scheduled.
    /// </summary>
    private PendingRebalance<TRange>? _pendingRebalance;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntentController{TRange,TData,TDomain}"/> class.
    /// </summary>
    /// <param name="state">The cache state.</param>
    /// <param name="decisionEngine">The decision engine for rebalance logic.</param>
    /// <param name="executor">The executor for performing rebalance operations.</param>
    /// <param name="debounceDelay">The debounce delay before executing rebalance.</param>
    /// <param name="cacheDiagnostics">The diagnostics interface for recording cache metrics and events related to rebalance intents.</param>
    /// <remarks>
    /// This constructor composes the Intent Controller with the Execution Scheduler
    /// to form the complete Rebalance Intent Manager actor.
    /// </remarks>
    public IntentController(
        CacheState<TRange, TData, TDomain> state,
        RebalanceDecisionEngine<TRange, TDomain> decisionEngine,
        RebalanceExecutor<TRange, TData, TDomain> executor,
        TimeSpan debounceDelay,
        ICacheDiagnostics cacheDiagnostics
    )
    {
        _state = state;
        _decisionEngine = decisionEngine;
        _cacheDiagnostics = cacheDiagnostics;
        // Compose with scheduler component
        _scheduler = new RebalanceScheduler<TRange, TData, TDomain>(
            executor,
            debounceDelay,
            cacheDiagnostics
        );
    }

    /// <summary>
    /// Cancels any pending or ongoing rebalance execution.
    /// This method is called by the User Path to ensure exclusive cache access
    /// before performing cache mutations (satisfies Invariant A.1-0a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is synchronous and returns immediately after signaling cancellation.
    /// The background rebalance task will handle the cancellation asynchronously.
    /// </para>
    /// <para>
    /// User Path never waits for rebalance to fully complete - it just ensures
    /// the cancellation signal is sent before proceeding with its own mutations.
    /// </para>
    /// <para><strong>Lock-Free Implementation:</strong></para>
    /// <para>
    /// Uses <see cref="System.Threading.Interlocked"/> atomic exchange to clear the current intent
    /// without requiring locks. This ensures thread-safety and prevents race conditions
    /// while maintaining non-blocking semantics.
    /// </para>
    /// </remarks>
    public void CancelPendingRebalance()
    {
        var cancellationTokenSource = Interlocked.Exchange(ref _currentIntentCts, null);

        if (cancellationTokenSource == null)
        {
            return;
        }

        if (cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();

        // Clear pending rebalance snapshot since no rebalance is scheduled
        Volatile.Write(ref _pendingRebalance, null);
    }

    /// <summary>
    /// Publishes a rebalance intent triggered by a user request.
    /// This method is fire-and-forget and returns immediately.
    /// </summary>
    /// <param name="intent">The data that was actually delivered to the user for the requested range.</param>
    /// <remarks>
    /// <para>
    /// Every user access produces a rebalance intent. This method implements the
    /// decision-driven Intent Controller pattern by:
    /// <list type="bullet">
    /// <item><description>Evaluating rebalance necessity via DecisionEngine</description></item>
    /// <item><description>Conditionally canceling previous intent only if new rebalance should schedule</description></item>
    /// <item><description>Creating a new intent with unique identity (CancellationTokenSource)</description></item>
    /// <item><description>Delegating to scheduler for debounce and execution</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The intent contains both the requested range and the assembled data.
    /// This allows Rebalance Execution to use the assembled data as an authoritative source,
    /// avoiding duplicate fetches and ensuring consistency.
    /// </para>
    /// <para>
    /// This implements the decision-driven model: Intent → Decision → Scheduling → Execution.
    /// No implicit triggers, no blind cancellations, no decision leakage across components.
    /// </para>
    /// <para>
    /// Responsibility separation: Decision logic in DecisionEngine, intent lifecycle here,
    /// scheduling/execution delegated to RebalanceScheduler.
    /// </para>
    /// </remarks>
    public void PublishIntent(Intent<TRange, TData, TDomain> intent)
    {
        // Step 1: Evaluate rebalance necessity (Decision Engine is SOLE AUTHORITY)
        // Capture pending rebalance state for Stage 2 validation (atomic read)
        var pendingSnapshot = Volatile.Read(ref _pendingRebalance);
        
        var decision = _decisionEngine.Evaluate(
            requestedRange: intent.RequestedRange,
            currentCacheState: _state,
            pendingRebalance: pendingSnapshot
        );

        // Track skip reason for observability
        RecordReason(decision.Reason);

        // Step 2: If decision says skip, publish diagnostic and return early
        if (!decision.ShouldSchedule)
        {
            return;
        }

        // Step 3: Decision confirmed rebalance is necessary - create new intent identity
        var newCts = new CancellationTokenSource();
        var intentToken = newCts.Token;

        // Step 4: Cancel pending rebalance (mechanical safeguard for state transition)
        // This is NOT a blind cancellation - it only happens when DecisionEngine validated necessity
        var oldCts = Interlocked.Exchange(ref _currentIntentCts, newCts);
        if (oldCts is not null)
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        // Step 5: Update pending rebalance snapshot for next Stage 2 validation
        // todo make this object as a return result of the _scheduler.ScheduleRebalance(). Let's scheduler be a keeper of rebalance execution infrastructure like threading, debounle delay, catching and handling exceptions, cancellations
        var newPending = new PendingRebalance<TRange>(
            decision.DesiredRange!.Value,
            decision.DesiredNoRebalanceRange
        );
        Volatile.Write(ref _pendingRebalance, newPending);

        // Step 6: Delegate to scheduler with decision for debounce and execution
        _scheduler.ScheduleRebalance(intent, decision, intentToken);

        _cacheDiagnostics.RebalanceIntentPublished();
    }

    /// <summary>
    /// Records the skip reason for diagnostic and observability purposes.
    /// Maps decision reasons to diagnostic events.
    /// </summary>
    private void RecordReason(RebalanceReason reason)
    {
        switch (reason)
        {
            case RebalanceReason.WithinCurrentNoRebalanceRange:
                // todo add specific log for this reason
            case RebalanceReason.WithinPendingNoRebalanceRange:
                _cacheDiagnostics.RebalanceSkippedNoRebalanceRange();
                break;
            case RebalanceReason.DesiredEqualsCurrent:
                _cacheDiagnostics.RebalanceSkippedSameRange();
                break;
            case RebalanceReason.RebalanceRequired:
                // todo add specific log for this reason
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }
    }

    /// <summary>
    /// Waits for the latest scheduled rebalance background Task to complete.
    /// Provides deterministic synchronization for infrastructure scenarios.
    /// </summary>
    /// <param name="timeout">
    /// Maximum time to wait for idle state. Defaults to 30 seconds.
    /// </param>
    /// <returns>A Task that completes when the background rebalance has finished.</returns>
    /// <remarks>
    /// <para><strong>Idle Proxy Responsibility:</strong></para>
    /// <para>
    /// This method delegates to <see cref="RebalanceScheduler{TRange,TData,TDomain}"/> which owns
    /// the background Task lifecycle. IntentController acts as a proxy, exposing the idle
    /// synchronization mechanism without implementing Task tracking itself.
    /// </para>
    /// <para>
    /// This is an infrastructure API useful for testing, graceful shutdown, health checks,
    /// and other scenarios requiring synchronization with background rebalance operations.
    /// Intent lifecycle and cancellation logic remain unchanged.
    /// </para>
    /// </remarks>
    public Task WaitForIdleAsync(TimeSpan? timeout = null) => _scheduler.WaitForIdleAsync(timeout);
}