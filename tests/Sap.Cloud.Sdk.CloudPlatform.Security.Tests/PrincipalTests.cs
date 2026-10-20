using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Tests;

public class PrincipalTests : SecurityStateReset
{
    private static string PrincipalIdFor(object claims) =>
        WithAuthorization("Bearer " + TestJwt.Create(claims), () => PrincipalAccessor.GetCurrentPrincipal().PrincipalId);

    [Theory]
    [InlineData("client_credentials", "the-client")]
    [InlineData("password", "the-user")]
    [InlineData("authorization_code", "the-user")]
    [InlineData("user_token", "the-user")]
    [InlineData("urn:ietf:params:oauth:grant-type:jwt-bearer", "the-user")]
    [InlineData("urn:ietf:params:oauth:grant-type:saml2-bearer", "the-user")]
    public void Xsuaa_PrincipalDependsOnGrantType(string grantType, string expected)
    {
        Assert.Equal(expected, PrincipalIdFor(new { grant_type = grantType, client_id = "the-client", user_name = "the-user" }));
    }

    [Fact]
    public void Ias_PrefersSubForUsers_ThenUserUuid_ThenEmail()
    {
        Assert.Equal("sub-id", PrincipalIdFor(new { sap_id_type = "user", sub = "sub-id", user_uuid = "uuid", email = "e@x" }));
        Assert.Equal("uuid", PrincipalIdFor(new { sap_id_type = "app", sub = "sub-id", user_uuid = "uuid", email = "e@x" }));
        Assert.Equal("e@x", PrincipalIdFor(new { sub = "sub-id", email = "e@x" }));
    }

    [Fact]
    public void BasicCredentials_AreUsedWithoutJwt()
    {
        var id = WithAuthorization("Basic " + Convert.ToBase64String("basic-user:pw"u8.ToArray()),
            () => PrincipalAccessor.GetCurrentPrincipal().PrincipalId);
        Assert.Equal("basic-user", id);
    }

    [Fact]
    public void NoSource_FailsWithAllCauses()
    {
        var exception = Assert.IsType<PrincipalAccessException>(PrincipalAccessor.TryGetCurrentPrincipal().Exception);
        var causes = Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(4, causes.InnerExceptions.Count); // thread context, XSUAA, IAS, Basic
    }

    [Fact]
    public void ExecuteWithPrincipal_And_Fallback()
    {
        var principal = new DefaultPrincipal("explicit");
        Assert.Equal("explicit", PrincipalAccessor.ExecuteWithPrincipal(principal, () => PrincipalAccessor.GetCurrentPrincipal().PrincipalId));
        Assert.Equal("explicit", PrincipalAccessor.ExecuteWithFallbackPrincipal(() => principal, () => PrincipalAccessor.GetCurrentPrincipal().PrincipalId));

        PrincipalAccessor.FallbackPrincipal = () => null;
        Assert.IsType<PrincipalAccessException>(PrincipalAccessor.TryGetCurrentPrincipal().Exception);
        PrincipalAccessor.FallbackPrincipal = () => new DefaultPrincipal("fallback");
        Assert.Equal("fallback", PrincipalAccessor.GetCurrentPrincipal().PrincipalId);
    }

    [Fact]
    public void ContextPrincipal_WinsOverToken()
    {
        var id = WithAuthorization("Bearer " + TestJwt.Create(new { grant_type = "password", user_name = "from-token" }),
            () => PrincipalAccessor.ExecuteWithPrincipal(new DefaultPrincipal("from-context"),
                () => PrincipalAccessor.GetCurrentPrincipal().PrincipalId));
        Assert.Equal("from-context", id);
    }
}
