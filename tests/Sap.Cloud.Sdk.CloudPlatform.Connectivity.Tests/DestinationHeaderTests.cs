using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

/// <summary>Which headers <see cref="DefaultHttpDestination.GetHeaders(Uri)"/> produces per authentication type.</summary>
public class DestinationHeaderTests : ConnectivityStateReset
{
    private static readonly Uri RequestUri = new("https://example.org/api");

    private static DefaultHttpDestination.Builder Builder() => DefaultHttpDestination.CreateBuilder(RequestUri);

    private static IRequestHeaderContainer RequestWith(string name, params string[] values) =>
        DefaultRequestHeaderContainer.Builder().WithHeader(name, values).Build();

    [Fact]
    public void BasicAuthentication_AddsAuthorizationHeader()
    {
        var destination = Builder().BasicCredentials("user", "pass").Build();

        var header = Assert.Single(destination.GetHeaders());
        Assert.Equal(new Header("Authorization", "Basic dXNlcjpwYXNz"), header);
    }

    [Fact]
    public void BasicAuthentication_WithoutCredentials_Throws()
    {
        var destination = Builder().AuthenticationType(AuthenticationType.BasicAuthentication).Build();

        var e = Assert.Throws<DestinationAccessException>(() => destination.GetHeaders());
        Assert.Contains("no credentials available", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoAuthentication_AddsNoHeaders()
    {
        var destination = Builder().AuthenticationType(AuthenticationType.NoAuthentication).Build();

        Assert.Empty(destination.GetHeaders());
    }

    [Fact]
    public void NoAuthentication_WithBasicCredentialProperties_UpgradesToBasic()
    {
        var destination = Builder().Property("User", "u").Property("Password", "p").Build();

        Assert.Equal(AuthenticationType.BasicAuthentication, destination.AuthenticationType);
        Assert.Single(destination.GetHeaders());
    }

    [Theory]
    [InlineData("forwardAuthToken")]
    [InlineData("HTML5.ForwardAuthToken")]
    public void NoAuthentication_WithForwardAuthTokenProperty_UpgradesToTokenForwarding(string property)
    {
        var destination = Builder().Property(property, "true").Build();

        Assert.Equal(AuthenticationType.TokenForwarding, destination.AuthenticationType);
    }

    [Fact]
    public void OtherAuthenticationTypes_AddNoAuthorizationHeaderThemselves()
    {
        foreach (var type in new[]
        {
            AuthenticationType.OAuth2ClientCredentials, AuthenticationType.OAuth2SamlBearerAssertion,
            AuthenticationType.PrincipalPropagation, AuthenticationType.ClientCertificateAuthentication,
        })
        {
            Assert.Empty(Builder().AuthenticationType(type).Build().GetHeaders());
        }
    }

    [Fact]
    public void TokenForwarding_TakesTokenFromCurrentRequestHeaders()
    {
        var destination = Builder().AuthenticationType(AuthenticationType.TokenForwarding).Build();

        var result = RequestHeaderAccessor.ExecuteWithHeaderContainer(
            RequestWith("Authorization", "BeArEr someToken"),
            () => destination.GetHeaders());

        Assert.Equal(new[] { new Header("Authorization", "BeArEr someToken") }, result);
    }

    [Fact]
    public void TokenForwarding_ForwardsAllAuthorizationHeaders()
    {
        var destination = Builder().AuthenticationType(AuthenticationType.TokenForwarding).Build();

        var result = RequestHeaderAccessor.ExecuteWithHeaderContainer(
            RequestWith("Authorization", "some Value", "some other Value"),
            () => destination.GetHeaders());

        Assert.Equal(["Authorization", "Authorization"], result.Select(h => h.Name));
        Assert.Equal(["some Value", "some other Value"], result.Select(h => h.Value));
    }

    [Fact]
    public void TokenForwarding_WithoutAuthorizationHeader_AddsNothing()
    {
        var destination = Builder().AuthenticationType(AuthenticationType.TokenForwarding).Build();

        Assert.Empty(RequestHeaderAccessor.ExecuteWithHeaderContainer(
            DefaultRequestHeaderContainer.Empty,
            () => destination.GetHeaders()));
        Assert.Empty(RequestHeaderAccessor.ExecuteWithHeaderContainer(
            RequestWith("SomeOtherKey", "Some other value"),
            () => destination.GetHeaders()));
    }

    [Fact]
    public void TokenForwarding_WithoutRequestContext_AddsNothing()
    {
        var destination = Builder().AuthenticationType(AuthenticationType.TokenForwarding).Build();

        Assert.Empty(destination.GetHeaders());
    }

    [Fact]
    public void ExplicitAuthorizationHeader_SuppressesAuthTypeHeader()
    {
        var destination = Builder().BasicCredentials("user", "pass").Header("authorization", "Custom abc").Build();

        var header = Assert.Single(destination.GetHeaders());
        Assert.Equal("Custom abc", header.Value);
    }

    [Fact]
    public void AuthorizationFromHeaderProvider_SuppressesAuthTypeHeader()
    {
        var destination = Builder()
            .BasicCredentials("user", "pass")
            .HeaderProviders(new StaticHeaderProvider(new Header("Authorization", "Bearer from-provider")))
            .Build();

        Assert.Equal("Bearer from-provider", Assert.Single(destination.GetHeaders()).Value);
    }

    [Fact]
    public void HeaderOrder_IsCustomThenProvidersThenPropertiesThenAuthThenProxy()
    {
        var destination = Builder()
            .Header("custom", "1")
            .HeaderProviders(new StaticHeaderProvider(new Header("provider", "2")))
            .Property("URL.headers.fromProperty", "3")
            .BasicCredentials("u", "p")
            .Proxy(new Uri("http://proxy:1"))
            .ProxyAuthorization("Bearer x")
            .Build();

        Assert.Equal(
            ["custom", "provider", "fromProperty", "Authorization", "Proxy-Authorization"],
            destination.GetHeaders().Select(h => h.Name));
    }

    [Fact]
    public void HeadersFromProperties_AreMatchedCaseInsensitivelyAndLastValueWins()
    {
        var destination = Builder()
            .Property("URL.HeaDeRs.foo1", "bar1")
            .Property("url.headers.foo2", "bar2")
            .Property("URL.headers.foo2", "bar2b")
            .Header("foo3", "bar3")
            .Property("foo4", "bar4")
            .Build();

        var headers = destination.GetHeaders(RequestUri);

        Assert.Equal(3, headers.Count);
        Assert.Contains(new Header("foo1", "bar1"), headers);
        Assert.Contains(new Header("foo2", "bar2b"), headers);
        Assert.Contains(new Header("foo3", "bar3"), headers);
    }

    [Fact]
    public void SameHeaderAsPropertyAndHeader_IsDuplicated()
    {
        var destination = Builder().Property("URL.headers.foo", "bar").Header("foo", "bar").Build();

        Assert.Equal(2, destination.GetHeaders().Count(h => h == new Header("foo", "bar")));
    }

    [Fact]
    public void HeaderProvider_ReceivesDestinationAndRequestUri()
    {
        DestinationRequestContext? seen = null;
        var destination = Builder().HeaderProviders(new CapturingProvider(c => seen = c)).Build();

        destination.GetHeaders(new Uri("https://example.org/other"));

        Assert.NotNull(seen);
        Assert.Same(destination, seen.Destination);
        Assert.Equal(new Uri("https://example.org/other"), seen.RequestUri);
    }

    [Fact]
    public void HeaderProvider_Exception_IsWrapped()
    {
        var destination = Builder().HeaderProviders(new ThrowingHeaderProvider()).Build();

        var e = Assert.Throws<DestinationAccessException>(() => destination.GetHeaders());
        Assert.Contains("ThrowingHeaderProvider", e.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(e.InnerException);
    }

    [Fact]
    public void RegisteredHeaderProvider_AppliesToAllDestinations()
    {
        DestinationHeaderProviders.Register(new StaticHeaderProvider(new Header("global", "g")));

        Assert.Contains(new Header("global", "g"), Builder().Build().GetHeaders());
    }

    [Fact]
    public void RegisteredHeaderProvider_CanBeUnregistered()
    {
        var provider = new StaticHeaderProvider(new Header("global", "g"));
        DestinationHeaderProviders.Register(provider);
        Assert.True(DestinationHeaderProviders.Unregister(provider));

        Assert.DoesNotContain(new Header("global", "g"), Builder().Build().GetHeaders());
        Assert.False(DestinationHeaderProviders.Unregister(provider));
    }

    [Fact]
    public void ErpHeaderProvider_IsRegisteredByDefault_AndAddsSapClient()
    {
        // The module initializer registers the provider; the base class re-registers it after resets.
        Assert.Contains(DestinationHeaderProviders.Registered, p => p is ErpDestinationHeaderProvider);

        var destination = Builder().Property(DestinationProperty.SapClient, "123").Build();
        Assert.Equal(new Header("sap-client", "123"), Assert.Single(destination.GetHeaders()));
    }

    private sealed class CapturingProvider(Action<DestinationRequestContext> capture) : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext)
        {
            capture(requestContext);
            return [];
        }
    }
}
