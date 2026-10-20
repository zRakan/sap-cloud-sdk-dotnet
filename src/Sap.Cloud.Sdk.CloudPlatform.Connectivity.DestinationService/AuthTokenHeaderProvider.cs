using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Adds the authentication tokens the destination service returned as headers to requests sent to the destination.
/// </summary>
/// <remarks>
/// <para>
/// Each token contributes the header the service suggested (<c>http_header</c>), or, as a legacy fallback, an
/// <c>Authorization</c> header made of type and value. A token without either, or with an error, fails with a
/// <see cref="DestinationAccessException"/>. For SAML assertion destinations the <c>x-sap-security-session: create</c>
/// header is added.
/// </para>
/// <para>
/// The provider does not renew tokens: expired tokens are only logged. Renewal happens in the destination cache, which treats
/// a destination with an expired token as a miss and fetches it again.
/// </para>
/// </remarks>
internal sealed class AuthTokenHeaderProvider : IDestinationHeaderProvider
{
    private const string SecuritySessionHeader = "x-sap-security-session";
    private const string CreateSessionValue = "create";

    private static readonly ILogger Log = SdkLogging.CreateLogger<AuthTokenHeaderProvider>();

    public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        var destination = requestContext.Destination;
        var authenticationType = destination.AuthenticationType;
        List<Header> result = [];

        if (authenticationType == AuthenticationType.SamlAssertion)
        {
            result.Add(new Header(SecuritySessionHeader, CreateSessionValue));
        }

        var tokens = destination.TryGet(DestinationProperty.AuthTokens, out var list)
            ? list.OfType<DestinationServiceV1Response.DestinationAuthToken>().ToList()
            : [];

        if (tokens.Count > 0)
        {
            result.AddRange(GetDestinationHeaders(tokens));
        }
        else if (IsAuthTokenExpected(authenticationType))
        {
            Log.LogWarning("The destination service did not include an auth token in the response.");
        }

        var now = DateTimeOffset.UtcNow;
        if (tokens.Any(t => t.ExpiryTimestamp is { } expiry && expiry < now))
        {
            Log.LogWarning(
                "An authorization token of destination {Name} has expired. "
                + "Please ensure that you don't reuse destination objects in your code for longer periods of time.",
                destination.TryGet(DestinationProperty.Name, out var name) ? name : destination.ToString());
        }

        return result;
    }

    private static List<Header> GetDestinationHeaders(List<DestinationServiceV1Response.DestinationAuthToken> authTokens)
    {
        List<Header> result = [];
        foreach (var authToken in authTokens)
        {
            if (authToken.HttpHeaderSuggestion is { } header)
            {
                result.Add(header);
                continue;
            }
            // fallback for legacy logic, if users created an instance manually
            if (!string.IsNullOrEmpty(authToken.Type) && !string.IsNullOrEmpty(authToken.Value))
            {
                Log.LogWarning(
                    "Header suggestion is missing from destination service response. "
                    + "Falling back to constructing an authorization header from type and value. "
                    + "This is unexpected, please report this at https://github.com/SAP/cloud-sdk-java/issues.");
                result.Add(new Header("Authorization", authToken.Type + " " + authToken.Value));
                continue;
            }
            throw new DestinationAccessException(
                "Failed to read authentication token. The destination service responded with an auth token that could not be interpreted.");
        }
        return result;
    }

    private static bool IsAuthTokenExpected(AuthenticationType authType) => authType
        is not (AuthenticationType.NoAuthentication
            or AuthenticationType.PrincipalPropagation
            or AuthenticationType.ClientCertificateAuthentication);
}
