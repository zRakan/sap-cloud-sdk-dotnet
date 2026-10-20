using System.Globalization;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class ErpDestinationHeaderProviderTests : ConnectivityStateReset
{
    private static readonly Uri TestUri = new("https://sap.com");

    [Fact]
    public void NoErpProperties_ProduceNoHeaders() =>
        Assert.Empty(DefaultHttpDestination.CreateBuilder(TestUri).Build().GetHeaders(TestUri));

    [Fact]
    public void ClientAndLanguage_AreSent()
    {
        var destination = DefaultHttpDestination.CreateBuilder(TestUri)
            .Property(DestinationProperty.SapClient, "123")
            .Property(DestinationProperty.SapLanguage, "it")
            .Build();

        Assert.Equal(
            [new Header("sap-client", "123"), new Header("sap-language", "it")],
            destination.GetHeaders(TestUri));
    }

    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("zh_Hans", "zh")]
    [InlineData("EN", "en")]
    public void LanguageTag_IsReducedToTheLanguage(string tag, string expected)
    {
        var destination = DefaultHttpDestination.CreateBuilder(TestUri).Property(DestinationProperty.SapLanguage, tag).Build();

        Assert.Equal(new Header("sap-language", expected), Assert.Single(destination.GetHeaders(TestUri)));
    }

    [Fact]
    public void DynamicLanguage_UsesCurrentCulture_AndWinsOverStaticLanguage()
    {
        var destination = DefaultHttpDestination.CreateBuilder(TestUri)
            .Property(DestinationProperty.SapLanguage, "fr")
            .Property(DestinationProperty.DynamicSapLanguage, true)
            .Build();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");

            Assert.Equal(new Header("sap-language", "it"), Assert.Single(destination.GetHeaders(TestUri)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DynamicLanguageDisabled_UsesStaticLanguage()
    {
        var destination = DefaultHttpDestination.CreateBuilder(TestUri)
            .Property(DestinationProperty.SapLanguage, "fr")
            .Property("cloudsdk.dynamicSapLanguage", "false")
            .Build();

        Assert.Equal(new Header("sap-language", "fr"), Assert.Single(destination.GetHeaders(TestUri)));
    }
}
