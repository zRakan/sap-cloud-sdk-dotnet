using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>Configuration of <see cref="DestinationService.Cache"/> (port of <c>DestinationServiceCacheTest</c>).</summary>
public sealed class DestinationCacheConfigurationTests : DestinationServiceTestBase
{
    [Fact]
    public void Disable_DisablesTheCache_AndRejectsFurtherConfiguration()
    {
        Assert.True(DestinationService.Cache.IsEnabled);

        DestinationService.Cache.Disable();

        Assert.False(DestinationService.Cache.IsEnabled);
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.EnableChangeDetection);
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.DisableChangeDetection);
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.InstanceSingle);
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.InstanceAll);
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.GetIsolationLocks);
        Assert.Throws<InvalidOperationException>(() => DestinationService.Cache.SetSizeLimit(1));
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.DisableSizeLimit);
        Assert.Throws<InvalidOperationException>(DestinationService.Cache.DisableExpiration);
        Assert.Throws<InvalidOperationException>(() => DestinationService.Cache.SetExpiration(TimeSpan.FromSeconds(1), CacheExpirationStrategy.WhenCreated));
    }

    [Fact]
    public void Reset_CreatesNewCaches()
    {
        var single = DestinationService.Cache.InstanceSingle();
        var all = DestinationService.Cache.InstanceAll();

        DestinationService.Cache.Reset();

        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
        Assert.NotSame(all, DestinationService.Cache.InstanceAll());
    }

    [Fact]
    public void ChangeDetection_RecreatesTheCaches()
    {
        var single = DestinationService.Cache.InstanceSingle();
        var all = DestinationService.Cache.InstanceAll();
        Assert.True(DestinationService.Cache.IsChangeDetectionEnabled);

        DestinationService.Cache.DisableChangeDetection();

        Assert.False(DestinationService.Cache.IsChangeDetectionEnabled);
        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
        Assert.NotSame(all, DestinationService.Cache.InstanceAll());

        single = DestinationService.Cache.InstanceSingle();
        DestinationService.Cache.EnableChangeDetection();
        Assert.True(DestinationService.Cache.IsChangeDetectionEnabled);
        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
    }

    [Fact]
    public void Expiration_RecreatesTheCaches_AndDisablingItDisablesChangeDetection()
    {
        Assert.Equal(DestinationService.Cache.DefaultExpirationDuration, DestinationService.Cache.ExpirationDuration);
        var single = DestinationService.Cache.InstanceSingle();
        var all = DestinationService.Cache.InstanceAll();
        var newDuration = TimeSpan.FromSeconds(1);

        DestinationService.Cache.SetExpiration(newDuration, CacheExpirationStrategy.WhenCreated);

        Assert.Equal(newDuration, DestinationService.Cache.ExpirationDuration);
        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
        Assert.NotSame(all, DestinationService.Cache.InstanceAll());
        single = DestinationService.Cache.InstanceSingle();
        all = DestinationService.Cache.InstanceAll();

        DestinationService.Cache.DisableExpiration();

        Assert.Null(DestinationService.Cache.ExpirationDuration);
        Assert.False(DestinationService.Cache.IsChangeDetectionEnabled);
        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
        Assert.NotSame(all, DestinationService.Cache.InstanceAll());
    }

    [Fact]
    public void EnablingChangeDetection_WithoutExpiration_RestoresTheDefaultExpiration()
    {
        DestinationService.Cache.DisableExpiration();

        DestinationService.Cache.EnableChangeDetection();

        Assert.True(DestinationService.Cache.IsChangeDetectionEnabled);
        Assert.Equal(DestinationService.Cache.DefaultExpirationDuration, DestinationService.Cache.ExpirationDuration);
    }

    [Fact]
    public void Size_RecreatesOnlyTheCacheOfSingleDestinations()
    {
        var single = DestinationService.Cache.InstanceSingle();
        var all = DestinationService.Cache.InstanceAll();

        DestinationService.Cache.SetSizeLimit(100);
        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
        Assert.Same(all, DestinationService.Cache.InstanceAll());

        single = DestinationService.Cache.InstanceSingle();
        DestinationService.Cache.DisableSizeLimit();
        Assert.NotSame(single, DestinationService.Cache.InstanceSingle());
        Assert.Same(all, DestinationService.Cache.InstanceAll());
    }

    [Fact]
    public void Caches_AreRegisteredWithTheCacheManager()
    {
        var registered = CacheManager.GetCacheList();

        Assert.Contains(DestinationService.Cache.InstanceSingle().Memory, registered);
        Assert.Contains(DestinationService.Cache.InstanceAll().Memory, registered);
        Assert.Contains(DestinationService.Cache.GetIsolationLocks().Memory, registered);

        var replaced = DestinationService.Cache.InstanceSingle().Memory;
        DestinationService.Cache.SetSizeLimit(5);
        Assert.DoesNotContain(replaced, CacheManager.GetCacheList());
        Assert.Contains(DestinationService.Cache.InstanceSingle().Memory, CacheManager.GetCacheList());
    }

    [Fact]
    public async Task CacheManager_CanInvalidateCachedDestinations_PerTenant()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        var adapter = new FakeAdapter().Respond("/v1/destinations/d", Responses.Http("d", "https://example.com"));
        var service = CreateService(adapter);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly));
        foreach (var tenant in new[] { "tenant-a", "tenant-b" })
        {
            await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant(tenant), () => service.TryGetDestinationAsync("d", options));
        }
        Assert.Equal(2, DestinationService.Cache.InstanceSingle().Count);

        CacheManager.InvalidateTenantCaches("tenant-a");

        var remaining = Assert.Single(DestinationService.Cache.InstanceSingle().Keys);
        Assert.Equal("tenant-b", remaining.TenantId);
    }

    [Fact]
    public void ExperimentalSettings_AreIdentifiedInOptions()
    {
        var experimental = new[]
        {
            Options(Augmenter().CustomHeaders(new Header("experimental-header", "value"))),
            Options(Augmenter().CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.Subaccount)),
            Options(Augmenter().CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.Instance)),
            Options(Augmenter().FragmentName("a-fragment")),
        };
        var legacy = new[]
        {
            NoOptions,
            Options(Augmenter().RefreshToken("a-token")),
            Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider)),
            Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.ForwardUserToken)),
            Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.ExchangeOnly).RetrievalStrategy(DestinationServiceRetrievalStrategy.CurrentTenant)),
            Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly).RetrievalStrategy(DestinationServiceRetrievalStrategy.OnlySubscriber)),
        };

        Assert.All(legacy, options => Assert.False(DestinationService.Cache.IsUsingExperimentalFeatures(options)));
        Assert.All(experimental, options => Assert.True(DestinationService.Cache.IsUsingExperimentalFeatures(options)));
    }

    [Fact]
    public async Task ExpiredEntries_AreFetchedAgain()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        DestinationService.Cache.DisableChangeDetection();
        DestinationService.Cache.SetExpiration(TimeSpan.FromMilliseconds(300), CacheExpirationStrategy.WhenCreated);
        var adapter = new FakeAdapter().Respond("/v1/destinations/d", Responses.Http("d", "https://example.com"));
        var service = CreateService(adapter);

        await service.TryGetDestinationAsync("d", NoOptions);
        await service.TryGetDestinationAsync("d", NoOptions);
        Assert.Single(adapter.Calls);

        await Task.Delay(500);
        await service.TryGetDestinationAsync("d", NoOptions);
        Assert.Equal(2, adapter.Calls.Count);
    }

    [Fact]
    public async Task ZeroExpiration_EffectivelyDisablesTheCache()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        DestinationService.Cache.DisableChangeDetection();
        DestinationService.Cache.SetExpiration(TimeSpan.Zero, CacheExpirationStrategy.WhenCreated);
        var adapter = new FakeAdapter().Respond("/v1/destinations/d", Responses.Http("d", "https://example.com"));
        var service = CreateService(adapter);

        await service.TryGetDestinationAsync("d", NoOptions);
        await service.TryGetDestinationAsync("d", NoOptions);

        Assert.Equal(2, adapter.Calls.Count);
        Assert.Equal(0, DestinationService.Cache.InstanceSingle().Count);
    }

    [Fact]
    public async Task SizeLimit_IsRespected_ByNotStoringFurtherDestinations()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        DestinationService.Cache.DisableChangeDetection();
        DestinationService.Cache.SetSizeLimit(1);
        var adapter = new FakeAdapter()
            .Respond("/v1/destinations/a", Responses.Http("a", "https://a.example.com"))
            .Respond("/v1/destinations/b", Responses.Http("b", "https://b.example.com"));
        var service = CreateService(adapter);

        await service.TryGetDestinationAsync("a", NoOptions);
        await service.TryGetDestinationAsync("b", NoOptions);

        Assert.Equal(1, DestinationService.Cache.InstanceSingle().Count);
    }
}
