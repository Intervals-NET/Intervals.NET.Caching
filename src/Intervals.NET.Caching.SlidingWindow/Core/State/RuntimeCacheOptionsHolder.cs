namespace Intervals.NET.Caching.SlidingWindow.Core.State;

/// <summary>
/// Thread-safe holder for the current <see cref="RuntimeCacheOptions"/> snapshot. See docs/sliding-window/ for design details.
/// </summary>
internal sealed class RuntimeCacheOptionsHolder
{
    // The currently active configuration snapshot.
    // Written via Volatile.Write (release fence); read via Volatile.Read (acquire fence).
    private RuntimeCacheOptions _current;

    /// <summary>
    /// Initializes a new <see cref="RuntimeCacheOptionsHolder"/> with the provided initial snapshot.
    /// </summary>
    /// <param name="initial">The initial runtime options snapshot. Must not be <c>null</c>.</param>
    public RuntimeCacheOptionsHolder(RuntimeCacheOptions initial)
    {
        _current = initial;
    }

    /// <summary>
    /// Returns the currently active <see cref="RuntimeCacheOptions"/> snapshot.
    /// </summary>
    public RuntimeCacheOptions Current => Volatile.Read(ref _current);

    /// <summary>
    /// Atomically replaces the current snapshot with <paramref name="newOptions"/>.
    /// </summary>
    /// <param name="newOptions">The new options snapshot. Must not be <c>null</c>.</param>
    public void Update(RuntimeCacheOptions newOptions)
    {
        Volatile.Write(ref _current, newOptions);
    }
}
