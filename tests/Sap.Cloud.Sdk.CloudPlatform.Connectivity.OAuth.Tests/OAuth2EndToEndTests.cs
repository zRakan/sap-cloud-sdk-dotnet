using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

public sealed class OAuth2EndToEndTests : OAuthTestBase
{
    [Fact]
    public async Task DestinationHeaders_ContainBearerToken_AndCheckTenantConsistency()
    {
        var handler = new FakeHandler(_ => FakeHandler.Token("e2e-token"));
        var service = OAuth2Service.CreateBuilder()
            .WithTokenUri("https://p.authentication.example.com")
            .WithIdentity(OAuth2ClientIdentity.From(new ClientCredentials("e2e", "s")))
            .WithHttpClientFactory(_ => new HttpClient(handler))
            .Build();
        var destination = DefaultHttpDestination.CreateBuilder("https://api.example.com")
            .Name("d")
            .Property(DestinationProperty.TenantId, "tenant-a")
            .HeaderProviders(new OAuth2HeaderProvider(service, "Authorization"))
            .Build();

        var headers = await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("tenant-a", "a"),
            async () => await destination.GetHeadersAsync(destination.Uri));
        Assert.Equal("Bearer e2e-token", Assert.Single(headers, h => h.Name == "Authorization").Value);

        // A destination created for tenant-a must not be used for tenant-b.
        var e = await Assert.ThrowsAnyAsync<Exception>(() => TenantAccessor.ExecuteWithTenantAsync(
            new DefaultTenant("tenant-b", "b"), async () => await destination.GetHeadersAsync(destination.Uri)));
        Assert.IsType<Exceptions.DestinationAccessException>(e.InnerException);
        Assert.Contains("does not match the current tenant", e.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests); // no token was requested for tenant-b
    }

    [Fact]
    public void OAuth2DestinationBuilder_CreatesDestinationWithProvider()
    {
        var destination = OAuth2DestinationBuilder.ForTargetUrl("https://api.example.com")
            .WithTokenEndpoint("https://t.authentication.example.com")
            .WithClient(new ClientCredentials("builder-client", "s"), OnBehalfOf.TechnicalUserProvider)
            .Build();

        var provider = OAuthProvider(destination);
        Assert.Equal("https://t.authentication.example.com/oauth/token", provider.Service.TokenUri.AbsoluteUri);
        Assert.Equal(OnBehalfOf.TechnicalUserProvider, provider.Service.OnBehalfOf);
        Assert.Equal(OAuth2ServiceBindingDestinationLoader.StableHash("builder-client").ToString(System.Globalization.CultureInfo.InvariantCulture),
            destination.TryGet(DestinationProperty.Name, out var name) ? name : null);
    }

    [Fact]
    public void StableHash_MatchesJavaStringHashCode()
    {
        // "hello".hashCode() in Java is 99162322.
        Assert.Equal(99162322, OAuth2ServiceBindingDestinationLoader.StableHash("hello"));
    }
}
