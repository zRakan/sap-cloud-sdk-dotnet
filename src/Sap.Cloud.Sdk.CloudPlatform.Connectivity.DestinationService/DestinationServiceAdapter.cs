using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The HTTP access to the REST API of the destination service.</summary>
internal interface IDestinationServiceAdapter
{
    /// <summary>The tenant that owns the destination service instance (the provider tenant).</summary>
    /// <exception cref="DestinationAccessException">The tenant cannot be determined from the service binding.</exception>
    string GetProviderTenantId();

    /// <summary>Sends a GET request to the destination service.</summary>
    /// <param name="servicePath">The path below <c>destination-configuration</c>, for example <c>/v1/destinations/name</c>.</param>
    /// <param name="strategy">On whose behalf to send the request, and which headers to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response body.</returns>
    /// <exception cref="DestinationNotFoundException">The service responded with 404.</exception>
    /// <exception cref="DestinationAccessException">The request failed.</exception>
    Task<string> GetConfigurationAsJsonAsync(string servicePath, DestinationRetrievalStrategy strategy, CancellationToken cancellationToken);
}

/// <summary>
/// Performs the requests to <c>/destination-configuration/v1/destinations/{name}</c> (and <c>/v2/...</c>),
/// <c>/v1/subaccountDestinations</c> and <c>/v1/instanceDestinations</c>.
/// </summary>
/// <remarks>
/// <para>
/// The destination used to reach the service is created from the <c>destination</c> service binding with
/// <see cref="ServiceBindingDestinationLoader.DefaultLoaderChain"/>, once per <see cref="OnBehalfOf"/> (failures are not
/// remembered). It carries the OAuth header provider that obtains the bearer token for the service, on behalf of the
/// requested identity.
/// </para>
/// <para>
/// Headers added to single destination requests: <c>x-user-token</c> or <c>x-refresh-token</c> (forwarding the token),
/// <c>x-fragment-name</c> and the custom headers of the options. Lists of destinations get none of them.
/// </para>
/// </remarks>
internal sealed class DestinationServiceAdapter : IDestinationServiceAdapter
{
    private const string ServicePath = "destination-configuration";

    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationServiceAdapter>();

    private readonly Func<OnBehalfOf, CancellationToken, Task<IHttpDestination>> _serviceDestinationLoader;
    private readonly Func<IServiceBinding> _serviceBindingSupplier;
    private readonly Func<IHttpDestination, HttpClient> _httpClientSource;
    private readonly string? _providerTenantId;
    private readonly ConcurrentDictionary<OnBehalfOf, IHttpDestination> _serviceDestinations = new();

    public DestinationServiceAdapter()
        : this(null, null, null)
    {
    }

    /// <param name="serviceDestinationLoader">Creates the destination that reaches the service; defaults to the binding based chain.</param>
    /// <param name="serviceBindingSupplier">Finds the <c>destination</c> service binding.</param>
    /// <param name="providerTenantId">The provider tenant, if known; otherwise it is read from the binding.</param>
    /// <param name="httpClientSource">
    /// Creates the <see cref="HttpClient"/> for the service destination; defaults to
    /// <see cref="HttpClientAccessor.GetHttpClient(IDestination)"/>. A seam for tests.
    /// </param>
    public DestinationServiceAdapter(
        Func<OnBehalfOf, CancellationToken, Task<IHttpDestination>>? serviceDestinationLoader,
        Func<IServiceBinding>? serviceBindingSupplier,
        string? providerTenantId,
        Func<IHttpDestination, HttpClient>? httpClientSource = null)
    {
        _serviceBindingSupplier = serviceBindingSupplier ?? GetDestinationServiceBinding;
        _serviceDestinationLoader = serviceDestinationLoader ?? LoadServiceDestinationAsync;
        _providerTenantId = providerTenantId;
        _httpClientSource = httpClientSource ?? (destination => HttpClientAccessor.GetHttpClient(destination));
    }

    public string GetProviderTenantId()
    {
        if (_providerTenantId is not null)
        {
            return _providerTenantId;
        }

        IServiceBinding binding;
        try
        {
            binding = _serviceBindingSupplier();
        }
#pragma warning disable CA1031 // Mirrors Java: whatever fails while resolving the binding is reported as DestinationAccessException.
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new DestinationAccessException(
                "Could not resolve destination to Destination Service on behalf of provider.",
                e);
        }

        if (binding.Credentials.TryGetValue("tenantid", out var tenantId) && tenantId is not null)
        {
            return Convert.ToString(tenantId, CultureInfo.InvariantCulture)!;
        }

        throw new DestinationAccessException(
            "The provider tenant id is not defined in the service binding. "
            + "Please verify that the service binding contains the field 'tenantid' in the credentials list.");
    }

    public async Task<string> GetConfigurationAsJsonAsync(
        string servicePath,
        DestinationRetrievalStrategy strategy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servicePath);
        ArgumentNullException.ThrowIfNull(strategy);
        if (!_serviceDestinations.TryGetValue(strategy.Behalf, out var serviceDestination))
        {
            serviceDestination = await _serviceDestinationLoader(strategy.Behalf, cancellationToken).ConfigureAwait(false)
                ?? throw new DestinationAccessException(
                    $"Destination for Destination Service on behalf of {strategy.Behalf} not found.");
            // a concurrent computation may have won; either result is equivalent
            serviceDestination = _serviceDestinations.GetOrAdd(strategy.Behalf, serviceDestination);
        }

        using var request = PrepareRequest(servicePath, strategy);
        // the destination pipeline rewrites the request URI; report the path as it was requested
        var requestPath = request.RequestUri!.OriginalString;
        try
        {
            using var client = _httpClientSource(serviceDestination);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await HandleResponseAsync(requestPath, response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new DestinationAccessException(e.Message, e);
        }
    }

    private async Task<IHttpDestination> LoadServiceDestinationAsync(OnBehalfOf behalf, CancellationToken cancellationToken)
    {
        try
        {
            var binding = _serviceBindingSupplier();
            var options = ServiceBindingDestinationOptions.ForService(binding).OnBehalfOf(behalf).Build();
            var result = await ServiceBindingDestinationLoader.DefaultLoaderChain
                .TryGetDestinationAsync(options, cancellationToken)
                .ConfigureAwait(false);
            return result.GetValueOrThrow();
        }
#pragma warning disable CA1031 // Mirrors Java: every failure to create the service destination is a DestinationAccessException.
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new DestinationAccessException(
                $"Could not resolve destination to Destination Service on behalf of {behalf}.",
                e);
        }
    }

    private static async Task<string> HandleResponseAsync(
        string requestUri,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var reasonPhrase = response.ReasonPhrase ?? response.StatusCode.ToString();
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Destination service returned HTTP status {Status} ({Reason})", statusCode, reasonPhrase);
        }

        string? body = null;
        DestinationAccessException? bodyFailure = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or IOException)
        {
            bodyFailure = new DestinationAccessException("Failed to read body from HTTP response", e);
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            if (bodyFailure is not null)
            {
                throw bodyFailure;
            }
            return string.IsNullOrEmpty(body)
                ? throw new DestinationAccessException("Failed to get destinations: no body returned in response.")
                : body;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DestinationNotFoundException(null, "Destination could not be found for path " + requestUri + ".");
        }

        var message =
            $"Failed to get destinations: destination service responded with HTTP status {statusCode.ToString(CultureInfo.InvariantCulture)} "
            + $"({reasonPhrase.ToUpperInvariant()}) at '{requestUri}'.";
        Log.LogError("{Message}", message);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Body of the failed destination service response: {Body}", body ?? bodyFailure?.Message);
        }
        throw new DestinationAccessException(message);
    }

    private static HttpRequestMessage PrepareRequest(string servicePath, DestinationRetrievalStrategy strategy)
    {
        var requestUri = $"/{ServicePath}{servicePath}";
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Querying Destination Service via URI {Uri}.", requestUri);
        }
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(requestUri, UriKind.Relative));

        if (!servicePath.StartsWith(DestinationService.PathDefault, StringComparison.Ordinal)
            && !servicePath.StartsWith(DestinationService.PathV2, StringComparison.Ordinal))
        {
            // additional headers and settings are only needed for single destination requests
            return request;
        }

        switch (strategy.TokenForwarding)
        {
            case TokenForwarding.UserToken:
                request.Headers.TryAddWithoutValidation("x-user-token", strategy.Token);
                break;
            case TokenForwarding.RefreshToken:
                request.Headers.TryAddWithoutValidation("x-refresh-token", strategy.Token);
                break;
            default:
                break;
        }
        if (strategy.Fragment is not null)
        {
            request.Headers.TryAddWithoutValidation("x-fragment-name", strategy.Fragment);
        }
        foreach (var header in strategy.AdditionalHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }
        return request;
    }

    internal static IServiceBinding GetDestinationServiceBinding()
    {
        var matching = DefaultServiceBindingAccessor.Instance.GetServiceBindings()
            .Where(binding => ServiceIdentifier.Destination.Equals(binding.GetServiceIdentifier()))
            .ToList();

        return matching.Count switch
        {
            0 => throw new NoServiceBindingException("Please make sure you have the Destination Service bound to your application."),
            1 => matching[0],
            _ => throw new MultipleServiceBindingsException("Having multiple Destination Service bindings is not supported."),
        };
    }
}
