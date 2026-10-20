namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;

/// <summary>A successful response of a token endpoint.</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="ExpiresAt">When the token expires.</param>
/// <param name="RefreshToken">The refresh token, if any.</param>
/// <param name="TokenType">The token type.</param>
internal sealed record OAuth2TokenResponse(
    string? AccessToken,
    DateTimeOffset ExpiresAt,
    string? RefreshToken,
    string? TokenType)
{
    public override string ToString() =>
        $"OAuth2TokenResponse(accessToken=(hidden), expiresAt={ExpiresAt:O}, refreshToken=(hidden), tokenType={TokenType})";
}
