using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>Extraction of key stores and trust stores from the certificates of the destination service (port of <c>DestinationKeyStoreExtractorTest</c>).</summary>
public sealed class DestinationKeyStoreExtractorTests
{
    private const string DestinationName = "Testname";
    private const string KeyStorePassword = "Initial1";
    private const string Pkcs12Password = "cca-password";
    private const string PemPassword = "foobar";

    private static DefaultHttpDestination Destination(
        string? keyStoreLocation,
        string? keyStorePassword,
        string? trustStoreLocation,
        string fileName,
        string content,
        AuthenticationType? authType = null)
    {
        var builder = DefaultHttpDestination.CreateBuilder("https://www.sap.de").Name(DestinationName);
        if (keyStoreLocation is not null)
        {
            builder.Property(DestinationProperty.KeyStoreLocation, keyStoreLocation);
        }
        if (keyStorePassword is not null)
        {
            builder.Property(DestinationProperty.KeyStorePassword, keyStorePassword);
        }
        if (trustStoreLocation is not null)
        {
            builder.Property(DestinationProperty.TrustStoreLocation, trustStoreLocation);
            builder.Property(DestinationProperty.TrustStorePassword, KeyStorePassword);
        }
        if (authType is { } type)
        {
            builder.Property(DestinationProperty.AuthType, type);
        }
        builder.Property(
            DestinationProperty.Certificates,
            (IReadOnlyList<object?>)[new DestinationServiceV1Response.DestinationCertificate { Name = fileName, Content = content, Type = "CERTIFICATE" }]);
        return builder.Build();
    }

    private static DefaultHttpDestination KeyStoreDestination(string fileName, string? password, string content) =>
        Destination(fileName, password, null, fileName, content);

    private static DefaultHttpDestination TrustStoreDestination(string fileName, string content) =>
        Destination(null, null, fileName, fileName, content);

    [Fact]
    public void NoCertificatesDefined_MeansNoKeyStore()
    {
        var sut = new DestinationKeyStoreExtractor(DefaultDestination.CreateBuilder().Build());

        Assert.Null(sut.GetKeyStore());
        Assert.Null(sut.GetTrustStore());
    }

    [Theory]
    [MemberData(nameof(AllAuthenticationTypes))]
    public void KeyStoreIsIgnoredForSelectAuthTypes(AuthenticationType authType)
    {
        var destination = DefaultHttpDestination.CreateBuilder("foo.com")
            .Property(DestinationProperty.AuthType, authType)
            .Property(DestinationProperty.KeyStoreLocation, "foo")
            .Build();
        var sut = new DestinationKeyStoreExtractor(destination);

        if (authType is AuthenticationType.SamlAssertion or AuthenticationType.OAuth2SamlBearerAssertion)
        {
            Assert.Null(sut.GetKeyStore());
        }
        else
        {
            Assert.Throws<DestinationAccessException>(sut.GetKeyStore);
        }
    }

    public static TheoryData<AuthenticationType> AllAuthenticationTypes() => [.. Enum.GetValues<AuthenticationType>()];

    [Fact]
    public void Pkcs12KeyStore_IsReadWithPassword_AndTheKeyComesFirst()
    {
        var store = new DestinationKeyStoreExtractor(
            KeyStoreDestination("secret-store.p12", Pkcs12Password, KeyStoreTestData.Pkcs12WithPassword)).GetKeyStore();

        Assert.NotNull(store);
        var first = store[0];
        Assert.True(first.HasPrivateKey);
        Assert.NotNull(first.GetRSAPrivateKey());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Pkcs12KeyStore_WithoutPassword_IsRead(string? password)
    {
        var destination = KeyStoreDestination("secret-store.p12", password, KeyStoreTestData.Pkcs12NoPassword);

        var store = new DestinationKeyStoreExtractor(destination).GetKeyStore();

        Assert.NotNull(store);
        Assert.Single(store);
        Assert.True(store[0].HasPrivateKey);
    }

    [Fact]
    public void Pkcs12KeyStore_WithWrongPassword_Fails()
    {
        var destination = KeyStoreDestination("secret-store.p12", "wrong", KeyStoreTestData.Pkcs12WithPassword);

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetKeyStore());

        Assert.Equal("Failed to load key store.", failure.Message);
        Assert.IsAssignableFrom<CryptographicException>(failure.InnerException);
    }

    [Fact]
    public void PemKeyStore_IsReadWithCertificateAndEncryptedKey()
    {
        var destination = KeyStoreDestination("certs.pem", PemPassword, KeyStoreTestData.PemWithKey);

        var store = new DestinationKeyStoreExtractor(destination).GetKeyStore();

        Assert.NotNull(store);
        Assert.True(store[0].HasPrivateKey);
        Assert.NotNull(store[0].GetRSAPrivateKey());
        Assert.Contains("CN=pem-test", store[0].Subject, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-----BEGIN CERTIFICATE-----\nFOO\n-----END CERTIFICATE-----\n")]
    [InlineData("-----BEGIN PRIVATE KEY-----\nFOO\n-----END PRIVATE KEY-----\n")]
    public void BrokenPemKeyStore_Fails(string pem)
    {
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));
        var destination = KeyStoreDestination("foo.pem", PemPassword, content);

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetKeyStore());

        Assert.Contains("Failed to instantiate new KeyStore.", failure.Message, StringComparison.Ordinal);
        Assert.StartsWith("PEM format cannot be parsed", failure.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrokenPemKeyStore_WithSeveralCertificatesAndNoKey_Fails()
    {
        var pem = string.Concat(Enumerable.Repeat("-----BEGIN CERTIFICATE-----\nFOO\n-----END CERTIFICATE-----\n", 5));
        var destination = KeyStoreDestination("foo.pem", PemPassword, Convert.ToBase64String(Encoding.UTF8.GetBytes(pem)));

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetKeyStore());

        Assert.Contains("No private key entry found", failure.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyStore_WithUnsupportedExtension_Fails()
    {
        var destination = KeyStoreDestination("store.jpg", null, KeyStoreTestData.Pkcs12NoPassword);

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetKeyStore());

        Assert.Contains("Supported extensions", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyStore_InJksFormat_IsNotSupported()
    {
        var destination = KeyStoreDestination("store.jks", KeyStorePassword, KeyStoreTestData.JksTrustCert);

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetKeyStore());

        Assert.Contains("JKS key stores are not supported", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyStore_WithoutMatchingCertificate_Fails()
    {
        var destination = Destination("other.p12", null, null, "store.p12", KeyStoreTestData.Pkcs12NoPassword);

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetKeyStore());

        Assert.Equal(
            "Failed to resolve key store 'other.p12' in destination 'Testname' as no matching certificate was found.",
            failure.Message);
    }

    [Theory]
    [InlineData("trustcert.crt")]
    [InlineData("trustcert.cer")]
    [InlineData("trustcert.der")]
    public void TrustStore_FromCertificateFile_ContainsTheCertificate(string fileName)
    {
        var store = new DestinationKeyStoreExtractor(TrustStoreDestination(fileName, KeyStoreTestData.Certificate)).GetTrustStore();

        Assert.NotNull(store);
        var certificate = Assert.Single(store);
        Assert.False(certificate.HasPrivateKey);
        Assert.Contains("CN=", certificate.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void TrustStore_FromDerEncodedCertificate_ContainsTheCertificate()
    {
        var pem = Encoding.ASCII.GetString(Convert.FromBase64String(KeyStoreTestData.Certificate));
        var der = X509Certificate2.CreateFromPem(pem).RawData;

        var store = new DestinationKeyStoreExtractor(TrustStoreDestination("trustcert.der", Convert.ToBase64String(der))).GetTrustStore();

        Assert.Single(store!);
    }

    [Fact]
    public void TrustStore_FromJksFile_ContainsTheCertificatesOfTheEntries()
    {
        var store = new DestinationKeyStoreExtractor(TrustStoreDestination("trustcert.jks", KeyStoreTestData.JksTrustCert)).GetTrustStore();

        Assert.NotNull(store);
        var certificate = Assert.Single(store);
        Assert.False(string.IsNullOrEmpty(certificate.Subject));
    }

    [Fact]
    public void TrustStore_FromUnsupportedFileExtension_Fails()
    {
        var destination = TrustStoreDestination("trustcert.jpg", KeyStoreTestData.JksTrustCert);

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetTrustStore());

        Assert.Contains("Could not create Trust Store from file \"trustcert.jpg\"", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAAA")]
    [InlineData("/u3+7QAAAAIAAAABAAAAAQAJ")]
    public void TrustStore_FromBrokenJksFile_Fails(string content)
    {
        var destination = TrustStoreDestination("trustcert.jks", content);

        Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetTrustStore());
    }

    [Fact]
    public void TrustStore_FromContentThatIsNotBase64_Fails()
    {
        var destination = TrustStoreDestination("trustcert.crt", "this is not base64!");

        var failure = Assert.Throws<DestinationAccessException>(() => new DestinationKeyStoreExtractor(destination).GetTrustStore());

        Assert.IsType<FormatException>(failure.InnerException);
    }
}
