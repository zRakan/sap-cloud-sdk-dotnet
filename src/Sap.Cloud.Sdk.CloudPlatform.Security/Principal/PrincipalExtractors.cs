using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal;

/// <summary>Determines the current principal from one source.</summary>
internal interface IPrincipalExtractor
{
    Result<ISecurityPrincipal> TryGetCurrentPrincipal();
}

/// <summary>Extracts the principal from an XSUAA (OAuth 2.0) token, based on its grant type.</summary>
internal sealed class OAuth2AuthTokenPrincipalExtractor : IPrincipalExtractor
{
    private const string ClientIdClaim = "client_id";
    private const string UserNameClaim = "user_name";
    private const string GrantTypeClaim = "grant_type";

    private static readonly ILogger Log = SdkLogging.CreateLogger<OAuth2AuthTokenPrincipalExtractor>();

    private readonly Dictionary<string, Func<AuthToken, string?>> _idExtractors = new(StringComparer.Ordinal)
    {
        ["password"] = t => t.GetClaimAsString(UserNameClaim),
        ["client_credentials"] = t => t.GetClaimAsString(ClientIdClaim),
        ["authorization_code"] = t => t.GetClaimAsString(UserNameClaim),
        ["user_token"] = t => t.GetClaimAsString(UserNameClaim),
        ["urn:ietf:params:oauth:grant-type:saml2-bearer"] = t => t.GetClaimAsString(UserNameClaim),
        ["urn:ietf:params:oauth:grant-type:jwt-bearer"] = t => t.GetClaimAsString(UserNameClaim),
    };

    /// <summary>Sets the logic that reads the principal ID for a grant type.</summary>
    internal void SetIdExtractorFunction(string grantType, Func<AuthToken, string?> extractor) =>
        _idExtractors[grantType] = extractor;

    public Result<ISecurityPrincipal> TryGetCurrentPrincipal() =>
        AuthTokenAccessor.TryGetCurrentToken().Map<ISecurityPrincipal>(token => new DefaultPrincipal(GetPrincipalId(token)));

    private string GetPrincipalId(AuthToken token)
    {
        var grantType = token.GetClaimAsString(GrantTypeClaim)
            ?? throw new PrincipalAccessException("The current JWT does not contain any grant type.");

        if (!_idExtractors.TryGetValue(grantType, out var extractor))
        {
            throw new PrincipalAccessException($"There is no reader registered for grant type '{grantType}'.");
        }

        string? principalId;
        try
        {
            principalId = extractor(token);
        }
        catch (Exception e) when (e is not PrincipalAccessException)
        {
            throw new PrincipalAccessException($"Could not read id for grant type {grantType} from JWT.", e);
        }

        if (principalId is null)
        {
            throw new PrincipalAccessException($"The principalId for grant type {grantType} must not be null.");
        }
        Log.LogDebug("Extracted principal from the current JWT.");
        return principalId;
    }
}

/// <summary>Extracts the principal from an IAS (OpenID Connect) token.</summary>
internal sealed class OidcAuthTokenPrincipalExtractor : IPrincipalExtractor
{
    public Result<ISecurityPrincipal> TryGetCurrentPrincipal() =>
        AuthTokenAccessor.TryGetCurrentToken().Map<ISecurityPrincipal>(token => new DefaultPrincipal(GetPrincipalId(token)));

    private static string GetPrincipalId(AuthToken token)
    {
        // Preferred: sap_id_type=user together with sub.
        if (token.GetClaimAsString("sap_id_type") == "user" && token.GetClaimAsString("sub") is { } sub)
        {
            return sub;
        }
        // Legacy fallbacks.
        return token.GetClaimAsString("user_uuid")
            ?? token.GetClaimAsString("email")
            ?? throw new PrincipalAccessException(
                "The current JWT does not contain a valid principal identifier. "
                + "Expected one of: sap_id_type='user' with sub claim, user_uuid, or email.");
    }
}

/// <summary>Uses the user name of the Basic Authentication credentials as principal.</summary>
internal sealed class BasicCredentialsPrincipalExtractor : IPrincipalExtractor
{
    public Result<ISecurityPrincipal> TryGetCurrentPrincipal() =>
        BasicAuthenticationAccessor.TryGetCurrentBasicCredentials()
            .Map<ISecurityPrincipal>(c => new DefaultPrincipal(c.Username));
}
