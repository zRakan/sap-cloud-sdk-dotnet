using Sap.Cloud.Sdk.CloudPlatform.Connectivity;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>
/// The hook through which fluent helpers and lazily loaded navigation properties obtain an <see cref="HttpClient"/> for a
/// destination.
/// </summary>
/// <remarks>
/// Replaces <c>HttpClientAccessor.getHttpClient(destination)</c>. This module does not depend on the HTTP layer of the Cloud Platform
/// connectivity: <c>Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http</c> sets <see cref="Provider"/> when it is referenced. The default
/// throws <see cref="InvalidOperationException"/>. Applications can also pass an <see cref="HttpClient"/> to <c>ExecuteAsync</c> directly.
/// </remarks>
public static class ODataHttpClientProvider
{
    private static readonly Func<IHttpDestinationProperties, HttpClient> Default = static _ =>
        throw new InvalidOperationException(
            "No HttpClient provider is registered for destinations. Reference Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http "
            + "(which registers the provider) or set ODataHttpClientProvider.Provider, or pass an HttpClient to ExecuteAsync.");

    private static volatile Func<IHttpDestinationProperties, HttpClient>? _provider;

    /// <summary>The function that creates or looks up the <see cref="HttpClient"/> for a destination.</summary>
    public static Func<IHttpDestinationProperties, HttpClient> Provider
    {
        get => _provider ?? Default;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _provider = value;
        }
    }

    /// <summary>Restores the default provider, which throws <see cref="InvalidOperationException"/>.</summary>
    public static void Reset() => _provider = null;
}

/// <summary>Where a fluent helper runs its requests: an <see cref="HttpClient"/> or a destination resolved to one on demand.</summary>
/// <remarks>Entities remember the target of the request that returned them to load navigation properties later.</remarks>
public sealed class ODataExecutionTarget
{
    private readonly HttpClient? _httpClient;

    private ODataExecutionTarget(HttpClient? httpClient, IHttpDestinationProperties? destination)
    {
        _httpClient = httpClient;
        Destination = destination;
    }

    /// <summary>The destination, if the target was created from one.</summary>
    public IHttpDestinationProperties? Destination { get; }

    /// <summary>Creates a target that uses the given client.</summary>
    /// <param name="httpClient">The client.</param>
    /// <returns>The target.</returns>
    public static ODataExecutionTarget ForClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return new ODataExecutionTarget(httpClient, null);
    }

    /// <summary>Creates a target that obtains its client from <see cref="ODataHttpClientProvider"/>.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The target.</returns>
    public static ODataExecutionTarget ForDestination(IHttpDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new ODataExecutionTarget(null, destination);
    }

    /// <summary>Gets the client to use now.</summary>
    /// <returns>The client.</returns>
    /// <exception cref="InvalidOperationException">The target is a destination and no provider is registered.</exception>
    public HttpClient GetHttpClient() => _httpClient ?? ODataHttpClientProvider.Provider(Destination!);
}

/// <summary>A request that can be executed against an <see cref="HttpClient"/> or a destination.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>Port of <c>FluentHelperExecutable</c> (<c>executeRequest(destination)</c>).</remarks>
public interface IFluentHelperExecutable<TResult>
{
    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default);

    /// <summary>Executes the request against a destination; the client is obtained from <see cref="ODataHttpClientProvider"/>.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<TResult> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default);
}
