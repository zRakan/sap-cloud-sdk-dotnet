namespace Sap.Cloud.Sdk.CloudPlatform.Security.Tests;

public class CredentialsTests
{
    [Fact]
    public void BasicCredentials_HeaderAndToString()
    {
        var credentials = new BasicCredentials("user", "pa:ss");
        Assert.Equal("Basic dXNlcjpwYTpzcw==", credentials.HttpHeaderValue);
        Assert.DoesNotContain("pa:ss", credentials.ToString(), StringComparison.Ordinal);
        Assert.Equal(new BasicCredentials("user", "pa:ss"), credentials);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("Bearer token")]
    [InlineData("  bearer   token  ")]
    public void BearerCredentials_StripPrefix(string input)
    {
        var credentials = new BearerCredentials(input);
        Assert.Equal("token", credentials.Token);
        Assert.Equal("Bearer token", credentials.HttpHeaderValue);
        Assert.DoesNotContain("token", credentials.ToString().Replace("token=", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void ClientSecrets_AreHidden()
    {
        Assert.DoesNotContain("s3cr3t", new ClientCredentials("id", "s3cr3t").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PEM", new ClientCertificate("id", "PEM-CERT", "PEM-KEY").ToString(), StringComparison.Ordinal);
        Assert.Equal("aWQ6czNjcjN0", BasicAuthHeaderEncoder.EncodeClientCredentialsBase64(new ClientCredentials("id", "s3cr3t")));
    }
}
