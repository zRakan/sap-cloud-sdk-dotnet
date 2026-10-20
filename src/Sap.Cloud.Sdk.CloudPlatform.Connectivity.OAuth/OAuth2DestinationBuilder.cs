using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Builds an HTTP destination that authenticates with OAuth 2.0 client credentials or a client certificate.</summary>
/// <example>
/// <code>
/// var destination = OAuth2DestinationBuilder
///     .ForTargetUrl("https://api.example.com")
///     .WithTokenEndpoint("https://tenant.authentication.eu10.hana.ondemand.com/oauth/token")
///     .WithClient(new ClientCredentials("id", "secret"), OnBehalfOf.TechnicalUserCurrentTenant)
///     .Build();
/// </code>
/// </example>
public static class OAuth2DestinationBuilder
{
    /// <summary>Starts building a destination for the given target URL.</summary>
    /// <param name="targetUrl">The URL of the system to call.</param>
    /// <returns>The next builder step.</returns>
    public static BuilderWithTargetUrl ForTargetUrl(string targetUrl)
    {
        ArgumentNullException.ThrowIfNull(targetUrl);
        return new BuilderWithTargetUrl(targetUrl);
    }

    /// <summary>Builder step: the target URL is set.</summary>
    public sealed class BuilderWithTargetUrl
    {
        private readonly string _targetUrl;

        internal BuilderWithTargetUrl(string targetUrl) => _targetUrl = targetUrl;

        /// <summary>Sets the token endpoint.</summary>
        /// <param name="tokenUrl">The token endpoint; without a path, <c>/oauth/token</c> is appended.</param>
        /// <returns>The next builder step.</returns>
        public BuilderWithTokenEndpoint WithTokenEndpoint(string tokenUrl)
        {
            ArgumentNullException.ThrowIfNull(tokenUrl);
            return new BuilderWithTokenEndpoint(_targetUrl, tokenUrl);
        }
    }

    /// <summary>Builder step: target URL and token endpoint are set.</summary>
    public sealed class BuilderWithTokenEndpoint
    {
        private readonly string _targetUrl;
        private readonly string _tokenUrl;

        internal BuilderWithTokenEndpoint(string targetUrl, string tokenUrl)
        {
            _targetUrl = targetUrl;
            _tokenUrl = tokenUrl;
        }

        /// <summary>Sets the client identity and on whose behalf tokens are retrieved.</summary>
        /// <param name="clientIdentity"><see cref="ClientCredentials"/> or <see cref="ClientCertificate"/>.</param>
        /// <param name="behalf">On whose behalf tokens are retrieved.</param>
        /// <returns>A destination builder with an OAuth 2.0 header provider; further properties can be set.</returns>
        public DefaultHttpDestination.Builder WithClient(ISecurityCredentials clientIdentity, OnBehalfOf behalf)
        {
            ArgumentNullException.ThrowIfNull(clientIdentity);
            var identity = OAuth2ClientIdentity.From(clientIdentity);
            var service = OAuth2Service.CreateBuilder()
                .WithTokenUri(_tokenUrl)
                .WithIdentity(identity)
                .WithOnBehalfOf(behalf)
                .Build();
            return DefaultHttpDestination.CreateBuilder(_targetUrl)
                .Name(OAuth2ServiceBindingDestinationLoader.StableHash(identity.Id).ToString(System.Globalization.CultureInfo.InvariantCulture))
                .HeaderProviders(new OAuth2HeaderProvider(service, "Authorization"));
        }
    }
}
