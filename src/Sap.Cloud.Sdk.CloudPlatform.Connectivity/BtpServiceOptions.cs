namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Options for destinations created from the bindings of specific BTP services.</summary>
/// <remarks>
/// Java's enums implementing <c>OptionsEnhancer</c> are sealed classes with static instances, because .NET enums cannot
/// implement interfaces. The deprecated members (<c>IasOptions.withTargetUri</c>, <c>IasTargetUri</c> and the
/// two-argument <c>withConsumerClient</c>) are not ported.
/// </remarks>
#pragma warning disable CA1034 // The nested option types mirror the Java API and group the options per service.
public static class BtpServiceOptions
{
    /// <summary>Selects the API of the business rules service.</summary>
    public sealed class BusinessRulesOptions : IOptionsEnhancer<BusinessRulesOptions>
    {
        /// <summary>The authoring API.</summary>
        public static readonly BusinessRulesOptions AuthoringApi = new("AUTHORING_API");

        /// <summary>The execution API.</summary>
        public static readonly BusinessRulesOptions ExecutionApi = new("EXECUTION_API");

        private readonly string _name;

        private BusinessRulesOptions(string name) => _name = name;

        /// <inheritdoc/>
        public BusinessRulesOptions Value => this;

        /// <inheritdoc/>
        public override string ToString() => _name;
    }

    /// <summary>Selects the API of the workflow service.</summary>
    public sealed class WorkflowOptions : IOptionsEnhancer<WorkflowOptions>
    {
        /// <summary>The REST API.</summary>
        public static readonly WorkflowOptions RestApi = new("REST_API");

        /// <summary>The OData API.</summary>
        public static readonly WorkflowOptions ODataApi = new("ODATA_API");

        private readonly string _name;

        private WorkflowOptions(string name) => _name = name;

        /// <inheritdoc/>
        public WorkflowOptions Value => this;

        /// <inheritdoc/>
        public override string ToString() => _name;
    }

    /// <summary>Selects the API of the business logging service.</summary>
    public sealed class BusinessLoggingOptions : IOptionsEnhancer<BusinessLoggingOptions>
    {
        /// <summary>The configuration API.</summary>
        public static readonly BusinessLoggingOptions ConfigApi = new("CONFIG_API");

        /// <summary>The text API.</summary>
        public static readonly BusinessLoggingOptions TextApi = new("TEXT_API");

        /// <summary>The read API.</summary>
        public static readonly BusinessLoggingOptions ReadApi = new("READ_API");

        /// <summary>The write API.</summary>
        public static readonly BusinessLoggingOptions WriteApi = new("WRITE_API");

        private readonly string _name;

        private BusinessLoggingOptions(string name) => _name = name;

        /// <inheritdoc/>
        public BusinessLoggingOptions Value => this;

        /// <inheritdoc/>
        public override string ToString() => _name;
    }

    /// <summary>Options for authentication services.</summary>
    public static class AuthenticationServiceOptions
    {
        /// <summary>Sets the URI of the target application.</summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<Uri> WithTargetUri(string uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            return WithTargetUri(new Uri(uri, UriKind.RelativeOrAbsolute));
        }

        /// <summary>Sets the URI of the target application.</summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<Uri> WithTargetUri(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            return new TargetUri(uri);
        }

        /// <summary>The URI of the target application.</summary>
        /// <param name="Value">The URI.</param>
        public sealed record TargetUri(Uri Value) : IOptionsEnhancer<Uri>;
    }

    /// <summary>Options for the identity authentication service (IAS).</summary>
    public static class IasOptions
    {
        /// <summary>Do not request a token for the technical provider user.</summary>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<bool> WithoutTokenForTechnicalProviderUser() =>
            new NoTokenForTechnicalProviderUser(true);

        /// <summary>Sets the name of the application.</summary>
        /// <param name="applicationName">The name.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<IasCommunicationOptions> WithApplicationName(string applicationName) =>
            new IasCommunicationOptions(applicationName, null, null, null, null);

        /// <summary>Sets the provider client.</summary>
        /// <param name="providerClientId">The client ID.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<IasCommunicationOptions> WithProviderClient(string providerClientId) =>
            new IasCommunicationOptions(null, providerClientId, null, null, null);

        /// <summary>Sets the provider client and tenant.</summary>
        /// <param name="providerClientId">The client ID.</param>
        /// <param name="providerTenantId">The tenant ID.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<IasCommunicationOptions> WithProviderClient(
            string providerClientId,
            string providerTenantId) =>
            new IasCommunicationOptions(null, providerClientId, providerTenantId, null, null);

        /// <summary>Sets the consumer client.</summary>
        /// <param name="consumerClientId">The client ID.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<IasCommunicationOptions> WithConsumerClient(string consumerClientId) =>
            new IasCommunicationOptions(null, null, null, consumerClientId, null);

        /// <summary>Sets the token format.</summary>
        /// <param name="format">The format.</param>
        /// <returns>The option.</returns>
        public static IOptionsEnhancer<string> WithTokenFormat(string format) => new TokenFormat(format);

        /// <summary>Marker for "no token for the technical provider user".</summary>
        /// <param name="Value">The flag.</param>
        public sealed record NoTokenForTechnicalProviderUser(bool Value) : IOptionsEnhancer<bool>;

        /// <summary>Identifies the communicating applications of an IAS token exchange.</summary>
        /// <param name="ApplicationName">The application name.</param>
        /// <param name="ProviderClientId">The provider client ID.</param>
        /// <param name="ProviderTenantId">The provider tenant ID.</param>
        /// <param name="ConsumerClientId">The consumer client ID.</param>
        /// <param name="ConsumerTenantId">The consumer tenant ID.</param>
        public sealed record IasCommunicationOptions(
            string? ApplicationName,
            string? ProviderClientId,
            string? ProviderTenantId,
            string? ConsumerClientId,
            string? ConsumerTenantId) : IOptionsEnhancer<IasCommunicationOptions>
        {
            /// <inheritdoc/>
            public IasCommunicationOptions Value => this;
        }

        /// <summary>The requested token format.</summary>
        /// <param name="Value">The format.</param>
        public sealed record TokenFormat(string Value) : IOptionsEnhancer<string>;
    }
}
#pragma warning restore CA1034
