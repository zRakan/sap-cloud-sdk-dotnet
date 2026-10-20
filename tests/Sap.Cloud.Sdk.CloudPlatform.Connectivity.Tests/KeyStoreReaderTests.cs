using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class KeyStoreReaderTests
{
    [Fact]
    public void CreateKeyStore_FromPem_HoldsTheCertificateWithItsKey()
    {
        var pair = TestCertificates.Create("client");

        var keyStore = KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.KeyPem);

        var certificate = Assert.Single(keyStore);
        Assert.Equal("CN=client", certificate.Subject);
        Assert.True(certificate.HasPrivateKey);
        using var rsa = certificate.GetRSAPrivateKey();
        Assert.NotNull(rsa);
        var signature = rsa.SignData("data"u8, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var publicKey = certificate.GetRSAPublicKey();
        Assert.True(publicKey!.VerifyData("data"u8, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void CreateKeyStore_WithEncryptedKey_UsesThePassword()
    {
        var pair = TestCertificates.Create();

        var keyStore = KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.EncryptedKeyPem, pair.Password);

        Assert.True(Assert.Single(keyStore).HasPrivateKey);
    }

    [Fact]
    public void CreateKeyStore_WithEncryptedKeyAndWrongOrNoPassword_Throws()
    {
        var pair = TestCertificates.Create();

        Assert.IsType<DestinationAccessException>(
            Record.Exception(() => KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.EncryptedKeyPem, "wrong")));
        Assert.IsType<DestinationAccessException>(
            Record.Exception(() => KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.EncryptedKeyPem)));
    }

    [Fact]
    public void CreateKeyStore_WithCertificateChain_KeepsAllCertificates_KeyOnTheFirst()
    {
        var leaf = TestCertificates.Create("leaf");
        var issuer = TestCertificates.Create("issuer");

        var keyStore = KeyStoreReader.CreateKeyStore(leaf.CertificatePem + "\n" + issuer.CertificatePem, leaf.KeyPem);

        Assert.Equal(2, keyStore.Count);
        Assert.Equal("CN=leaf", keyStore[0].Subject);
        Assert.True(keyStore[0].HasPrivateKey);
        Assert.Equal("CN=issuer", keyStore[1].Subject);
        Assert.False(keyStore[1].HasPrivateKey);
    }

    [Fact]
    public void CreateKeyStore_WithoutCertificate_Throws()
    {
        var pair = TestCertificates.Create();

        var e = Assert.Throws<DestinationAccessException>(() => KeyStoreReader.CreateKeyStore("no certificate here", pair.KeyPem));
        Assert.Contains("did not contain any valid X.509 certificates", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateKeyStore_WithInvalidKey_Throws_WithoutLeakingTheKey()
    {
        var pair = TestCertificates.Create();

        var e = Assert.Throws<DestinationAccessException>(() => KeyStoreReader.CreateKeyStore(pair.CertificatePem, "not a key"));
        Assert.Contains("did not contain a valid PEM key", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateKeyStore_WithKeyOfAnotherCertificate_Throws()
    {
        var one = TestCertificates.Create("one");
        var other = TestCertificates.Create("other");

        Assert.Throws<DestinationAccessException>(() => KeyStoreReader.CreateKeyStore(one.CertificatePem, other.KeyPem));
    }

    [Fact]
    public void LoadCertificates_ReadsAllCertificateBlocks()
    {
        var pair = TestCertificates.Create();

        Assert.Equal(2, KeyStoreReader.LoadCertificates(pair.CertificatePem + "\n" + pair.CertificatePem).Count);
        Assert.Throws<ArgumentException>(() => KeyStoreReader.LoadCertificates(pair.KeyPem));
    }

    [Fact]
    public void KeyStoreComparator_ComparesCertificatesOnly()
    {
        var pair = TestCertificates.Create();
        var withKey = KeyStoreReader.CreateKeyStore(pair.CertificatePem, pair.KeyPem);
        var withoutKey = KeyStoreReader.LoadCertificates(pair.CertificatePem);
        var other = TestCertificates.CreateKeyStore("other");

        Assert.True(DestinationKeyStoreComparator.KeyStoresEqual(withKey, withoutKey));
        Assert.Equal(
            DestinationKeyStoreComparator.ResolveKeyStoreHashCode(withKey),
            DestinationKeyStoreComparator.ResolveKeyStoreHashCode(withoutKey));
        Assert.False(DestinationKeyStoreComparator.KeyStoresEqual(withKey, other));
        Assert.False(DestinationKeyStoreComparator.KeyStoresEqual(withKey, new X509Certificate2Collection()));
        Assert.True(DestinationKeyStoreComparator.KeyStoresEqual(null, new X509Certificate2Collection()));
    }
}
