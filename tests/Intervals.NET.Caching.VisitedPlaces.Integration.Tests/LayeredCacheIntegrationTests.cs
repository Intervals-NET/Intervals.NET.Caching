using Intervals.NET.Caching.Extensions;
using Intervals.NET.Caching.Layered;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Policies;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;
using Intervals.NET.Caching.VisitedPlaces.Public.Extensions;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Integration.Tests;

/// <summary>
/// Integration tests for the layered cache feature with <see cref="VisitedPlacesCache{TRange,TData,TDomain}"/>.
/// Verifies that multi-layer stacks propagate data correctly, support all four
/// <c>AddVisitedPlacesLayer</c> overloads, converge via <c>WaitForIdleAsync</c>,
/// and dispose cleanly.
/// </summary>
public sealed class LayeredCacheIntegrationTests
{
    private static readonly IntegerFixedStepDomain Domain = new();

    private static IDataSource<int, int> CreateRealDataSource() => new SimpleTestDataSource();

    // Standard eviction configuration used by all layers in these tests
    private static void ConfigureEviction(EvictionConfigBuilder<int, int> b) =>
        b.AddPolicy(new MaxSegmentCountPolicy<int, int>(100))
         .WithSelector(new LruEvictionSelector<int, int>());

    // ============================================================
    // DATA CORRECTNESS
    // ============================================================

    /// <summary>
    /// A two-layer VPC stack returns the correct data values from the outermost layer.
    /// </summary>
    [Fact]
    public async Task TwoLayerCache_GetData_ReturnsCorrectValues()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        var range = Factories.Range.Closed<int>(100, 110);

        // ACT
        var result = await cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT
        var array = result.Data.ToArray();
        Assert.Equal(11, array.Length);
        for (var i = 0; i < array.Length; i++)
        {
            Assert.Equal(100 + i, array[i]);
        }
    }

    /// <summary>
    /// A three-layer VPC stack propagates data through all layers and returns correct values.
    /// </summary>
    [Fact]
    public async Task ThreeLayerCache_GetData_ReturnsCorrectValues()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        var range = Factories.Range.Closed<int>(200, 215);

        // ACT
        var result = await cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT
        var array = result.Data.ToArray();
        Assert.Equal(16, array.Length);
        for (var i = 0; i < array.Length; i++)
        {
            Assert.Equal(200 + i, array[i]);
        }
    }

    /// <summary>
    /// Multiple sequential non-overlapping requests through a two-layer stack all return correct data.
    /// </summary>
    [Fact]
    public async Task TwoLayerCache_SubsequentRequests_ReturnCorrectValues()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        var ranges = new[]
        {
            Factories.Range.Closed<int>(0, 10),
            Factories.Range.Closed<int>(100, 110),
            Factories.Range.Closed<int>(500, 510),
        };

        // ACT & ASSERT
        foreach (var range in ranges)
        {
            var result = await cache.GetDataAsync(range, CancellationToken.None);
            var array = result.Data.ToArray();
            Assert.Equal(11, array.Length);
            var start = (int)range.Start;
            for (var i = 0; i < array.Length; i++)
            {
                Assert.Equal(start + i, array[i]);
            }
        }
    }

    /// <summary>
    /// A single-element range is returned correctly through a layered stack.
    /// </summary>
    [Fact]
    public async Task TwoLayerCache_SingleElementRange_ReturnsCorrectValue()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        // ACT
        var range = Factories.Range.Closed<int>(42, 42);
        var result = await cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT
        var array = result.Data.ToArray();
        Assert.Single(array);
        Assert.Equal(42, array[0]);
    }

    // ============================================================
    // LAYER COUNT
    // ============================================================

    [Fact]
    public async Task TwoLayerCache_LayerCount_IsTwo()
    {
        // ARRANGE
        await using var layered = (LayeredRangeCache<int, int, IntegerFixedStepDomain>)
            await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
                .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
                .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
                .BuildAsync();

        // ASSERT
        Assert.Equal(2, layered.LayerCount);
    }

    [Fact]
    public async Task ThreeLayerCache_LayerCount_IsThree()
    {
        // ARRANGE
        await using var layered = (LayeredRangeCache<int, int, IntegerFixedStepDomain>)
            await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
                .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
                .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
                .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
                .BuildAsync();

        // ASSERT
        Assert.Equal(3, layered.LayerCount);
    }

    // ============================================================
    // CONVERGENCE / WAITFORIDLEASYNC
    // ============================================================

    [Fact]
    public async Task TwoLayerCache_WaitForIdleAsync_ConvergesWithoutException()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        await cache.GetDataAsync(Factories.Range.Closed<int>(100, 110), CancellationToken.None);

        // ACT
        var exception = await Record.ExceptionAsync(() => cache.WaitForIdleAsync());

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task TwoLayerCache_AfterConvergence_DataStillCorrect()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        var range = Factories.Range.Closed<int>(50, 60);

        await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ACT — re-read same range after convergence
        var result = await cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT
        var array = result.Data.ToArray();
        Assert.Equal(11, array.Length);
        for (var i = 0; i < array.Length; i++)
        {
            Assert.Equal(50 + i, array[i]);
        }
    }

    [Fact]
    public async Task TwoLayerCache_GetDataAndWaitForIdleAsync_ReturnsCorrectData()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        var range = Factories.Range.Closed<int>(300, 315);

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(range);

        // ASSERT
        var array = result.Data.ToArray();
        Assert.Equal(16, array.Length);
        for (var i = 0; i < array.Length; i++)
        {
            Assert.Equal(300 + i, array[i]);
        }
    }

    // ============================================================
    // DISPOSAL
    // ============================================================

    [Fact]
    public async Task TwoLayerCache_DisposeAsync_CompletesWithoutException()
    {
        // ARRANGE
        var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        await cache.GetDataAsync(Factories.Range.Closed<int>(1, 10), CancellationToken.None);

        // ACT
        var exception = await Record.ExceptionAsync(() => cache.DisposeAsync().AsTask());

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public async Task TwoLayerCache_DisposeWithoutAnyRequests_CompletesWithoutException()
    {
        // ARRANGE — build but never use
        var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions())
            .BuildAsync();

        // ACT
        var exception = await Record.ExceptionAsync(() => cache.DisposeAsync().AsTask());

        // ASSERT
        Assert.Null(exception);
    }

    // ============================================================
    // ALL FOUR ADDVISITEDPLACESLAYER OVERLOADS
    // ============================================================

    /// <summary>
    /// Overload 1: policies + selector + options + diagnostics
    /// </summary>
    [Fact]
    public async Task AddVisitedPlacesLayer_Overload_PoliciesSelectorOptionsDiagnostics_Works()
    {
        // ARRANGE
        IReadOnlyList<IEvictionPolicy<int, int>> policies = [new MaxSegmentCountPolicy<int, int>(100)];
        IEvictionSelector<int, int> selector = new LruEvictionSelector<int, int>();
        var diagnostics = new EventCounterCacheDiagnostics();

        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(policies, selector, TestHelpers.CreateDefaultOptions(), diagnostics)
            .BuildAsync();

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(Factories.Range.Closed<int>(0, 9));

        // ASSERT
        Assert.Equal(10, result.Data.Length);
        Assert.True(diagnostics.NormalizationRequestProcessed >= 1);
    }

    /// <summary>
    /// Overload 2: policies + selector + configure (options builder) + diagnostics
    /// </summary>
    [Fact]
    public async Task AddVisitedPlacesLayer_Overload_PoliciesSelectorConfigureDiagnostics_Works()
    {
        // ARRANGE
        IReadOnlyList<IEvictionPolicy<int, int>> policies = [new MaxSegmentCountPolicy<int, int>(100)];
        IEvictionSelector<int, int> selector = new LruEvictionSelector<int, int>();

        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(
                policies,
                selector,
                configure: b => b.WithEventChannelCapacity(64))
            .BuildAsync();

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(Factories.Range.Closed<int>(0, 9));

        // ASSERT
        Assert.Equal(10, result.Data.Length);
    }

    /// <summary>
    /// Overload 3: configureEviction + options + diagnostics
    /// </summary>
    [Fact]
    public async Task AddVisitedPlacesLayer_Overload_ConfigureEvictionOptionsDiagnostics_Works()
    {
        // ARRANGE
        var diagnostics = new EventCounterCacheDiagnostics();

        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions(), diagnostics)
            .BuildAsync();

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(Factories.Range.Closed<int>(10, 19));

        // ASSERT
        Assert.Equal(10, result.Data.Length);
        Assert.Equal(10, result.Data.Span[0]);
        Assert.True(diagnostics.UserRequestServed >= 1);
    }

    /// <summary>
    /// Overload 4: configureEviction + configure (options builder) + diagnostics
    /// </summary>
    [Fact]
    public async Task AddVisitedPlacesLayer_Overload_ConfigureEvictionConfigureDiagnostics_Works()
    {
        // ARRANGE
        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(
                configureEviction: ConfigureEviction,
                configure: b => b.WithEventChannelCapacity(32))
            .BuildAsync();

        // ACT
        var result = await cache.GetDataAndWaitForIdleAsync(Factories.Range.Closed<int>(20, 29));

        // ASSERT
        Assert.Equal(10, result.Data.Length);
        Assert.Equal(20, result.Data.Span[0]);
    }

    // ============================================================
    // PER-LAYER DIAGNOSTICS
    // ============================================================

    [Fact]
    public async Task TwoLayerCache_WithPerLayerDiagnostics_EachLayerTracksIndependently()
    {
        // ARRANGE
        var innerDiagnostics = new EventCounterCacheDiagnostics();
        var outerDiagnostics = new EventCounterCacheDiagnostics();

        await using var cache = await VisitedPlacesCacheBuilder.Layered(CreateRealDataSource(), Domain)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions(), innerDiagnostics)
            .AddVisitedPlacesLayer(ConfigureEviction, TestHelpers.CreateDefaultOptions(), outerDiagnostics)
            .BuildAsync();

        // ACT
        await cache.GetDataAndWaitForIdleAsync(Factories.Range.Closed<int>(100, 110));

        // ASSERT — outer layer records the user request
        Assert.Equal(1, outerDiagnostics.UserRequestServed);

        // ASSERT — data is correct on a re-read
        var result = await cache.GetDataAsync(Factories.Range.Closed<int>(100, 110), CancellationToken.None);
        Assert.Equal(11, result.Data.Length);
        Assert.Equal(100, result.Data.Span[0]);
        Assert.Equal(110, result.Data.Span[^1]);
    }
}
