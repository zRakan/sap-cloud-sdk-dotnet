using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Reads typed destination properties.</summary>
internal interface IPropertyKeyExtractor
{
    bool TryGet<T>(DestinationPropertyKey<T> key, [MaybeNullWhen(false)] out T value);
}

/// <summary>
/// Extracts the key store (client certificate) and trust store from the certificates the destination service returned.
/// </summary>
/// <remarks>
/// <para>
/// Supported key store formats, by file extension: <c>pfx</c> and <c>p12</c> (PKCS#12) and <c>pem</c> (certificate
/// followed by or mixed with a private key). Supported trust store formats: <c>crt</c>, <c>cer</c> and <c>der</c> (a single
/// certificate in DER or PEM form) and <c>jks</c> (read for its certificates only).
/// </para>
/// <para>
/// Differences from Java: <c>KeyStore</c> is an <see cref="X509Certificate2Collection"/> whose first entry carries the
/// private key; JKS key stores (client certificates in JKS files) cannot be loaded, because .NET has no JKS support and
/// the key protection of JKS is proprietary.
/// </para>
/// </remarks>
internal sealed partial class DestinationKeyStoreExtractor(IPropertyKeyExtractor destination)
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationKeyStoreExtractor>();

    private static readonly string[] SupportedKeyStoreExtensions = ["pfx", "p12", "pem"];
    private static readonly string[] SupportedTrustStoreExtensions = ["jks", "crt", "cer", "der"];

    public DestinationKeyStoreExtractor(IDestinationProperties properties)
        : this(new PropertiesAdapter(properties))
    {
    }

    /// <summary>Gets the trust store, if the destination has one.</summary>
    /// <returns>The trust store, or <see langword="null"/>.</returns>
    /// <exception cref="DestinationAccessException">The trust store cannot be created.</exception>
    public X509Certificate2Collection? GetTrustStore()
    {
        if (!destination.TryGet(DestinationProperty.TrustStoreLocation, out _))
        {
            return null;
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Properties {Location} and {Password} found for destination {Name}",
                DestinationProperty.TrustStoreLocation.KeyName,
                DestinationProperty.TrustStorePassword.KeyName,
                destination.TryGet(DestinationProperty.Name, out var name) ? name : "without name");
        }

        var certificate = GetDestinationCertificateFromProperty(DestinationProperty.TrustStoreLocation);
        var extension = GetExtension(certificate.Name!);

        if (extension == "jks")
        {
            return JksCertificateReader.ReadTrustedCertificates(DecodeBase64(certificate.Content!, "Failed to load key store."));
        }
        if (extension is "crt" or "cer" or "der")
        {
            return CreateTrustStoreFromCertificate(certificate.Content!);
        }

        throw new DestinationAccessException(
            $"Could not create Trust Store from file \"{certificate.Name}\". Supported file extensions: {string.Join(", ", SupportedTrustStoreExtensions)}");
    }

    /// <summary>Gets the key store, if the destination has one and its authentication type needs key material.</summary>
    /// <returns>The key store (first entry with the private key), or <see langword="null"/>.</returns>
    /// <exception cref="DestinationAccessException">The key store cannot be created.</exception>
    public X509Certificate2Collection? GetKeyStore()
    {
        if (!destination.TryGet(DestinationProperty.KeyStoreLocation, out _) || !AuthTypeRequiresLoadingKeyMaterial())
        {
            return null;
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Properties {Location} and {Password} found for destination {Name}",
                DestinationProperty.KeyStoreLocation.KeyName,
                DestinationProperty.KeyStorePassword.KeyName,
                destination.TryGet(DestinationProperty.Name, out var name) ? name : "without name");
        }

        var certificate = GetDestinationCertificateFromProperty(DestinationProperty.KeyStoreLocation);
        destination.TryGet(DestinationProperty.KeyStorePassword, out var password);
        var extension = GetExtension(certificate.Name!);
        return extension switch
        {
            "pfx" or "p12" => LoadPkcs12(certificate.Content!, password),
            "pem" => CreateKeyStoreFromPem(certificate.Content!, password),
            "jks" => throw new DestinationAccessException(
                "Could not create Key Store from a JKS file: JKS key stores are not supported on .NET. "
                + $"Upload the client certificate as PKCS#12 ({string.Join(", ", SupportedKeyStoreExtensions)}) instead."),
            _ => throw new DestinationAccessException(
                $"Could not create Key Store with file extension: {extension}. Supported extensions: [{string.Join(", ", SupportedKeyStoreExtensions)}]"),
        };
    }

    internal static X509Certificate2Collection CreateKeyStoreFromPem(string base64Content, string? password)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64Content)).Trim();

            var match = PemCertificates().Match(decoded);
            if (!match.Success)
            {
                throw new ArgumentException("PEM format cannot be parsed: No certificate entry found.");
            }
            var key = (decoded[..match.Index] + decoded[(match.Index + match.Length)..]).Trim();
            if (key.Length == 0)
            {
                throw new ArgumentException("PEM format cannot be parsed: No private key entry found.");
            }
            return KeyStoreReader.CreateKeyStore(match.Value, key, string.IsNullOrEmpty(password) ? null : password);
        }
#pragma warning disable CA1031 // Mirrors Java: every failure to read the PEM data is reported as a DestinationAccessException.
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new DestinationAccessException("Failed to instantiate new KeyStore.", e);
        }
    }

    private static X509Certificate2Collection LoadPkcs12(string base64Content, string? password)
    {
        var bytes = DecodeBase64(base64Content, "Failed to load key store.");
        try
        {
            X509Certificate2Collection loaded;
            try
            {
                // PKCS#12 requires a password to access the key (e.g. for mTLS); a missing one is the empty password.
                loaded = X509CertificateLoader.LoadPkcs12Collection(bytes, password ?? string.Empty);
            }
            catch (CryptographicException) when (string.IsNullOrEmpty(password))
            {
                // files without any password protection are distinct from files with an empty password
                loaded = X509CertificateLoader.LoadPkcs12Collection(bytes, null);
            }
            return PutCertificateWithKeyFirst(loaded);
        }
        catch (CryptographicException e)
        {
            throw new DestinationAccessException("Failed to load key store.", e);
        }
    }

    private static X509Certificate2Collection PutCertificateWithKeyFirst(X509Certificate2Collection loaded)
    {
        var ordered = new X509Certificate2Collection();
        foreach (var certificate in loaded.Where(c => c.HasPrivateKey).Concat(loaded.Where(c => !c.HasPrivateKey)))
        {
            ordered.Add(certificate);
        }
        return ordered;
    }

    private static X509Certificate2Collection CreateTrustStoreFromCertificate(string base64Content)
    {
        var bytes = DecodeBase64(base64Content, "Failed to load key store.");
        try
        {
            var collection = new X509Certificate2Collection();
            if (Encoding.ASCII.GetString(bytes).Contains("-----BEGIN", StringComparison.Ordinal))
            {
                collection.ImportFromPem(Encoding.ASCII.GetString(bytes));
            }
            else
            {
                collection.Add(X509CertificateLoader.LoadCertificate(bytes));
            }
            return collection;
        }
        catch (CryptographicException e)
        {
            throw new DestinationAccessException("Failed to load key store.", e);
        }
    }

    private static byte[] DecodeBase64(string content, string failureMessage)
    {
        try
        {
            return Convert.FromBase64String(content);
        }
        catch (FormatException e)
        {
            throw new DestinationAccessException(failureMessage, e);
        }
    }

    private DestinationServiceV1Response.DestinationCertificate GetDestinationCertificateFromProperty(
        DestinationPropertyKey<string> locationKey)
    {
        destination.TryGet(locationKey, out var location);
        destination.TryGet(DestinationProperty.Name, out var name);
        var certificates = destination.TryGet(DestinationProperty.Certificates, out var list)
            ? list
            : (IReadOnlyList<object?>)[];
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Considering Key Store Location {Location}. Found {Count} key store certificates.",
                location,
                certificates.Count);
        }

        var certificate = certificates
            .OfType<DestinationServiceV1Response.DestinationCertificate>()
            .FirstOrDefault(c => c.Name is not null && c.Name == location && c.Content is not null);
        return certificate
            ?? throw new DestinationAccessException(
                $"Failed to resolve key store '{location}' in destination '{name ?? "without name"}' as no matching certificate was found.");
    }

    private static string GetExtension(string name)
    {
        var extension = Path.GetExtension(name.ToLowerInvariant());
        return extension.TrimStart('.');
    }

    private bool AuthTypeRequiresLoadingKeyMaterial()
    {
        var authType = AuthenticationType.NoAuthentication;
        try
        {
            if (destination.TryGet(DestinationProperty.AuthType, out var parsed))
            {
                authType = parsed;
            }
        }
        catch (ArgumentException)
        {
            // an unknown authentication type is treated like a missing one
        }
        return authType is not (AuthenticationType.OAuth2SamlBearerAssertion or AuthenticationType.SamlAssertion);
    }

    [GeneratedRegex("-+BEGIN CERTIFICATE-+.*-+END CERTIFICATE-+", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PemCertificates();

    private sealed class PropertiesAdapter(IDestinationProperties properties) : IPropertyKeyExtractor
    {
        public bool TryGet<T>(DestinationPropertyKey<T> key, [MaybeNullWhen(false)] out T value) =>
            properties.TryGet(key, out value);
    }
}
