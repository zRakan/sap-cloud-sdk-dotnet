using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using static Sap.Cloud.Sdk.CloudPlatform.Connectivity.BtpServiceOptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Matches service binding options to the <see cref="IOAuth2PropertySupplier"/> responsible for them.</summary>
internal sealed class OAuth2PropertySupplierResolver(
    Func<ServiceBindingDestinationOptions, bool> matcher,
    Func<ServiceBindingDestinationOptions, IOAuth2PropertySupplier> resolver)
{
    public static OAuth2PropertySupplierResolver ForServiceIdentifier(
        ServiceIdentifier identifier,
        Func<ServiceBindingDestinationOptions, IOAuth2PropertySupplier> resolver) =>
        new(options => identifier.Equals(options.ServiceBinding.GetServiceIdentifier()), resolver);

    public bool Matches(ServiceBindingDestinationOptions options) => matcher(options);

    public IOAuth2PropertySupplier Resolve(ServiceBindingDestinationOptions options) => resolver(options);
}

/// <summary>Property suppliers for the BTP services supported out of the box.</summary>
internal static class BtpServicePropertySuppliers
{
    private static readonly Func<Uri, Uri> RemovePath = uri => new Uri(uri, "/");

    public static readonly OAuth2PropertySupplierResolver Xsuaa =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(ServiceIdentifier.Xsuaa, o => new XsuaaSupplier(o));

    public static readonly OAuth2PropertySupplierResolver Destination =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(ServiceIdentifier.Destination, o => new DestinationSupplier(o));

    public static readonly OAuth2PropertySupplierResolver Connectivity =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(ServiceIdentifier.Connectivity, o => new ConnectivityProxySupplier(o));

    public static readonly OAuth2PropertySupplierResolver IdentityAuthentication =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(ServiceIdentifier.IdentityAuthentication, o => new IdentityAuthenticationSupplier(o));

    public static readonly OAuth2PropertySupplierResolver Workflow =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(
            ServiceIdentifier.Workflow,
            o => new MultiUrlPropertySupplier<WorkflowOptions>(o, new Dictionary<WorkflowOptions, (string, Func<Uri, Uri>?)>
            {
                [WorkflowOptions.RestApi] = ("workflow_rest_url", null),
                [WorkflowOptions.ODataApi] = ("workflow_odata_url", null),
            }));

    public static readonly OAuth2PropertySupplierResolver BusinessRules =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(
            ServiceIdentifier.BusinessRules,
            o => new MultiUrlPropertySupplier<BusinessRulesOptions>(o, new Dictionary<BusinessRulesOptions, (string, Func<Uri, Uri>?)>
            {
                [BusinessRulesOptions.AuthoringApi] = ("rule_repository_url", null),
                [BusinessRulesOptions.ExecutionApi] = ("rule_runtime_url", null),
            }));

    public static readonly OAuth2PropertySupplierResolver BusinessLogging =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(
            ServiceIdentifier.BusinessLogging,
            o => new MultiUrlPropertySupplier<BusinessLoggingOptions>(o, new Dictionary<BusinessLoggingOptions, (string, Func<Uri, Uri>?)>
            {
                [BusinessLoggingOptions.ConfigApi] = ("configservice", RemovePath),
                [BusinessLoggingOptions.TextApi] = ("textresourceservice", RemovePath),
                [BusinessLoggingOptions.ReadApi] = ("readservice", RemovePath),
                [BusinessLoggingOptions.WriteApi] = ("writeservice", RemovePath),
            }));

    public static readonly OAuth2PropertySupplierResolver AiCore =
        OAuth2PropertySupplierResolver.ForServiceIdentifier(ServiceIdentifier.AiCore, o => new AiCoreSupplier(o));

    public static List<OAuth2PropertySupplierResolver> GetDefaultServiceResolvers() =>
        [Xsuaa, Destination, Connectivity, BusinessRules, Workflow, BusinessLogging, IdentityAuthentication, AiCore];

    private sealed class XsuaaSupplier(ServiceBindingDestinationOptions options) : DefaultOAuth2PropertySupplier(options, [])
    {
        public override Uri ServiceUri =>
            Options.TryGetOption<AuthenticationServiceOptions.TargetUri, Uri>(out var target) ? target : base.ServiceUri;
    }

    private sealed class DestinationSupplier(ServiceBindingDestinationOptions options) : DefaultOAuth2PropertySupplier(options, [])
    {
        public override Uri ServiceUri => GetCredentialOrThrow<Uri>("uri");
    }

    private sealed class ConnectivityProxySupplier(ServiceBindingDestinationOptions options) : DefaultOAuth2PropertySupplier(options, [])
    {
        public override Uri ServiceUri
        {
            get
            {
                var host = GetCredentialOrThrow<string>("onpremise_proxy_host");
                var port = GetIntCredential("onpremise_proxy_http_port") ?? GetIntCredential("onpremise_proxy_port")
                    ?? throw new DestinationAccessException("Failed to resolve property [onpremise_proxy_port] from service binding.");
                return new UriBuilder("http", host, port).Uri;
            }
        }
    }

    private sealed class AiCoreSupplier(ServiceBindingDestinationOptions options) : DefaultOAuth2PropertySupplier(options, [])
    {
        public override Uri ServiceUri => GetCredentialOrThrow<Uri>("serviceurls", "AI_API_URL");
    }

    private sealed class IdentityAuthenticationSupplier(ServiceBindingDestinationOptions options) : DefaultOAuth2PropertySupplier(options, [])
    {
        public override Uri ServiceUri =>
            Options.TryGetOption<AuthenticationServiceOptions.TargetUri, Uri>(out var target) ? target : base.ServiceUri;

        public override Uri TokenUri => new(GetCredentialOrThrow<string>("url").TrimEnd('/') + "/oauth2/token");

        public override OAuth2Options OAuth2Options
        {
            get
            {
                var builder = OAuth2Options.Builder();
                if (Options.TryGetOption<OAuth2Options.TokenRetrievalTimeout, Resilience.ResilienceConfiguration.TimeLimiterConfiguration>(out var timeout))
                {
                    builder.WithTimeLimiter(timeout);
                }
                if (SkipTokenRetrieval())
                {
                    builder.WithSkipTokenRetrieval(true);
                }
                else
                {
                    AttachIasCommunicationOptions(builder);
                    builder.WithTokenRetrievalParameter("app_tid", GetCredentialOrThrow<string>("app_tid"));
                    if (Options.TryGetOption<IasOptions.TokenFormat, string>(out var format))
                    {
                        builder.WithTokenRetrievalParameter("token_format", format);
                    }
                }
                AttachClientKeyStore(builder);
                builder.WithBtpTenantApiBaseUri(GetCredential<Uri>("btp-tenant-api"));
                return builder.Build();
            }
        }

        private void AttachIasCommunicationOptions(OAuth2Options.OAuth2OptionsBuilder builder)
        {
            if (!Options.TryGetOption<IasOptions.IasCommunicationOptions, IasOptions.IasCommunicationOptions>(out var o))
            {
                return;
            }
            if (o.ApplicationName is not null)
            {
                builder.WithTokenRetrievalParameter("resource", "urn:sap:identity:application:provider:name:" + o.ApplicationName);
            }
            else if (o.ProviderClientId is not null)
            {
                var resource = "urn:sap:identity:application:provider:clientid:" + o.ProviderClientId;
                if (o.ProviderTenantId is not null)
                {
                    resource += ":apptid:" + o.ProviderTenantId;
                }
                builder.WithTokenRetrievalParameter("resource", resource);
            }
            else if (o.ConsumerClientId is not null)
            {
                var resource = "urn:sap:identity:consumer:clientid:" + o.ConsumerClientId;
                if (o.ConsumerTenantId is not null)
                {
                    resource += ":apptid:" + o.ConsumerTenantId;
                }
                builder.WithTokenRetrievalParameter("resource", resource);
            }
        }

        private bool SkipTokenRetrieval()
        {
            var noTokenRequired = Options.TryGetOption<IasOptions.NoTokenForTechnicalProviderUser, bool>(out var flag) && flag;
            if (!noTokenRequired)
            {
                return false;
            }
            return Options.OnBehalfOf switch
            {
                OnBehalfOf.NamedUserCurrentTenant => false,
                OnBehalfOf.TechnicalUserProvider => true,
                _ => CurrentTenantIsProvider(),
            };
        }

        private bool CurrentTenantIsProvider()
        {
            var tenantId = TenantAccessor.TryGetCurrentTenant().Map(t => t.TenantId).GetValueOrDefault();
            return tenantId is null
                || string.Equals(tenantId, GetCredentialOrThrow<string>("app_tid"), StringComparison.OrdinalIgnoreCase);
        }

        private void AttachClientKeyStore(OAuth2Options.OAuth2OptionsBuilder builder)
        {
            if (ClientIdentity is not ClientCertificate certificate)
            {
                return;
            }
            try
            {
                builder.WithClientKeyStore(KeyStoreReader.CreateKeyStore(certificate.Certificate, certificate.Key));
            }
            catch (Exception e) when (e is not DestinationAccessException)
            {
                throw new DestinationAccessException("Unable to extract client key store from IAS service binding.", e);
            }
        }
    }
}

/// <summary>A property supplier for services whose binding contains several API URLs below <c>endpoints</c>.</summary>
/// <typeparam name="T">The option type that selects the API.</typeparam>
internal sealed class MultiUrlPropertySupplier<T>(
    ServiceBindingDestinationOptions options,
    IReadOnlyDictionary<T, (string UrlKey, Func<Uri, Uri>? Transformation)> urlExtractors)
    : DefaultOAuth2PropertySupplier(options)
    where T : class, IOptionsEnhancer<T>
{
    public override Uri ServiceUri
    {
        get
        {
            if (!Options.TryGetOption<T, T>(out var option))
            {
                throw new DestinationAccessException(
                    $"No option given for which API should be used. Please include an option {typeof(T).Name} in the service binding destination options.");
            }
            if (!urlExtractors.TryGetValue(option, out var extractor))
            {
                throw new InvalidOperationException(
                    $"Found option value {option} for {typeof(T).Name}, but no URL key was registered for this value.");
            }
            var uri = GetCredentialOrThrow<Uri>("endpoints", extractor.UrlKey);
            return extractor.Transformation?.Invoke(uri) ?? uri;
        }
    }
}
