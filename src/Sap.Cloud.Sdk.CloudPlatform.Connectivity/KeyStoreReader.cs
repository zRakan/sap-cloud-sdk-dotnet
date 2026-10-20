using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Creates key stores (<see cref="X509Certificate2Collection"/>s) from PEM encoded certificates and keys.</summary>
/// <remarks>
/// Replaces the BouncyCastle based Java implementation with <see cref="X509Certificate2"/>. The first certificate
/// of the certificate PEM is the client certificate and carries the private key; further certificates are its chain.
/// Java's key store alias has no equivalent and is omitted.
/// </remarks>
public static class KeyStoreReader
{
    private const string MessageCert = "Provided certificate data did not contain any valid X.509 certificates.";
    private const string MessageKey = "Provided key data did not contain a valid PEM key";

    /// <summary>Creates a key store from a PEM certificate (chain) and PEM private key.</summary>
    /// <param name="certificatePem">One or more PEM <c>CERTIFICATE</c> blocks; the first one is the client certificate.</param>
    /// <param name="keyPem">A PEM private key (PKCS#1, PKCS#8, SEC1 or encrypted PKCS#8).</param>
    /// <param name="password">The password for an encrypted key; ignored for unencrypted keys.</param>
    /// <returns>The key store. The caller owns the certificates and should dispose them.</returns>
    /// <exception cref="DestinationAccessException">The certificates or the key cannot be read.</exception>
    public static X509Certificate2Collection CreateKeyStore(string certificatePem, string keyPem, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(certificatePem);
        ArgumentNullException.ThrowIfNull(keyPem);

        var certificates = Result.Of(() => LoadCertificates(certificatePem))
            .GetValueOrThrow(e => new DestinationAccessException(MessageCert, e));
        var keyStore = new X509Certificate2Collection();
        try
        {
            var leaf = Result.Of(() => LoadClientCertificate(certificates[0], keyPem, password))
                .GetValueOrThrow(e => new DestinationAccessException(MessageKey, e));
            keyStore.Add(leaf);
            for (var i = 1; i < certificates.Count; i++)
            {
                keyStore.Add(certificates[i]);
            }
        }
        catch
        {
            DisposeAll(keyStore);
            throw;
        }
        finally
        {
            certificates[0].Dispose();
        }
        return keyStore;
    }

    /// <summary>Reads all PEM <c>CERTIFICATE</c> blocks.</summary>
    /// <param name="certificatePem">The PEM text.</param>
    /// <returns>The certificates.</returns>
    /// <exception cref="ArgumentException">There is no valid certificate.</exception>
    public static X509Certificate2Collection LoadCertificates(string certificatePem)
    {
        ArgumentNullException.ThrowIfNull(certificatePem);
        var collection = new X509Certificate2Collection();
        collection.ImportFromPem(certificatePem);
        if (collection.Count == 0)
        {
            throw new ArgumentException(MessageCert, nameof(certificatePem));
        }
        return collection;
    }

    private static X509Certificate2 LoadClientCertificate(X509Certificate2 certificate, string keyPem, string? password)
    {
        var encrypted = keyPem.Contains("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal);
        if (encrypted)
        {
            return password is null
                ? throw new CryptographicException("The key is encrypted but no password was provided.")
                : CreateWithKey(certificate, keyPem, password);
        }
        return CreateWithKey(certificate, keyPem, null);
    }

    private static X509Certificate2 CreateWithKey(X509Certificate2 certificate, string keyPem, string? password)
    {
        var pem = certificate.ExportCertificatePem();
        return password is null
            ? X509Certificate2.CreateFromPem(pem, keyPem)
            : X509Certificate2.CreateFromEncryptedPem(pem, keyPem, password);
    }

    private static void DisposeAll(X509Certificate2Collection collection)
    {
        foreach (var certificate in collection)
        {
            certificate.Dispose();
        }
    }
}
