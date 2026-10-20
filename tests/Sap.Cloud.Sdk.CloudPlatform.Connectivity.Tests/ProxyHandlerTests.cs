using Microsoft.IdentityModel.JsonWebTokens;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using Sap.Cloud.Sdk.TestSupport;
using ProxyOptions = Sap.Cloud.Sdk.CloudPlatform.Connectivity.ServiceBindingDestinationOptions.Options.ProxyOptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class ProxyHandlerTests : ConnectivityStateReset
{
    private static DefaultHttpDestination.Builder OnPremiseBuilder() =>
        DefaultHttpDestination.CreateBuilder("http://my-target.com")
            .Name("name")
            .Property(DestinationProperty.ProxyType, ProxyType.OnPremise);

    private static CountingHandler Handler(List<ServiceBindingDestinationOptions>? seen = null, params IServiceBinding[] bindings) =>
        new(
            new FixedBindingAccessor(bindings.Length == 0 ? [Bindings.Of("connectivity"), Bindings.Of("destination")] : bindings),
            new FakeBindingLoader(options =>
            {
                seen?.Add(options);
                return options.TryGetOption<ProxyOptions, IHttpDestination>(out var proxied)
                    ? Result<IHttpDestination>.Success(proxied)
                    : Result<IHttpDestination>.Failure(new InvalidOperationException("no proxy option"));
            }));

    private static AuthToken Token() => new(new JsonWebToken(TestJwt.Create(new { sub = "user" })));

    [Fact]
    public void NonOnPremiseDestinations_AreNotHandled()
    {
        var handler = Handler();
        var builder = DefaultHttpDestination.CreateBuilder("http://x").Property(DestinationProperty.ProxyType, ProxyType.Internet);
        builder.ProxyHandler = handler;

        builder.Build();

        Assert.Equal(0, handler.HandleCalls);
    }

    [Fact]
    public void OnPremiseDestination_AddsLocationIdProviderExactlyOnce_AlsoWhenCopied()
    {
        var handler = Handler();
        var builder = OnPremiseBuilder().CloudConnectorLocationId("loc-1");
        builder.ProxyHandler = handler;
        var baseDestination = builder.Build();

        var copy1Builder = baseDestination.ToBuilder();
        copy1Builder.ProxyHandler = handler;
        var copy1 = copy1Builder.Build();
        var copy2Builder = copy1.ToBuilder();
        copy2Builder.ProxyHandler = handler;
        var copy2 = copy2Builder.Build();

        foreach (var destination in new[] { baseDestination, copy1, copy2 })
        {
            Assert.Single(destination.CustomHeaderProviders);
            Assert.Contains(new Header("SAP-Connectivity-SCC-Location_ID", "loc-1"), destination.GetHeaders());
        }
        Assert.Equal(3, handler.CanHandleCalls);
        Assert.Equal(1, handler.HandleCalls);
    }

    [Fact]
    public void OnPremiseDestination_WithoutLocationId_AddsNoLocationHeader()
    {
        var builder = OnPremiseBuilder();
        builder.ProxyHandler = Handler();

        Assert.DoesNotContain(builder.Build().GetHeaders(), h => h.Name == "SAP-Connectivity-SCC-Location_ID");
    }

    [Fact]
    public void TechnicalUser_OfCurrentTenant_IsTheDefault()
    {
        var seen = new List<ServiceBindingDestinationOptions>();
        var builder = OnPremiseBuilder();
        builder.ProxyHandler = Handler(seen);

        builder.Build();

        var options = Assert.Single(seen);
        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, options.OnBehalfOf);
        Assert.Equal("connectivity", options.ServiceBinding.ServiceName);
    }

    [Fact]
    public void EmptyTenantId_MeansProviderTenant()
    {
        var seen = new List<ServiceBindingDestinationOptions>();
        var builder = OnPremiseBuilder().Property(DestinationProperty.TenantId, "");
        builder.ProxyHandler = Handler(seen);

        builder.Build();

        Assert.Equal(OnBehalfOf.TechnicalUserProvider, Assert.Single(seen).OnBehalfOf);
    }

    [Fact]
    public void NonEmptyTenantId_MeansCurrentTenant()
    {
        var seen = new List<ServiceBindingDestinationOptions>();
        var builder = OnPremiseBuilder().Property(DestinationProperty.TenantId, "tenant");
        builder.ProxyHandler = Handler(seen);

        builder.Build();

        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, Assert.Single(seen).OnBehalfOf);
    }

    [Fact]
    public void PrincipalPropagationWithTokenExchange_IsNamedUser()
    {
        var seen = new List<ServiceBindingDestinationOptions>();
        var builder = OnPremiseBuilder()
            .AuthenticationType(AuthenticationType.PrincipalPropagation)
            .Property(DestinationProperty.PrincipalPropagationMode, PrincipalPropagationMode.TokenExchange);
        builder.ProxyHandler = Handler(seen);

        var destination = builder.Build();

        Assert.Equal(OnBehalfOf.NamedUserCurrentTenant, Assert.Single(seen).OnBehalfOf);
        Assert.DoesNotContain(destination.CustomHeaderProviders, p => p is DefaultHttpDestinationBuilderProxyHandler.SapConnectivityAuthenticationHeaderProvider);
    }

    [Fact]
    public void PrincipalPropagationWithTokenForwarding_ForwardsTheCurrentTokenToTheConnectivityProxy()
    {
        var seen = new List<ServiceBindingDestinationOptions>();
        var builder = OnPremiseBuilder().AuthenticationType(AuthenticationType.PrincipalPropagation);
        builder.ProxyHandler = Handler(seen);
        var token = Token();

        var destination = builder.Build();
        var headers = AuthTokenAccessor.ExecuteWithAuthToken(token, () => destination.GetHeaders());

        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, Assert.Single(seen).OnBehalfOf);
        Assert.Contains(new Header("SAP-Connectivity-Authentication", "Bearer " + token.Jwt.EncodedToken), headers);
    }

    [Fact]
    public void PrincipalPropagationWithUnknownMode_Fails()
    {
        var builder = OnPremiseBuilder()
            .AuthenticationType(AuthenticationType.PrincipalPropagation)
            .Property("cloudsdk.principalPropagationMode", "bogus");
        builder.ProxyHandler = Handler();

        var e = Assert.Throws<DestinationAccessException>(builder.Build);
        Assert.IsType<InvalidOperationException>(e.InnerException);
    }

    [Fact]
    public void WithoutConnectivityBinding_Fails()
    {
        var builder = OnPremiseBuilder();
        builder.ProxyHandler = Handler(null, Bindings.Of("destination"));

        var e = Assert.Throws<DestinationAccessException>(builder.Build);
        Assert.Contains("Unable to resolve proxy configuration", e.Message, StringComparison.Ordinal);
        Assert.Contains("No service bindings found matching Connectivity", e.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithMultipleConnectivityBindings_Fails()
    {
        var builder = OnPremiseBuilder();
        builder.ProxyHandler = Handler(null, Bindings.Of("connectivity"), Bindings.Of("connectivity"));

        var e = Assert.Throws<DestinationAccessException>(builder.Build);
        Assert.Contains("More than one", e.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultHandler_UsesTheRegisteredServiceBindingDestinationLoaders()
    {
        DefaultServiceBindingAccessor.Instance = new FixedBindingAccessor(Bindings.Of("connectivity"));
        IHttpDestination? proxied = null;
        ServiceBindingDestinationLoader.RegisterDefaultLoader(new FakeBindingLoader(o =>
        {
            o.TryGetOption<ProxyOptions, IHttpDestination>(out proxied);
            return Result<IHttpDestination>.Success(proxied!);
        }));

        var destination = OnPremiseBuilder().Build();

        Assert.NotNull(proxied);
        Assert.Equal(new Uri("http://my-target.com"), destination.Uri);
    }

    [Fact]
    public void DefaultHandler_WithoutLoaders_FailsWithAccessException()
    {
        DefaultServiceBindingAccessor.Instance = new FixedBindingAccessor(Bindings.Of("connectivity"));

        var e = Assert.Throws<DestinationAccessException>(() => OnPremiseBuilder().Build());
        Assert.Contains("no delegate loaders", e.InnerException!.Message, StringComparison.Ordinal);
    }

    private sealed class CountingHandler(IServiceBindingAccessor accessor, IServiceBindingDestinationLoader loader)
        : DefaultHttpDestinationBuilderProxyHandler
    {
        public int CanHandleCalls { get; private set; }

        public int HandleCalls { get; private set; }

        internal override bool CanHandle(DefaultHttpDestination.Builder builder)
        {
            CanHandleCalls++;
            return base.CanHandle(builder);
        }

        internal override DefaultHttpDestination Handle(DefaultHttpDestination.Builder builder)
        {
            HandleCalls++;
            return base.Handle(builder);
        }

        internal override IServiceBindingAccessor GetServiceBindingAccessor() => accessor;

        internal override IServiceBindingDestinationLoader GetServiceBindingDestinationLoader() => loader;
    }
}
