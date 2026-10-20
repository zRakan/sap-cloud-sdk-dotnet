using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

public sealed class OAuth2TokenServiceTests : IDisposable
{
    private static readonly Uri TokenEndpoint = new("https://provider.authentication.eu10.hana.ondemand.com/oauth/token");
    private static readonly OAuth2ClientIdentity SecretIdentity =
        OAuth2ClientIdentity.From(new ClientCredentials("client-id", "client-secret"));

    private readonly ManualTimeProvider _time = new();
    private int _tokenCounter;
    private readonly FakeHandler _handler;
    private readonly OAuth2TokenService _service;

    public OAuth2TokenServiceTests()
    {
        _handler = new FakeHandler(_ => FakeHandler.Token($"token-{++_tokenCounter}"));
        _service = new OAuth2TokenService(new HttpClient(_handler), TokenCacheParameters.Default, _time, ownsHttpClient: true);
    }

    public void Dispose()
    {
        _service.Dispose();
        TokenClientConfiguration.Instance = null!;
    }

    private Task<OAuth2TokenResponse> ClientCredentials(string? zoneId = null, string? subdomain = null, IReadOnlyDictionary<string, string>? extra = null, bool disableCache = false) =>
        _service.RetrieveAccessTokenViaClientCredentialsGrantAsync(TokenEndpoint, SecretIdentity, zoneId, subdomain, extra, disableCache, default);

    [Fact]
    public async Task ClientCredentials_SendsFormAndHeaders()
    {
        var response = await ClientCredentials(zoneId: "tenant-zid");

        Assert.Equal("token-1", response.AccessToken);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(TokenEndpoint, request.Uri);
        Assert.Equal("tenant-zid", request.Headers["X-zid"]);
        Assert.Contains("application/json", request.Headers["Accept"], StringComparison.Ordinal);
        Assert.True(request.Headers.ContainsKey("X-CorrelationID"));
        Assert.Equal(
            new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = "client-id", ["client_secret"] = "client-secret" },
            FormBody.Parse(request.Body));
    }

    [Fact]
    public async Task Subdomain_ReplacesFirstHostLabel()
    {
        await ClientCredentials(subdomain: "consumer");
        Assert.Equal("https://consumer.authentication.eu10.hana.ondemand.com/oauth/token", _handler.Requests[0].Uri.AbsoluteUri);
        Assert.False(_handler.Requests[0].Headers.ContainsKey("X-zid"));
    }

    [Theory]
    [InlineData("https://localhost/oauth/token", "sub", "https://localhost/oauth/token")]
    [InlineData("https://a.b.c/oauth/token", "", "https://a.b.c/oauth/token")]
    [InlineData("https://a.b.c:8443/oauth/token?x=1", "sub", "https://sub.b.c:8443/oauth/token?x=1")]
    public void ReplaceSubdomain(string uri, string subdomain, string expected)
    {
        Assert.Equal(expected, OAuth2TokenService.ReplaceSubdomain(new Uri(uri), subdomain).AbsoluteUri);
    }

    [Fact]
    public async Task OptionalParameters_DoNotOverrideRequiredOnes()
    {
        await ClientCredentials(extra: new Dictionary<string, string> { ["grant_type"] = "password", ["client_id"] = "evil", ["app_tid"] = "t" });
        var form = FormBody.Parse(_handler.Requests[0].Body);
        Assert.Equal("client_credentials", form["grant_type"]);
        Assert.Equal("client-id", form["client_id"]);
        Assert.Equal("t", form["app_tid"]);
    }

    [Fact]
    public async Task JwtBearer_SendsAssertion_WithZoneHeaderOrSubdomain()
    {
        await _service.RetrieveAccessTokenViaJwtBearerGrantWithZoneIdAsync(TokenEndpoint, SecretIdentity, "user-jwt", null, false, "zid-1", default);
        await _service.RetrieveAccessTokenViaJwtBearerGrantAsync(TokenEndpoint, SecretIdentity, "user-jwt", "consumer", null, false, default);

        var withZone = _handler.Requests[0];
        Assert.Equal("zid-1", withZone.Headers["X-zid"]);
        Assert.Equal(TokenEndpoint, withZone.Uri);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", FormBody.Parse(withZone.Body)["grant_type"]);
        Assert.Equal("user-jwt", FormBody.Parse(withZone.Body)["assertion"]);

        Assert.StartsWith("https://consumer.", _handler.Requests[1].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificateIdentity_DoesNotSendSecret()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var identity = OAuth2ClientIdentity.From(new ClientCertificate("cert-client", certificate.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem()));

        Assert.True(identity.IsCertificateBased);
        Assert.True(identity.Certificate.HasPrivateKey);
        await _service.RetrieveAccessTokenViaClientCredentialsGrantAsync(TokenEndpoint, identity, null, null, null, false, default);

        var form = FormBody.Parse(_handler.Requests[0].Body);
        Assert.Equal("cert-client", form["client_id"]);
        Assert.False(form.ContainsKey("client_secret"));
        Assert.DoesNotContain("BEGIN", identity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tokens_AreCachedPerRequest_AndRenewedBeforeExpiry()
    {
        Assert.Equal("token-1", (await ClientCredentials(zoneId: "a")).AccessToken);
        Assert.Equal("token-1", (await ClientCredentials(zoneId: "a")).AccessToken);
        Assert.Equal("token-2", (await ClientCredentials(zoneId: "b")).AccessToken); // other tenant, other entry
        Assert.Equal("token-3", (await ClientCredentials(zoneId: "a", disableCache: true)).AccessToken);

        // 3600s token, 30s delta: still valid after 3560s, renewed after 3575s.
        _time.Now = _time.Now.AddSeconds(3560);
        Assert.Equal("token-1", (await ClientCredentials(zoneId: "a")).AccessToken);
        _time.Now = _time.Now.AddSeconds(15);
        Assert.Equal("token-4", (await ClientCredentials(zoneId: "a")).AccessToken);
    }

    [Fact]
    public async Task ErrorResponse_ThrowsWithStatusAndBody()
    {
        using var service = new OAuth2TokenService(
            new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{"error":"unauthorized"}""") })),
            TokenCacheParameters.Default, ownsHttpClient: true);

        var e = await Assert.ThrowsAsync<OAuth2ServiceException>(() =>
            service.RetrieveAccessTokenViaClientCredentialsGrantAsync(TokenEndpoint, SecretIdentity, null, null, null, false, default));
        Assert.Equal(HttpStatusCode.Unauthorized, e.StatusCode);
        Assert.Contains("unauthorized", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("client-secret", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retries_OnlyWhenEnabled()
    {
        var calls = 0;
        HttpResponseMessage Respond(RecordedRequest _) => ++calls < 3 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : FakeHandler.Token("ok");

        using var withoutRetry = new OAuth2TokenService(new HttpClient(new FakeHandler(Respond)), TokenCacheParameters.Disabled, ownsHttpClient: true);
        await Assert.ThrowsAsync<OAuth2ServiceException>(() =>
            withoutRetry.RetrieveAccessTokenViaClientCredentialsGrantAsync(TokenEndpoint, SecretIdentity, null, null, null, false, default));

        calls = 0;
        TokenClientConfiguration.Instance = new TokenClientConfiguration { IsRetryEnabled = true, RetryDelay = TimeSpan.FromMilliseconds(1) };
        using var withRetry = new OAuth2TokenService(new HttpClient(new FakeHandler(Respond)), TokenCacheParameters.Disabled, ownsHttpClient: true);
        var response = await withRetry.RetrieveAccessTokenViaClientCredentialsGrantAsync(TokenEndpoint, SecretIdentity, null, null, null, false, default);
        Assert.Equal("ok", response.AccessToken);
        Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData("""{"access_token":"t","expires_in":"120"}""", true)]
    [InlineData("""{"access_token":"t"}""", false)]
    [InlineData("not json", false)]
    public async Task ResponseParsing(string body, bool succeeds)
    {
        using var service = new OAuth2TokenService(
            new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })),
            TokenCacheParameters.Disabled, _time, ownsHttpClient: true);
        var call = () => service.RetrieveAccessTokenViaClientCredentialsGrantAsync(TokenEndpoint, SecretIdentity, null, null, null, false, default);
        if (succeeds)
        {
            Assert.Equal(_time.Now.AddSeconds(120), (await call()).ExpiresAt);
        }
        else
        {
            await Assert.ThrowsAsync<OAuth2ServiceException>(call);
        }
    }
}
