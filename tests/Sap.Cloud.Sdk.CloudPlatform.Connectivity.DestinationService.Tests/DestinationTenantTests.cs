using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Strategy = Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceRetrievalStrategy;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>
/// Which tenant a retrieved destination belongs to, and what that means for using it in another tenant (port of the
/// tenant table of <c>DestinationServicePrincipalPropagationTest</c>).
/// </summary>
public sealed class DestinationTenantTests : DestinationServiceTestBase
{
    private const string Destination = """
        {
          "destinationConfiguration": {
            "URL": "https://example.com",
            "Name": "test",
            "ProxyType": "Internet",
            "Authentication": "PrincipalPropagation",
            "Type": "HTTP"
          }
        }
        """;

    private static readonly DefaultTenant Some1 = new("foo");
    private static readonly DefaultTenant Some2 = new("bar");

    public static TheoryData<string?, string?, Strategy?, bool, bool> Cases() => new()
    {
        // destination tenant, runtime tenant, retrieval strategy, retrieval succeeds, destination remains usable at runtime
        // same provider tenant
        { null, null, null, true, true },
        { null, null, Strategy.AlwaysProvider, true, true },
        { null, null, Strategy.CurrentTenant, true, true },

        // same current tenant
        { "foo", "foo", null, true, true },
        { "foo", "foo", Strategy.AlwaysProvider, true, false },
        { "foo", "foo", Strategy.CurrentTenant, true, true },
        { "foo", "foo", Strategy.OnlySubscriber, true, true },

        // different current tenants
        { "foo", "bar", null, true, false },
        { "foo", "bar", Strategy.AlwaysProvider, true, false },
        { "foo", "bar", Strategy.CurrentTenant, true, false },
        { "foo", "bar", Strategy.OnlySubscriber, true, false },

        // mixed provider and current tenants
        { null, "foo", null, true, false },
        { null, "foo", Strategy.AlwaysProvider, true, false },
        { null, "foo", Strategy.CurrentTenant, true, false },
        { "foo", null, null, true, false },
        { "foo", null, Strategy.AlwaysProvider, true, true },
        { "foo", null, Strategy.CurrentTenant, true, false },
        { "foo", null, Strategy.OnlySubscriber, true, false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheTenantOfTheDestination_DecidesWhetherItCanBeUsedInTheRuntimeTenant(
        string? destinationTenant,
        string? runtimeTenant,
        Strategy? strategy,
        bool retrievalSucceeds,
        bool usableAtRuntime)
    {
        DestinationService.Cache.DisablePreLookupCheck();
        CurrentToken = PlainToken();
        var service = CreateService(new FakeAdapter().Respond("/v1/destinations/test", Destination));
        var builder = Augmenter();
        if (strategy is { } value)
        {
            builder.RetrievalStrategy(value);
        }
        CurrentTenant = destinationTenant is null ? null : new DefaultTenant(destinationTenant);

        var result = await service.TryGetDestinationAsync("test", Options(builder));

        Assert.Equal(retrievalSucceeds, result.IsSuccess);
        var http = Assert.IsType<DefaultHttpDestination>(result.GetValueOrThrow());
        Assert.True(http.TryGet(DestinationProperty.TenantId, out var tenantId));
        // The OAuth header providers compare this tenant with the one of the request; they refuse to be used for another tenant.
        var currentAtRuntime = runtimeTenant ?? string.Empty;
        Assert.Equal(usableAtRuntime, tenantId == currentAtRuntime);
    }

    [Fact]
    public async Task OnPremiseDestinations_GetTheirProxyFromTheConnectivityService()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        DefaultServiceBindingAccessor.Instance = new FixedBindingAccessor(
            new DefaultServiceBinding(
                "connectivity",
                "connectivity",
                "lite",
                [],
                new Dictionary<string, object?>
                {
                    ["clientid"] = "CLIENT_ID",
                    ["clientsecret"] = "CLIENT_SECRET",
                    ["url"] = "https://auth.example.com",
                    ["onpremise_proxy_host"] = "localhost",
                    ["onpremise_proxy_port"] = "8888",
                }));
        CurrentToken = PlainToken();
        var service = CreateService(new FakeAdapter().Respond(
            "/v1/destinations/test",
            """
            {
              "destinationConfiguration": {
                "URL": "https://example.com",
                "Name": "test",
                "ProxyType": "OnPremise",
                "Authentication": "PrincipalPropagation",
                "CloudConnectorLocationId": "LOC1",
                "Type": "HTTP"
              }
            }
            """));

        var destination = Assert.IsType<DefaultHttpDestination>((await service.TryGetDestinationAsync("test", NoOptions)).GetValueOrThrow());

        Assert.Equal(ProxyType.OnPremise, destination.ProxyType);
        Assert.Equal(new Uri("http://localhost:8888"), destination.ProxyConfiguration!.Uri);
        Assert.True(destination.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal(string.Empty, tenantId); // empty tenant id = provider
        Assert.Equal(3, destination.CustomHeaderProviders.Count); // location id, user token, proxy authorization
    }

    private sealed class FixedBindingAccessor(params IServiceBinding[] bindings) : IServiceBindingAccessor
    {
        public IReadOnlyList<IServiceBinding> GetServiceBindings() => bindings;
    }
}
