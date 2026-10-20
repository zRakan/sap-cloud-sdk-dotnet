using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class DefaultHttpDestinationTests : ConnectivityStateReset
{
    private static readonly Uri ValidUri = new("https://www.sap.de");

    [Fact]
    public void FromProperties_CopiesAllProperties()
    {
        var filled = DefaultHttpDestination.CreateBuilder(ValidUri).Property("someKey", "someValue").Build();
        var copy = DefaultHttpDestination.FromProperties(filled).Build();

        Assert.NotSame(filled, copy);
        Assert.Equal("someValue", copy.GetProperty("someKey"));
    }

    [Fact]
    public void CanBeConstructedFrom_DestinationWithoutUri_IsFalse() =>
        Assert.False(DefaultHttpDestination.CanBeConstructedFrom(DefaultDestination.CreateBuilder().Build()));

    [Fact]
    public void CanBeConstructedFrom_DestinationWithUri_IsTrue() =>
        Assert.True(DefaultHttpDestination.CanBeConstructedFrom(DefaultHttpDestination.CreateBuilder(ValidUri).Build()));

    [Fact]
    public void Uri_ReturnsConfiguredUri() =>
        Assert.Equal(ValidUri, DefaultHttpDestination.CreateBuilder(ValidUri).Build().Uri);

    [Fact]
    public void Build_UriOfWrongType_Throws()
    {
        var baseDestination = DefaultDestination.CreateBuilder().Property("url", 42).Build();
        Assert.Throws<ArgumentException>(() => DefaultHttpDestination.FromProperties(baseDestination).Build());
    }

    [Fact]
    public void Build_WithoutUri_Throws()
    {
        Assert.Throws<ArgumentException>(() => DefaultHttpDestination.FromMap(new Dictionary<string, object>()).Build());
        Assert.Throws<ArgumentException>(
            () => DefaultHttpDestination.FromProperties(DefaultDestination.CreateBuilder().Build()).Build());
    }

    [Fact]
    public void Build_SetsTypeHttp()
    {
        var destination = DefaultHttpDestination.CreateBuilder(ValidUri).Build();

        Assert.True(destination.TryGet(DestinationProperty.Type, out var type));
        Assert.Equal(DestinationType.Http, type);
    }

    [Fact]
    public void Equals_SameConfiguration_IsEqualWithSameHashCode()
    {
        var header = new Header("name", "value");
        var first = DefaultHttpDestination.CreateBuilder(ValidUri).Header(header).Build();
        var second = DefaultHttpDestination.CreateBuilder(ValidUri).Header(header).Build();

        Assert.Equal(first, second);
        Assert.NotSame(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, DefaultHttpDestination.CreateBuilder("https://uri2").Build());
    }

    [Fact]
    public void Equals_KeyStoresWithSameCertificates_AreEqual()
    {
        var pair = TestCertificates.Create();
        var keyStore1 = KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.KeyPem);
        var keyStore2 = KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.KeyPem);
        var dest1 = DefaultHttpDestination.CreateBuilder(ValidUri).KeyStore(keyStore1).Build();
        var dest2 = DefaultHttpDestination.CreateBuilder(ValidUri).KeyStore(keyStore2).Build();
        var dest3 = DefaultHttpDestination.CreateBuilder(ValidUri).KeyStore(new X509Certificate2Collection()).Build();

        Assert.Equal(dest1, dest2);
        Assert.Equal(dest1.GetHashCode(), dest2.GetHashCode());
        Assert.NotEqual(dest1, dest3);
        Assert.NotEqual(dest1.GetHashCode(), dest3.GetHashCode());
    }

    [Fact]
    public void KeyStoreAndPassword_CanBeSetAndRead()
    {
        var keyStore = new X509Certificate2Collection();
        var destination = DefaultHttpDestination.CreateBuilder("some-uri").KeyStore(keyStore).KeyStorePassword("some-password").Build();

        Assert.Same(keyStore, destination.KeyStore);
        Assert.Equal("some-password", destination.KeyStorePassword);
    }

    [Fact]
    public void KeyStoreSupplier_IsInvokedOnEachAccess()
    {
        var first = new X509Certificate2Collection();
        var second = new X509Certificate2Collection();
        var current = first;
        var destination = DefaultHttpDestination.CreateBuilder("some-uri").KeyStoreSupplier(() => current).Build();

        Assert.Same(first, destination.KeyStore);
        current = second;
        Assert.Same(second, destination.KeyStore);
    }

    [Fact]
    public void TrustStore_CanBeSetAndRead()
    {
        var trustStore = new X509Certificate2Collection();
        var destination = DefaultHttpDestination.CreateBuilder("some-uri").TrustStore(trustStore).TrustStorePassword("pw").Build();

        Assert.Same(trustStore, destination.TrustStore);
        Assert.Equal("pw", destination.TrustStorePassword);
        Assert.Null(DefaultHttpDestination.CreateBuilder("some-uri").Build().TrustStore);
    }

    [Fact]
    public void SecurityConfigurationStrategy_DefaultsToFromDestination()
    {
        Assert.Equal(SecurityConfigurationStrategy.FromDestination, DefaultHttpDestination.CreateBuilder("some-uri").Build().SecurityConfigurationStrategy);
        Assert.Equal(
            SecurityConfigurationStrategy.FromPlatform,
            DefaultHttpDestination.CreateBuilder("some-uri").SecurityConfiguration(SecurityConfigurationStrategy.FromPlatform).Build().SecurityConfigurationStrategy);
    }

    [Fact]
    public void TrustAll_ViaBuilderOrFallbackProperty()
    {
        Assert.False(DefaultHttpDestination.CreateBuilder("u").Build().IsTrustingAllCertificates);
        Assert.True(DefaultHttpDestination.CreateBuilder("u").TrustAllCertificates().Build().IsTrustingAllCertificates);
        Assert.True(DefaultHttpDestination.CreateBuilder("u").Property("isTrustingAllCertificates", "TRUE").Build().IsTrustingAllCertificates);
        Assert.False(DefaultHttpDestination.CreateBuilder("u").Property("TrustAll", "nope").Build().IsTrustingAllCertificates);
    }

    [Fact]
    public void FromDestination_WithModification_KeepsEverythingElse()
    {
        var uri = new Uri("foo", UriKind.Relative);
        var header = new Header("foo", "bar");
        var first = DefaultHttpDestination.CreateBuilder(uri)
            .AuthenticationType(AuthenticationType.BasicAuthentication)
            .BasicCredentials(new BasicCredentials("foo", "bar"))
            .KeyStorePassword("bar")
            .Header(header)
            .Property("foo", "bar")
            .Build();

        var second = DefaultHttpDestination.FromDestination(first)
            .AuthenticationType(AuthenticationType.OAuth2SamlBearerAssertion)
            .Build();

        Assert.Equal(uri, second.Uri);
        Assert.Equal(AuthenticationType.OAuth2SamlBearerAssertion, second.AuthenticationType);
        Assert.Equal("bar", second.KeyStorePassword);
        Assert.Equal("bar", second.GetProperty("foo"));
        Assert.Contains(header, second.GetHeaders(uri));
        Assert.Equal("foo", second.BasicCredentials!.Username);
        Assert.Equal("bar", second.BasicCredentials.Password);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FromDestination_WithoutModification_ReproducesDestination()
    {
        var uri = new Uri("URI", UriKind.Relative);
        var customHeader = new Header("custom-header", "custom-header-value");
        var providerHeader = new Header("header-from-header-provider", "value-from-header-provider");
        var provider = new StaticHeaderProvider(providerHeader);
        var trustStore = new X509Certificate2Collection();
        var keyStore = new X509Certificate2Collection();
        var first = DefaultHttpDestination.CreateBuilder(uri)
            .AuthenticationType(AuthenticationType.BasicAuthentication)
            .BasicCredentials(new BasicCredentials("basic-username", "basic-password"))
            .KeyStorePassword("keystore-password")
            .Header(customHeader)
            .Property("foo", "bar")
            .HeaderProviders(provider)
            .SecurityConfiguration(SecurityConfigurationStrategy.FromPlatform)
            .TrustStore(trustStore)
            .KeyStore(keyStore)
            .ProxyAuthorization("Bearer some-token")
            .Proxy(new Uri("http://proxy:8080"))
            .Property("URL.headers.foo1", "bar1")
            .Build();

        var second = DefaultHttpDestination.FromDestination(first).Build();

        Assert.Equal(AuthenticationType.BasicAuthentication, second.AuthenticationType);
        Assert.Equal("keystore-password", second.KeyStorePassword);
        Assert.Contains(customHeader, second.CustomHeaders);
        Assert.Same(provider, Assert.Single(second.CustomHeaderProviders));
        Assert.Equal(SecurityConfigurationStrategy.FromPlatform, second.SecurityConfigurationStrategy);
        Assert.Same(trustStore, second.TrustStore);
        Assert.Same(keyStore, second.KeyStore);
        Assert.Equal(new Uri("http://proxy:8080"), second.ProxyConfiguration!.Uri);
        Assert.Equal(new BearerCredentials("Bearer some-token"), second.ProxyConfiguration.Credentials);

        var headers = second.GetHeaders(uri);
        Assert.Equal(5, headers.Count);
        Assert.Contains(new Header("Proxy-Authorization", "Bearer some-token"), headers);
        Assert.Contains(new Header("foo1", "bar1"), headers);
        Assert.Single(headers, h => h.Name == "Authorization");
        Assert.Contains(customHeader, headers);
        Assert.Contains(providerHeader, headers);
        Assert.Equal(first, second);
    }

    [Fact]
    public void FromDestination_AddingHeadersAndProviders_DoesNotChangeTheOriginal()
    {
        var header1 = new Header("foo", "bar");
        var header2 = new Header("foo1", "bar1");
        var custom1 = new Header("custom-header1", "custom-value1");
        var custom2 = new Header("custom-header2", "custom-value2");
        var first = DefaultHttpDestination.CreateBuilder("some-uri")
            .HeaderProviders(new StaticHeaderProvider(header1))
            .Header(custom1)
            .Build();

        var second = DefaultHttpDestination.FromDestination(first)
            .HeaderProviders(new StaticHeaderProvider(header2))
            .Header(custom2)
            .Build();

        Assert.Equal(2, second.CustomHeaderProviders.Count);
        Assert.Equal(new[] { custom1, custom2 }, second.CustomHeaders);
        Assert.Equal(new[] { custom1, custom2, header1, header2 }, second.GetHeaders());
        Assert.Single(first.CustomHeaderProviders);
        Assert.Equal(new[] { custom1, header1 }, first.GetHeaders());
        Assert.NotEqual(first, second);
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void FromDestination_ForOtherImplementation_Throws()
    {
        Assert.Throws<ArgumentException>(() => DefaultHttpDestination.FromDestination(new OtherDestination()));
    }

    [Fact]
    public void ToBuilder_ContainsAllProperties()
    {
        var header = new Header("StaticHeader", "value");
        var provider = new StaticHeaderProvider();
        var keyStore = new X509Certificate2Collection();
        var trustStore = new X509Certificate2Collection();
        var destination = DefaultHttpDestination.CreateBuilder("foo.bar")
            .Property("foo", "bar")
            .Property("bar", 42)
            .Header(header)
            .HeaderProviders(provider)
            .KeyStore(keyStore)
            .TrustStore(trustStore)
            .TrustAllCertificates()
            .Build();

        var copy = destination.ToBuilder().Build();

        Assert.Equal("foo.bar", copy.GetProperty("URL"));
        Assert.Equal("bar", copy.GetProperty("foo"));
        Assert.Equal(42, copy.GetProperty("bar"));
        Assert.Equal(new[] { header }, copy.CustomHeaders);
        Assert.Same(provider, Assert.Single(copy.CustomHeaderProviders));
        Assert.Same(keyStore, copy.KeyStore);
        Assert.Same(trustStore, copy.TrustStore);
        Assert.True(copy.IsTrustingAllCertificates);
    }

    [Fact]
    public void ProxyConfiguration_ViaThreeDifferentWays_IsEqual()
    {
        var expected = new ProxyConfiguration(new Uri("http://proxy.internet.com:8080"));

        var viaConvenience = DefaultHttpDestination.CreateBuilder(ValidUri).ProxyConfiguration(expected).Build();
        var viaUri = DefaultHttpDestination.CreateBuilder(ValidUri).Property(DestinationProperty.ProxyUri, new Uri("http://proxy.internet.com:8080")).Build();
        var viaHostAndPort = DefaultHttpDestination.CreateBuilder(ValidUri).Proxy("proxy.internet.com", 8080).Build();

        Assert.Equal(expected, viaConvenience.ProxyConfiguration);
        Assert.Equal(expected, viaUri.ProxyConfiguration);
        Assert.Equal(expected, viaHostAndPort.ProxyConfiguration);
    }

    [Fact]
    public void ProxyConfiguration_WithoutScheme_DefaultsToHttp()
    {
        var destination = DefaultHttpDestination.CreateBuilder(ValidUri).Property(DestinationProperty.ProxyUri, new Uri("proxy:1234")).Build();

        var proxyUri = destination.ProxyConfiguration!.Uri;
        Assert.Equal("http", proxyUri.Scheme);
        Assert.Equal("proxy", proxyUri.Host);
        Assert.Equal(1234, proxyUri.Port);
    }

    [Fact]
    public void ProxyConfiguration_WithoutProxyProperties_IsNull() =>
        Assert.Null(DefaultHttpDestination.CreateBuilder(ValidUri).Build().ProxyConfiguration);

    [Fact]
    public void ProxyAuthorization_IsAddedAsHeader()
    {
        var destination = DefaultHttpDestination.CreateBuilder("foo")
            .Property(DestinationProperty.ProxyUri, new Uri("https://proxy"))
            .Property(DestinationProperty.ProxyAuth, "Bearer 1234")
            .Build();

        var header = Assert.Single(destination.GetHeaders(), h => h.Name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Bearer 1234", header.Value);
    }

    [Fact]
    public void ProxyConfiguration_OverwritesProxyUriAndAuthButNotFallbackProperties()
    {
        var baseProperties = DefaultDestination.CreateBuilder()
            .Name("name")
            .Uri("my-target.com")
            .Property(DestinationProperty.ProxyUri, new Uri("http://initial.uri:1234"))
            .Property(DestinationProperty.ProxyHost, "initial.host")
            .Property(DestinationProperty.ProxyPort, 4321)
            .Property(DestinationProperty.ProxyAuth, "Bearer initial-token")
            .Build();
        var proxy = new ProxyConfiguration(new Uri("http://overwritten.uri:5678"), new BearerCredentials("Bearer overwritten-token"));

        var destination = DefaultHttpDestination.FromProperties(baseProperties).Uri("my-target.com").ProxyConfiguration(proxy).Build();

        Assert.Equal(proxy, destination.ProxyConfiguration);
        Assert.True(destination.TryGet(DestinationProperty.ProxyUri, out var proxyUri));
        Assert.Equal(new Uri("http://overwritten.uri:5678"), proxyUri);
        Assert.True(destination.TryGet(DestinationProperty.ProxyAuth, out var proxyAuth));
        Assert.Equal("Bearer overwritten-token", proxyAuth);
        Assert.True(destination.TryGet(DestinationProperty.ProxyHost, out var host));
        Assert.Equal("initial.host", host);
        Assert.True(destination.TryGet(DestinationProperty.ProxyPort, out var port));
        Assert.Equal(4321, port);
    }

    [Fact]
    public void ProxyConfiguration_WithoutCredentials_RemovesProxyAuth()
    {
        var destination = DefaultHttpDestination.CreateBuilder("foo")
            .ProxyAuthorization("Bearer x")
            .ProxyConfiguration(new ProxyConfiguration(new Uri("http://proxy:1")))
            .Build();

        Assert.False(destination.TryGet(DestinationProperty.ProxyAuth, out _));
        Assert.Empty(destination.GetHeaders());
    }

    [Fact]
    public void ProxyConfiguration_WithUnsupportedCredentials_Throws()
    {
        var builder = DefaultHttpDestination.CreateBuilder("foo.bar");
        ISecurityCredentials[] unsupported = [new ClientCredentials("id", "secret"), new ClientCertificate("id", "cert", "key")];

        foreach (var credentials in unsupported)
        {
            var e = Assert.Throws<ArgumentException>(
                () => builder.ProxyConfiguration(ProxyConfiguration.Of("foo.bar", credentials)));
            Assert.Contains("The provided proxy credentials", e.Message, StringComparison.Ordinal);
            Assert.Contains($" ({credentials.GetType().Name}) ", e.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", e.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProxyConfiguration_BasicCredentials_AreEncoded()
    {
        var uri = new Uri("foo.bar", UriKind.Relative);
        var destination = DefaultHttpDestination.CreateBuilder(uri)
            .ProxyConfiguration(ProxyConfiguration.Of(new Uri("http://proxy:1"), new BasicCredentials("user", "pass")))
            .Build();

        Assert.True(destination.TryGet(DestinationProperty.ProxyAuth, out var auth));
        Assert.Equal("Basic dXNlcjpwYXNz", auth);
        Assert.Equal(new BasicCredentials("user", "pass"), destination.ProxyConfiguration!.Credentials);
    }

    [Theory]
    [InlineData("token", "Bearer token")]
    [InlineData("Bearer token", "Bearer token")]
    [InlineData("bEArEr token", "Bearer token")]
    [InlineData("Bearertoken", "Bearer Bearertoken")]
    public void ProxyConfiguration_BearerPrefixIsNormalized(string token, string expected)
    {
        var destination = DefaultHttpDestination.CreateBuilder("foo.bar")
            .ProxyConfiguration(ProxyConfiguration.Of(new Uri("http://proxy:1"), new BearerCredentials(token)))
            .Build();

        Assert.True(destination.TryGet(DestinationProperty.ProxyAuth, out var auth));
        Assert.Equal(expected, auth);
    }

    [Fact]
    public void ProxyBasicAuthWithColonInPassword_IsParsedAtFirstColon()
    {
        var encoded = Convert.ToBase64String("user:pa:ss"u8);
        var destination = DefaultHttpDestination.CreateBuilder("foo")
            .Proxy(new Uri("http://proxy:1"))
            .ProxyAuthorization("Basic " + encoded)
            .Build();

        Assert.Equal(new BasicCredentials("user", "pa:ss"), destination.ProxyConfiguration!.Credentials);
    }

    [Fact]
    public void ProxyBasicAuthWithoutColon_Throws()
    {
        var builder = DefaultHttpDestination.CreateBuilder("foo")
            .Proxy(new Uri("http://proxy:1"))
            .ProxyAuthorization("Basic " + Convert.ToBase64String("nocolon"u8));

        Assert.Throws<DestinationAccessException>(builder.Build);
    }

    [Fact]
    public void ProxyAuthorizationOfUnknownScheme_IsIgnored()
    {
        var destination = DefaultHttpDestination.CreateBuilder("foo")
            .Proxy(new Uri("http://proxy:1"))
            .ProxyAuthorization("Digest abcdefgh")
            .Build();

        Assert.Null(destination.ProxyConfiguration!.Credentials);
        Assert.Empty(destination.GetHeaders());
    }

    [Fact]
    public void ProxyType_DefaultsToInternet_AndIsParsedLeniently()
    {
        Assert.Equal(ProxyType.Internet, DefaultHttpDestination.CreateBuilder("u").Build().ProxyType);
        Assert.Equal(ProxyType.PrivateLink, DefaultHttpDestination.CreateBuilder("u").Property("ProxyType", "PrivateLink").Build().ProxyType);
    }

    [Fact]
    public void ToString_PrintsOnlySafeProperties()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://user:uri-secret@host.example/path")
            .Name("my-destination")
            .BasicCredentials("the-user", "basic-secret-password")
            .KeyStorePassword("keystore-secret")
            .TrustStorePassword("truststore-secret")
            .ProxyAuthorization("Bearer proxy-secret-token")
            .Header("X-Secret", "header-secret")
            .Property("clientsecret", "client-secret")
            .Build();

        var text = destination.ToString();

        Assert.Contains("name=my-destination", text, StringComparison.Ordinal);
        Assert.Contains("host.example/path", text, StringComparison.Ordinal);
        Assert.Contains("authType=BasicAuthentication", text, StringComparison.Ordinal);
        foreach (var secret in new[] { "uri-secret", "basic-secret-password", "the-user", "keystore-secret", "truststore-secret", "proxy-secret-token", "header-secret", "client-secret" })
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ToString_OfCredentialCarryingTypes_HidesSecrets()
    {
        var proxy = new ProxyConfiguration(new Uri("http://proxy:1"), new BasicCredentials("u", "proxy-password"));
        Assert.DoesNotContain("proxy-password", proxy.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer-secret", new ProxyConfiguration(new Uri("http://p:1"), new BearerCredentials("Bearer-secret")).ToString(), StringComparison.Ordinal);
        var header = new Header("Authorization", "Bearer abc");
        Assert.DoesNotContain("abc", header.ToString(), StringComparison.Ordinal);
        Assert.Contains("Authorization", header.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_OfDefaultDestination_HidesSecretProperties()
    {
        var destination = DefaultDestination.CreateBuilder()
            .Name("dest")
            .Uri("https://example.org")
            .Property("Password", "pw-secret")
            .Property("clientSecret", "cs-secret")
            .Property("cloudsdk.authTokens", new List<string> { "token-secret" })
            .Property("access_token", "at-secret")
            .Property("tokenServiceURL", "https://token.example")
            .Property("ProxyAuthorization", "Bearer pa-secret")
            .Build();

        var text = destination.ToString();

        Assert.Contains("Name=dest", text, StringComparison.Ordinal);
        Assert.Contains("https://example.org", text, StringComparison.Ordinal);
        Assert.Contains("https://token.example", text, StringComparison.Ordinal);
        foreach (var secret in new[] { "pw-secret", "cs-secret", "token-secret", "at-secret", "pa-secret" })
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("pw-secret", destination.ToBuilder().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceBinding_ToString_HidesCredentials() =>
        Assert.DoesNotContain("binding-secret", Bindings.Of("xsuaa").ToString(), StringComparison.Ordinal);

    private sealed class OtherDestination : IDestination
    {
        public IReadOnlyCollection<string> PropertyNames => [];

        public object? GetProperty(string key) => null;
    }
}
