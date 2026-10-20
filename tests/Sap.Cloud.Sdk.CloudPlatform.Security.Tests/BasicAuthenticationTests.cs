using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Tests;

public class BasicAuthenticationTests : SecurityStateReset
{
    private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));

    [Theory]
    [InlineData("Basic ")]
    [InlineData("basic ")]
    [InlineData("BASIC   ")]
    public void Credentials_AreDecoded(string prefix)
    {
        var credentials = WithAuthorization(prefix + Encode("user:pass"),
            () => BasicAuthenticationAccessor.TryGetCurrentBasicCredentials().GetValueOrThrow());
        Assert.Equal(new BasicCredentials("user", "pass"), credentials);
    }

    [Fact]
    public void PasswordMayContainColons()
    {
        var credentials = WithAuthorization("Basic " + Encode("user:pa:ss"),
            () => BasicAuthenticationAccessor.TryGetCurrentBasicCredentials().GetValueOrThrow());
        Assert.Equal("pa:ss", credentials.Password);
    }

    [Theory]
    [InlineData("Bearer abc")]
    [InlineData("Basic !!!")]
    [InlineData("Basic dXNlcg==")] // "user" without colon
    public void InvalidHeader_Fails(string authorization)
    {
        var result = WithAuthorization(authorization, BasicAuthenticationAccessor.TryGetCurrentBasicCredentials);
        Assert.IsType<BasicAuthenticationAccessException>(result.Exception);
    }

    [Fact]
    public void NoHeaders_Fails()
    {
        Assert.IsType<BasicAuthenticationAccessException>(
            BasicAuthenticationAccessor.TryGetCurrentBasicCredentials().Exception);
    }
}
