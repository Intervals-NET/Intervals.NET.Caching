using Intervals.NET.Caching.Infrastructure.Diagnostics;

namespace Intervals.NET.Caching;

/// <summary>
/// No-op implementation of <see cref="ICacheDiagnostics"/> that silently discards all events.
/// Use this as a base class or standalone default when diagnostics are not required.
/// </summary>
/// <remarks>
/// <para>
/// Access the shared singleton via <see cref="Instance"/> to avoid unnecessary allocations.
/// </para>
/// <para>
/// Package-specific no-op implementations (e.g., <c>NoOpDiagnostics</c> in SlidingWindow and
/// VisitedPlaces) extend this class by adding no-op bodies for their own package-specific methods.
/// </para>
/// </remarks>
public class NoOpCacheDiagnostics : ICacheDiagnostics
{
    /// <summary>
    /// A shared singleton instance. Use this to avoid unnecessary allocations.
    /// </summary>
    public static readonly NoOpCacheDiagnostics Instance = new();

    /// <inheritdoc/>
    public virtual void UserRequestServed() { }

    /// <inheritdoc/>
    public virtual void UserRequestFullCacheHit() { }

    /// <inheritdoc/>
    public virtual void UserRequestPartialCacheHit() { }

    /// <inheritdoc/>
    public virtual void UserRequestFullCacheMiss() { }

    /// <inheritdoc/>
    public virtual void BackgroundOperationFailed(Exception ex)
    {
        // Intentional no-op: this implementation discards all diagnostics including failures.
        // For production systems, use a custom ICacheDiagnostics implementation that logs
        // to your observability pipeline.
    }
}
