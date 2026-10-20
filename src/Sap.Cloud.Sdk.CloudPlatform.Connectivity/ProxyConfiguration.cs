using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The proxy through which a destination is reached.</summary>
/// <param name="Uri">The proxy URI.</param>
/// <param name="Credentials">Optional credentials for the proxy.</param>
/// <remarks>The credentials types hide their secrets in <c>ToString</c>.</remarks>
public sealed record ProxyConfiguration(Uri Uri, ISecurityCredentials? Credentials = null)
{
    /// <summary>Creates a proxy configuration from a URI string.</summary>
    /// <param name="uri">The proxy URI.</param>
    /// <param name="credentials">Optional credentials.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="UriFormatException">The URI is invalid.</exception>
    public static ProxyConfiguration Of(string uri, ISecurityCredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return new(new Uri(uri, UriKind.RelativeOrAbsolute), credentials);
    }

    /// <summary>Creates a proxy configuration.</summary>
    /// <param name="uri">The proxy URI.</param>
    /// <param name="credentials">Optional credentials.</param>
    /// <returns>The configuration.</returns>
    public static ProxyConfiguration Of(Uri uri, ISecurityCredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return new(uri, credentials);
    }
}
