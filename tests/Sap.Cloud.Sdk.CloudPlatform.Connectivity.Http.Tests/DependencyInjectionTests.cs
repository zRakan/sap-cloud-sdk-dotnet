using Microsoft.Extensions.DependencyInjection;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

public class DependencyInjectionTests : HttpStateReset
{
    /// <summary>One recording primary handler per destination host.</summary>
    private readonly Dictionary<string, RecordingHandler> _primaries = [];

    private void UseFakePrimaryHandlers() =>
        DefaultDestinationHttpClientFactory.Shared = new DefaultDestinationHttpClientFactory(
            new DestinationHttpClientFactoryOptions(),
            destination =>
            {
                var host = destination!.Uri.Host;
                if (!_primaries.TryGetValue(host, out var handler))
                {
                    _primaries[host] = handler = new RecordingHandler();
                }
                return handler;
            });

    private sealed class TenantAwareLoader : IDestinationLoader
    {
        public List<string> Requests { get; } = [];

        public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
        {
            Requests.Add(destinationName);
            var tenant = TenantAccessor.TryGetCurrentTenant().GetValueOrDefault()?.TenantId ?? "none";
            return Result<IDestination>.Success(
                DefaultHttpDestination.CreateBuilder($"https://{tenant}.example/api").Header("X-Tenant", tenant).Build());
        }
    }

    private static ServiceProvider Build(Action<IHttpClientBuilder> configure)
    {
        var services = new ServiceCollection();
        configure(services.AddHttpClient("sap"));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task NamedDestination_IsLookedUpPerRequest_SoTenantsAreIsolated()
    {
        UseFakePrimaryHandlers();
        var loader = new TenantAwareLoader();
        DestinationAccessor.Loader = loader;
        using var provider = Build(b => b.AddSapDestination("MyDestination"));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sap");

        await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("one"), () => client.GetAsync("Entity"));
        await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("two"), () => client.GetAsync("Entity"));
        await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("one"), () => client.GetAsync("Other"));

        Assert.Equal(["MyDestination", "MyDestination", "MyDestination"], loader.Requests);
        var one = _primaries["one.example"].Requests;
        var two = _primaries["two.example"].Requests;
        Assert.Equal(["https://one.example/api/Entity", "https://one.example/api/Other"], one.Select(r => r.Uri.OriginalString));
        Assert.Equal(["https://two.example/api/Entity"], two.Select(r => r.Uri.OriginalString));
        Assert.Equal("one", one[0].Header("X-Tenant"));
        Assert.Equal("two", two[0].Header("X-Tenant"));
    }

    [Fact]
    public async Task ProviderOverload_UsesTheReturnedDestination()
    {
        UseFakePrimaryHandlers();
        var destination = DefaultHttpDestination.CreateBuilder("https://provided.example/svc/").Header("X-Provided", "yes").Build();
        using var provider = Build(b => b.AddSapDestination(_ => destination));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sap");

        await client.GetAsync("/Entity?$top=1");

        var request = _primaries["provided.example"].Requests.Single();
        Assert.Equal("https://provided.example/svc/Entity?$top=1", request.Uri.OriginalString);
        Assert.Equal("yes", request.Header("X-Provided"));
    }

    [Fact]
    public async Task AsyncHeaderProviders_AreAwaited_WithTheFinalUri()
    {
        UseFakePrimaryHandlers();
        var headerProvider = new AsyncHeaderProvider(uri => [new Header("X-Path", uri.AbsolutePath)]);
        var destination = DefaultHttpDestination.CreateBuilder("https://async.example/svc").HeaderProviders(headerProvider).Build();
        using var provider = Build(b => b.AddSapDestination(_ => destination));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sap");

        await client.GetAsync("Entity");

        Assert.Equal("/svc/Entity", _primaries["async.example"].Requests.Single().Header("X-Path"));
    }

    [Fact]
    public async Task MutatingRequests_FetchACsrfToken()
    {
        UseFakePrimaryHandlers();
        var destination = DefaultHttpDestination.CreateBuilder("https://csrf.example/svc/").Build();
        using var provider = Build(b => b.AddSapDestination(_ => destination));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sap");

        await client.PostAsync("Entity", new StringContent("{}"));

        var requests = _primaries["csrf.example"].Requests;
        Assert.Equal(HttpMethod.Head, requests[0].Method);
        Assert.Equal("https://csrf.example/svc/", requests[0].Uri.ToString());
        Assert.Equal(HttpMethod.Post, requests[1].Method);
    }

    [Fact]
    public async Task UnknownDestination_FailsTheRequest()
    {
        UseFakePrimaryHandlers();
        DestinationAccessor.Loader = new FailingLoader();
        using var provider = Build(b => b.AddSapDestination("Missing"));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sap");

        await Assert.ThrowsAsync<DestinationNotFoundException>(() => client.GetAsync("x"));
    }

    [Fact]
    public void BaseAddress_IsAPlaceholder_AndArgumentsAreValidated()
    {
        using var provider = Build(b => b.AddSapDestination("MyDestination"));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sap");

        Assert.Equal(DestinationHttpClientBuilderExtensions.PlaceholderBaseAddress, client.BaseAddress);
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddHttpClient("x").AddSapDestination(""));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddHttpClient("x").AddSapDestination((Func<IServiceProvider, IHttpDestinationProperties>)null!));
    }

    private sealed class FailingLoader : IDestinationLoader
    {
        public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options) =>
            Result<IDestination>.Failure(new DestinationNotFoundException(destinationName));
    }
}
