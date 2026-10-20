using System.Collections.Immutable;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Creates an HTTP destination from a service binding.</summary>
/// <remarks>
/// Implementations (for example for the XSUAA or destination service) live in other modules and register themselves with
/// <see cref="ServiceBindingDestinationLoader.RegisterDefaultLoader"/>. Loading is synchronous here because this module does no I/O;
/// implementations that do I/O override <see cref="TryGetDestinationAsync"/>.
/// </remarks>
public interface IServiceBindingDestinationLoader
{
    /// <summary>Creates a destination.</summary>
    /// <param name="options">The binding and further options.</param>
    /// <returns>The destination; a failure with <see cref="Exceptions.DestinationNotFoundException"/> means "not responsible".</returns>
    Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options);

    /// <summary>Creates a destination, or throws.</summary>
    /// <param name="options">The binding and further options.</param>
    /// <returns>The destination.</returns>
    /// <exception cref="Exceptions.DestinationAccessException">The destination cannot be created.</exception>
    /// <exception cref="Exceptions.DestinationNotFoundException">The loader is not responsible for the binding.</exception>
    IHttpDestination GetDestination(ServiceBindingDestinationOptions options) => TryGetDestination(options).GetValueOrThrow();

    /// <summary>Creates a destination asynchronously. By default this calls <see cref="TryGetDestination"/>.</summary>
    /// <param name="options">The binding and further options.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination.</returns>
    Task<Result<IHttpDestination>> TryGetDestinationAsync(
        ServiceBindingDestinationOptions options,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetDestination(options));
}

/// <summary>Entry points and registry for <see cref="IServiceBindingDestinationLoader"/>s.</summary>
public static class ServiceBindingDestinationLoader
{
    private static readonly Lock Gate = new();
    private static ImmutableList<IServiceBindingDestinationLoader> _defaultLoaders = [];

    /// <summary>The loaders registered for the default chain.</summary>
    public static IReadOnlyList<IServiceBindingDestinationLoader> DefaultLoaders
    {
        get
        {
            SdkModules.EnsureLoaded();
            return _defaultLoaders;
        }
    }

    /// <summary>
    /// The chain of all registered loaders. It reads the registry on every call, so loaders registered later are used.
    /// </summary>
    public static IServiceBindingDestinationLoader DefaultLoaderChain { get; } =
        new DefaultServiceBindingDestinationLoaderChain(() => DefaultLoaders);

    /// <summary>Creates a chain of the given loaders.</summary>
    /// <param name="delegateLoaders">The loaders, in the order in which they are tried.</param>
    /// <returns>The chain.</returns>
    public static IServiceBindingDestinationLoader NewLoaderChain(IEnumerable<IServiceBindingDestinationLoader> delegateLoaders)
    {
        ArgumentNullException.ThrowIfNull(delegateLoaders);
        var loaders = delegateLoaders.ToImmutableList();
        return new DefaultServiceBindingDestinationLoaderChain(() => loaders);
    }

    /// <summary>Registers a loader for the default chain (Java: <c>ServiceLoader</c>). Registering the same instance twice has no effect.</summary>
    /// <param name="loader">The loader.</param>
    public static void RegisterDefaultLoader(IServiceBindingDestinationLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Gate)
        {
            if (!_defaultLoaders.Contains(loader))
            {
                _defaultLoaders = _defaultLoaders.Add(loader);
            }
        }
    }

    /// <summary>Removes a loader from the default chain.</summary>
    /// <param name="loader">The loader.</param>
    /// <returns><see langword="true"/> if it was registered.</returns>
    public static bool UnregisterDefaultLoader(IServiceBindingDestinationLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Gate)
        {
            var updated = _defaultLoaders.Remove(loader);
            var removed = updated.Count != _defaultLoaders.Count;
            _defaultLoaders = updated;
            return removed;
        }
    }

    /// <summary>Removes all loaders from the default chain.</summary>
    public static void ClearDefaultLoaders()
    {
        lock (Gate)
        {
            _defaultLoaders = [];
        }
    }
}
