using Sap.Cloud.Sdk.CloudPlatform.Connectivity;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Hook that turns a destination into the <see cref="HttpClient"/> used by the request builders.
/// </summary>
/// <remarks>
/// Defaults to <see cref="HttpClientAccessor.GetHttpClient(IHttpDestinationProperties)"/>, like Java's
/// <c>HttpClientAccessor.getHttpClient(destination)</c>. Replace it to customize how clients are created.
/// </remarks>
public static class ODataHttpClientProvider
{
    private static readonly Func<IHttpDestinationProperties, HttpClient> DefaultProvider = HttpClientAccessor.GetHttpClient;

    private static Func<IHttpDestinationProperties, HttpClient> s_provider = DefaultProvider;

    /// <summary>
    /// Creates (or looks up) the <see cref="HttpClient"/> for a destination. Setting <see langword="null"/> restores
    /// the default, <see cref="HttpClientAccessor.GetHttpClient(IHttpDestinationProperties)"/>.
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
