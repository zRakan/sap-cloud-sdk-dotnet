using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;

/// <summary>
/// The identity used to authenticate at a token endpoint: a client secret, or an X.509 client certificate (mTLS).
/// </summary>
/// <remarks>
/// Port of <c>com.sap.cloud.security.config.ClientIdentity</c>. Create it from the SDK's
/// <see cref="ClientCredentials"/> or <see cref="ClientCertificate"/> with <see cref="From"/>.
/// </remarks>
internal sealed class OAuth2ClientIdentity : IEquatable<OAuth2ClientIdentity>
{
    private readonly Lazy<X509Certificate2>? _certificate;

    private OAuth2ClientIdentity(string id, string? secret, string? certificatePem, string? keyPem)
    {
        Id = id;
        Secret = secret;
        CertificatePem = certificatePem;
        KeyPem = keyPem;
        if (certificatePem is not null && keyPem is not null)
        {
            _certificate = new Lazy<X509Certificate2>(() => LoadCertificate(certificatePem, keyPem));
        }
    }

    public string Id { get; }

    public string? Secret { get; }

    public string? CertificatePem { get; }

    public string? KeyPem { get; }

    public bool IsCertificateBased => _certificate is not null;

    /// <summary>The client certificate with private key. Only for certificate-based identities.</summary>
    public X509Certificate2 Certificate =>
        _certificate?.Value ?? throw new InvalidOperationException($"Client identity '{Id}' is not certificate-based.");

    public static OAuth2ClientIdentity From(ISecurityCredentials credentials) => credentials switch
    {
        ClientCredentials c => new(c.ClientId, c.ClientSecret, null, null),
        ClientCertificate c => new(c.ClientId, null, c.Certificate, c.Key),
        _ => throw new ArgumentException(
            $"Unsupported client identity type {credentials?.GetType().Name ?? "null"}.", nameof(credentials)),
    };

    public bool Equals(OAuth2ClientIdentity? other) =>
        other is not null
        && Id == other.Id
        && Secret == other.Secret
        && CertificatePem == other.CertificatePem
        && KeyPem == other.KeyPem;

    public override bool Equals(object? obj) => Equals(obj as OAuth2ClientIdentity);

    public override int GetHashCode() => HashCode.Combine(Id, Secret, CertificatePem, KeyPem);

    public override string ToString() =>
        $"OAuth2ClientIdentity(id={Id}, {(IsCertificateBased ? "certificate" : "secret")}=(hidden))";

    private static X509Certificate2 LoadCertificate(string certificatePem, string keyPem)
    {
        using var ephemeral = X509Certificate2.CreateFromPem(certificatePem, keyPem);
        // Re-import so the private key is usable for TLS client authentication on all platforms.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }
}
