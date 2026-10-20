using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

public sealed class OAuth2ServiceBindingDestinationLoaderTests : OAuthTestBase
{
    private readonly OAuth2ServiceBindingDestinationLoader _loader = new();

    [Fact]
    public void Xsuaa_SecretBinding()
    {
        var destination = _loader.TryGetDestination(Options(Binding("xsuaa", XsuaaCredentials()))).GetValueOrThrow();

        Assert.Equal("https://provider.authentication.eu10.hana.ondemand.com/", destination.Uri.AbsoluteUri);
        var provider = OAuthProvider(destination);
        Assert.Equal("Authorization", provider.AuthHeaderName);
        Assert.Equal("https://provider.authentication.eu10.hana.ondemand.com/oauth/token", provider.Service.TokenUri.AbsoluteUri);
        Assert.Equal("xsuaa-client", provider.Service.Identity.Id);
        Assert.Equal("xsuaa-secret", provider.Service.Identity.Secret);
        Assert.Equal(TenantPropagationStrategy.ZidHeader, provider.Service.TenantPropagationStrategy);
        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, provider.Service.OnBehalfOf);
        Assert.StartsWith("xsuaa-", destination.TryGet(DestinationProperty.Name, out var name) ? name : "", StringComparison.Ordinal);
    }

    [Fact]
    public void Xsuaa_TargetUriOption_OverridesServiceUri()
    {
        var destination = _loader.TryGetDestination(Options(Binding("xsuaa", XsuaaCredentials()), OnBehalfOf.TechnicalUserProvider,
            BtpServiceOptions.AuthenticationServiceOptions.WithTargetUri("https://app.example.com"))).GetValueOrThrow();
        Assert.Equal("https://app.example.com/", destination.Uri.AbsoluteUri);
        Assert.Equal(OnBehalfOf.TechnicalUserProvider, OAuthProvider(destination).Service.OnBehalfOf);
    }

    [Fact]
    public void X509Binding_UsesCertUrlAndCertificate()
    {
        var credentials = XsuaaCredentials();
        credentials.Remove("clientsecret");
        credentials["credential-type"] = "x509";
        credentials["certurl"] = "https://provider.authentication.cert.eu10.hana.ondemand.com";
        credentials["certificate"] = "-----BEGIN CERTIFICATE-----";
        credentials["key"] = "-----BEGIN PRIVATE KEY-----";

        var provider = OAuthProvider(_loader.TryGetDestination(Options(Binding("xsuaa", credentials))).GetValueOrThrow());

        Assert.Equal("https://provider.authentication.cert.eu10.hana.ondemand.com/oauth/token", provider.Service.TokenUri.AbsoluteUri);
        Assert.True(provider.Service.Identity.IsCertificateBased);
        Assert.Null(provider.Service.Identity.Secret);
    }

    [Fact]
    public void DestinationService_UsesUriAndRootCredentials()
    {
        var credentials = XsuaaCredentials();
        credentials["uri"] = "https://destination-configuration.cfapps.eu10.hana.ondemand.com";
        var destination = _loader.TryGetDestination(Options(Binding("destination", credentials))).GetValueOrThrow();
        Assert.Equal("https://destination-configuration.cfapps.eu10.hana.ondemand.com/", destination.Uri.AbsoluteUri);
    }

    [Fact]
    public void ConnectivityService_YieldsProxyUrl()
    {
        var credentials = XsuaaCredentials();
        credentials["onpremise_proxy_host"] = "connectivityproxy.internal";
        credentials["onpremise_proxy_http_port"] = 20003L;
        var destination = _loader.TryGetDestination(Options(Binding("connectivity", credentials))).GetValueOrThrow();
        Assert.Equal("http://connectivityproxy.internal:20003/", destination.Uri.AbsoluteUri);
    }

    [Fact]
    public void ConnectivityService_AsProxy_AddsProxyAuthorizationProvider()
    {
        var credentials = XsuaaCredentials();
        credentials["onpremise_proxy_host"] = "connectivityproxy.internal";
        credentials["onpremise_proxy_port"] = "20003";
        var onPremise = DefaultHttpDestination.CreateBuilder("http://onprem.example:8000").Name("onprem").Build();

        var destination = _loader.TryGetDestination(Options(Binding("connectivity", credentials), OnBehalfOf.TechnicalUserCurrentTenant,
            ServiceBindingDestinationOptions.Options.ProxyOptions.DestinationToBeProxied(onPremise))).GetValueOrThrow();

        Assert.Equal("http://onprem.example:8000/", destination.Uri.AbsoluteUri);
        Assert.Equal("Proxy-Authorization", OAuthProvider(destination).AuthHeaderName);
        Assert.Equal(new Uri("http://connectivityproxy.internal:20003"), destination.ProxyConfiguration!.Uri);
    }

    [Fact]
    public void IdentityService_UsesOAuth2TokenPathAndAppTid()
    {
        var credentials = Map(("clientid", "ias-client"), ("clientsecret", "s"), ("url", "https://provider.accounts.ondemand.com/"), ("app_tid", "provider-tid"));
        var provider = OAuthProvider(_loader.TryGetDestination(Options(Binding("identity", credentials), OnBehalfOf.TechnicalUserCurrentTenant,
            BtpServiceOptions.IasOptions.WithApplicationName("my-app"), BtpServiceOptions.IasOptions.WithTokenFormat("jwt"))).GetValueOrThrow());

        Assert.Equal("https://provider.accounts.ondemand.com/oauth2/token", provider.Service.TokenUri.AbsoluteUri);
        Assert.Equal(TenantPropagationStrategy.TenantSubdomain, provider.Service.TenantPropagationStrategy);
        Assert.Equal("provider-tid", provider.Service.AdditionalParameters["app_tid"]);
        Assert.Equal("urn:sap:identity:application:provider:name:my-app", provider.Service.AdditionalParameters["resource"]);
        Assert.Equal("jwt", provider.Service.AdditionalParameters["token_format"]);
    }

    [Fact]
    public void IdentityService_NoTokenForProvider_SkipsTokenRetrieval()
    {
        var credentials = Map(("clientid", "ias-client"), ("clientsecret", "s"), ("url", "https://provider.accounts.ondemand.com"), ("app_tid", "provider-tid"));
        var options = Options(Binding("identity", credentials), OnBehalfOf.TechnicalUserProvider,
            BtpServiceOptions.IasOptions.WithoutTokenForTechnicalProviderUser());
        var destination = (DefaultHttpDestination)_loader.TryGetDestination(options).GetValueOrThrow();
        Assert.Empty(destination.CustomHeaderProviders);

        // Named users always need a token.
        var named = (DefaultHttpDestination)_loader.TryGetDestination(Options(Binding("identity", credentials), OnBehalfOf.NamedUserCurrentTenant,
            BtpServiceOptions.IasOptions.WithoutTokenForTechnicalProviderUser())).GetValueOrThrow();
        Assert.Single(named.CustomHeaderProviders);

        // Current tenant = provider tenant (case-insensitive) → no token.
        var currentIsProvider = TenantAccessor.ExecuteWithTenant(new DefaultTenant("PROVIDER-TID", "p"), () =>
            (DefaultHttpDestination)_loader.TryGetDestination(Options(Binding("identity", credentials), OnBehalfOf.TechnicalUserCurrentTenant,
                BtpServiceOptions.IasOptions.WithoutTokenForTechnicalProviderUser())).GetValueOrThrow());
        Assert.Empty(currentIsProvider.CustomHeaderProviders);
    }

    [Fact]
    public void MultiUrlService_RequiresOption()
    {
        var credentials = Map(("uaa", Map(("clientid", "wf"), ("clientsecret", "s"), ("url", "https://p.authentication.example.com"))),
            ("endpoints", Map(("workflow_rest_url", "https://wf.example.com/rest"), ("workflow_odata_url", "https://wf.example.com/odata"))));
        var binding = Binding("workflow", credentials);

        Assert.IsType<DestinationAccessException>(_loader.TryGetDestination(Options(binding)).Exception);
        var destination = _loader.TryGetDestination(Options(binding, OnBehalfOf.TechnicalUserCurrentTenant, BtpServiceOptions.WorkflowOptions.ODataApi)).GetValueOrThrow();
        Assert.Equal("https://wf.example.com/odata", destination.Uri.AbsoluteUri);
    }

    [Fact]
    public void BusinessLogging_RemovesPath()
    {
        var credentials = Map(("uaa", Map(("clientid", "bl"), ("clientsecret", "s"), ("url", "https://p.authentication.example.com"))),
            ("endpoints", Map(("readservice", "https://bl.example.com/some/path"))));
        var destination = _loader.TryGetDestination(Options(Binding("business-logging", credentials), OnBehalfOf.TechnicalUserCurrentTenant,
            BtpServiceOptions.BusinessLoggingOptions.ReadApi)).GetValueOrThrow();
        Assert.Equal("https://bl.example.com/", destination.Uri.AbsoluteUri);
    }

    [Fact]
    public void UnknownService_IsNotFound()
    {
        Assert.IsType<DestinationNotFoundException>(_loader.TryGetDestination(Options(Binding("unknown-service", XsuaaCredentials()))).Exception);
    }

    [Fact]
    public void MissingSecret_IsAccessFailure()
    {
        var credentials = XsuaaCredentials();
        credentials.Remove("clientsecret");
        Assert.IsType<DestinationAccessException>(_loader.TryGetDestination(Options(Binding("xsuaa", credentials))).Exception);
    }

    [Fact]
    public void RegisteredSupplier_TakesPrecedence()
    {
        OAuth2ServiceBindingDestinationLoader.RegisterPropertySupplier(new ServiceIdentifier("custom"), o => new DefaultOAuth2PropertySupplier(o, []));
        var credentials = XsuaaCredentials();
        var destination = new OAuth2ServiceBindingDestinationLoader().TryGetDestination(Options(Binding("custom", credentials))).GetValueOrThrow();
        Assert.Equal("https://provider.authentication.eu10.hana.ondemand.com/", destination.Uri.AbsoluteUri);
    }

    [Fact]
    public void Loaders_AreRegisteredByDefault()
    {
        var destination = ServiceBindingDestinationLoader.DefaultLoaderChain.TryGetDestination(Options(Binding("xsuaa", XsuaaCredentials())));
        Assert.True(destination.IsSuccess, destination.Exception?.ToString());
    }

    [Fact]
    public void Secrets_AreNotPrintedByDestination()
    {
        var destination = _loader.TryGetDestination(Options(Binding("xsuaa", XsuaaCredentials()))).GetValueOrThrow();
        Assert.DoesNotContain("xsuaa-secret", destination.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("xsuaa-secret", OAuthProvider(destination).Service.Identity.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("binding-secret", CredentialType.BindingSecret)]
    [InlineData("X509_GENERATED", CredentialType.X509Generated)]
    [InlineData("x509_attested", CredentialType.X509Attested)]
    [InlineData("other", null)]
    public void ParseCredentialType(string value, CredentialType? expected) =>
        Assert.Equal(expected, DefaultOAuth2PropertySupplier.ParseCredentialType(value));
}
