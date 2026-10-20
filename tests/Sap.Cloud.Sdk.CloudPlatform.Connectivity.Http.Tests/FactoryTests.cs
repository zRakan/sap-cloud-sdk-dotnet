using System.Net;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

public class FactoryTests : HttpStateReset
{
    private readonly List<RecordingHandler> _created = [];

    private DefaultDestinationHttpClientFactory NewFactory(DestinationHttpClientFactoryOptions? options = null) =>
        new(options ?? new DestinationHttpClientFactoryOptions(), _ =>
        {
            var handler = new RecordingHandler();
            _created.Add(handler);
            return handler;
        });

    private static DefaultHttpDestination Destination(string uri = "https://host/base") =>
        DefaultHttpDestination.CreateBuilder(uri).Build();

    private static DefaultHttpDestination UserBoundDestination() =>
        DefaultHttpDestination.CreateBuilder("https://host/base").AuthenticationType(AuthenticationType.OAuth2SamlBearerAssertion).Build();

    private static T InTenant<T>(string tenant, Func<T> func) => TenantAccessor.ExecuteWithTenant(new DefaultTenant(tenant), func);

    private static T InTenantAndPrincipal<T>(string tenant, string principal, Func<T> func) =>
        InTenant(tenant, () => PrincipalAccessor.ExecuteWithPrincipal(new DefaultPrincipal(principal), func));

    [Fact]
    public void Client_IsBoundToTheDestination()
    {
        using var factory = NewFactory();

        using var client = factory.CreateHttpClient(Destination("https://host/base"));

        Assert.Equal(new Uri("https://host/base"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(2), client.Timeout);
    }

    [Fact]
    public void ClientWithoutDestination_HasNoBaseAddress()
    {
        using var factory = NewFactory();

        using var client = factory.CreateHttpClient();

        Assert.Null(client.BaseAddress);
    }

    [Fact]
    public async Task Pipeline_MergesUri_AddsHeaders_AndFetchesCsrfToken()
    {
        using var factory = NewFactory();
        var destination = DefaultHttpDestination.CreateBuilder("https://host/service").Header("X-Dest", "1").Build();
        using var client = factory.CreateHttpClient(destination);

        await client.PostAsync("Entity", new StringContent("{}"));

        var recorder = _created.Single();
        Assert.Equal(2, recorder.Requests.Count);
        Assert.Equal(HttpMethod.Head, recorder.Requests[0].Method);
        Assert.Equal("https://host/service/", recorder.Requests[0].Uri.ToString());
        Assert.Equal("1", recorder.Requests[0].Header("X-Dest"));
        Assert.Equal("https://host/service/Entity", recorder.Requests[1].Uri.ToString());
        Assert.Equal("1", recorder.Requests[1].Header("X-Dest"));
    }

    [Fact]
    public async Task CsrfHandling_CanBeDisabled()
    {
        using var factory = NewFactory(new DestinationHttpClientFactoryOptions { EnableCsrfTokenHandling = false });
        using var client = factory.CreateHttpClient(Destination());

        await client.PostAsync("Entity", new StringContent("{}"));

        Assert.Single(_created.Single().Requests);
    }

    [Fact]
    public void OnPremiseDestinationWithoutProxy_IsRejected()
    {
        using var factory = NewFactory();
        var destination = new StubHttpDestination(new Uri("http://virtual:80"), ProxyType.OnPremise, null);

        Assert.Throws<DestinationAccessException>(() => factory.CreateHttpClient(destination));
        Assert.Empty(_created);
    }

    // ---- cache ---------------------------------------------------------------------------------------------------

    [Fact]
    public void EqualDestinations_ShareOneHandler()
    {
        using var factory = NewFactory();

        using var first = factory.CreateHttpClient(Destination());
        using var second = factory.CreateHttpClient(Destination());

        Assert.Single(_created);
        Assert.Equal(1, factory.CachedHandlerCount);
    }

    [Fact]
    public void DifferentDestinations_GetDifferentHandlers()
    {
        using var factory = NewFactory();

        using var first = factory.CreateHttpClient(Destination("https://one"));
        using var second = factory.CreateHttpClient(Destination("https://two"));
        using var third = factory.CreateHttpClient();

        Assert.Equal(3, _created.Count);
    }

    [Fact]
    public void ClientsWithoutDestination_ShareOneHandler()
    {
        using var factory = NewFactory();

        using var first = factory.CreateHttpClient();
        using var second = factory.CreateHttpClient();

        Assert.Single(_created);
    }

    [Fact]
    public void Tenants_AreIsolated_ForTheSameDestination()
    {
        using var factory = NewFactory();

        using var noTenant = factory.CreateHttpClient(Destination());
        using var tenantOne = InTenant("tenant-1", () => factory.CreateHttpClient(Destination()));
        using var tenantTwo = InTenant("tenant-2", () => factory.CreateHttpClient(Destination()));
        using var tenantOneAgain = InTenant("tenant-1", () => factory.CreateHttpClient(Destination()));

        Assert.Equal(3, _created.Count);
    }

    [Fact]
    public void Tenants_AreIsolated_ForClientsWithoutDestination()
    {
        using var factory = NewFactory();

        using var one = InTenant("tenant-1", () => factory.CreateHttpClient());
        using var two = InTenant("tenant-2", () => factory.CreateHttpClient());

        Assert.Equal(2, _created.Count);
    }

    [Fact]
    public void PrincipalsDoNotMatter_ForDestinationsWithoutUserBinding()
    {
        using var factory = NewFactory();

        using var one = InTenantAndPrincipal("t", "alice", () => factory.CreateHttpClient(Destination()));
        using var two = InTenantAndPrincipal("t", "bob", () => factory.CreateHttpClient(Destination()));

        Assert.Single(_created);
    }

    [Fact]
    public void UserBoundDestinations_AreIsolatedByTenantAndPrincipal()
    {
        using var factory = NewFactory();

        using var alice = InTenantAndPrincipal("t", "alice", () => factory.CreateHttpClient(UserBoundDestination()));
        using var aliceAgain = InTenantAndPrincipal("t", "alice", () => factory.CreateHttpClient(UserBoundDestination()));
        using var bob = InTenantAndPrincipal("t", "bob", () => factory.CreateHttpClient(UserBoundDestination()));
        using var aliceOtherTenant = InTenantAndPrincipal("u", "alice", () => factory.CreateHttpClient(UserBoundDestination()));

        Assert.Equal(3, _created.Count);
    }

    [Fact]
    public void UserBoundDestination_WithoutPrincipal_Fails()
    {
        using var factory = NewFactory();

        var ex = Assert.Throws<HttpClientInstantiationException>(() => factory.CreateHttpClient(UserBoundDestination()));

        Assert.Contains("cache key", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_created);
    }

    [Fact]
    public void PrincipalPropagationDestination_RequiresAPrincipal()
    {
        using var factory = NewFactory();
        var destination = DefaultHttpDestination.CreateBuilder("https://host").AuthenticationType(AuthenticationType.PrincipalPropagation).Build();

        var failure = InTenant("t", () => Record.Exception(() => factory.CreateHttpClient(destination)));

        Assert.IsType<HttpClientInstantiationException>(failure);
    }

    [Fact]
    public void Cache_IsRegisteredWithTheCacheManager_AndCanBeInvalidatedPerTenant()
    {
        using var factory = NewFactory();
        using var one = InTenant("tenant-1", () => factory.CreateHttpClient(Destination()));
        using var two = InTenant("tenant-2", () => factory.CreateHttpClient(Destination()));
        Assert.Equal(2, factory.CachedHandlerCount);

        CacheManager.InvalidateTenantCaches("tenant-1");

        Assert.Equal(1, factory.CachedHandlerCount);
        using var recreated = InTenant("tenant-1", () => factory.CreateHttpClient(Destination()));
        Assert.Equal(3, _created.Count);
    }

    [Fact]
    public async Task EvictedHandlers_AreDisposedOnlyAfterTheGracePeriod()
    {
        using var factory = NewFactory();
        factory.DisposalGracePeriod = TimeSpan.FromMilliseconds(300);
        using var client = factory.CreateHttpClient(Destination());
        var handler = _created.Single();

        CacheManager.InvalidateAll();

        Assert.False(handler.Disposed);
        await client.GetAsync("still-works");
        Assert.Single(handler.Requests);
        for (var i = 0; i < 100 && !handler.Disposed; i++)
        {
            await Task.Delay(50);
        }
        Assert.True(handler.Disposed);
    }

    [Fact]
    public void Handlers_ExpireAfterTheCacheDuration()
    {
        using var factory = NewFactory(new DestinationHttpClientFactoryOptions { CacheDuration = TimeSpan.FromMilliseconds(100) });
        using var first = factory.CreateHttpClient(Destination());

        Thread.Sleep(300);
        using var second = factory.CreateHttpClient(Destination());

        Assert.Equal(2, _created.Count);
    }

    [Fact]
    public void DisabledCache_CreatesAHandlerPerClient_AndTheClientDisposesIt()
    {
        using var factory = NewFactory(new DestinationHttpClientFactoryOptions { CacheDuration = TimeSpan.Zero });

        var first = factory.CreateHttpClient(Destination());
        using var second = factory.CreateHttpClient(Destination());
        first.Dispose();

        Assert.Equal(2, _created.Count);
        Assert.True(_created[0].Disposed);
        Assert.False(_created[1].Disposed);
    }

    [Fact]
    public void PrimaryHandlerFailure_IsWrapped()
    {
        using var factory = new DefaultDestinationHttpClientFactory(new DestinationHttpClientFactoryOptions(), _ => throw new InvalidOperationException("boom"));

        var ex = Assert.Throws<HttpClientInstantiationException>(() => factory.CreateHttpClient(Destination()));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Dispose_UnregistersTheCache()
    {
        var factory = NewFactory();
        var before = CacheManager.GetCacheList().Count;
        Assert.True(before >= 1);

        factory.Dispose();

        Assert.Equal(before - 1, CacheManager.GetCacheList().Count);
    }
}

public class HttpClientAccessorTests : HttpStateReset
{
    private sealed class FakeFactory(Func<HttpClient> create) : IDestinationHttpClientFactory
    {
        public HttpClient CreateHttpClient(IHttpDestinationProperties destination) => create();

        public HttpClient CreateHttpClient() => create();
    }

    [Fact]
    public void DefaultFactory_IsUsed_AndNullRestoresIt()
    {
        var custom = new FakeFactory(() => new HttpClient());
        HttpClientAccessor.HttpClientFactory = custom;
        Assert.Same(custom, HttpClientAccessor.HttpClientFactory);

        HttpClientAccessor.HttpClientFactory = null!;

        Assert.IsType<DefaultDestinationHttpClientFactory>(HttpClientAccessor.HttpClientFactory);
    }

    [Fact]
    public void GetHttpClient_UsesTheConfiguredFactory()
    {
        using var expected = new HttpClient();
        HttpClientAccessor.HttpClientFactory = new FakeFactory(() => expected);

        Assert.Same(expected, HttpClientAccessor.GetHttpClient());
        Assert.Same(expected, HttpClientAccessor.GetHttpClient(DefaultHttpDestination.CreateBuilder("https://host").Build()));
    }

    [Fact]
    public void GetHttpClient_WithDefaultFactory_TargetsTheDestination()
    {
        using var client = HttpClientAccessor.GetHttpClient(DefaultHttpDestination.CreateBuilder("https://host/base").Build());

        Assert.Equal(new Uri("https://host/base"), client.BaseAddress);
    }

    [Fact]
    public void FactoryFailures_AreWrapped_ExceptDestinationAndInstantiationErrors()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host").Build();

        HttpClientAccessor.HttpClientFactory = new FakeFactory(() => throw new InvalidOperationException("boom"));
        var wrapped = Assert.Throws<HttpClientInstantiationException>(() => HttpClientAccessor.GetHttpClient(destination));
        Assert.IsType<InvalidOperationException>(wrapped.InnerException);
        Assert.Throws<HttpClientInstantiationException>(() => HttpClientAccessor.GetHttpClient());

        HttpClientAccessor.HttpClientFactory = new FakeFactory(() => throw new DestinationAccessException("nope"));
        Assert.Throws<DestinationAccessException>(() => HttpClientAccessor.GetHttpClient(destination));

        HttpClientAccessor.HttpClientFactory = new FakeFactory(() => throw new HttpClientInstantiationException("nope"));
        var same = Assert.Throws<HttpClientInstantiationException>(() => HttpClientAccessor.GetHttpClient(destination));
        Assert.Equal("nope", same.Message);
    }

    [Fact]
    public void NonHttpDestination_IsRejected()
    {
        IDestination destination = DefaultDestination.CreateBuilder().Property("Name", "no-uri").Build();

        var result = HttpClientAccessor.TryGetHttpClient(destination);

        Assert.True(result.IsFailure);
        Assert.IsType<DestinationAccessException>(result.Exception);
    }
}
