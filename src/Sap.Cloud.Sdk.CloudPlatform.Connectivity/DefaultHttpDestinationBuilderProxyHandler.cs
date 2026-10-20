using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Turns a builder for an on-premise destination into a destination that is routed through the Cloud Connector, using
/// the connectivity service binding and the registered <see cref="IServiceBindingDestinationLoader"/>s.
/// </summary>
internal class DefaultHttpDestinationBuilderProxyHandler
{
    internal virtual bool CanHandle(DefaultHttpDestination.Builder builder)
    {
        var isOnPremise = builder.TryGet(DestinationProperty.ProxyType, out var proxyType) && proxyType == ProxyType.OnPremise;
        if (!isOnPremise)
        {
            return false;
        }
        var handledAlready = builder.CustomHeaderProvidersInternal.Any(p => p is SapConnectivityLocationIdHeaderProvider);
        return !handledAlready;
    }

    internal virtual DefaultHttpDestination Handle(DefaultHttpDestination.Builder builder)
    {
        if (!builder.TryGet(DestinationProperty.ProxyType, out var proxyType) || proxyType != ProxyType.OnPremise)
        {
            var name = builder.TryGet(DestinationProperty.Name, out var n) ? n : "unnamed-destination";
            throw new InvalidOperationException(
                $"Proxy handler was invoked unexpectedly for destination {name} which does not have proxy type 'OnPremise'.");
        }

        // Add the location id header provider, useful to identify one of multiple cloud connectors.
        builder.HeaderProviders(new SapConnectivityLocationIdHeaderProvider());

        // Resolve the connectivity service binding, necessary for on-premise proxying.
        var serviceBinding = GetServiceBindingConnectivity();
        var derivedOnBehalfOf = DeriveOnBehalfOf(builder);
        var destination = builder.BuildInternal();

        var options = ServiceBindingDestinationOptions
            .ForService(serviceBinding)
            .OnBehalfOf(derivedOnBehalfOf)
            .WithOption(ServiceBindingDestinationOptions.Options.ProxyOptions.DestinationToBeProxied(destination))
            .Build();
        return (DefaultHttpDestination)GetServiceBindingDestinationLoader().GetDestination(options);
    }

    internal virtual IServiceBindingAccessor GetServiceBindingAccessor() => DefaultServiceBindingAccessor.Instance;

    internal virtual IServiceBindingDestinationLoader GetServiceBindingDestinationLoader() =>
        ServiceBindingDestinationLoader.DefaultLoaderChain;

    // Auth type            | destination tenant | PP mode           | result
    // *                    + TENANT_PROVIDER    + *                -> TECHNICAL_USER_PROVIDER
    // *                    + *                  + *                -> TECHNICAL_USER_CURRENT_TENANT
    // PrincipalPropagation + TENANT_PROVIDER    + TOKEN_FORWARDING -> TECHNICAL_USER_PROVIDER   (not implemented in Java either)
    // PrincipalPropagation + *                  + TOKEN_FORWARDING -> TECHNICAL_USER_CURRENT_TENANT
    // PrincipalPropagation + *                  + TOKEN_EXCHANGE   -> NAMED_USER_CURRENT_TENANT
    //
    // note: destination tenant is one of: [undefined, empty, non-empty]. empty is listed here as TENANT_PROVIDER.
    private static OnBehalfOf DeriveOnBehalfOf(DefaultHttpDestination.Builder builder)
    {
        var hasAuthType = builder.TryGet(DestinationProperty.AuthType, out var authType)
            || builder.TryGet(DestinationProperty.AuthTypeFallback, out authType);

        if (hasAuthType && authType == AuthenticationType.PrincipalPropagation)
        {
            var mode = builder.TryGet(DestinationProperty.PrincipalPropagationMode, out var m)
                ? m
                : PrincipalPropagationMode.TokenForwarding;
            switch (mode)
            {
                case PrincipalPropagationMode.TokenExchange:
                    return OnBehalfOf.NamedUserCurrentTenant;
                case PrincipalPropagationMode.TokenForwarding:
                    builder.HeaderProviders(new SapConnectivityAuthenticationHeaderProvider());
                    return OnBehalfOf.TechnicalUserCurrentTenant;
                default:
                    throw new InvalidOperationException("Principal propagation mode is unknown.");
            }
        }

        var isProvider = builder.TryGet(DestinationProperty.TenantId, out var tenantId) && tenantId.Length == 0;
        return isProvider ? OnBehalfOf.TechnicalUserProvider : OnBehalfOf.TechnicalUserCurrentTenant;
    }

    private IServiceBinding GetServiceBindingConnectivity()
    {
        var serviceBindings = GetServiceBindingAccessor().GetServiceBindings()
            .Where(b => ServiceIdentifier.Connectivity.Equals(b.GetServiceIdentifier()))
            .ToList();
        if (serviceBindings.Count == 0)
        {
            throw new DestinationAccessException(
                "Unable to resolve connectivity service binding. No service bindings found matching Connectivity.");
        }
        if (serviceBindings.Count > 1)
        {
            throw new DestinationAccessException(
                "Unable to resolve connectivity service binding. More than one service bindings found that match Connectivity.");
        }
        return serviceBindings[0];
    }

    internal sealed class SapConnectivityAuthenticationHeaderProvider : IDestinationHeaderProvider
    {
        private const string HeaderName = "SAP-Connectivity-Authentication";

        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext)
        {
            var headerValue = "Bearer " + AuthTokenAccessor.GetCurrentToken().Jwt.EncodedToken;
            return [new Header(HeaderName, headerValue)];
        }
    }

    internal sealed class SapConnectivityLocationIdHeaderProvider : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext)
        {
            ArgumentNullException.ThrowIfNull(requestContext);
            return requestContext.Destination.TryGet(DestinationProperty.CloudConnectorLocationId, out var locationId)
                ? [new Header("SAP-Connectivity-SCC-Location_ID", locationId)]
                : [];
        }
    }
}
