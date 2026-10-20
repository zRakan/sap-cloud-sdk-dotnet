using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Tests;

public class AuthTokenTests : SecurityStateReset
{
    private static readonly string Jwt = TestJwt.Create(new { sub = "user", zid = "tenant", exp = 1 });

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("bearer ")]
    [InlineData("BEARER ")]
    public void Token_IsDecodedFromAuthorizationHeader(string prefix)
    {
        var token = WithAuthorization(prefix + Jwt, AuthTokenAccessor.GetCurrentToken);
        Assert.Equal("user", token.GetClaimAsString("sub"));
        Assert.Null(token.GetClaimAsString("exp")); // not a string
        Assert.Null(token.GetClaimAsString("missing"));
    }

    [Fact]
    public void NoHeaders_Fails()
    {
        Assert.Throws<AuthTokenAccessException>(AuthTokenAccessor.GetCurrentToken);
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz", "no JWT bearer")]
    [InlineData("Bearer not-a-jwt", "Failed to decode the access token")]
    public void InvalidHeader_Fails(string authorization, string expectedMessage)
    {
        var result = WithAuthorization(authorization, AuthTokenAccessor.TryGetCurrentToken);
        var exception = Assert.IsType<AuthTokenAccessException>(result.Exception);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleAuthorizationHeaders_Fail()
    {
        var headers = RequestHeaders.DefaultRequestHeaderContainer.Builder()
            .WithHeader("Authorization", "Bearer " + Jwt, "Bearer " + Jwt).Build();
        var result = RequestHeaders.RequestHeaderAccessor.ExecuteWithHeaderContainer(headers, AuthTokenAccessor.TryGetCurrentToken);
        Assert.Contains("multiple", result.Exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteWithAuthToken_OverridesHeaderToken()
    {
        var other = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(TestJwt.Create(new { sub = "other" })));

        var sub = await AuthTokenAccessor.ExecuteWithAuthTokenAsync(other, async () =>
        {
            await Task.Yield();
            return AuthTokenAccessor.GetCurrentToken().GetClaimAsString("sub");
        });

        Assert.Equal("other", sub);
    }

    [Fact]
    public void FallbackToken_IsUsedWhenNoTokenAvailable()
    {
        var fallback = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(Jwt));
        AuthTokenAccessor.FallbackToken = () => fallback;
        Assert.Same(fallback, AuthTokenAccessor.GetCurrentToken());
    }

    [Fact]
    public void Equality_IsBasedOnEncodedToken()
    {
        var a = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(Jwt));
        var b = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(Jwt));
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.DoesNotContain(Jwt, a.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteWith_RequiresDefaultFacade()
    {
        AuthTokenAccessor.AuthTokenFacade = new CustomFacade();
        var token = new AuthToken(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(Jwt));
        Assert.Throws<ThreadContextExecutionException>(() => AuthTokenAccessor.ExecuteWithAuthToken(token, () => 1));
    }

    private sealed class CustomFacade : IAuthTokenFacade
    {
        public Result<AuthToken> TryGetCurrentToken() => Result.Failure<AuthToken>(new InvalidOperationException());
    }
}
