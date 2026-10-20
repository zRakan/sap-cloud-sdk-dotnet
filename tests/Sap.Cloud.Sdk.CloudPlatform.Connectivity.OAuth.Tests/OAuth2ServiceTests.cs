using System.Net;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

public sealed class OAuth2ServiceTests : OAuthTestBase
{
    private static readonly Uri XsuaaTokenUri = new("https://provider.authentication.eu10.hana.ondemand.com/oauth/token");
    private static readonly Uri IasTokenUri = new("https://provider.accounts.ondemand.com/oauth2/token");

    private readonly FakeHandler _handler = new(_ => FakeHandler.Token("the-token"));

    private OAuth2Service Service(
        OnBehalfOf behalf,
        TenantPropagationStrategy strategy = TenantPropagationStrategy.ZidHeader,
        Uri? tokenUri = null,
        IasTenantHostResolver? resolver = null,
        Uri? btpApi = null,
        [System.Runtime.CompilerServices.CallerMemberName] string clientId = "")
    {
        var builder = OAuth2Service.CreateBuilder()
            .WithTokenUri(tokenUri ?? XsuaaTokenUri)
            .WithIdentity(OAuth2ClientIdentity.From(new ClientCredentials(clientId, "secret")))
            .WithOnBehalfOf(behalf)
            .WithTenantPropagationStrategy(strategy)
            .WithBtpTenantApiUri(btpApi)
            .WithHttpClientFactory(_ => new HttpClient(_handler));
        if (resolver is not null)
        {
            builder.WithIasTenantHostResolver(resolver);
        }
        return builder.Build();
    }

    private static T InTenant<T>(ITenant tenant, Func<T> func) => TenantAccessor.ExecuteWithTenant(tenant, func);

    [Fact]
    public void TokenUriWithoutPath_GetsXsuaaTokenPath()
    {
        var service = OAuth2Service.CreateBuilder().WithTokenUri("https://x.example.com")
            .WithIdentity(OAuth2ClientIdentity.From(new ClientCredentials("a", "b"))).Build();
        Assert.Equal("https://x.example.com/oauth/token", service.TokenUri.AbsoluteUri);
    }

    [Fact]
    public async Task TechnicalUserProvider_SendsNoTenant()
    {
        var token = await InTenant(new DefaultTenant("subscriber", "sub"), () => Service(OnBehalfOf.TechnicalUserProvider).RetrieveAccessTokenAsync(default));

        Assert.Equal("the-token", token);
        var request = Assert.Single(_handler.Requests);
        Assert.False(request.Headers.ContainsKey("X-zid"));
        Assert.Equal(XsuaaTokenUri, request.Uri);
    }

    [Fact]
    public async Task TechnicalUserCurrentTenant_Xsuaa_SendsZidHeader()
    {
        await InTenant(new DefaultTenant("subscriber", "sub"), () => Service(OnBehalfOf.TechnicalUserCurrentTenant).RetrieveAccessTokenAsync(default));

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("subscriber", request.Headers["X-zid"]);
        Assert.Equal(XsuaaTokenUri, request.Uri);
        Assert.Equal("client_credentials", FormBody.Parse(request.Body)["grant_type"]);
    }

    [Fact]
    public async Task TechnicalUserCurrentTenant_Ias_UsesSubdomainAndAppTid()
    {
        await InTenant(new DefaultTenant("subscriber-id", "subscriber"),
            () => Service(OnBehalfOf.TechnicalUserCurrentTenant, TenantPropagationStrategy.TenantSubdomain, IasTokenUri).RetrieveAccessTokenAsync(default));

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://subscriber.accounts.ondemand.com/oauth2/token", request.Uri.AbsoluteUri);
        Assert.False(request.Headers.ContainsKey("X-zid"));
        Assert.Equal("subscriber-id", FormBody.Parse(request.Body)["app_tid"]);
        Assert.False(FormBody.Parse(request.Body).ContainsKey("refresh_expiry"));
    }

    [Fact]
    public async Task Ias_ConcurrentTenants_DoNotShareParameters()
    {
        var service = Service(OnBehalfOf.TechnicalUserCurrentTenant, TenantPropagationStrategy.TenantSubdomain, IasTokenUri);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant($"t{i}", $"s{i}"), () => service.RetrieveAccessTokenAsync(default))));

        Assert.Equal(20, _handler.Requests.Count);
        Assert.All(_handler.Requests, r =>
            Assert.Equal("s" + FormBody.Parse(r.Body)["app_tid"][1..], r.Uri.Host.Split('.')[0]));
    }

    [Fact]
    public async Task Ias_TenantWithoutSubdomain_IsResolvedViaBtpTenantApi()
    {
        var resolver = new FixedResolver("resolved");
        await InTenant(new DefaultTenant("t-1"),
            () => Service(OnBehalfOf.TechnicalUserCurrentTenant, TenantPropagationStrategy.TenantSubdomain, IasTokenUri, resolver, new Uri("https://btp-api.example.com")).RetrieveAccessTokenAsync(default));

        Assert.Equal(("https://btp-api.example.com/", "t-1"), resolver.Calls.Single());
        Assert.StartsWith("https://resolved.", _handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ias_TenantWithoutSubdomain_AndNoBtpApi_Fails()
    {
        var e = await Assert.ThrowsAnyAsync<Exception>(() => InTenant(new DefaultTenant("t-1"),
            () => Service(OnBehalfOf.TechnicalUserCurrentTenant, TenantPropagationStrategy.TenantSubdomain, IasTokenUri).RetrieveAccessTokenAsync(default)));
        Assert.Contains("btp-tenant-api", e.ToString(), StringComparison.Ordinal);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task NamedUser_Xsuaa_ExchangesUserTokenWithZidFromToken()
    {
        var jwt = TestJwt.Create(new { zid = "subscriber", iss = "https://sub.example.com" });
        var token = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(jwt));

        await AuthTokenAccessor.ExecuteWithAuthTokenAsync(token, () => Service(OnBehalfOf.NamedUserCurrentTenant).RetrieveAccessTokenAsync(default));

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("subscriber", request.Headers["X-zid"]);
        var form = FormBody.Parse(request.Body);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", form["grant_type"]);
        Assert.Equal(jwt, form["assertion"]);
    }

    [Fact]
    public async Task NamedUser_Ias_SetsAppTidAndRefreshExpiry()
    {
        var jwt = TestJwt.Create(new { app_tid = "app-tenant", iss = "https://consumer.accounts.ondemand.com" });
        var token = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(jwt));

        await AuthTokenAccessor.ExecuteWithAuthTokenAsync(token,
            () => Service(OnBehalfOf.NamedUserCurrentTenant, TenantPropagationStrategy.TenantSubdomain, IasTokenUri).RetrieveAccessTokenAsync(default));

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://consumer.accounts.ondemand.com/oauth2/token", request.Uri.AbsoluteUri);
        var form = FormBody.Parse(request.Body);
        Assert.Equal("app-tenant", form["app_tid"]);
        Assert.Equal("0", form["refresh_expiry"]);
    }

    [Fact]
    public async Task NamedUser_TenantMismatch_Fails()
    {
        var token = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(TestJwt.Create(new { zid = "a", iss = "https://a.example.com" })));
        var e = await Assert.ThrowsAnyAsync<Exception>(() => AuthTokenAccessor.ExecuteWithAuthTokenAsync(token,
            () => TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("b", "b"), () => Service(OnBehalfOf.NamedUserCurrentTenant).RetrieveAccessTokenAsync(default))));
        Assert.Contains("different tenant IDs", e.ToString(), StringComparison.Ordinal);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task NamedUser_WithoutUserToken_Fails()
    {
        var e = await Assert.ThrowsAnyAsync<Exception>(() => Service(OnBehalfOf.NamedUserCurrentTenant).RetrieveAccessTokenAsync(default));
        Assert.Contains("current user token", e.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthorized_ForSubscriber_ExplainsSubscription()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") });
        var service = OAuth2Service.CreateBuilder().WithTokenUri(XsuaaTokenUri)
            .WithIdentity(OAuth2ClientIdentity.From(new ClientCredentials(nameof(Unauthorized_ForSubscriber_ExplainsSubscription), "s")))
            .WithHttpClientFactory(_ => new HttpClient(handler)).Build();

        var e = await Assert.ThrowsAnyAsync<Exception>(() => InTenant(new DefaultTenant("sub", "sub"), () => service.RetrieveAccessTokenAsync(default)));
        var failed = FindInner<TokenRequestFailedException>(e);
        Assert.Contains("subscribed for the current tenant", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingAccessToken_Fails()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"expires_in":10}""") });
        var service = OAuth2Service.CreateBuilder().WithTokenUri(XsuaaTokenUri)
            .WithIdentity(OAuth2ClientIdentity.From(new ClientCredentials(nameof(MissingAccessToken_Fails), "s")))
            .WithHttpClientFactory(_ => new HttpClient(handler)).Build();

        await Assert.ThrowsAsync<Exceptions.DestinationOAuthTokenException>(() => service.RetrieveAccessTokenAsync(default));
    }

    [Fact]
    public async Task TokenServices_AreCachedPerTenant()
    {
        var service = Service(OnBehalfOf.TechnicalUserCurrentTenant);
        await InTenant(new DefaultTenant("a", "a"), () => service.RetrieveAccessTokenAsync(default));
        await InTenant(new DefaultTenant("a", "a"), () => service.RetrieveAccessTokenAsync(default));
        await InTenant(new DefaultTenant("b", "b"), () => service.RetrieveAccessTokenAsync(default));

        Assert.Equal(2, _handler.Requests.Count); // second call for tenant a is served from the token cache
    }

    [Fact]
    public void ResilienceConfiguration_IsTenantIsolated_WithDefaultTimeout()
    {
        var service = Service(OnBehalfOf.TechnicalUserProvider);
        Assert.Equal(Resilience.ResilienceIsolationMode.TenantOptional, service.ResilienceConfiguration.IsolationMode);
        Assert.Equal(TimeSpan.FromSeconds(10), service.ResilienceConfiguration.TimeLimiter.TimeoutDuration);
        Assert.Equal("provider.authentication.eu10.hana.ondemand.com-" + nameof(ResilienceConfiguration_IsTenantIsolated_WithDefaultTimeout), service.ResilienceConfiguration.Identifier);
    }

    [Theory]
    [InlineData("""{"token_endpoint":"https://my-tenant.accounts.ondemand.com/oauth2/token"}""", "my-tenant")]
    public void ExtractSubdomainFromTokenEndpoint(string body, string expected) =>
        Assert.Equal(expected, IasTenantHostResolver.ExtractSubdomainFromTokenEndpoint(body));

    [Theory]
    [InlineData("{}")]
    [InlineData("nope")]
    [InlineData("""{"token_endpoint":"https://localhost/x"}""")]
    public void ExtractSubdomainFromTokenEndpoint_Invalid(string body) =>
        Assert.Throws<Exceptions.DestinationAccessException>(() => IasTenantHostResolver.ExtractSubdomainFromTokenEndpoint(body));

    private static T FindInner<T>(Exception e)
        where T : Exception
    {
        for (Exception? current = e; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }
        throw new Xunit.Sdk.XunitException($"No {typeof(T).Name} in {e}");
    }

    private sealed class FixedResolver(string subdomain) : IasTenantHostResolver(new HttpClient())
    {
        public List<(string, string)> Calls { get; } = [];

        public override Task<string> ResolveAsync(Uri btpTenantApiUri, string tenantId, CancellationToken cancellationToken)
        {
            Calls.Add((btpTenantApiUri.AbsoluteUri, tenantId));
            return Task.FromResult(subdomain);
        }
    }
}
