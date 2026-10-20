using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Creates destinations for service bindings with OAuth 2.0 credentials (XSUAA, Destination, Connectivity, IAS,
/// Workflow, Business Rules, Business Logging, AI Core). Each request gets a bearer token for the current tenant or
/// user, depending on <see cref="ServiceBindingDestinationOptions.OnBehalfOf"/>.
/// </summary>
public class OAuth2ServiceBindingDestinationLoader : IServiceBindingDestinationLoader
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<OAuth2ServiceBindingDestinationLoader>();
    private static readonly Lock ResolversLock = new();
    private static List<OAuth2PropertySupplierResolver> _defaultResolvers = BtpServicePropertySuppliers.GetDefaultServiceResolvers();

    private readonly IReadOnlyList<OAuth2PropertySupplierResolver>? _resolvers;

    /// <summary>Creates a loader that uses the default and registered property suppliers.</summary>
    public OAuth2ServiceBindingDestinationLoader()
    {
    }

    internal OAuth2ServiceBindingDestinationLoader(IReadOnlyList<OAuth2PropertySupplierResolver> resolvers)
    {
        _resolvers = resolvers;
    }

    private IReadOnlyList<OAuth2PropertySupplierResolver> Resolvers
    {
        get
        {
            if (_resolvers is not null)
            {
                return _resolvers;
            }
            lock (ResolversLock)
            {
                return [.. _defaultResolvers];
            }
        }
    }

    /// <summary>Registers a property supplier for a service, taking precedence over the existing ones.</summary>
    /// <param name="service">The service.</param>
    /// <param name="propertySupplier">Creates the supplier.</param>
    public static void RegisterPropertySupplier(
        ServiceIdentifier service,
        Func<ServiceBindingDestinationOptions, IOAuth2PropertySupplier> propertySupplier)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(propertySupplier);
        Prepend(OAuth2PropertySupplierResolver.ForServiceIdentifier(service, propertySupplier));
    }

    /// <summary>Registers a property supplier for matching options, taking precedence over the existing ones.</summary>
    /// <param name="optionsMatcher">Decides whether the supplier is responsible.</param>
    /// <param name="propertySupplier">Creates the supplier.</param>
    public static void RegisterPropertySupplier(
        Func<ServiceBindingDestinationOptions, bool> optionsMatcher,
        Func<ServiceBindingDestinationOptions, IOAuth2PropertySupplier> propertySupplier)
    {
        ArgumentNullException.ThrowIfNull(optionsMatcher);
        ArgumentNullException.ThrowIfNull(propertySupplier);
        Prepend(new OAuth2PropertySupplierResolver(optionsMatcher, propertySupplier));
    }

    /// <summary>Restores the default property suppliers. Intended for tests.</summary>
    public static void ResetPropertySuppliers()
    {
        lock (ResolversLock)
        {
            _defaultResolvers = BtpServicePropertySuppliers.GetDefaultServiceResolvers();
        }
    }

    private static void Prepend(OAuth2PropertySupplierResolver resolver)
    {
        lock (ResolversLock)
        {
            _defaultResolvers = [resolver, .. _defaultResolvers];
        }
    }

    /// <inheritdoc/>
    public Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var identifier = options.ServiceBinding.GetServiceIdentifier();
        var resolvers = Resolvers;
        var supplier = GetPropertySupplier(options, resolvers);
        if (supplier is null)
        {
            return Result.Failure<IHttpDestination>(new DestinationNotFoundException(null,
                $"Could not transform service binding with identifier '{identifier}' into a destination: None of the {resolvers.Count} "
                + "property suppliers matched the service binding format. In case the service binding is not supported by default you "
                + $"may register your own supplier with {nameof(OAuth2ServiceBindingDestinationLoader)}.{nameof(RegisterPropertySupplier)}."));
        }

        Uri serviceUri;
        Uri tokenUri;
        ISecurityCredentials clientIdentity;
        OAuth2Options oauth2Options;
        try
        {
            serviceUri = supplier.ServiceUri;
            tokenUri = supplier.TokenUri;
            clientIdentity = supplier.ClientIdentity;
            oauth2Options = supplier.OAuth2Options;
        }
        catch (DestinationAccessException e)
        {
            return Result.Failure<IHttpDestination>(e);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Failure<IHttpDestination>(new DestinationAccessException(
                $"Failed to retrieve OAuth2 properties for service binding of service '{identifier}'.", e));
        }

        try
        {
            var behalf = options.OnBehalfOf;
            if (options.TryGetOption<ServiceBindingDestinationOptions.Options.ProxyOptions, IHttpDestination>(out var toBeProxied))
            {
                return Result.Success<IHttpDestination>(
                    ToProxiedDestination(toBeProxied, serviceUri, tokenUri, clientIdentity, behalf, oauth2Options, identifier));
            }
            return Result.Success<IHttpDestination>(
                ToDestination(serviceUri, tokenUri, clientIdentity, behalf, oauth2Options, identifier));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Failure<IHttpDestination>(
                new DestinationAccessException("Failed to instantiate OAuth destination based on given properties.", e));
        }
    }

    private static IOAuth2PropertySupplier? GetPropertySupplier(
        ServiceBindingDestinationOptions options,
        IReadOnlyList<OAuth2PropertySupplierResolver> resolvers)
    {
        foreach (var resolver in resolvers)
        {
            try
            {
                if (!resolver.Matches(options))
                {
                    continue;
                }
                var supplier = resolver.Resolve(options);
                if (supplier.IsOAuth2Binding)
                {
                    return supplier;
                }
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Supplier {Supplier} was applied but claims the binding is not an OAuth2 binding.", supplier.GetType().Name);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.LogError(e, "Failed to apply an OAuth2 property supplier to the given service binding options.");
            }
        }
        return null;
    }

    internal static IHttpDestination ToDestination(
        Uri serviceUri,
        Uri tokenUri,
        ISecurityCredentials clientIdentity,
        OnBehalfOf behalf,
        OAuth2Options oauth2Options,
        ServiceIdentifier? serviceIdentifier)
    {
        var identity = OAuth2ClientIdentity.From(clientIdentity);
        var name = $"{serviceIdentifier?.ToString() ?? "unknown"}-{StableHash(identity.Id)}";
        var builder = DefaultHttpDestination.CreateBuilder(serviceUri).Name(name);
        if (!oauth2Options.SkipTokenRetrieval)
        {
            builder.HeaderProviders(CreateHeaderProvider(tokenUri, identity, behalf, "Authorization", oauth2Options, serviceIdentifier));
        }
        if (oauth2Options.ClientKeyStoreSupplier is { } keyStore)
        {
            builder.KeyStoreSupplier(keyStore);
        }
        return builder.Build();
    }

    internal static IHttpDestination ToProxiedDestination(
        IHttpDestination destinationToBeProxied,
        Uri proxyUrl,
        Uri tokenUrl,
        ISecurityCredentials clientIdentity,
        OnBehalfOf behalf,
        OAuth2Options oauth2Options,
        ServiceIdentifier? serviceIdentifier)
    {
        var identity = OAuth2ClientIdentity.From(clientIdentity);
        var builder = DefaultHttpDestination.FromDestination(destinationToBeProxied);
        if (!oauth2Options.SkipTokenRetrieval)
        {
            builder.HeaderProviders(CreateHeaderProvider(tokenUrl, identity, behalf, "Proxy-Authorization", oauth2Options, serviceIdentifier));
        }
        if (oauth2Options.ClientKeyStoreSupplier is { } keyStore)
        {
            builder.KeyStoreSupplier(keyStore);
        }
        if (destinationToBeProxied.ProxyConfiguration is null)
        {
            builder.Proxy(proxyUrl);
        }
        return builder.BuildInternal();
    }

    internal static OAuth2HeaderProvider CreateHeaderProvider(
        Uri tokenUrl,
        OAuth2ClientIdentity identity,
        OnBehalfOf behalf,
        string authHeader,
        OAuth2Options oauth2Options,
        ServiceIdentifier? serviceIdentifier)
    {
        var service = OAuth2Service.CreateBuilder()
            .WithTokenUri(tokenUrl)
            .WithIdentity(identity)
            .WithOnBehalfOf(behalf)
            .WithTenantPropagationStrategyFrom(serviceIdentifier)
            .WithAdditionalParameters(oauth2Options.AdditionalTokenRetrievalParameters)
            .WithTimeLimiter(oauth2Options.TimeLimiter)
            .WithTokenCacheParameters(oauth2Options.TokenCacheParameters)
            .WithBtpTenantApiUri(oauth2Options.BtpTenantApiBaseUri)
            .Build();
        return new OAuth2HeaderProvider(service, authHeader);
    }

    // Java uses String.hashCode(), which is stable across runs; string.GetHashCode() is randomized in .NET.
    internal static int StableHash(string value)
    {
        var hash = 0;
        foreach (var c in value)
        {
            hash = unchecked((31 * hash) + c);
        }
        return hash;
    }
}
