using System.Text.Json;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using static Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceV1Response;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>Building destinations from the response of the destination service (port of <c>DestinationServiceFactoryTest</c>).</summary>
public sealed class DestinationServiceFactoryTests : DestinationServiceTestBase
{
    private static DestinationServiceV1Response Response(
        Dictionary<string, string> configuration,
        IReadOnlyList<DestinationAuthToken>? tokens = null,
        IReadOnlyList<DestinationCertificate>? certificates = null) =>
        new() { DestinationConfiguration = configuration, AuthTokens = tokens, Certificates = certificates };

    [Fact]
    public void TenantIsSet()
    {
        var response = Response(new() { ["Type"] = "HTTP", ["URL"] = "http://foo/" });

        var result = TenantAccessor.ExecuteWithTenant(
            new DefaultTenant("tenant1"),
            () => DestinationServiceFactory.FromDestinationServiceV1Response(response));

        Assert.IsType<DefaultHttpDestination>(result);
        Assert.True(result.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal("tenant1", tenantId);
    }

    [Fact]
    public void ProviderTenantIsEmptyString()
    {
        var response = Response(new() { ["Type"] = "HTTP", ["URL"] = "http://foo/" });

        var result = DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.IsType<DefaultHttpDestination>(result);
        Assert.True(result.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal(string.Empty, tenantId);
    }

    [Fact]
    public void ProviderRetrieval_IsExpressedByAnEmptyTenant_EvenIfThereIsACurrentTenant()
    {
        var response = Response(new() { ["Type"] = "HTTP", ["URL"] = "http://foo/" });

        var result = TenantAccessor.ExecuteWithTenant(
            new DefaultTenant("tenant1"),
            () => DestinationServiceFactory.FromDestinationServiceV1Response(response, OnBehalfOf.TechnicalUserProvider));

        Assert.True(result.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal(string.Empty, tenantId);
    }

    [Fact]
    public void DefaultDestination_IsCreatedForDestinationsWithoutType()
    {
        var result = DestinationServiceFactory.FromDestinationServiceV1Response(Response(new() { ["foo"] = "bar" }));

        Assert.IsType<DefaultDestination>(result);
        Assert.Equal("bar", result.GetProperty("foo"));
    }

    [Fact]
    public void RfcDestination_IsAGenericDestination()
    {
        var result = DestinationServiceFactory.FromDestinationServiceV1Response(
            Response(new() { ["Type"] = "RFC", ["Name"] = "rfcDestination" }));

        Assert.IsType<DefaultDestination>(result);
        Assert.True(result.TryGet(DestinationProperty.Type, out var type));
        Assert.Equal(DestinationType.Rfc, type);
        Assert.True(result.TryGet(DestinationProperty.Name, out var name));
        Assert.Equal("rfcDestination", name);
    }

    [Fact]
    public void HttpDestination_StoresTokensAndCertificates_AndChangeDetectionProperties()
    {
        var token = new DestinationAuthToken();
        var certificate = new DestinationCertificate();
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "httpDestination", ["URL"] = "https://example.com" },
            [token],
            [certificate]);

        var result = DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.IsType<DefaultHttpDestination>(result);
        Assert.True(result.TryGet(DestinationProperty.Type, out var type));
        Assert.Equal(DestinationType.Http, type);
        Assert.True(result.TryGet(DestinationProperty.Uri, out var uri));
        Assert.Equal("https://example.com", uri);
        Assert.True(result.TryGet(DestinationProperty.AuthTokens, out var tokens));
        Assert.Equal([token], tokens);
        Assert.True(result.TryGet(DestinationProperty.Certificates, out var certificates));
        Assert.Equal([certificate], certificates);
        Assert.True(result.TryGet(DestinationProperty.PropertiesForChangeDetection, out var relevant));
        Assert.Equal(["Name", "Type", "URL"], relevant.Order());
    }

    [Fact]
    public void HttpDestination_WithTokens_GetsTheAuthTokenHeaderProvider()
    {
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "d", ["URL"] = "https://example.com", ["Authentication"] = "OAuth2ClientCredentials" },
            [new DestinationAuthToken { HttpHeaderSuggestion = new Header("Authorization", "Bearer abc") }]);

        var destination = (DefaultHttpDestination)DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.IsType<AuthTokenHeaderProvider>(Assert.Single(destination.CustomHeaderProviders));
        Assert.Equal([new Header("Authorization", "Bearer abc")], destination.GetHeaders());
    }

    [Fact]
    public void TokenErrors_AreThrown()
    {
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "httpDestination", ["URL"] = "https://example.com" },
            [new DestinationAuthToken { Value = "success" }, new DestinationAuthToken { Error = "some-error-message" }]);

        var failure = Assert.Throws<DestinationAccessException>(() => DestinationServiceFactory.FromDestinationServiceV1Response(response));

        Assert.Contains("some-error-message", failure.Message, StringComparison.Ordinal);
        Assert.Contains("httpDestination", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardAuthTokenProperty_SetsTheAuthenticationType()
    {
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "httpDestination", ["URL"] = "https://example.com", ["forwardAuthToken"] = "true" });

        var result = DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.Equal(AuthenticationType.TokenForwarding, result.AsHttp().AuthenticationType);
    }

    [Fact]
    public void Regression_AuthTokensAreAlwaysStored_ForNonHttpDestinations()
    {
        var token = new DestinationAuthToken();

        var result = DestinationServiceFactory.FromDestinationServiceV1Response(Response(new() { ["Type"] = "MAIL" }, [token]));

        Assert.True(result.TryGet(DestinationProperty.AuthTokens, out var tokens));
        Assert.Equal([token], tokens);
    }

    [Fact]
    public void Regression_AuthTokenErrorOnNonHttpDestination_LeadsToAnException()
    {
        var failure = Assert.Throws<DestinationAccessException>(() => DestinationServiceFactory.FromDestinationServiceV1Response(
            Response(new() { ["Type"] = "MAIL" }, [new DestinationAuthToken { Error = "some error" }])));

        Assert.Contains("some error", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenExpiry_IsDerivedFromExpiresIn_OrTheCacheExpiration()
    {
        var before = DateTimeOffset.UtcNow;
        var explicitExpiry = new DestinationAuthToken { ExpiresIn = "3600" };
        var defaultExpiry = new DestinationAuthToken { ExpiresIn = "" };
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "d", ["URL"] = "https://example.com", ["Authentication"] = "OAuth2ClientCredentials" },
            [explicitExpiry, defaultExpiry]);

        DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.InRange(explicitExpiry.ExpiryTimestamp!.Value, before.AddSeconds(3599), before.AddSeconds(3601 + 5));
        // tokens without expiration are assumed to expire after the cache expiration
        Assert.InRange(
            defaultExpiry.ExpiryTimestamp!.Value,
            before + DestinationService.Cache.DefaultExpirationDuration - TimeSpan.FromSeconds(1),
            before + DestinationService.Cache.DefaultExpirationDuration + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BasicAuthenticationTokens_WithoutExpiresIn_NeverExpire()
    {
        var token = new DestinationAuthToken { HttpHeaderSuggestion = new Header("Authorization", "Basic abc") };
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "d", ["URL"] = "https://example.com", ["Authentication"] = "BasicAuthentication", ["User"] = "u", ["Password"] = "p" },
            [token]);

        DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.Null(token.ExpiryTimestamp);
    }

    [Fact]
    public void TokenExpiry_WithDisabledCacheExpiration_IsUnbounded()
    {
        DestinationService.Cache.DisableExpiration();
        var token = new DestinationAuthToken();
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "d", ["URL"] = "https://example.com", ["Authentication"] = "OAuth2ClientCredentials" },
            [token]);

        DestinationServiceFactory.FromDestinationServiceV1Response(response);

        Assert.Null(token.ExpiryTimestamp);
    }

    [Fact]
    public void InvalidExpiresIn_IsRejected()
    {
        var response = Response(
            new() { ["Type"] = "HTTP", ["Name"] = "d", ["URL"] = "https://example.com", ["Authentication"] = "OAuth2ClientCredentials" },
            [new DestinationAuthToken { ExpiresIn = "soon" }]);

        Assert.Throws<DestinationAccessException>(() => DestinationServiceFactory.FromDestinationServiceV1Response(response));
    }

    [Fact]
    public void CertificateExpiry_LimitsTheCacheDuration_OnlyWithChangeDetection()
    {
        var withChangeDetection = new DestinationCertificate { Name = "c.crt" };
        var withoutChangeDetection = new DestinationCertificate { Name = "c.crt" };
        var configuration = new Dictionary<string, string> { ["Type"] = "HTTP", ["Name"] = "d", ["URL"] = "https://example.com" };

        DestinationServiceFactory.FromDestinationServiceV1Response(Response(configuration, certificates: [withChangeDetection]));
        DestinationService.Cache.DisableChangeDetection();
        DestinationServiceFactory.FromDestinationServiceV1Response(Response(configuration, certificates: [withoutChangeDetection]));

        Assert.NotNull(withChangeDetection.ExpiryTimestamp);
        Assert.Null(withoutChangeDetection.ExpiryTimestamp);
    }

    [Fact]
    public void KeyStoreAndTrustStore_AreExtractedFromCertificates()
    {
        var response = Response(
            new()
            {
                ["Type"] = "HTTP",
                ["Name"] = "d",
                ["URL"] = "https://example.com",
                ["Authentication"] = "ClientCertificateAuthentication",
                ["KeyStoreLocation"] = "client.p12",
                ["KeyStorePassword"] = "cca-password",
                ["TrustStoreLocation"] = "trust.crt",
            },
            certificates:
            [
                new DestinationCertificate { Name = "client.p12", Content = KeyStoreTestData.Pkcs12WithPassword, Type = "CERTIFICATE" },
                new DestinationCertificate { Name = "trust.crt", Content = KeyStoreTestData.Certificate, Type = "CERTIFICATE" },
            ]);

        var destination = DestinationServiceFactory.FromDestinationServiceV1Response(response).AsHttp();

        Assert.NotNull(destination.KeyStore);
        Assert.True(destination.KeyStore[0].HasPrivateKey);
        Assert.Single(destination.TrustStore!);
    }

    [Fact]
    public void ConfigurationWithoutName_StillBuildsADestination()
    {
        var result = DestinationServiceFactory.FromDestinationServiceV1Response(Response(new() { ["Type"] = "HTTP", ["URL"] = "https://example.com" }));

        Assert.False(result.TryGet(DestinationProperty.Name, out _));
    }
}

/// <summary>Parsing the JSON of the destination service (port of <c>DestinationServiceV1ResponseTest</c>).</summary>
public sealed class DestinationServiceV1ResponseTests
{
    [Fact]
    public void Parsing_ReadsConfigurationTokensAndErrors()
    {
        const string payload = """
            {
              "owner": { "SubaccountId": "1234", "InstanceId": null },
              "destinationConfiguration": {
                "Name": "DummySAPAssertionSSODestination",
                "Type": "HTTP",
                "URL": "https://some.system.com",
                "SystemUser": "SomeUser",
                "Authentication": "SAPAssertionSSO"
              },
              "authTokens": [
                {
                  "type": "MYSAPSSO2",
                  "value": "someString",
                  "http_header": { "key": "Cookie", "value": "MYSAPSSO2=someString" }
                },
                { "error":"Simulated failure." }
              ]
            }
            """;

        var sut = Parse(payload);

        Assert.Equal("1234", sut.Owner!.SubaccountId);
        Assert.Null(sut.Owner.InstanceId);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Name"] = "DummySAPAssertionSSODestination",
                ["Type"] = "HTTP",
                ["URL"] = "https://some.system.com",
                ["SystemUser"] = "SomeUser",
                ["Authentication"] = "SAPAssertionSSO",
            },
            sut.DestinationConfiguration);

        // the tokens implement value equality, which is important for the HTTP client cache
        Assert.Equal(
            [
                new DestinationServiceV1Response.DestinationAuthToken
                {
                    Type = "MYSAPSSO2",
                    Value = "someString",
                    HttpHeaderSuggestion = new Header("Cookie", "MYSAPSSO2=someString"),
                },
                new DestinationServiceV1Response.DestinationAuthToken { Error = "Simulated failure." },
            ],
            sut.AuthTokens);
        Assert.Null(sut.Certificates);
    }

    [Fact]
    public void Parsing_ReadsCertificates_AndNonStringValues()
    {
        const string payload = """
            {
              "destinationConfiguration": { "Name": "d", "Retries": 3, "TrustAll": true, "Ignored": null },
              "authTokens": [ { "type": "Bearer", "value": "v", "expires_in": 3600 } ],
              "certificates": [ { "Name": "a.p12", "Content": "QUJD", "Type": "CERTIFICATE" } ]
            }
            """;

        var sut = Parse(payload);

        Assert.Equal("3", sut.DestinationConfiguration["Retries"]);
        Assert.Equal("true", sut.DestinationConfiguration["TrustAll"]);
        Assert.False(sut.DestinationConfiguration.ContainsKey("Ignored"));
        Assert.Equal("3600", sut.AuthTokens![0].ExpiresIn);
        Assert.Equal(
            new DestinationServiceV1Response.DestinationCertificate { Name = "a.p12", Content = "QUJD", Type = "CERTIFICATE" },
            Assert.Single(sut.Certificates!));
        Assert.Null(sut.Owner);
    }

    [Theory]
    [InlineData("{ \"destinationConfiguration\" : { \"Name ... } }")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    public void Parsing_InvalidDocuments_ThrowsJsonException(string payload) =>
        Assert.ThrowsAny<JsonException>(() => Parse(payload));

    [Fact]
    public void TokensAndCertificates_DoNotRevealTheirSecretsInToString()
    {
        var token = new DestinationServiceV1Response.DestinationAuthToken
        {
            Type = "Bearer",
            Value = "secret-token-value",
            HttpHeaderSuggestion = new Header("Authorization", "Bearer secret-header-value"),
            Error = null,
        };
        var certificate = new DestinationServiceV1Response.DestinationCertificate { Name = "a.p12", Content = "c2VjcmV0LWNvbnRlbnQ=" };

        var text = token + " " + certificate;

        Assert.DoesNotContain("secret-token-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-header-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("c2VjcmV0", text, StringComparison.Ordinal);
        Assert.Contains("a.p12", text, StringComparison.Ordinal);
    }

    private static DestinationServiceV1Response Parse(string payload) => DestinationServiceV1Response.Parse(payload);
}

/// <summary>Headers from the authentication tokens (port of <c>AuthTokenHeaderProviderTest</c>).</summary>
public sealed class AuthTokenHeaderProviderTests
{
    private readonly AuthTokenHeaderProvider _sut = new();

    private static DestinationRequestContext Context(params DestinationServiceV1Response.DestinationAuthToken[] tokens)
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://www.sap.de")
            .Property(DestinationProperty.AuthTokens, (IReadOnlyList<object?>)[.. tokens])
            .Build();
        return new DestinationRequestContext(destination, new Uri("https://www.sap.de"));
    }

    [Fact]
    public void SecuritySessionHeader_IsAddedForSamlAssertions()
    {
        var destination = DefaultHttpDestination.CreateBuilder("foo").AuthenticationType(AuthenticationType.SamlAssertion).Build();

        var result = _sut.GetHeaders(new DestinationRequestContext(destination, new Uri("https://www.sap.de")));

        Assert.Equal([new Header("x-sap-security-session", "create")], result);
    }

    [Fact]
    public void HeaderSuggestion_IsUsed() =>
        Assert.Equal(
            [new Header("foo", "bar")],
            _sut.GetHeaders(Context(new DestinationServiceV1Response.DestinationAuthToken { HttpHeaderSuggestion = new Header("foo", "bar") })));

    [Fact]
    public void MultipleHeaderSuggestions_AreAllUsed()
    {
        var headers = new[] { new Header("header", "value"), new Header("header-duplicate", "foo"), new Header("header-duplicate", "bar"), new Header("header-special-chars", "?!$^(:);") };

        var result = _sut.GetHeaders(Context([.. headers.Select(h => new DestinationServiceV1Response.DestinationAuthToken { HttpHeaderSuggestion = h })]));

        Assert.Equal(headers.Order(HeaderComparer.Instance), result.Order(HeaderComparer.Instance));
    }

    [Fact]
    public void LegacyFallback_BuildsAnAuthorizationHeaderFromTypeAndValue() =>
        Assert.Equal(
            [new Header("Authorization", "foo bar")],
            _sut.GetHeaders(Context(new DestinationServiceV1Response.DestinationAuthToken { Type = "foo", Value = "bar" })));

    [Fact]
    public void EmptyToken_CausesAFailure() =>
        Assert.Throws<DestinationAccessException>(() => _sut.GetHeaders(Context(new DestinationServiceV1Response.DestinationAuthToken())));

    [Fact]
    public void TokenWithOnlyAnError_CausesAFailure() =>
        Assert.Throws<DestinationAccessException>(
            () => _sut.GetHeaders(Context(new DestinationServiceV1Response.DestinationAuthToken { Error = "error" })));

    [Fact]
    public void WithoutTokens_NoHeadersAreAdded() =>
        Assert.Empty(_sut.GetHeaders(Context()));

    private sealed class HeaderComparer : IComparer<Header>
    {
        public static HeaderComparer Instance { get; } = new();

        public int Compare(Header? x, Header? y) =>
            string.CompareOrdinal(x!.Name + x.Value, y!.Name + y.Value);
    }
}
