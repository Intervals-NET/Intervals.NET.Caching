using Intervals.NET.Caching.VisitedPlaces.Core;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Eviction.Selectors;

/// <summary>
/// Unit tests for <see cref="LruEvictionSelector{TRange,TData}"/>.
/// Validates that <see cref="IEvictionSelector{TRange,TData}.TrySelectCandidate"/> returns the
/// least recently used segment (oldest <c>LastAccessedAt</c>) from the sample.
/// All datasets are small (≤ SampleSize = 32), so sampling is exhaustive and deterministic.
/// </summary>
public sealed class LruEvictionSelectorTests
{
    private static readonly IReadOnlySet<CachedSegment<int, int>> NoImmune =
        new HashSet<CachedSegment<int, int>>();

    private readonly LruEvictionSelector<int, int> _selector = new();

    #region TrySelectCandidate — Returns LRU Candidate

    [Fact]
    public void TrySelectCandidate_ReturnsTrueAndSelectsLeastRecentlyUsed()
    {
        // ARRANGE
        var baseTime = DateTime.UtcNow;
        var old = CreateSegmentWithLastAccess(0, 5, baseTime.AddHours(-2));
        var recent = CreateSegmentWithLastAccess(10, 15, baseTime);

        InitializeStorage(_selector, [old, recent]);

        // ACT
        var result = _selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — old (least recently used) is selected
        Assert.True(result);
        Assert.Same(old, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithReversedInput_StillSelectsLeastRecentlyUsed()
    {
        // ARRANGE — storage in reverse order (recent first)
        var baseTime = DateTime.UtcNow;
        var old = CreateSegmentWithLastAccess(0, 5, baseTime.AddHours(-2));
        var recent = CreateSegmentWithLastAccess(10, 15, baseTime);

        // Storage insertion order does not matter — sampling is random
        InitializeStorage(_selector, [recent, old]);

        // ACT
        var result = _selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — still selects the LRU regardless of insertion order
        Assert.True(result);
        Assert.Same(old, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithMultipleCandidates_SelectsOldestAccess()
    {
        // ARRANGE
        var baseTime = DateTime.UtcNow.AddHours(-3);
        var seg1 = CreateSegmentWithLastAccess(0, 5, baseTime);                  // oldest access
        var seg2 = CreateSegmentWithLastAccess(10, 15, baseTime.AddHours(1));
        var seg3 = CreateSegmentWithLastAccess(20, 25, baseTime.AddHours(2));
        var seg4 = CreateSegmentWithLastAccess(30, 35, baseTime.AddHours(3));    // most recent

        InitializeStorage(_selector, [seg3, seg1, seg4, seg2]);

        // ACT
        var result = _selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — seg1 has oldest LastAccessedAt → selected by LRU
        Assert.True(result);
        Assert.Same(seg1, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithSingleCandidate_ReturnsThatCandidate()
    {
        // ARRANGE
        var seg = CreateSegmentWithLastAccess(0, 5, DateTime.UtcNow);
        InitializeStorage(_selector, [seg]);

        // ACT
        var result = _selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT
        Assert.True(result);
        Assert.Same(seg, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithEmptyStorage_ReturnsFalse()
    {
        // ARRANGE — initialize with empty storage
        InitializeStorage(_selector, []);

        // ACT
        var result = _selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT
        Assert.False(result);
    }

    #endregion

    #region TrySelectCandidate — Immunity

    [Fact]
    public void TrySelectCandidate_WhenLruCandidateIsImmune_SelectsNextLru()
    {
        // ARRANGE
        var baseTime = DateTime.UtcNow;
        var old = CreateSegmentWithLastAccess(0, 5, baseTime.AddHours(-2));      // LRU — immune
        var recent = CreateSegmentWithLastAccess(10, 15, baseTime);

        InitializeStorage(_selector, [old, recent]);

        var immune = new HashSet<CachedSegment<int, int>> { old };

        // ACT
        var result = _selector.TrySelectCandidate(immune, out var candidate);

        // ASSERT — old is immune, so next LRU (recent) is selected
        Assert.True(result);
        Assert.Same(recent, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WhenAllCandidatesAreImmune_ReturnsFalse()
    {
        // ARRANGE
        var seg = CreateSegmentWithLastAccess(0, 5, DateTime.UtcNow);
        InitializeStorage(_selector, [seg]);
        var immune = new HashSet<CachedSegment<int, int>> { seg };

        // ACT
        var result = _selector.TrySelectCandidate(immune, out _);

        // ASSERT
        Assert.False(result);
    }

    #endregion

    #region InitializeMetadata / UpdateMetadata

    [Fact]
    public void InitializeMetadata_SetsLastAccessedAt()
    {
        // ARRANGE
        var now = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);
        var selector = new LruEvictionSelector<int, int>(timeProvider: fakeTime);
        var segment = CreateSegmentRaw(0, 5);

        // ACT
        selector.InitializeMetadata(segment);

        // ASSERT
        var meta = Assert.IsType<LruEvictionSelector<int, int>.LruMetadata>(segment.EvictionMetadata);
        Assert.Equal(now.UtcDateTime, meta.LastAccessedAt);
    }

    [Fact]
    public void UpdateMetadata_RefreshesLastAccessedAt()
    {
        // ARRANGE
        var initialTime = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var updatedTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(initialTime);
        var selector = new LruEvictionSelector<int, int>(timeProvider: fakeTime);

        var segment = CreateSegmentRaw(0, 5);
        selector.InitializeMetadata(segment); // sets LastAccessedAt = initialTime

        // ACT — advance fake clock then update
        fakeTime.SetUtcNow(updatedTime);
        selector.UpdateMetadata([segment]);

        // ASSERT
        var meta = Assert.IsType<LruEvictionSelector<int, int>.LruMetadata>(segment.EvictionMetadata);
        Assert.Equal(updatedTime.UtcDateTime, meta.LastAccessedAt);
    }

    [Fact]
    public void UpdateMetadata_WithNullMetadata_LazilyInitializesMetadata()
    {
        // ARRANGE — segment has no metadata yet
        var now = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);
        var selector = new LruEvictionSelector<int, int>(timeProvider: fakeTime);
        var segment = CreateSegmentRaw(0, 5);

        // ACT
        selector.UpdateMetadata([segment]);

        // ASSERT — metadata lazily created
        var meta = Assert.IsType<LruEvictionSelector<int, int>.LruMetadata>(segment.EvictionMetadata);
        Assert.Equal(now.UtcDateTime, meta.LastAccessedAt);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a <see cref="SnapshotAppendBufferStorage{TRange,TData}"/> populated with
    /// <paramref name="segments"/> and injects it into <paramref name="selector"/> via
    /// <see cref="IStorageAwareEvictionSelector{TRange,TData}"/>.
    /// </summary>
    private static void InitializeStorage(
        IEvictionSelector<int, int> selector,
        IEnumerable<CachedSegment<int, int>> segments)
    {
        var storage = new SnapshotAppendBufferStorage<int, int>();
        foreach (var seg in segments)
        {
            storage.TryAdd(seg);
        }

        if (selector is IStorageAwareEvictionSelector<int, int> storageAware)
        {
            storageAware.Initialize(storage);
        }
    }

    private static CachedSegment<int, int> CreateSegmentWithLastAccess(int start, int end, DateTime lastAccess)
    {
        var segment = CreateSegmentRaw(start, end);
        segment.EvictionMetadata = new LruEvictionSelector<int, int>.LruMetadata(lastAccess);
        return segment;
    }

    private static CachedSegment<int, int> CreateSegmentRaw(int start, int end)
    {
        var range = TestHelpers.CreateRange(start, end);
        return new CachedSegment<int, int>(
            range,
            new ReadOnlyMemory<int>(new int[end - start + 1]));
    }

    #endregion

    #region Test Doubles

    /// <summary>
    /// A controllable <see cref="TimeProvider"/> for deterministic timestamp assertions.
    /// </summary>
    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    #endregion
}
