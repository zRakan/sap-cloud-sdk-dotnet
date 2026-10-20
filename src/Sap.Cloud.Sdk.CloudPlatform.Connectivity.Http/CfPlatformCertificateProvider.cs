using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Provides the Cloud Foundry instance identity certificate (<c>CF_INSTANCE_CERT</c>, <c>CF_INSTANCE_KEY</c>) as TLS
/// client certificate.
/// </summary>
/// <remarks>
/// Port of <c>CfPlatformSslContextProvider</c>. Java builds an <c>SSLContext</c>; .NET has no such object, so the
/// certificate is used as client certificate of the connection. The buildpack security provider case is not ported.
/// The result is cached for five minutes (well below the one hour of buffer time CF gives) and the files are only
/// re-read if the modification time of the certificate changed.
/// </remarks>
internal sealed class CfPlatformCertificateProvider(Func<string, string?> environmentVariableReader) : IDisposable
{
    internal const string CertEnvironmentVariable = "CF_INSTANCE_CERT";
    internal const string KeyEnvironmentVariable = "CF_INSTANCE_KEY";

    private static readonly ILogger Log = SdkLogging.CreateLogger<CfPlatformCertificateProvider>();
    private readonly Lock _gate = new();
    private X509Certificate2? _certificate;
    private DateTime _cachedAtUtc;
    private DateTime? _lastModifiedUtc;

    public static CfPlatformCertificateProvider Default { get; } = new(Environment.GetEnvironmentVariable);

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the certificate with private key, or <see langword="null"/> if the environment does not define one.</summary>
    /// <exception cref="CloudPlatformException">A declared file does not exist.</exception>
    public X509Certificate2? GetClientCertificate()
    {
        lock (_gate)
        {
            if (_certificate is not null && CacheDuration > TimeSpan.Zero && _cachedAtUtc + CacheDuration >= DateTime.UtcNow)
            {
                return _certificate;
            }
            var certPath = environmentVariableReader(CertEnvironmentVariable);
            var keyPath = environmentVariableReader(KeyEnvironmentVariable);
            if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(keyPath))
            {
                Log.LogWarning(
                    "Unable to create TLS configuration from environment: Environment variables {Cert} and/or {Key} are not defined. "
                    + "Proceeding without platform provided identity certificate. mTLS connections to other systems may not be possible.",
                    CertEnvironmentVariable,
                    KeyEnvironmentVariable);
                return null;
            }
            AssertExists(certPath, "certificate", CertEnvironmentVariable);
            AssertExists(keyPath, "key", KeyEnvironmentVariable);

            var lastModified = File.GetLastWriteTimeUtc(certPath);
            if (_certificate is not null && _lastModifiedUtc == lastModified)
            {
                Log.LogTrace("Certificate file is unchanged, using cached certificate.");
                _cachedAtUtc = DateTime.UtcNow;
                return _certificate;
            }
            var keyStore = KeyStoreReader.CreateKeyStore(File.ReadAllText(certPath), File.ReadAllText(keyPath));
            // The leaf (with private key) is kept; the previous one is left to the finalizer because connections may
            // still use it. The chain certificates are not needed.
            _certificate = keyStore[0];
            for (var i = 1; i < keyStore.Count; i++)
            {
                keyStore[i].Dispose();
            }
            _lastModifiedUtc = lastModified;
            _cachedAtUtc = DateTime.UtcNow;
            return _certificate;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _certificate?.Dispose();
            _certificate = null;
        }
    }

    private static void AssertExists(string path, string kind, string variable)
    {
        if (!File.Exists(path))
        {
            throw new CloudPlatformException($"Failed to read the {kind} file declared in {variable}: File does not exist.");
        }
    }
}
