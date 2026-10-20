using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class EnumTests
{
    [Fact]
    public void AuthenticationType_IdentifiersRoundTrip()
    {
        foreach (var type in Enum.GetValues<AuthenticationType>())
        {
            Assert.Equal(type, AuthenticationTypeExtensions.OfIdentifier(type.GetIdentifier()));
        }
        Assert.Equal("OAuth2SAMLBearerAssertion", AuthenticationType.OAuth2SamlBearerAssertion.GetIdentifier());
        Assert.Equal("SAPAssertionSSO", AuthenticationType.SapAssertionSso.GetIdentifier());
        Assert.Equal("OAuth2JWTBearer", AuthenticationType.OAuth2JwtBearer.GetIdentifier());
    }

    [Fact]
    public void AuthenticationType_UnknownOrDifferentlyCasedIdentifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => AuthenticationTypeExtensions.OfIdentifier("basicauthentication"));
        Assert.Throws<ArgumentException>(() => AuthenticationTypeExtensions.OfIdentifier("unknown"));
    }

    [Fact]
    public void AuthenticationType_OfIdentifierOrDefault_FallsBack()
    {
        Assert.Equal(AuthenticationType.TokenForwarding, AuthenticationTypeExtensions.OfIdentifierOrDefault(null, AuthenticationType.TokenForwarding));
        Assert.Equal(AuthenticationType.TokenForwarding, AuthenticationTypeExtensions.OfIdentifierOrDefault("x", AuthenticationType.TokenForwarding));
        Assert.Equal(AuthenticationType.BasicAuthentication, AuthenticationTypeExtensions.OfIdentifierOrDefault("BasicAuthentication", AuthenticationType.TokenForwarding));
    }

    [Theory]
    [InlineData("Internet", ProxyType.Internet)]
    [InlineData("OnPremise", ProxyType.OnPremise)]
    [InlineData("PrivateLink", ProxyType.PrivateLink)]
    public void ProxyType_Identifiers(string identifier, ProxyType expected)
    {
        Assert.Equal(expected, ProxyTypeExtensions.OfIdentifier(identifier));
        Assert.Equal(identifier, expected.GetIdentifier());
    }

    [Theory]
    [InlineData("onpremise")]
    [InlineData("On-Premise")]
    [InlineData("ON_PREMISE")]
    [InlineData("OnPremise")]
    public void ProxyType_SensitiveParsing_AcceptsSpellings(string identifier) =>
        Assert.Equal(ProxyType.OnPremise, ProxyTypeExtensions.OfIdentifierSensitive(identifier));

    [Fact]
    public void ProxyType_StrictParsing_RejectsSpellings()
    {
        Assert.Throws<ArgumentException>(() => ProxyTypeExtensions.OfIdentifier("onpremise"));
        Assert.Throws<ArgumentException>(() => ProxyTypeExtensions.OfIdentifierSensitive("nope"));
        Assert.Equal(ProxyType.Internet, ProxyTypeExtensions.OfIdentifierOrDefault("nope", ProxyType.Internet));
        Assert.Equal(ProxyType.PrivateLink, ProxyTypeExtensions.OfIdentifierOrDefault(null, ProxyType.PrivateLink));
    }

    [Fact]
    public void DestinationType_Identifiers()
    {
        Assert.Equal("HTTP", DestinationType.Http.GetIdentifier());
        Assert.Equal(DestinationType.Rfc, DestinationTypeExtensions.OfIdentifier("RFC"));
        Assert.Equal(DestinationType.Mail, DestinationTypeExtensions.OfIdentifierOrDefault("MAIL", DestinationType.Http));
        Assert.Equal(DestinationType.Http, DestinationTypeExtensions.OfIdentifierOrDefault("?", DestinationType.Http));
        Assert.Throws<ArgumentException>(() => DestinationTypeExtensions.OfIdentifier("http"));
    }

    [Fact]
    public void SecurityConfigurationStrategy_Identifiers()
    {
        Assert.Equal("from_platform", SecurityConfigurationStrategy.FromPlatform.GetIdentifier());
        Assert.Equal(SecurityConfigurationStrategy.FromDestination, SecurityConfigurationStrategyExtensions.OfIdentifierOrDefault(null));
        Assert.Equal(SecurityConfigurationStrategy.FromPlatform, SecurityConfigurationStrategyExtensions.OfIdentifierOrDefault("from_platform"));
        Assert.Throws<ArgumentException>(() => SecurityConfigurationStrategyExtensions.OfIdentifierOrDefault("other"));
    }

    [Fact]
    public void PrincipalPropagationMode_UnknownValuesBecomeUnknown()
    {
        Assert.Equal(PrincipalPropagationMode.TokenForwarding, PrincipalPropagationModeExtensions.OfIdentifier("token_forwarding"));
        Assert.Equal(PrincipalPropagationMode.TokenExchange, PrincipalPropagationModeExtensions.OfIdentifier(" TOKEN_EXCHANGE "));
        Assert.Equal(PrincipalPropagationMode.Unknown, PrincipalPropagationModeExtensions.OfIdentifier("whatever"));
    }

    [Theory]
    [InlineData(AuthenticationType.NoAuthentication, null, false)]
    [InlineData(AuthenticationType.BasicAuthentication, null, false)]
    [InlineData(AuthenticationType.PrincipalPropagation, null, false)]
    [InlineData(AuthenticationType.ClientCertificateAuthentication, null, false)]
    [InlineData(AuthenticationType.OAuth2ClientCredentials, null, false)]
    [InlineData(AuthenticationType.OAuth2Password, null, false)]
    [InlineData(AuthenticationType.OAuth2TechnicalUserPropagation, null, false)]
    [InlineData(AuthenticationType.TokenForwarding, null, false)]
    [InlineData(AuthenticationType.OAuth2RefreshToken, null, false)]
    [InlineData(AuthenticationType.OAuth2JwtBearer, null, true)]
    [InlineData(AuthenticationType.OAuth2UserTokenExchange, null, true)]
    [InlineData(AuthenticationType.SamlAssertion, null, true)]
    [InlineData(AuthenticationType.OAuth2SamlBearerAssertion, null, true)]
    [InlineData(AuthenticationType.SapAssertionSso, "", true)]
    [InlineData(AuthenticationType.OAuth2SamlBearerAssertion, "  ", true)]
    [InlineData(AuthenticationType.OAuth2SamlBearerAssertion, "system-user", false)]
    [InlineData(AuthenticationType.SapAssertionSso, "system-user", false)]
    [InlineData(AuthenticationType.OAuth2JwtBearer, "system-user", true)]
    public void RequiresUserTokenExchange_ByAuthenticationType(AuthenticationType type, string? systemUser, bool expected) =>
        Assert.Equal(expected, DestinationUtility.RequiresUserTokenExchange(type, systemUser));

    [Fact]
    public void RequiresUserTokenExchange_ReadsPropertiesWithFallbackKeys()
    {
        var withFallback = DefaultDestination.CreateBuilder().Property("authType", "OAuth2JWTBearer").Build();
        var withSystemUser = DefaultDestination.CreateBuilder()
            .Property("Authentication", "OAuth2SAMLBearerAssertion")
            .Property("SystemUser", "x")
            .Build();

        Assert.True(DestinationUtility.RequiresUserTokenExchange(withFallback));
        Assert.False(DestinationUtility.RequiresUserTokenExchange(withSystemUser));
        Assert.False(DestinationUtility.RequiresUserTokenExchange(DefaultDestination.CreateBuilder().Build()));
    }

    [Fact]
    public void Exceptions_CarryDestinationInformation()
    {
        var notFound = new DestinationNotFoundException("abc");
        var unsupported = new DestinationTypeNotSupportedException("abc", DestinationType.Rfc);
        var oauth = new DestinationOAuthTokenException("abc");

        Assert.Equal("Destination 'abc' not found.", notFound.Message);
        Assert.Equal("Destination  not found.", new DestinationNotFoundException().Message);
        Assert.Equal("abc", unsupported.DestinationName);
        Assert.Equal(DestinationType.Rfc, unsupported.DestinationType);
        Assert.Contains("'RFC' is not supported", unsupported.Message, StringComparison.Ordinal);
        Assert.Contains("'abc'", oauth.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<DestinationAccessException>(oauth);
        Assert.IsAssignableFrom<DestinationAccessException>(new DestinationPathsNotMergeableException());
        Assert.IsAssignableFrom<Exception>(new HttpClientInstantiationException("m"));
    }
}
