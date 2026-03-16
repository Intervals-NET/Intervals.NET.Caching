using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.Layered;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Policies;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;
using Intervals.NET.Caching.VisitedPlaces.Public;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.DataSources;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Public.Cache;

/// <summary>
/// Unit tests for <see cref="VisitedPlacesCacheBuilder"/> (static entry point) and
/// <see cref="VisitedPlacesCacheBuilder{TRange,TData,TDomain}"/> (single-cache builder).
/// Validates construction, null-guard enforcement, options configuration (pre-built and inline),
/// eviction wiring, diagnostics wiring, and the resulting <see cref="IVisitedPlacesCache{TRange,TData,TDomain}"/>.
/// </summary>
public sealed class VisitedPlacesCacheBuilderTests
{
    #region Test Infrastructure

    private static IntegerFixedStepDomain Domain => new();

    private static IDataSource<int, int> CreateDataSource() => new SimpleTestDataSource();

    private static VisitedPlacesCacheOptions<int, int> DefaultOptions() =>
        TestHelpers.CreateDefaultOptions();

    private static void ConfigureEviction(EvictionConfigBuilder<int, int> b) =>
        b.AddPolicy(new MaxSegmentCountPolicy<int, int>(100))
         .WithSelector(new LruEvictionSelector<int, int>());

    #endregion

    #region VisitedPlacesCacheBuilder.For() — Null Guard Tests

    [Fact]
    public void For_WithNullDataSource_ThrowsArgumentNullException()
    {
        // ACT
        var exception = Record.Exception(() =>
            VisitedPlacesCacheBuilder.For<int, int, IntegerFixedStepDomain>(null!, Domain));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("dataSource", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void For_WithNullDomain_ThrowsArgumentNullException()
    {
        // ARRANGE — use a reference-type TDomain to allow null
        var dataSource = CreateDataSource();

        // ACT
        var exception = Record.Exception(() =>
            VisitedPlacesCacheBuilder.For<int, int, IRangeDomain<int>>(dataSource, null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("domain", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void For_WithValidArguments_ReturnsBuilder()
    {
        // ACT
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ASSERT
        Assert.NotNull(builder);
    }

    #endregion

    #region VisitedPlacesCacheBuilder.Layered() — Null Guard Tests

    [Fact]
    public void Layered_WithNullDataSource_ThrowsArgumentNullException()
    {
        // ACT
        var exception = Record.Exception(() =>
            VisitedPlacesCacheBuilder.Layered<int, int, IntegerFixedStepDomain>(null!, Domain));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("dataSource", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void Layered_WithNullDomain_ThrowsArgumentNullException()
    {
        // ARRANGE — use a reference-type TDomain to allow null
        var dataSource = CreateDataSource();

        // ACT
        var exception = Record.Exception(() =>
            VisitedPlacesCacheBuilder.Layered<int, int, IRangeDomain<int>>(dataSource, null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("domain", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void Layered_WithValidArguments_ReturnsLayeredBuilder()
    {
        // ACT
        var builder = VisitedPlacesCacheBuilder.Layered(CreateDataSource(), Domain);

        // ASSERT
        Assert.NotNull(builder);
        Assert.IsType<LayeredRangeCacheBuilder<int, int, IntegerFixedStepDomain>>(builder);
    }

    #endregion

    #region WithOptions(VisitedPlacesCacheOptions) Tests

    [Fact]
    public void WithOptions_WithNullOptions_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ACT
        var exception = Record.Exception(() =>
            builder.WithOptions((VisitedPlacesCacheOptions<int, int>)null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("options", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void WithOptions_WithValidOptions_ReturnsBuilderForFluentChaining()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ACT
        var returned = builder.WithOptions(DefaultOptions());

        // ASSERT — same instance for fluent chaining
        Assert.Same(builder, returned);
    }

    #endregion

    #region WithOptions(Action<VisitedPlacesCacheOptionsBuilder>) Tests

    [Fact]
    public void WithOptions_WithNullDelegate_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ACT
        var exception = Record.Exception(() =>
            builder.WithOptions((Action<VisitedPlacesCacheOptionsBuilder<int, int>>)null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("configure", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void WithOptions_WithInlineDelegate_ReturnsBuilderForFluentChaining()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ACT
        var returned = builder.WithOptions(o => o.WithEventChannelCapacity(64));

        // ASSERT
        Assert.Same(builder, returned);
    }

    #endregion

    #region WithDiagnostics Tests

    [Fact]
    public void WithDiagnostics_WithNullDiagnostics_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ACT
        var exception = Record.Exception(() => builder.WithDiagnostics(null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("diagnostics", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void WithDiagnostics_WithValidDiagnostics_ReturnsBuilderForFluentChaining()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);
        var diagnostics = new EventCounterCacheDiagnostics();

        // ACT
        var returned = builder.WithDiagnostics(diagnostics);

        // ASSERT
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithDiagnostics_WithoutCallingIt_DoesNotThrowOnBuild()
    {
        // ARRANGE — diagnostics is optional; NoOpDiagnostics.Instance should be used
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions())
            .WithEviction(ConfigureEviction);

        // ACT
        var exception = Record.Exception(() => builder.Build());

        // ASSERT
        Assert.Null(exception);
    }

    #endregion

    #region WithEviction(IReadOnlyList, IEvictionSelector) Tests

    [Fact]
    public void WithEviction_WithNullPolicies_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);
        IEvictionSelector<int, int> selector = new LruEvictionSelector<int, int>();

        // ACT
        var exception = Record.Exception(() => builder.WithEviction(null!, selector));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("policies", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void WithEviction_WithEmptyPolicies_ThrowsArgumentException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);
        IEvictionSelector<int, int> selector = new LruEvictionSelector<int, int>();

        // ACT
        var exception = Record.Exception(() =>
            builder.WithEviction([], selector));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentException>(exception);
        Assert.Contains("policies", ((ArgumentException)exception).ParamName);
    }

    [Fact]
    public void WithEviction_WithNullSelector_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);
        IReadOnlyList<IEvictionPolicy<int, int>> policies = [new MaxSegmentCountPolicy<int, int>(10)];

        // ACT
        var exception = Record.Exception(() => builder.WithEviction(policies, null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Contains("selector", ((ArgumentNullException)exception).ParamName);
    }

    [Fact]
    public void WithEviction_WithValidArguments_ReturnsBuilderForFluentChaining()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);
        IReadOnlyList<IEvictionPolicy<int, int>> policies = [new MaxSegmentCountPolicy<int, int>(10)];
        IEvictionSelector<int, int> selector = new LruEvictionSelector<int, int>();

        // ACT
        var returned = builder.WithEviction(policies, selector);

        // ASSERT
        Assert.Same(builder, returned);
    }

    #endregion

    #region WithEviction(Action<EvictionConfigBuilder>) Tests

    [Fact]
    public void WithEviction_WithNullDelegate_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain);

        // ACT
        var exception = Record.Exception(() =>
            builder.WithEviction((Action<EvictionConfigBuilder<int, int>>)null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
    }

    [Fact]
    public void WithEviction_DelegateWithNoPolicies_ThrowsInvalidOperationException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions());

        // ACT — WithEviction eagerly calls Build() on the EvictionConfigBuilder, so the
        // exception fires inside WithEviction itself, not deferred to Build()
        var exception = Record.Exception(() =>
            builder.WithEviction(b => b.WithSelector(new LruEvictionSelector<int, int>())));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void WithEviction_DelegateWithNoSelector_ThrowsInvalidOperationException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions());

        // ACT — WithEviction eagerly calls Build() on the EvictionConfigBuilder, so the
        // exception fires inside WithEviction itself, not deferred to Build()
        var exception = Record.Exception(() =>
            builder.WithEviction(b => b.AddPolicy(new MaxSegmentCountPolicy<int, int>(10))));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    #endregion

    #region Build() Tests

    [Fact]
    public void Build_WithoutOptions_ThrowsInvalidOperationException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithEviction(ConfigureEviction);

        // ACT
        var exception = Record.Exception(() => builder.Build());

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Build_WithoutEviction_ThrowsInvalidOperationException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions());

        // ACT
        var exception = Record.Exception(() => builder.Build());

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Build_CalledTwice_ThrowsInvalidOperationException()
    {
        // ARRANGE
        var builder = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions())
            .WithEviction(ConfigureEviction);

        builder.Build(); // first call

        // ACT — second call should throw
        var exception = Record.Exception(() => builder.Build());

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task Build_WithPreBuiltOptions_ReturnsNonNull()
    {
        // ARRANGE & ACT
        await using var cache = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions())
            .WithEviction(ConfigureEviction)
            .Build();

        // ASSERT
        Assert.NotNull(cache);
    }

    [Fact]
    public async Task Build_WithInlineOptions_ReturnsNonNull()
    {
        // ARRANGE & ACT
        await using var cache = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(o => o.WithEventChannelCapacity(64))
            .WithEviction(ConfigureEviction)
            .Build();

        // ASSERT
        Assert.NotNull(cache);
    }

    [Fact]
    public async Task Build_ReturnedCacheImplementsIVisitedPlacesCache()
    {
        // ARRANGE & ACT
        await using var cache = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions())
            .WithEviction(ConfigureEviction)
            .Build();

        // ASSERT
        Assert.IsAssignableFrom<IVisitedPlacesCache<int, int, IntegerFixedStepDomain>>(cache);
    }

    #endregion

    #region End-to-End Tests

    [Fact]
    public async Task Build_WithDiagnostics_DiagnosticsReceiveEvents()
    {
        // ARRANGE
        var diagnostics = new EventCounterCacheDiagnostics();

        await using var cache = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions())
            .WithEviction(ConfigureEviction)
            .WithDiagnostics(diagnostics)
            .Build();

        var range = TestHelpers.CreateRange(1, 10);

        // ACT
        await cache.GetDataAsync(range, CancellationToken.None);
        await cache.WaitForIdleAsync();

        // ASSERT — at least one user request was served
        Assert.True(diagnostics.UserRequestServed >= 1,
            "Diagnostics should have received at least one user request event.");
    }

    [Fact]
    public async Task Build_WithPreBuiltOptions_CanFetchData()
    {
        // ARRANGE
        await using var cache = VisitedPlacesCacheBuilder.For(CreateDataSource(), Domain)
            .WithOptions(DefaultOptions())
            .WithEviction(ConfigureEviction)
            .Build();

        var range = TestHelpers.CreateRange(1, 10);

        // ACT
        var result = await cache.GetDataAsync(range, CancellationToken.None);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Length);
        await cache.WaitForIdleAsync();
    }

    #endregion
}
