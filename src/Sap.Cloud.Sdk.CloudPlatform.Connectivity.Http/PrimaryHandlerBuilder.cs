using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Builds the <see cref="SocketsHttpHandler"/> that sends the requests of a destination.</summary>
/// <remarks>
/// Port of the connection manager, proxy and TLS parts of <c>DefaultApacheHttpClient5Factory</c> and
/// <c>SSLContextFactory</c>. The handler keeps cookies (Apache's default cookie store; SAP systems bind CSRF tokens to
/// session cookies) and follows redirects (Apache's default); it does not use the system proxy (Java: no
/// <c>useSystemProperties</c>).
/// </remarks>
internal static partial class PrimaryHandlerBuilder
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(PrimaryHandlerBuilder).FullName!);

    public static SocketsHttpHandler Build(
        IHttpDestinationProperties? destination,
        DestinationHttpClientFactoryOptions options,
        CfPlatformCertificateProvider platformCertificates)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = options.Timeout,
            PooledConnectionLifetime = options.PooledConnectionLifetime,
            MaxConnectionsPerServer = options.MaxConnectionsPerRoute,
            UseProxy = false,
        };
        if (destination is null)
        {
            return handler;
        }
        try
        {
            ConfigureProxy(handler, destination);
            handler.SslOptions = CreateSslOptions(destination, platformCertificates);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
        return handler;
    }

    private static void ConfigureProxy(SocketsHttpHandler handler, IHttpDestinationProperties destination)
    {
        var proxy = destination.ProxyConfiguration;
        if (proxy is null)
        {
            if (Log.IsEnabled(LogLevel.Trace))
            {
                Log.LogTrace("No proxy configuration found for destination {Destination}.", Describe(destination));
            }
            return;
        }
        var uri = proxy.Uri;
        if (!uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.Host))
        {
            LogProxyError(destination, "host");
            return;
        }
        if (!ExplicitPort().IsMatch(uri.OriginalString))
        {
            LogProxyError(destination, "port");
            return;
        }
        // No credentials here: a proxy password is sent as Proxy-Authorization header contained in the destination's
        // headers (DestinationRequestHandler). Note that this header does not reach the CONNECT request of an HTTPS target.
        handler.UseProxy = true;
        handler.Proxy = new WebProxy(new Uri(uri.GetLeftPart(UriPartial.Authority)));
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Using the proxy {Proxy} for destination {Destination}.", handler.Proxy.GetProxy(destination.Uri), Describe(destination));
        }
    }

    private static void LogProxyError(IHttpDestinationProperties destination, string missing)
    {
        if (Log.IsEnabled(LogLevel.Error))
        {
            Log.LogError(
                "Failed to load proxy information for destination {Destination}: Undefined {Missing} in URI of proxy configuration. This behavior may be expected in tests and some local runtimes.",
                Describe(destination),
                missing);
        }
    }

    private static SslClientAuthenticationOptions CreateSslOptions(
        IHttpDestinationProperties destination,
        CfPlatformCertificateProvider platformCertificates)
    {
        var ssl = new SslClientAuthenticationOptions();
        if (destination.SecurityConfigurationStrategy == SecurityConfigurationStrategy.FromPlatform)
        {
            ConfigureFromPlatform(ssl, platformCertificates);
            return ssl;
        }

        if (destination.TlsVersion is { } tlsVersion)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Using TLS protocol version \"{Version}\".", tlsVersion);
            }
            ssl.EnabledSslProtocols = ParseTlsVersion(tlsVersion);
        }

        if (destination.IsTrustingAllCertificates)
        {
            // Includes the hostname: Java's NoopHostnameVerifier is used together with TrustAllStrategy.
            if (Log.IsEnabled(LogLevel.Warning))
            {
                Log.LogWarning(
                    "Trusting all certificates for destination {Destination}: server certificates and host names are NOT validated. Do not use this in production.",
                    Describe(destination));
            }
#pragma warning disable CA5359 // Intentional and opt-in per destination (TrustAll property); logged as warning above. Port of Java's TrustAllStrategy.
            ssl.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#pragma warning restore CA5359
        }
        else if (destination.TrustStore is { Count: > 0 } trustStore)
        {
            Log.LogDebug("Using trust store of destination.");
            var snapshot = new X509Certificate2Collection(trustStore);
            ssl.RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                ValidateAgainstTrustStore(certificate, chain, errors, snapshot);
        }

        if (destination.KeyStore is { } keyStore)
        {
            var certificates = new X509CertificateCollection();
            foreach (var certificate in keyStore)
            {
                if (certificate.HasPrivateKey)
                {
                    certificates.Add(certificate);
                }
            }
            if (certificates.Count > 0)
            {
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Using key store of destination with {Count} client certificate(s).", certificates.Count);
                }
                ssl.ClientCertificates = certificates;
            }
            else
            {
                Log.LogDebug("The key store of the destination contains no certificate with private key.");
            }
        }
        return ssl;
    }

    private static void ConfigureFromPlatform(SslClientAuthenticationOptions ssl, CfPlatformCertificateProvider platformCertificates)
    {
        try
        {
            // Fail early if the files are broken; the callback below re-reads (cached) so rotated certificates are used.
            platformCertificates.GetClientCertificate();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new HttpClientInstantiationException("Failed to create default TLS configuration from the platform.", e);
        }
        ssl.LocalCertificateSelectionCallback = (_, _, _, _, _) => platformCertificates.GetClientCertificate()!;
        ssl.ClientCertificates = [];
    }

    /// <summary>
    /// Validates the server certificate against a custom trust store: the chain must end in one of the trusted
    /// certificates (the system store is not consulted), and the host name must match. Revocation is not checked, as in Java.
    /// </summary>
    internal static bool ValidateAgainstTrustStore(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors errors,
        X509Certificate2Collection trustStore)
    {
        if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
        {
            return false;
        }
        using var leaf = new X509Certificate2(certificate);
        using var customChain = new X509Chain();
        customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        customChain.ChainPolicy.CustomTrustStore.AddRange(trustStore);
        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (chain is not null)
        {
            foreach (var element in chain.ChainElements)
            {
                customChain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }
        return customChain.Build(leaf);
    }

    internal static SslProtocols ParseTlsVersion(string version) => version.Trim().ToUpperInvariant() switch
    {
        "TLS" => SslProtocols.None,
        "TLSV1.2" => SslProtocols.Tls12,
        "TLSV1.3" => SslProtocols.Tls13,
        _ => throw new HttpClientInstantiationException(
            $"The TLS version '{version}' is not supported. Supported versions: TLSv1.2, TLSv1.3."),
    };

    private static string Describe(IHttpDestinationProperties destination)
    {
        var uri = destination.Uri;
        var target = uri.IsAbsoluteUri ? $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}" : uri.OriginalString;
        return destination.TryGet(DestinationProperty.Name, out var name) ? $"{name} ({target})" : target;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*://[^/?#]*:\d+")]
    private static partial Regex ExplicitPort();
}
