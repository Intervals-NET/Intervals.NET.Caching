using Intervals.NET.Caching.Dto;
using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Integration tests for the lazy TTL expiration mechanism.
/// TTL segments are filtered on read (invisible to the User Path once expired) and physically
/// removed during the next <c>TryNormalize</c> pass triggered by the Background Path.
/// </summary>
public sealed class TtlExpirationTests : IAsyncDisposable
{
    private readonly IntegerFixedStepDomain _domain = new();
    private readonly EventCounterCacheDiagnostics _diagnostics = new();
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;

    public async ValueTask DisposeAsync()
    {
        if (_cache != null)
        {
            await _cache.DisposeAsync();
        }
    }

    // ============================================================
    // TTL DISABLED — baseline behaviour unchanged
    // ============================================================

    [Fact]
    public async Task TtlDisabled_SegmentIsNeverExpired()
    {
        // ARRANGE — no TTL configured; segment should stay in cache indefinitely
        var options = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 128, segmentTtl: null);
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options);

        var range = TestHelpers.CreateRange(0, 9);
        await _cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT — segment stored; no TTL expiry fired
        Assert.Equal(1, _diagnostics.BackgroundSegmentStored);
        Assert.Equal(0, _diagnostics.TtlSegmentExpired);

        // Advance a fake clock would do nothing (no TTL configured) — assert after
        // waiting for any spurious background activity
        await _cache.WaitForIdleAsync();
        Assert.Equal(0, _diagnostics.TtlSegmentExpired);
    }

    // ============================================================
    // TTL ENABLED — lazy filter (expiry on read, before normalization)
    // ============================================================

    [Fact]
    public async Task TtlEnabled_AfterTimeAdvances_ExpiredSegmentInvisibleOnRead()
    {
        // ARRANGE — appendBufferSize=8 (default) so normalization won't fire after 1 segment.
        // Use FakeTimeProvider so we can advance time without waiting.
        var fakeTime = new FakeTimeProvider();
        var options = new VisitedPlacesCacheOptions<int, int>(
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromSeconds(10));
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options, timeProvider: fakeTime);

        var range = TestHelpers.CreateRange(0, 9);

        // ACT — store segment, then advance time past TTL
        await _cache.GetDataAndWaitForIdleAsync(range);
        Assert.Equal(1, _diagnostics.BackgroundSegmentStored);

        fakeTime.Advance(TimeSpan.FromSeconds(11)); // past the 10s TTL

        // Read again — expired segment must be invisible (FullMiss, not FullHit)
        var result = await _cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT — user path sees a miss (lazy filter kicked in); normalization not yet run
        Assert.Equal(CacheInteraction.FullMiss, result.CacheInteraction);
        Assert.Equal(0, _diagnostics.TtlSegmentExpired); // physical removal not yet triggered

        await _cache.WaitForIdleAsync();
    }

    // ============================================================
    // TTL ENABLED — normalization discovers and removes expired segments
    // ============================================================

    [Fact]
    public async Task TtlEnabled_NormalizationTriggered_ExpiresAndReportsSegment()
    {
        // ARRANGE — appendBufferSize=1 so TryNormalize fires on every store.
        // Use FakeTimeProvider to control expiry deterministically.
        var fakeTime = new FakeTimeProvider();
        var storageOptions = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 1);
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: storageOptions,
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromSeconds(10));
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options, timeProvider: fakeTime);

        var range1 = TestHelpers.CreateRange(0, 9);
        var range2 = TestHelpers.CreateRange(20, 29); // second store triggers normalization

        // Store first segment
        await _cache.GetDataAndWaitForIdleAsync(range1);
        Assert.Equal(1, _diagnostics.BackgroundSegmentStored);
        Assert.Equal(0, _diagnostics.TtlSegmentExpired);

        // Advance time past TTL
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Store a second segment — TryNormalize fires, discovers segment1 is expired
        await _cache.GetDataAndWaitForIdleAsync(range2);
        await _cache.WaitForIdleAsync();

        // ASSERT — expired segment was discovered and reported
        Assert.Equal(1, _diagnostics.TtlSegmentExpired);
        Assert.Equal(0, _diagnostics.BackgroundOperationFailed);
    }

    [Fact]
    public async Task TtlEnabled_MultipleSegments_AllExpireOnNormalization()
    {
        // ARRANGE — appendBufferSize=1; FakeTimeProvider
        var fakeTime = new FakeTimeProvider();
        var storageOptions = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 1);
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: storageOptions,
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromSeconds(10));
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options, timeProvider: fakeTime);

        // Store two non-overlapping segments
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(0, 9));
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(20, 29));
        Assert.Equal(2, _diagnostics.BackgroundSegmentStored);

        // Advance time past TTL
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Trigger a third store to force normalization; both prior segments are now expired
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(40, 49));
        await _cache.WaitForIdleAsync();

        // ASSERT — both prior segments were expired during normalization
        Assert.Equal(2, _diagnostics.TtlSegmentExpired);
        Assert.Equal(0, _diagnostics.BackgroundOperationFailed);
    }

    // ============================================================
    // TTL + RE-FETCH — after expiry, next request is a FullMiss
    // ============================================================

    [Fact]
    public async Task TtlEnabled_AfterExpiry_SubsequentRequestRefetchesFromDataSource()
    {
        // ARRANGE — appendBufferSize=1 so normalization fires on every store
        var fakeTime = new FakeTimeProvider();
        var storageOptions = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 1);
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: storageOptions,
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromSeconds(10));
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options, timeProvider: fakeTime);

        var range = TestHelpers.CreateRange(0, 9);

        // First fetch — populates cache
        var result1 = await _cache.GetDataAndWaitForIdleAsync(range);
        Assert.Equal(CacheInteraction.FullMiss, result1.CacheInteraction);
        Assert.Equal(1, _diagnostics.BackgroundSegmentStored);

        // Advance time past TTL
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        _diagnostics.Reset();

        // Second fetch — expired segment is invisible on read → FullMiss; stores a new segment
        var result2 = await _cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT — full miss again (expired segment not visible), new segment stored
        Assert.Equal(CacheInteraction.FullMiss, result2.CacheInteraction);
        Assert.Equal(1, _diagnostics.BackgroundSegmentStored);
    }

    // ============================================================
    // TTL + EVICTION — idempotency (only one removal path fires)
    // ============================================================

    [Fact]
    public async Task TtlEnabled_TtlAndEvictionCompete_OnlyOneRemovalFires()
    {
        // ARRANGE — MaxSegmentCount(1) so a second store would normally evict the first.
        // appendBufferSize=1 so TryNormalize fires on the same step as the second store.
        // With the execution order (TryNormalize before Eviction), TTL wins: it removes
        // segment A in step 2b, so eviction in steps 3+4 finds no additional candidate.
        var fakeTime = new FakeTimeProvider();
        var storageOptions = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 1);
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: storageOptions,
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromSeconds(10));
        _cache = TestHelpers.CreateCacheWithSimpleSource(
            _domain, _diagnostics, options, maxSegmentCount: 1, timeProvider: fakeTime);

        // Store first segment
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(0, 9));
        Assert.Equal(1, _diagnostics.BackgroundSegmentStored);

        // Advance past TTL
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Store second segment — TryNormalize fires (TTL removes segment A), then eviction
        // finds no candidates to remove (only B which is just-stored and immune, count=1).
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(20, 29));
        await _cache.WaitForIdleAsync();

        // ASSERT — TTL fired for segment A; eviction did NOT also remove it
        Assert.Equal(1, _diagnostics.TtlSegmentExpired);
        Assert.Equal(0, _diagnostics.EvictionSegmentRemoved);
        Assert.Equal(0, _diagnostics.BackgroundOperationFailed);
    }

    // ============================================================
    // DISPOSAL — unexpired segments present; disposal completes cleanly
    // ============================================================

    [Fact]
    public async Task Disposal_WithUnexpiredSegments_CompletesCleanly()
    {
        // ARRANGE — very long TTL so segments won't expire during this test
        var fakeTime = new FakeTimeProvider();
        var options = new VisitedPlacesCacheOptions<int, int>(
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromHours(1));
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options, timeProvider: fakeTime);

        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(0, 9));
        Assert.Equal(0, _diagnostics.TtlSegmentExpired);

        // ACT — dispose cache while TTL is still far from expiry
        await _cache.DisposeAsync();
        _cache = null; // prevent DisposeAsync() from being called again in IAsyncDisposable

        // ASSERT — no crash, no TTL expiry, no background failures
        Assert.Equal(0, _diagnostics.TtlSegmentExpired);
        Assert.Equal(0, _diagnostics.BackgroundOperationFailed);
    }

    // ============================================================
    // DIAGNOSTICS — TtlSegmentExpired counter accuracy
    // ============================================================

    [Fact]
    public async Task TtlEnabled_DiagnosticsCounters_AreCorrect()
    {
        // ARRANGE — appendBufferSize=1; three segments stored, then all expired, then trigger normalization
        var fakeTime = new FakeTimeProvider();
        var storageOptions = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 1);
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: storageOptions,
            eventChannelCapacity: 128,
            segmentTtl: TimeSpan.FromSeconds(10));
        _cache = TestHelpers.CreateCacheWithSimpleSource(_domain, _diagnostics, options, timeProvider: fakeTime);

        // Store three non-overlapping segments
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(0, 9));
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(20, 29));
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(40, 49));
        Assert.Equal(3, _diagnostics.BackgroundSegmentStored);

        // Advance past TTL and trigger normalization via a fourth store
        fakeTime.Advance(TimeSpan.FromSeconds(11));
        await _cache.GetDataAndWaitForIdleAsync(TestHelpers.CreateRange(60, 69));
        await _cache.WaitForIdleAsync();

        // ASSERT — all three prior segments expired during normalization
        Assert.Equal(3, _diagnostics.TtlSegmentExpired);
        Assert.Equal(0, _diagnostics.BackgroundOperationFailed);
    }
}
