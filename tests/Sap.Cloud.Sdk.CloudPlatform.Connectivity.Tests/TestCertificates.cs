using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

internal sealed record TestKeyPair(string CertificatePem, string KeyPem, string EncryptedKeyPem, string Password);

internal static class TestCertificates
{
    public static TestKeyPair Create(string commonName = "test")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        const string password = "key-password";
        return new TestKeyPair(
            certificate.ExportCertificatePem(),
            rsa.ExportPkcs8PrivateKeyPem(),
            rsa.ExportEncryptedPkcs8PrivateKeyPem(
                password,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)),
            password);
    }

    public static X509Certificate2Collection CreateKeyStore(string commonName = "test")
    {
        var pair = Create(commonName);
        return KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.KeyPem);
    }
}
