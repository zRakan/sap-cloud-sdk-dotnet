using System.Text;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Derives structured values (proxy, credentials, authentication type, headers) from destination properties.</summary>
/// <remarks>Package-private in Java; public here because the destination service module lives in a separate assembly.</remarks>
public static class ComplexDestinationPropertyFactory
{
    private const string HeaderProperty = "URL.headers.";
    private const string BearerCredentialsPrefix = "bearer ";
    private const string BasicCredentialsPrefix = "basic ";
    private const string ProxyAuthorizationHeader = "Proxy-Authorization";

    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(ComplexDestinationPropertyFactory).FullName!);

    /// <summary>Gets the proxy configuration from the <c>Proxy</c> or <c>ProxyHost</c> and <c>ProxyPort</c> properties.</summary>
    /// <param name="baseProperties">The destination properties.</param>
    /// <returns>The configuration, or <see langword="null"/> if none is configured.</returns>
    /// <exception cref="DestinationAccessException">The proxy URI or credentials are invalid.</exception>
    public static ProxyConfiguration? GetProxyConfiguration(IDestinationProperties baseProperties)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        var hasProxyUri = baseProperties.TryGet(DestinationProperty.ProxyUri, out var proxyUri);
        var hasHost = baseProperties.TryGet(DestinationProperty.ProxyHost, out var proxyHost);
        var hasPort = baseProperties.TryGet(DestinationProperty.ProxyPort, out var proxyPort);
        var proxyAuth = GetProxyCredentials(baseProperties);

        if (hasProxyUri)
        {
            try
            {
                var withScheme = AddHttpSchemeIfNoSchemeIsPresent(proxyUri!.OriginalString);
                return new ProxyConfiguration(new Uri(withScheme, UriKind.Absolute), proxyAuth);
            }
            catch (UriFormatException e)
            {
                throw new DestinationAccessException("Invalid proxy URI.", e);
            }
        }
        if (hasHost && hasPort)
        {
            try
            {
                return new ProxyConfiguration(new UriBuilder("http", proxyHost, proxyPort).Uri, proxyAuth);
            }
            catch (Exception e) when (e is UriFormatException or ArgumentException)
            {
                throw new DestinationAccessException("Invalid proxy host or port.", e);
            }
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("No proxy URI or host and port specified. Continuing without proxy configuration.");
        }
        return null;
    }

    /// <summary>Gets the <c>Proxy-Authorization</c> headers for the credentials of a proxy.</summary>
    /// <param name="proxyConfiguration">The proxy configuration, if any.</param>
    /// <returns>The headers; empty for no credentials or unsupported credentials.</returns>
    public static IReadOnlyList<Header> GetProxyAuthorizationHeaders(ProxyConfiguration? proxyConfiguration) =>
        proxyConfiguration?.Credentials switch
        {
            BasicCredentials basic => [new Header(ProxyAuthorizationHeader, basic.HttpHeaderValue)],
            BearerCredentials bearer => [new Header(ProxyAuthorizationHeader, bearer.HttpHeaderValue)],
            _ => [],
        };

    /// <summary>Gets the proxy type, defaulting to <see cref="ProxyType.Internet"/>.</summary>
    /// <param name="baseProperties">The destination properties.</param>
    /// <returns>The proxy type.</returns>
    public static ProxyType? GetProxyType(IDestinationProperties baseProperties)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        if (baseProperties.TryGet(DestinationProperty.ProxyType, out var proxyType))
        {
            return proxyType;
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "No valid JSON primitive '{Property}' defined. Falling back to {Default}.",
                DestinationProperty.ProxyType,
                ProxyType.Internet);
        }
        return ProxyType.Internet;
    }

    /// <summary>Gets the Basic credentials from the <c>User</c> (or <c>username</c>) and <c>Password</c> properties.</summary>
    /// <param name="baseProperties">The destination properties.</param>
    /// <returns>The credentials, or <see langword="null"/> if user or password is missing.</returns>
    public static BasicCredentials? GetBasicCredentials(IDestinationProperties baseProperties)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        var hasUser = baseProperties.TryGet(DestinationProperty.BasicAuthUsername, out var username)
            || baseProperties.TryGet(DestinationProperty.BasicAuthUsernameFallback, out username);
        var hasPassword = baseProperties.TryGet(DestinationProperty.BasicAuthPassword, out var password);
        if (hasUser && hasPassword)
        {
            return new BasicCredentials(username!, password!);
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "No valid JSON primitives '{User}' and '{Password}' defined. Continuing without basic credentials.",
                DestinationProperty.BasicAuthUsername,
                DestinationProperty.BasicAuthPassword);
        }
        return null;
    }

    /// <summary>
    /// Gets the authentication type. <see cref="AuthenticationType.NoAuthentication"/> is upgraded to Basic if credentials
    /// are present, and to token forwarding if <c>forwardAuthToken</c> or <c>HTML5.ForwardAuthToken</c> is set.
    /// </summary>
    /// <param name="baseProperties">The destination properties.</param>
    /// <param name="basicCredentials">The Basic credentials, if any.</param>
    /// <returns>The authentication type.</returns>
    public static AuthenticationType GetAuthenticationType(
        IDestinationProperties baseProperties,
        BasicCredentials? basicCredentials)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        AuthenticationType authType;
        if (baseProperties.TryGet(DestinationProperty.AuthType, out var type)
            || baseProperties.TryGet(DestinationProperty.AuthTypeFallback, out type))
        {
            authType = type;
        }
        else
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(
                    "No valid JSON primitive '{AuthType}' or '{Fallback}' defined. Falling back to {Default}.",
                    DestinationProperty.AuthType,
                    DestinationProperty.AuthTypeFallback,
                    AuthenticationType.NoAuthentication);
            }
            authType = AuthenticationType.NoAuthentication;
        }

        if (authType == AuthenticationType.NoAuthentication && basicCredentials is not null)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Found Authentication Type NoAuthentication, but basic credentials are provided. Changing Auth Type to BasicAuthentication.");
            }
            return AuthenticationType.BasicAuthentication;
        }

        var forwardAuthToken = baseProperties.TryGet(DestinationProperty.ForwardAuthToken, out var forward)
            || baseProperties.TryGet(DestinationProperty.ApprouterForwardAuthToken, out forward)
            ? forward
            : false;
        if (authType == AuthenticationType.NoAuthentication && forwardAuthToken)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(
                    "Found Authentication Type NoAuthentication, but either {Forward} or {ApprouterForward} destination properties are provided. Changing Auth Type to TokenForwarding.",
                    DestinationProperty.ForwardAuthToken,
                    DestinationProperty.ApprouterForwardAuthToken);
            }
            return AuthenticationType.TokenForwarding;
        }
        return authType;
    }

    /// <summary>Gets the headers defined by <c>URL.headers.*</c> properties.</summary>
    /// <param name="baseProperties">The destination properties.</param>
    /// <returns>The headers.</returns>
    public static IReadOnlyList<Header> GetHeadersFromProperties(IDestinationProperties baseProperties)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        var result = new List<Header>();
        foreach (var propertyKey in baseProperties.PropertyNames)
        {
            if (!propertyKey.StartsWith(HeaderProperty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!baseProperties.TryGetAs<string>(propertyKey, out var value))
            {
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Cannot find header value for {Property}. Skipping the header.", propertyKey);
                }
                continue;
            }
            result.Add(new Header(propertyKey[HeaderProperty.Length..], value));
        }
        return result;
    }

    private static ISecurityCredentials? GetProxyCredentials(IDestinationProperties baseProperties)
    {
        if (!baseProperties.TryGet(DestinationProperty.ProxyAuth, out var proxyAuth))
        {
            return null;
        }
        if (proxyAuth.Length == 0)
        {
            return new NoCredentials();
        }
        if (proxyAuth.StartsWith(BasicCredentialsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var encoded = proxyAuth[BasicCredentialsPrefix.Length..];
            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch (FormatException e)
            {
                throw new DestinationAccessException(
                    "Invalid proxy basic credentials. Correct format should be username:password encoded in base64.",
                    e);
            }
            // Deviation: split at the first colon only (RFC 7617), so that passwords may contain colons.
            var separator = decoded.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new DestinationAccessException(
                    "Invalid proxy basic credentials. Correct format should be username:password encoded in base64.");
            }
            return new BasicCredentials(decoded[..separator], decoded[(separator + 1)..]);
        }
        if (proxyAuth.StartsWith(BearerCredentialsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new BearerCredentials(proxyAuth[BearerCredentialsPrefix.Length..]);
        }
        if (Log.IsEnabled(LogLevel.Error))
        {
            var masked = proxyAuth[..Math.Max(0, Math.Min(4, proxyAuth.Length - 4))] + "****";
            if (Log.IsEnabled(LogLevel.Error))
            {
                Log.LogError(
                    "Unsupported proxy credentials: {Credentials}. The only supported credential types are 'Bearer' and 'Basic'.",
                    masked);
            }
        }
        return null;
    }

    private static string AddHttpSchemeIfNoSchemeIsPresent(string proxyUri)
    {
        if (proxyUri.Contains("://", StringComparison.Ordinal))
        {
            return proxyUri;
        }
        if (Log.IsEnabled(LogLevel.Information))
        {
            Log.LogInformation("Proxy URI {ProxyUri} lacks required URI scheme. Scheme 'http' was added as default value.", proxyUri);
        }
        return "http://" + proxyUri;
    }
}
