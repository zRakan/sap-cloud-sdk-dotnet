using Sap.Cloud.Sdk.CloudPlatform.Connectivity;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Hook that turns a destination into the <see cref="HttpClient"/> used by the request builders.
/// </summary>
/// <remarks>
/// Replaces <c>HttpClientAccessor.getHttpClient(destination)</c>. The HTTP layer
/// (<c>Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http</c>) sets <see cref="Provider"/>; this project does not reference it.
/// </remarks>
public static class ODataHttpClientProvider
{
    private static readonly Func<IHttpDestinationProperties, HttpClient> DefaultProvider = static _ =>
        throw new InvalidOperationException(
            "No HttpClient provider is registered. Add a reference to Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http " +
            "(and make sure its module is initialized), or pass an HttpClient to ExecuteAsync instead of a destination.");

    private static Func<IHttpDestinationProperties, HttpClient> s_provider = DefaultProvider;

    /// <summary>
    /// Creates (or looks up) the <see cref="HttpClient"/> for a destination. The default throws
    /// <see cref="InvalidOperationException"/> until the connectivity HTTP layer registers itself; setting
    /// <see langword="null"/> restores the default.
    /// </summary>
    public static Func<IHttpDestinationProperties, HttpClient> Provider
    {
        get => s_provider;
        set => s_provider = value ?? DefaultProvider;
    }

    internal static HttpClient GetHttpClient(IHttpDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return s_provider(destination);
    }
}
