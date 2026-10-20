using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The authentication type of a destination.</summary>
/// <remarks>The wire identifiers (for example <c>BasicAuthentication</c>) are available via <see cref="AuthenticationTypeExtensions"/>.</remarks>
public enum AuthenticationType
{
    /// <summary>No authentication.</summary>
    NoAuthentication,

    /// <summary>HTTP Basic authentication.</summary>
    BasicAuthentication,

    /// <summary>Principal propagation to an on-premise system.</summary>
    PrincipalPropagation,

    /// <summary>Client certificate authentication.</summary>
    ClientCertificateAuthentication,

    /// <summary>OAuth 2.0 client credentials flow.</summary>
    OAuth2ClientCredentials,

    /// <summary>OAuth 2.0 SAML bearer assertion flow.</summary>
    OAuth2SamlBearerAssertion,

    /// <summary>OAuth 2.0 user token exchange.</summary>
    OAuth2UserTokenExchange,

    /// <summary>OAuth 2.0 JWT bearer flow.</summary>
    OAuth2JwtBearer,

    /// <summary>SAP assertion single sign-on.</summary>
    SapAssertionSso,

    /// <summary>OAuth 2.0 password flow.</summary>
    OAuth2Password,

    /// <summary>SAML assertion.</summary>
    SamlAssertion,

    /// <summary>OAuth 2.0 technical user propagation.</summary>
    OAuth2TechnicalUserPropagation,

    /// <summary>Forwards the <c>Authorization</c> header of the incoming request.</summary>
    TokenForwarding,

    /// <summary>OAuth 2.0 refresh token flow.</summary>
    OAuth2RefreshToken,
}

/// <summary>Identifier conversions for <see cref="AuthenticationType"/>.</summary>
public static class AuthenticationTypeExtensions
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(AuthenticationTypeExtensions).FullName!);

    private static readonly Dictionary<AuthenticationType, string> Identifiers =
        new Dictionary<AuthenticationType, string>
        {
            [AuthenticationType.NoAuthentication] = "NoAuthentication",
            [AuthenticationType.BasicAuthentication] = "BasicAuthentication",
            [AuthenticationType.PrincipalPropagation] = "PrincipalPropagation",
            [AuthenticationType.ClientCertificateAuthentication] = "ClientCertificateAuthentication",
            [AuthenticationType.OAuth2ClientCredentials] = "OAuth2ClientCredentials",
            [AuthenticationType.OAuth2SamlBearerAssertion] = "OAuth2SAMLBearerAssertion",
            [AuthenticationType.OAuth2UserTokenExchange] = "OAuth2UserTokenExchange",
            [AuthenticationType.OAuth2JwtBearer] = "OAuth2JWTBearer",
            [AuthenticationType.SapAssertionSso] = "SAPAssertionSSO",
            [AuthenticationType.OAuth2Password] = "OAuth2Password",
            [AuthenticationType.SamlAssertion] = "SAMLAssertion",
            [AuthenticationType.OAuth2TechnicalUserPropagation] = "OAuth2TechnicalUserPropagation",
            [AuthenticationType.TokenForwarding] = "TokenForwarding",
            [AuthenticationType.OAuth2RefreshToken] = "OAuth2RefreshToken",
        };

    /// <summary>Gets the identifier used in destination properties, for example <c>OAuth2ClientCredentials</c>.</summary>
    /// <param name="authenticationType">The authentication type.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this AuthenticationType authenticationType) =>
        Identifiers.TryGetValue(authenticationType, out var identifier)
            ? identifier
            : throw new ArgumentOutOfRangeException(nameof(authenticationType));

    /// <summary>Parses an identifier (case-sensitive).</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The authentication type.</returns>
    /// <exception cref="ArgumentException">The identifier is unknown.</exception>
    public static AuthenticationType OfIdentifier(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        foreach (var (type, id) in Identifiers)
        {
            if (string.Equals(id, identifier, StringComparison.Ordinal))
            {
                return type;
            }
        }
        throw new ArgumentException($"Unknown {nameof(AuthenticationType)}: {identifier}.", nameof(identifier));
    }

    /// <summary>Parses an identifier, falling back to a default for <see langword="null"/> or unknown identifiers.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="defaultAuthenticationType">The default.</param>
    /// <returns>The authentication type.</returns>
    public static AuthenticationType OfIdentifierOrDefault(
        string? identifier,
        AuthenticationType defaultAuthenticationType)
    {
        if (identifier is null)
        {
            return defaultAuthenticationType;
        }
        try
        {
            return OfIdentifier(identifier);
        }
        catch (ArgumentException)
        {
            if (Log.IsEnabled(LogLevel.Warning))
            {
                Log.LogWarning(
                    "Identifier '{Identifier}' is not supported. Falling back to {Default}.",
                    identifier,
                    defaultAuthenticationType.GetIdentifier());
            }
            return defaultAuthenticationType;
        }
    }
}
