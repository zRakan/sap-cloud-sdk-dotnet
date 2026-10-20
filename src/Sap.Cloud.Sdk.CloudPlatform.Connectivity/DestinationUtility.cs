using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Helpers to inspect destination properties.</summary>
public static class DestinationUtility
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(DestinationUtility).FullName!);

    /// <summary>Whether the destination needs the token of the current user to be exchanged.</summary>
    /// <param name="destination">The destination properties.</param>
    /// <returns><see langword="false"/> if there is no authentication type.</returns>
    public static bool RequiresUserTokenExchange(IDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.TryGet(DestinationProperty.AuthType, out var authType)
            && !destination.TryGet(DestinationProperty.AuthTypeFallback, out authType))
        {
            return false;
        }
        destination.TryGet(DestinationProperty.SystemUser, out var systemUser);
        return RequiresUserTokenExchange(authType, systemUser);
    }

    /// <summary>Whether an authentication type needs the token of the current user to be exchanged.</summary>
    /// <param name="authType">The authentication type.</param>
    /// <param name="systemUser">The value of the <c>SystemUser</c> property, if any.</param>
    /// <returns>
    /// <see langword="true"/> for the user-bound OAuth and SAML types; <see langword="false"/> for the SAML bearer
    /// and SAP assertion types if a system user is set.
    /// </returns>
    public static bool RequiresUserTokenExchange(AuthenticationType authType, string? systemUser)
    {
        if (!string.IsNullOrWhiteSpace(systemUser)
            && authType is AuthenticationType.OAuth2SamlBearerAssertion or AuthenticationType.SapAssertionSso)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(
                    "Destination properties do not qualify for user token exchange: authentication type is {AuthType}, but property {Property} is set.",
                    authType.GetIdentifier(),
                    DestinationProperty.SystemUser.KeyName);
            }
            return false;
        }
        return authType switch
        {
            AuthenticationType.NoAuthentication
                or AuthenticationType.BasicAuthentication
                or AuthenticationType.PrincipalPropagation
                or AuthenticationType.ClientCertificateAuthentication
                or AuthenticationType.OAuth2ClientCredentials
                or AuthenticationType.OAuth2Password
                or AuthenticationType.OAuth2TechnicalUserPropagation
                or AuthenticationType.TokenForwarding
                or AuthenticationType.OAuth2RefreshToken => false,
            AuthenticationType.OAuth2JwtBearer
                or AuthenticationType.OAuth2SamlBearerAssertion
                or AuthenticationType.SapAssertionSso
                or AuthenticationType.OAuth2UserTokenExchange
                or AuthenticationType.SamlAssertion => true,
            _ => throw new ArgumentOutOfRangeException(nameof(authType)),
        };
    }
}
