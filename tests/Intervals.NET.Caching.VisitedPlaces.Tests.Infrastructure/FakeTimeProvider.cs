namespace Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;

/// <summary>
/// A controllable <see cref="TimeProvider"/> for deterministic TTL testing.
/// Time only advances when explicitly requested via <see cref="Advance"/> or <see cref="SetUtcNow"/>.
/// Thread-safe: <see cref="GetUtcNow"/>, <see cref="Advance"/>, and <see cref="SetUtcNow"/> may be
/// called from any thread concurrently.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private DateTimeOffset _utcNow;

    /// <summary>
    /// Initializes a new <see cref="FakeTimeProvider"/> starting at <paramref name="start"/>,
    /// or <see cref="DateTimeOffset.UtcNow"/> if no start is provided.
    /// </summary>
    public FakeTimeProvider(DateTimeOffset? start = null) =>
        _utcNow = start ?? DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() { lock (_lock) { return _utcNow; } }

    /// <summary>Advances the clock by <paramref name="delta"/>.</summary>
    public void Advance(TimeSpan delta) { lock (_lock) { _utcNow = _utcNow.Add(delta); } }

    /// <summary>Sets the current UTC time to <paramref name="value"/>.</summary>
    public void SetUtcNow(DateTimeOffset value) { lock (_lock) { _utcNow = value; } }
}
