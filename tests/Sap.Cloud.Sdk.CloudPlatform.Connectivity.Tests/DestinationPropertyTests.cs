namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class DestinationPropertyTests
{
    [Fact]
    public void Properties_AreCaseInsensitive()
    {
        var destination = DefaultDestination.CreateBuilder().Property("URL", "https://x").Build();

        Assert.Equal("https://x", destination.GetProperty("url"));
        Assert.Equal("https://x", destination.GetProperty("Url"));
        Assert.Null(destination.GetProperty("missing"));
        Assert.Equal(["URL"], destination.PropertyNames);
    }

    [Fact]
    public void TypedKey_ConvertsStringsAndAcceptsInstances()
    {
        var fromString = DefaultDestination.CreateBuilder().Property("ProxyPort", "8080").Property("TrustAll", "TRUE").Build();
        var fromValue = DefaultDestination.CreateBuilder().Property("ProxyPort", 8080).Property("TrustAll", true).Build();

        Assert.True(fromString.TryGet(DestinationProperty.ProxyPort, out var port));
        Assert.Equal(8080, port);
        Assert.True(fromString.TryGet(DestinationProperty.TrustAll, out var trustAll));
        Assert.True(trustAll);
        Assert.True(fromValue.TryGet(DestinationProperty.ProxyPort, out port));
        Assert.Equal(8080, port);
        Assert.True(fromValue.TryGet(DestinationProperty.TrustAll, out trustAll));
        Assert.True(trustAll);
        Assert.False(fromValue.TryGet(DestinationProperty.TlsVersion, out _));
    }

    [Fact]
    public void TypedKey_WithWrongType_Throws()
    {
        var destination = DefaultDestination.CreateBuilder().Property("URL", 42).Build();

        var e = Assert.Throws<ArgumentException>(() => destination.TryGet(DestinationProperty.Uri, out _));
        Assert.Contains("Unexpected type", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnumKeys_ParseIdentifiers()
    {
        var destination = DefaultDestination.CreateBuilder()
            .Property("Authentication", "OAuth2SAMLBearerAssertion")
            .Property("ProxyType", "on-premise")
            .Property("Type", "RFC")
            .Property("cloudsdk.principalPropagationMode", " token_exchange ")
            .Property("cloudsdk.securityConfiguration", "from_platform")
            .Build();

        Assert.Equal(AuthenticationType.OAuth2SamlBearerAssertion, destination.GetOrDefault(DestinationProperty.AuthType, AuthenticationType.NoAuthentication));
        Assert.Equal(ProxyType.OnPremise, destination.GetOrDefault(DestinationProperty.ProxyType, ProxyType.Internet));
        Assert.Equal(DestinationType.Rfc, destination.GetOrDefault(DestinationProperty.Type, DestinationType.Http));
        Assert.Equal(PrincipalPropagationMode.TokenExchange, destination.GetOrDefault(DestinationProperty.PrincipalPropagationMode, PrincipalPropagationMode.Unknown));
        Assert.Equal(SecurityConfigurationStrategy.FromPlatform, destination.GetOrDefault(DestinationProperty.SecurityConfiguration, SecurityConfigurationStrategy.FromDestination));
    }

    [Fact]
    public void UnknownEnumIdentifier_Throws()
    {
        var destination = DefaultDestination.CreateBuilder().Property("Authentication", "Nope").Build();

        Assert.Throws<ArgumentException>(() => destination.TryGet(DestinationProperty.AuthType, out _));
    }

    [Fact]
    public void ListKeys_AcceptAnyEnumerable_ButNotStrings()
    {
        var destination = DefaultDestination.CreateBuilder()
            .Property("cloudsdk.certificates", new List<string> { "a", "b" })
            .Property("cloudsdk.authTokens", "not-a-list")
            .Property("cloudsdk.propertiesForChangeDetection", new[] { "x", "y" })
            .Build();

        Assert.True(destination.TryGet(DestinationProperty.Certificates, out var certificates));
        Assert.Equal(new object?[] { "a", "b" }, certificates);
        Assert.Throws<ArgumentException>(() => destination.TryGet(DestinationProperty.AuthTokens, out _));
        Assert.True(destination.TryGet(DestinationProperty.PropertiesForChangeDetection, out var names));
        Assert.Equal(new[] { "x", "y" }, names);
    }

    [Fact]
    public void ExtensionMethods_ConvertWithFunctionOrCast()
    {
        var destination = DefaultDestination.CreateBuilder().Property("shoeSize", "42").Property("name", "n").Build();

        Assert.True(destination.TryGet("shoeSize", o => int.Parse((string)o, System.Globalization.CultureInfo.InvariantCulture), out var size));
        Assert.Equal(42, size);
        Assert.True(destination.TryGetAs<string>("name", out var name));
        Assert.Equal("n", name);
        Assert.Throws<InvalidCastException>(() => destination.TryGetAs<int>("name", out _));
        Assert.Equal("fallback", destination.GetOrDefault(DestinationProperty.TlsVersion, "fallback"));
        Assert.Null(destination.GetOrNull(DestinationProperty.TlsVersion));
    }

    [Fact]
    public void DefaultDestination_Equality_IsStructural()
    {
        var a = DefaultDestination.CreateBuilder().Name("n").Property("list", new List<string> { "1" }).Build();
        var b = DefaultDestination.CreateBuilder().Property("LIST", new[] { "1" }).Name("n").Build();
        var c = DefaultDestination.CreateBuilder().Name("n").Build();

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void DefaultDestination_ToBuilder_RoundTrips()
    {
        var a = DefaultDestination.CreateBuilder().Name("n").Uri(new Uri("https://x")).Build();

        Assert.Equal(a, a.ToBuilder().Build());
        Assert.NotEqual(a, a.ToBuilder().Name("m").Build());
    }

    [Fact]
    public void AsHttp_ConvertsGenericDestinations()
    {
        IDestination generic = DefaultDestination.CreateBuilder().Name("n").Uri("https://x").Build();
        IDestination noUrl = DefaultDestination.CreateBuilder().Name("n").Build();

        Assert.True(generic.IsHttp());
        Assert.Equal(new Uri("https://x"), generic.AsHttp().Uri);
        Assert.False(noUrl.IsHttp());
        Assert.Throws<ArgumentException>(noUrl.AsHttp);
        var http = generic.AsHttp();
        Assert.Same(http, http.AsHttp());
    }

    [Fact]
    public void DestinationOptions_AreImmutableValueObjects()
    {
        var options = DestinationOptions.CreateBuilder().Parameter("a", 1).ParameterIfAbsent("a", 2).ParameterIfAbsent("b", "x").Build();
        var extended = DestinationOptions.CreateBuilder(options).Parameter("c", true).Build();

        Assert.Equal(1, options.Get("a"));
        Assert.Equal("x", options.Get("b"));
        Assert.Null(options.Get("c"));
        Assert.Equal(["a", "b", "c"], extended.OptionKeys.Order());
        Assert.Equal(DestinationOptions.CreateBuilder().Parameter("b", "x").Parameter("a", 1).Build(), options);
        Assert.NotEqual(options, extended);
    }

    [Fact]
    public void DestinationOptions_Augmenter_AddsParameters()
    {
        var options = DestinationOptions.CreateBuilder().AugmentBuilder(new Augmenter()).Build();

        Assert.Equal("augmented", options.Get("source"));
    }

    private sealed class Augmenter : IDestinationOptionsAugmenter
    {
        public void AugmentBuilder(DestinationOptions.Builder builder) => builder.Parameter("source", "augmented");
    }
}
