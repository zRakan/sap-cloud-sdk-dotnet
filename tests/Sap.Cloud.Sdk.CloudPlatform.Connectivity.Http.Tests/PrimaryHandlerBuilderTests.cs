using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

internal static class Certs
{
    public static X509Certificate2 Create(string commonName, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(notBefore ?? DateTimeOffset.UtcNow.AddDays(-1), notAfter ?? DateTimeOffset.UtcNow.AddDays(30));
    }

    public static X509Certificate2 PublicOnly(X509Certificate2 certificate) => X509CertificateLoader.LoadCertificate(certificate.RawData);
}

public class PrimaryHandlerBuilderTests : HttpStateReset
{
    private static readonly DestinationHttpClientFactoryOptions Defaults = new();

    private static SocketsHttpHandler Build(DefaultHttpDestination.Builder destination, DestinationHttpClientFactoryOptions? options = null) =>
        PrimaryHandlerBuilder.Build(destination.Build(), options ?? Defaults, new CfPlatformCertificateProvider(_ => null));

    [Fact]
    public void WithoutDestination_UsesDefaults()
    {
        using var handler = PrimaryHandlerBuilder.Build(null, Defaults, new CfPlatformCertificateProvider(_ => null));

        Assert.False(handler.UseProxy);
        Assert.True(handler.UseCookies);
        Assert.True(handler.AllowAutoRedirect);
        Assert.Equal(TimeSpan.FromMinutes(2), handler.ConnectTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);
        Assert.Equal(100, handler.MaxConnectionsPerServer);
    }

    [Fact]
    public void Options_AreApplied()
    {
        using var handler = Build(
            DefaultHttpDestination.CreateBuilder("https://host"),
            new DestinationHttpClientFactoryOptions
            {
                Timeout = TimeSpan.FromSeconds(7),
                MaxConnectionsPerRoute = 3,
                PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            });

        Assert.Equal(TimeSpan.FromSeconds(7), handler.ConnectTimeout);
        Assert.Equal(3, handler.MaxConnectionsPerServer);
        Assert.Equal(TimeSpan.FromMinutes(1), handler.PooledConnectionLifetime);
    }

    [Fact]
    public void Proxy_IsConfigured_WithoutCredentials()
    {
        using var handler = Build(DefaultHttpDestination.CreateBuilder("http://target:80")
            .ProxyConfiguration(ProxyConfiguration.Of("http://proxy.example:8080/ignored", new Sap.Cloud.Sdk.CloudPlatform.Security.BasicCredentials("u", "p"))));

        Assert.True(handler.UseProxy);
        var proxy = Assert.IsType<WebProxy>(handler.Proxy);
        Assert.Equal(new Uri("http://proxy.example:8080"), proxy.GetProxy(new Uri("http://target:80")));
        Assert.Null(proxy.Credentials);
    }

    [Fact]
    public void NoProxy_DoesNotUseSystemProxy()
    {
        using var handler = Build(DefaultHttpDestination.CreateBuilder("https://host"));

        Assert.False(handler.UseProxy);
        Assert.Null(handler.Proxy);
    }

    [Theory]
    [InlineData("http://proxy.example")]
    public void ProxyWithoutPort_IsIgnored_AndLogged(string proxy)
    {
        var logs = new CapturingLoggerFactory();
        SdkLogging.LoggerFactory = logs;

        using var handler = Build(DefaultHttpDestination.CreateBuilder("http://target")
            .ProxyConfiguration(new ProxyConfiguration(new Uri(proxy, UriKind.RelativeOrAbsolute))));

        Assert.False(handler.UseProxy);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("proxy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KeyStore_ProvidesTheClientCertificate()
    {
        using var certificate = Certs.Create("client");
        using var handler = Build(DefaultHttpDestination.CreateBuilder("https://host")
            .KeyStore([certificate]));

        var configured = Assert.Single(handler.SslOptions.ClientCertificates!);
        Assert.Equal(certificate.Thumbprint, Assert.IsType<X509Certificate2>(configured).Thumbprint);
    }

    [Fact]
    public void KeyStoreWithoutPrivateKey_ConfiguresNoClientCertificate()
    {
        using var certificate = Certs.PublicOnly(Certs.Create("client"));
        using var handler = Build(DefaultHttpDestination.CreateBuilder("https://host").KeyStore([certificate]));

        Assert.True(handler.SslOptions.ClientCertificates is null or { Count: 0 });
    }

    [Theory]
    [InlineData("TLSv1.2", SslProtocols.Tls12)]
    [InlineData("TLSv1.3", SslProtocols.Tls13)]
    [InlineData("TLS", SslProtocols.None)]
    public void TlsVersion_IsMapped(string version, SslProtocols expected)
    {
        using var handler = Build(DefaultHttpDestination.CreateBuilder("https://host").TlsVersion(version));

        Assert.Equal(expected, handler.SslOptions.EnabledSslProtocols);
    }

    [Fact]
    public void UnsupportedTlsVersion_IsRejected() =>
        Assert.Throws<HttpClientInstantiationException>(() => Build(DefaultHttpDestination.CreateBuilder("https://host").TlsVersion("TLSv1.1")));

    [Fact]
    public void TrustAll_AcceptsEverything_AndWarnsOncePerCachedDestination()
    {
        var logs = new CapturingLoggerFactory();
        SdkLogging.LoggerFactory = logs;
        var destination = DefaultHttpDestination.CreateBuilder("https://host").TrustAllCertificates().Build();
        using var factory = new DefaultDestinationHttpClientFactory(Defaults);

        using var first = factory.CreateHttpClient(destination);
        using var second = factory.CreateHttpClient(destination);

        Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Trusting all certificates", StringComparison.Ordinal));
        using var handler = PrimaryHandlerBuilder.Build(destination, Defaults, new CfPlatformCertificateProvider(_ => null));
        var callback = handler.SslOptions.RemoteCertificateValidationCallback!;
        Assert.True(callback(this, null, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void WithoutTrustAll_NoCallbackIsSet_AndTheSystemStoreIsUsed()
    {
        using var handler = Build(DefaultHttpDestination.CreateBuilder("https://host"));

        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }

    [Fact]
    public void TrustStore_TrustsOnlyItsCertificates()
    {
        using var trusted = Certs.Create("trusted");
        using var other = Certs.Create("other");
        using var trustedPublic = Certs.PublicOnly(trusted);
        using var handler = Build(DefaultHttpDestination.CreateBuilder("https://host").TrustStore([trustedPublic]));

        var callback = handler.SslOptions.RemoteCertificateValidationCallback!;

        Assert.True(callback(this, trusted, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(this, other, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void TrustStore_StillVerifiesTheHostName()
    {
        using var trusted = Certs.Create("trusted");
        var store = new X509Certificate2Collection(Certs.PublicOnly(trusted));

        Assert.False(PrimaryHandlerBuilder.ValidateAgainstTrustStore(
            trusted, null, SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors, store));
        Assert.False(PrimaryHandlerBuilder.ValidateAgainstTrustStore(trusted, null, SslPolicyErrors.RemoteCertificateNotAvailable, store));
        Assert.False(PrimaryHandlerBuilder.ValidateAgainstTrustStore(null, null, SslPolicyErrors.None, store));
    }

    [Fact]
    public void TrustStore_RejectsExpiredCertificates()
    {
        using var expired = Certs.Create("expired", DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        var store = new X509Certificate2Collection(Certs.PublicOnly(expired));

        Assert.False(PrimaryHandlerBuilder.ValidateAgainstTrustStore(expired, null, SslPolicyErrors.RemoteCertificateChainErrors, store));
    }

    // ---- platform (Cloud Foundry instance identity) -------------------------------------------------------------

    [Fact]
    public void FromPlatform_UsesTheInstanceIdentityCertificateAsClientCertificate()
    {
        using var directory = new TempDirectory();
        var variables = directory.WriteInstanceIdentity("instance");
        using var provider = new CfPlatformCertificateProvider(n => variables.GetValueOrDefault(n));
        var destination = DefaultHttpDestination.CreateBuilder("https://host")
            .SecurityConfiguration(SecurityConfigurationStrategy.FromPlatform)
            .TrustAllCertificates() // ignored for the platform strategy, as in Java
            .Build();

        using var handler = PrimaryHandlerBuilder.Build(destination, Defaults, provider);

        var selected = handler.SslOptions.LocalCertificateSelectionCallback!(this, "host", [], null, []);
        Assert.True(Assert.IsType<X509Certificate2>(selected).HasPrivateKey);
        Assert.Contains("CN=instance", selected.Subject, StringComparison.Ordinal);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }

    [Fact]
    public void Platform_WithoutEnvironmentVariables_WarnsAndProvidesNothing()
    {
        var logs = new CapturingLoggerFactory();
        SdkLogging.LoggerFactory = logs;
        using var provider = new CfPlatformCertificateProvider(_ => null);

        Assert.Null(provider.GetClientCertificate());
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("CF_INSTANCE_CERT", StringComparison.Ordinal));
    }

    [Fact]
    public void Platform_WithMissingFile_Fails()
    {
        using var provider = new CfPlatformCertificateProvider(name => name == "CF_INSTANCE_CERT" ? "/does/not/exist.crt" : "/does/not/exist.key");

        Assert.Throws<CloudPlatformException>(() => provider.GetClientCertificate());
    }

    [Fact]
    public void FromPlatform_WithMissingFile_FailsClientCreation()
    {
        using var provider = new CfPlatformCertificateProvider(_ => "/does/not/exist");
        var destination = DefaultHttpDestination.CreateBuilder("https://host").SecurityConfiguration(SecurityConfigurationStrategy.FromPlatform).Build();

        Assert.Throws<HttpClientInstantiationException>(() => PrimaryHandlerBuilder.Build(destination, Defaults, provider));
    }

    [Fact]
    public void Platform_CachesTheCertificate_AndReloadsWhenTheFileChanges()
    {
        using var directory = new TempDirectory();
        var variables = directory.WriteInstanceIdentity("first");
        using var provider = new CfPlatformCertificateProvider(n => variables.GetValueOrDefault(n));

        var first = provider.GetClientCertificate();
        Assert.Same(first, provider.GetClientCertificate());

        provider.CacheDuration = TimeSpan.Zero;
        Assert.Same(first, provider.GetClientCertificate()); // file unchanged: still cached

        directory.WriteInstanceIdentity("second");
        File.SetLastWriteTimeUtc(variables["CF_INSTANCE_CERT"]!, DateTime.UtcNow.AddMinutes(1));
        var second = provider.GetClientCertificate();

        Assert.NotSame(first, second);
        Assert.Contains("CN=second", second!.Subject, StringComparison.Ordinal);
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string _path = Directory.CreateTempSubdirectory("cf-identity").FullName;

        public Dictionary<string, string?> WriteInstanceIdentity(string commonName)
        {
            using var certificate = Certs.Create(commonName);
            using var rsa = certificate.GetRSAPrivateKey()!;
            var certPath = Path.Combine(_path, "instance.crt");
            var keyPath = Path.Combine(_path, "instance.key");
            File.WriteAllText(certPath, certificate.ExportCertificatePem());
            File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
            return new Dictionary<string, string?> { ["CF_INSTANCE_CERT"] = certPath, ["CF_INSTANCE_KEY"] = keyPath };
        }

        public void Dispose() => Directory.Delete(_path, recursive: true);
    }
}
