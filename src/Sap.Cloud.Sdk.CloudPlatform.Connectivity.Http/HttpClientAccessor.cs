using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Accessor for <see cref="HttpClient"/>s that target destinations.</summary>
/// <remarks>
/// <para>
/// Port of <c>ApacheHttpClient5Accessor</c> (and <c>HttpClientAccessor</c> of the httpclient4 module). The client cache
/// (<c>setHttpClientCache</c>) is part of the <see cref="DefaultDestinationHttpClientFactory"/>; configure it with
/// <see cref="DestinationHttpClientFactoryOptions"/> and set the factory as <see cref="HttpClientFactory"/>.
/// </para>
/// <para>
/// The factory is accessed concurrently, and setting a custom one affects all consumers of the accessor.
/// </para>
/// </remarks>
public static class HttpClientAccessor
{
    private static volatile IDestinationHttpClientFactory? _httpClientFactory;

    /// <summary>
    /// The factory used to create clients. Defaults to a <see cref="DefaultDestinationHttpClientFactory"/> with default
    /// options; setting <see langword="null"/> restores it.
    /// </summary>
    public static IDestinationHttpClientFactory HttpClientFactory
    {
        get => _httpClientFactory ?? DefaultDestinationHttpClientFactory.Shared;
        set => _httpClientFactory = value;
    }

    /// <summary>Gets an <see cref="HttpClient"/> that is independent of any destination.</summary>
    /// <returns>The client.</returns>
    /// <exception cref="HttpClientInstantiationException">The client cannot be created.</exception>
    public static HttpClient GetHttpClient() => TryGetHttpClient().GetValueOrThrow(failure => failure switch
    {
        DestinationAccessException or HttpClientInstantiationException => failure,
        _ => new HttpClientInstantiationException("Failed to get HttpClient.", failure),
    });

    /// <summary>Gets an <see cref="HttpClient"/> that is independent of any destination.</summary>
    /// <returns>The client, or the failure.</returns>
    public static Result<HttpClient> TryGetHttpClient() => Result.Of(() => HttpClientFactory.CreateHttpClient());

    /// <summary>Gets an <see cref="HttpClient"/> for a destination. Requests are sent to the destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The client. Its pipeline resources are cached; the client itself is cheap and should be short-lived.</returns>
    /// <exception cref="DestinationAccessException">The destination is not an HTTP destination or cannot be used.</exception>
    /// <exception cref="HttpClientInstantiationException">The client cannot be created.</exception>
    public static HttpClient GetHttpClient(IDestination destination) =>
        TryGetHttpClient(destination).GetValueOrThrow(failure => failure switch
        {
            DestinationAccessException or HttpClientInstantiationException => failure,
            _ => new HttpClientInstantiationException("Failed to get HttpClient for destination.", failure),
        });

    /// <summary>Gets an <see cref="HttpClient"/> for a destination. Requests are sent to the destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The client.</returns>
    /// <exception cref="DestinationAccessException">The destination cannot be used.</exception>
    /// <exception cref="HttpClientInstantiationException">The client cannot be created.</exception>
    public static HttpClient GetHttpClient(IHttpDestinationProperties destination) =>
        GetHttpClient((IDestination)destination);

    /// <summary>Gets an <see cref="HttpClient"/> for a destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The client, or the failure.</returns>
    public static Result<HttpClient> TryGetHttpClient(IDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.IsHttp())
        {
            return Result<HttpClient>.Failure(new DestinationAccessException("The given destination is not an HTTP destination."));
        }
        return Result.Of(() => HttpClientFactory.CreateHttpClient(destination.AsHttp()));
    }
}
