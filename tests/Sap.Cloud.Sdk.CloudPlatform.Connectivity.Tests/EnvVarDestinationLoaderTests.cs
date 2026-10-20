using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class EnvVarDestinationLoaderTests
{
    private const string VariableName = "destinations";

    private static Result<IDestination> Load(string json, string name) =>
        new EnvVarDestinationLoader(v => v == VariableName ? json : null, VariableName).TryGetDestination(name);

    private static IHttpDestination LoadHttp(string json, string name = "MyDestination") =>
        Load(json, name).GetValueOrThrow().AsHttp();

    [Fact]
    public void BasicAttributes_AreParsed()
    {
        var result = Load(
            """
            [{
              "name": "someDestination", "URL": "https://www.sap.de", "username": "USER", "password": "PASSWORD",
              "object": { "inner": "value", "other": "also value" }
            }]
            """,
            "someDestination");

        Assert.True(result.IsSuccess);
        var destination = result.GetValueOrThrow();
        Assert.True(destination.IsHttp());
        Assert.Equal(new Uri("https://www.sap.de"), destination.AsHttp().Uri);
        Assert.Contains("inner", Assert.IsType<string>(destination.GetProperty("object")), StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteDestination_IsParsed()
    {
        var destination = LoadHttp(
            """
            [{
              "type": "HTTP", "name": "MyDestination", "proxyType": "Internet", "description": "This destination rocks!",
              "authentication": "BasicAuthentication", "URL": "https://URL", "user": "USER", "password": "PASSWORD",
              "isTrustingAllCertificates": "true", "TrustStoreLocation": "LOCATION", "TrustStorePassword": "PASSWORD",
              "KeyStoreLocation": "LOCATION", "KeyStorePassword": "PASSWORD", "TLSVersion": "1.3", "proxy": "proxy:1234"
            }]
            """);

        Assert.Equal("This destination rocks!", destination.GetProperty("description"));
        Assert.True(destination.TryGet(DestinationProperty.Name, out var name));
        Assert.Equal("MyDestination", name);
        Assert.Equal(ProxyType.Internet, destination.ProxyType);
        Assert.Equal(new Uri("https://URL"), destination.Uri);
        Assert.True(destination.IsTrustingAllCertificates);
        Assert.Equal(new Uri("http://proxy:1234"), destination.ProxyConfiguration!.Uri);
        Assert.Equal("1.3", destination.TlsVersion);
        Assert.Equal(AuthenticationType.BasicAuthentication, destination.AuthenticationType);
        Assert.Equal(new Security.BasicCredentials("USER", "PASSWORD"), destination.BasicCredentials);
    }

    [Fact]
    public void ProxyUriWithScheme_KeepsScheme()
    {
        var proxy = LoadHttp("""[{"name":"MyDestination","URL":"https://URL","proxy":"https://proxy:1234"}]""").ProxyConfiguration!.Uri;

        Assert.Equal("https", proxy.Scheme);
        Assert.Equal("proxy", proxy.Host);
        Assert.Equal(1234, proxy.Port);
    }

    [Fact]
    public void ProxyUri_WinsOverProxyHostAndPort()
    {
        var proxy = LoadHttp(
            """[{"name":"MyDestination","URL":"https://URL","proxyHost":"looser","proxyPort":"5678","proxy":"winner:1234"}]""")
            .ProxyConfiguration!.Uri;

        Assert.Equal("winner", proxy.Host);
        Assert.Equal(1234, proxy.Port);
        Assert.Equal("http", proxy.Scheme);
    }

    [Fact]
    public void ProxyHostAndPort_AreCombined()
    {
        var proxy = LoadHttp(
            """[{"name":"MyDestination","URL":"https://URL","proxyHost":"proxy","proxyPort":"1234",}]""")
            .ProxyConfiguration!.Uri;

        Assert.Equal("proxy", proxy.Host);
        Assert.Equal(1234, proxy.Port);
        Assert.Equal("http", proxy.Scheme);
    }

    [Fact]
    public void NonStringValues_KeepTheirJsonText()
    {
        var destination = Load(
            """[{"name":"MyDestination","URL":"https://URL","shoeSize":42,"flag":true}]""",
            "MyDestination").GetValueOrThrow();

        Assert.Equal("42", destination.GetProperty("shoeSize"));
        Assert.Equal("true", destination.GetProperty("flag"));
    }

    [Fact]
    public void PropertyNames_AreCaseInsensitive_FallbackKeysWork()
    {
        var destination = LoadHttp(
            """[{"type":"HTTP","name":"MyDestination","URL":"https://URL","username":"USER","password":"PASSWORD","authtype":"BasicAuthentication"}]""");

        Assert.Equal("USER", destination.BasicCredentials!.Username);
        Assert.Equal(AuthenticationType.BasicAuthentication, destination.AuthenticationType);
    }

    [Fact]
    public void UserAndPassword_WithoutAuthentication_FallBackToBasic()
    {
        var destination = LoadHttp(
            """[{"type":"HTTP","name":"MyDestination","URL":"https://URL","username":"USER","password":"PASSWORD"}]""");

        Assert.Equal(AuthenticationType.BasicAuthentication, destination.AuthenticationType);
    }

    [Fact]
    public void TokenForwarding_IsRead()
    {
        Assert.Equal(
            AuthenticationType.TokenForwarding,
            LoadHttp("""[{"name":"MyDestination","URL":"https://URL","authentication":"TokenForwarding"}]""").AuthenticationType);
        Assert.Equal(
            AuthenticationType.TokenForwarding,
            LoadHttp("""[{"name":"MyDestination","URL":"https://URL","forwardAuthToken":"true"}]""").AuthenticationType);
    }

    [Fact]
    public void DestinationWithoutName_IsAnAccessError_WithoutLeakingValues()
    {
        var result = Load("""[{"type":"HTTP","URL":"https://URL","password":"very-secret"}]""", "MyDestination");

        var e = Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.DoesNotContain("very-secret", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownName_IsNotFound()
    {
        var result = Load("""[{"name":"other","URL":"https://URL"}]""", "MyDestination");

        Assert.IsType<DestinationNotFoundException>(result.Exception);
    }

    [Fact]
    public void EmptyArray_IsNotFound() =>
        Assert.IsType<DestinationNotFoundException>(Load("[]", "some-name").Exception);

    [Fact]
    public void MissingOrEmptyVariable_IsNotFound()
    {
        var missing = new EnvVarDestinationLoader(_ => null, VariableName).TryGetDestination("x");
        var empty = new EnvVarDestinationLoader(_ => "", VariableName).TryGetDestination("x");

        Assert.IsType<DestinationNotFoundException>(missing.Exception);
        Assert.IsType<DestinationNotFoundException>(empty.Exception);
    }

    [Fact]
    public void InvalidJson_IsAnAccessError() =>
        Assert.IsType<DestinationAccessException>(Load("[{", "x").Exception);

    [Fact]
    public void NonArrayJson_IsAnAccessError() =>
        Assert.IsType<DestinationAccessException>(Load("""{"name":"x"}""", "x").Exception);

    [Fact]
    public void RfcDestination_IsReturnedAsPlainDestination()
    {
        var destination = Load("""[{"name":"rfc","Type":"RFC","jco.client.ashost":"host"}]""", "rfc").GetValueOrThrow();

        Assert.IsType<DefaultDestination>(destination);
        Assert.False(destination.IsHttp());
    }

    [Fact]
    public void TryGetAllDestinations_ReturnsAllDestinations()
    {
        var loader = new EnvVarDestinationLoader(
            _ => """[{"name":"a","URL":"https://a"},{"name":"b","URL":"https://b"}]""",
            VariableName);

        var all = loader.TryGetAllDestinations().GetValueOrThrow();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void ReadsTheProcessEnvironmentByDefault()
    {
        const string variable = "SAP_SDK_TEST_DESTINATIONS_7f3a";
        Environment.SetEnvironmentVariable(variable, """[{"name":"fromEnv","URL":"https://env.example"}]""");
        try
        {
            var result = new EnvVarDestinationLoader(null, variable).TryGetDestination("fromEnv");

            Assert.Equal(new Uri("https://env.example"), result.GetValueOrThrow().AsHttp().Uri);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task TryGetDestinationAsync_ReturnsTheSameAsTheSyncMethod()
    {
        var loader = new EnvVarDestinationLoader(_ => """[{"name":"a","URL":"https://a"}]""", VariableName);

        var result = await loader.TryGetDestinationAsync("a", DestinationOptions.CreateBuilder().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
