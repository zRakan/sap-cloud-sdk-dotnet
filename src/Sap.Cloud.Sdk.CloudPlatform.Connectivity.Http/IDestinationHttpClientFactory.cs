using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Creates <see cref="HttpClient"/>s, optionally targeting a destination.</summary>
/// <remarks>
/// Port of <c>ApacheHttpClient5Factory</c> (and <c>HttpClientFactory</c>). Renamed because
/// <c>IHttpClientFactory</c> belongs to <c>Microsoft.Extensions.Http</c>. In Java the accessor caches the clients
/// the factory creates; here <see cref="DefaultDestinationHttpClientFactory"/> caches its expensive part (the
/// connection pool) itself, so custom implementations decide on their own caching strategy.
/// </remarks>
public interface IDestinationHttpClientFactory
{
    /// <summary>Creates an <see cref="HttpClient"/> for a destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The client.</returns>
    /// <exception cref="DestinationAccessException">The destination cannot be used, for example an on-premise destination without proxy.</exception>
    /// <exception cref="HttpClientInstantiationException">The client cannot be created.</exception>
    HttpClient CreateHttpClient(IHttpDestinationProperties destination);

    /// <summary>Creates an <see cref="HttpClient"/> that is not bound to a destination.</summary>
    /// <returns>The client.</returns>
    /// <exception cref="HttpClientInstantiationException">The client cannot be created.</exception>
    HttpClient CreateHttpClient();
}
