using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The properties of a destination that is reached over HTTP.</summary>
/// <remarks>
/// Port of <c>HttpDestinationProperties</c>. Java's <c>KeyStore</c> is an <see cref="X509Certificate2Collection"/>: the
/// entry with a private key is the client certificate, further entries are its chain (key store) or trusted
/// certificates (trust store). Java's <c>Option&lt;T&gt;</c> is a nullable type.
/// </remarks>
public interface IHttpDestinationProperties : IDestination
{
    /// <summary>The URI of the destination.</summary>
    Uri Uri { get; }

    /// <summary>The TLS version, if configured.</summary>
    string? TlsVersion { get; }

    /// <summary>The proxy configuration, if any.</summary>
    ProxyConfiguration? ProxyConfiguration { get; }

    /// <summary>The key store holding the client certificate, if any.</summary>
    X509Certificate2Collection? KeyStore { get; }

    /// <summary>The password of the key store, if configured. Confidential.</summary>
    string? KeyStorePassword { get; }

    /// <summary>Whether all server certificates are trusted. Disables certificate validation.</summary>
    bool IsTrustingAllCertificates { get; }

    /// <summary>The Basic credentials, if configured.</summary>
    BasicCredentials? BasicCredentials { get; }

    /// <summary>The authentication type.</summary>
    AuthenticationType AuthenticationType { get; }

    /// <summary>The proxy type, if configured.</summary>
    ProxyType? ProxyType { get; }

    /// <summary>The trust store, if any.</summary>
    X509Certificate2Collection? TrustStore { get; }

    /// <summary>The password of the trust store, if configured. Confidential.</summary>
    string? TrustStorePassword { get; }

    /// <summary>Where the TLS configuration comes from. Defaults to <see cref="SecurityConfigurationStrategy.FromDestination"/>.</summary>
    SecurityConfigurationStrategy SecurityConfigurationStrategy => SecurityConfigurationStrategyExtensions.Default;

    /// <summary>Gets the headers to send with a request.</summary>
    /// <param name="requestUri">The URI of the request.</param>
    /// <returns>The headers (the values may be confidential).</returns>
    /// <exception cref="Exceptions.DestinationAccessException">A header could not be created.</exception>
    IReadOnlyList<Header> GetHeaders(Uri requestUri);
}

/// <summary>Convenience overloads for <see cref="IHttpDestinationProperties"/>.</summary>
public static class HttpDestinationPropertiesExtensions
{
    /// <summary>Gets the headers to send with a request to the destination URI itself.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The headers (the values may be confidential).</returns>
    public static IReadOnlyList<Header> GetHeaders(this IHttpDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.GetHeaders(destination.Uri);
    }
}

/// <summary>An HTTP destination.</summary>
/// <remarks>Kept, as in Java, to ease the upgrade from older versions.</remarks>
public interface IHttpDestination : IHttpDestinationProperties
{
}
