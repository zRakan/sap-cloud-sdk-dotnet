namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>CSRF token wrapper type.</summary>
/// <param name="Token">The token value. Considered confidential: it is not printed by <c>ToString</c>.</param>
public sealed record CsrfToken(string Token)
{
    /// <summary>Hides the token.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => "CsrfToken(Token=(hidden))";
}
