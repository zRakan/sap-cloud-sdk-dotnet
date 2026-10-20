using Microsoft.IdentityModel.JsonWebTokens;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>A decoded JSON Web Token, typically taken from the <c>Authorization</c> header of the current request.</summary>
/// <remarks>
/// <b>The SDK decodes tokens but does not validate them</b> (signature, issuer, audience, expiry), matching the Java
/// SDK. Validation is the responsibility of the application's authentication layer, for example ASP.NET Core
/// authentication with the SAP identity service. Only trust tokens from requests that passed authentication.
/// </remarks>
public sealed class AuthToken : IEquatable<AuthToken>
{
    /// <summary>Creates a token.</summary>
    /// <param name="jwt">The decoded JWT.</param>
    public AuthToken(JsonWebToken jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        Jwt = jwt;
    }

    /// <summary>The decoded JWT.</summary>
    public JsonWebToken Jwt { get; }

    /// <summary>Gets a claim of the payload as string.</summary>
    /// <param name="claimName">The claim name.</param>
    /// <returns>The value, or <see langword="null"/> if the claim is missing or not a string.</returns>
    public string? GetClaimAsString(string claimName)
    {
        ArgumentNullException.ThrowIfNull(claimName);
        return Jwt.TryGetPayloadValue<object>(claimName, out var value) && value is string s ? s : null;
    }

    /// <inheritdoc/>
    public bool Equals(AuthToken? other) =>
        other is not null && string.Equals(Jwt.EncodedToken, other.Jwt.EncodedToken, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as AuthToken);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Jwt.EncodedToken);

    /// <summary>Hides the token, which is confidential.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => "AuthToken(jwt=(hidden))";
}
