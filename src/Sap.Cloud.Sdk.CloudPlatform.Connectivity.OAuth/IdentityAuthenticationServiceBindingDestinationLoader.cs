using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Creates destinations for re-use services that are backed by the SAP Identity Authentication Service (IAS):
/// bindings whose credentials contain <c>authentication-service.service-label = identity</c> and exactly one HTTP
/// endpoint. The destination targets that endpoint and authenticates with the binding's IAS credentials.
/// </summary>
public class IdentityAuthenticationServiceBindingDestinationLoader : IServiceBindingDestinationLoader
{
    private readonly Lazy<IServiceBindingDestinationLoader> _delegateLoader;

    /// <summary>Creates the loader; it delegates to the default service binding loader chain.</summary>
    public IdentityAuthenticationServiceBindingDestinationLoader()
    {
        _delegateLoader = new Lazy<IServiceBindingDestinationLoader>(() => ServiceBindingDestinationLoader.DefaultLoaderChain);
    }

    internal IdentityAuthenticationServiceBindingDestinationLoader(IServiceBindingDestinationLoader delegateLoader)
    {
        _delegateLoader = new Lazy<IServiceBindingDestinationLoader>(() => delegateLoader);
    }

    /// <inheritdoc/>
    public Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var binding = options.ServiceBinding;
        var message = $"Failed to create a destination for service '{binding.GetServiceIdentifier()}' using IAS OAuth credentials.";
        if (!IsBackedByIas(binding.Credentials))
        {
            return Result.Failure<IHttpDestination>(new DestinationNotFoundException(null,
                message + " The service binding does not represent a re-use service backed by IAS."));
        }

        HttpEndpoint endpoint;
        try
        {
            var endpoints = GetHttpEndpoints(binding.Credentials);
            if (endpoints.Count != 1)
            {
                throw new DestinationAccessException("The IAS-based service binding contains multiple HTTP endpoints.");
            }
            endpoint = endpoints[0];
        }
        catch (DestinationAccessException e)
        {
            return Result.Failure<IHttpDestination>(new DestinationAccessException(message, e));
        }

        ServiceBindingDestinationOptions.Builder optionsBuilder;
        try
        {
            optionsBuilder = ServiceBindingDestinationOptions.ForService(ServiceIdentifier.IdentityAuthentication);
        }
        catch (DestinationAccessException e)
        {
            return Result.Failure<IHttpDestination>(new DestinationAccessException(message, e));
        }
        optionsBuilder
            .OnBehalfOf(options.OnBehalfOf)
            .WithOption(BtpServiceOptions.AuthenticationServiceOptions.WithTargetUri(endpoint.Uri));
        if (!endpoint.AlwaysRequiresToken)
        {
            optionsBuilder.WithOption(BtpServiceOptions.IasOptions.WithoutTokenForTechnicalProviderUser());
        }

        var destination = _delegateLoader.Value.TryGetDestination(optionsBuilder.Build());
        return destination.IsSuccess
            ? destination
            : Result.Failure<IHttpDestination>(new DestinationAccessException(null, message, destination.Exception));
    }

    private static bool IsBackedByIas(IReadOnlyDictionary<string, object?> credentials) =>
        credentials.GetValueOrDefault("authentication-service") is IReadOnlyDictionary<string, object?> service
        && service.GetValueOrDefault("service-label") is string label
        && string.Equals(label, "identity", StringComparison.OrdinalIgnoreCase);

    private static List<HttpEndpoint> GetHttpEndpoints(IReadOnlyDictionary<string, object?> credentials)
    {
        var rawEndpoints = GetOrThrow<IReadOnlyDictionary<string, object?>>(credentials, "endpoints");
        var endpoints = new List<HttpEndpoint>();
        foreach (var name in rawEndpoints.Keys)
        {
            var entry = GetOrThrow<IReadOnlyDictionary<string, object?>>(rawEndpoints, name);
            var protocol = entry.GetValueOrDefault("protocol") as string ?? "http";
            if (!string.Equals(protocol, "http", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            endpoints.Add(new HttpEndpoint(
                new Uri(GetOrThrow<string>(entry, "uri"), UriKind.Absolute),
                GetWithFallbackOrThrow(entry, "always-requires-token", true),
                GetWithFallbackOrThrow(entry, "requires-mtls", true)));
        }
        return endpoints;
    }

    private static T GetOrThrow<T>(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var value))
        {
            throw new DestinationAccessException($"The '{key}' attribute of the IAS-based service binding is missing.");
        }
        return value is T typed
            ? typed
            : throw new DestinationAccessException(
                $"The '{key}' attribute of the IAS-based service binding is expected to be an instance of {typeof(T).Name}, "
                + $"but is of type {value?.GetType().Name ?? "null"} instead.");
    }

    private static bool GetWithFallbackOrThrow(IReadOnlyDictionary<string, object?> map, string key, bool fallback) =>
        map.ContainsKey(key) ? GetOrThrow<bool>(map, key) : fallback;

    private sealed record HttpEndpoint(Uri Uri, bool AlwaysRequiresToken, bool RequiresMutualTls);
}
