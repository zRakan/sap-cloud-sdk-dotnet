using System.Collections.Immutable;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Adds headers to requests sent to an HTTP destination.</summary>
/// <remarks>
/// Port of <c>DestinationHeaderProvider</c>. Custom providers are attached with
/// <see cref="DefaultHttpDestination.Builder.HeaderProviders"/>; providers registered with
/// <see cref="DestinationHeaderProviders.Register"/> apply to every destination (Java: <c>ServiceLoader</c>).
/// </remarks>
public interface IDestinationHeaderProvider
{
    /// <summary>Gets the headers for a request.</summary>
    /// <param name="requestContext">The destination and request URI.</param>
    /// <returns>The headers to add.</returns>
    IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext);
}

/// <summary>The registry of <see cref="IDestinationHeaderProvider"/>s that apply to every HTTP destination.</summary>
/// <remarks>Replaces the Java <c>META-INF/services</c> registration. The built-in <see cref="ErpDestinationHeaderProvider"/> is registered by default.</remarks>
public static class DestinationHeaderProviders
{
    private static readonly Lock Gate = new();
    private static ImmutableList<IDestinationHeaderProvider> _providers = [];

    /// <summary>The registered providers, in registration order.</summary>
    public static IReadOnlyList<IDestinationHeaderProvider> Registered => _providers;

    /// <summary>Registers a provider for all destinations. Registering the same instance twice has no effect.</summary>
    /// <param name="provider">The provider.</param>
    public static void Register(IDestinationHeaderProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            if (!_providers.Contains(provider))
            {
                _providers = _providers.Add(provider);
            }
        }
    }

    /// <summary>Removes a provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns><see langword="true"/> if it was registered.</returns>
    public static bool Unregister(IDestinationHeaderProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            var updated = _providers.Remove(provider);
            var removed = updated.Count != _providers.Count;
            _providers = updated;
            return removed;
        }
    }

    /// <summary>Removes all providers of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The provider type.</typeparam>
    /// <returns>The number of removed providers.</returns>
    public static int UnregisterAll<T>()
        where T : IDestinationHeaderProvider
    {
        lock (Gate)
        {
            var before = _providers.Count;
            _providers = _providers.RemoveAll(p => p is T);
            return before - _providers.Count;
        }
    }
}
