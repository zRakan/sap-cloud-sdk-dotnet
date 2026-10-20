using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>
/// An ordered set of <see cref="IThreadContextListener"/>s, keyed by priority. New chains start with the globally
/// registered default listeners.
/// </summary>
/// <remarks>
/// The Java SDK discovers default listeners with <c>ServiceLoader</c>. In .NET each SDK assembly registers its
/// listeners from a module initializer, so a listener is a default once its assembly has been loaded.
/// </remarks>
public sealed class DefaultThreadContextListenerChain
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultThreadContextListenerChain>();
    private static readonly Lock DefaultsLock = new();
    private static readonly Dictionary<int, IThreadContextListener> Defaults = [];

    private readonly Dictionary<int, IThreadContextListener> _listeners;

    /// <summary>Creates a chain containing the current default listeners.</summary>
    public DefaultThreadContextListenerChain()
    {
        lock (DefaultsLock)
        {
            _listeners = new Dictionary<int, IThreadContextListener>(Defaults);
        }
    }

    /// <summary>Registers a listener that every new chain starts with, replacing one with the same priority.</summary>
    /// <param name="listener">The listener.</param>
    public static void RegisterDefaultListener(IThreadContextListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (DefaultsLock)
        {
            Defaults[listener.Priority] = listener;
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Registered default listener {Listener} with priority {Priority}.",
            listener.GetType().FullName, listener.Priority);
        }
    }

    /// <summary>Removes the default listener with the given priority.</summary>
    /// <param name="priority">The priority.</param>
    public static void UnregisterDefaultListener(int priority)
    {
        lock (DefaultsLock)
        {
            Defaults.Remove(priority);
        }
    }

    /// <summary>The default listeners, ordered by priority.</summary>
    /// <returns>The listeners.</returns>
    public static IReadOnlyList<IThreadContextListener> GetDefaultListeners()
    {
        lock (DefaultsLock)
        {
            return [.. Defaults.Values.OrderBy(l => l.Priority)];
        }
    }

    /// <summary>The listeners of this chain, ordered by priority.</summary>
    public IReadOnlyList<IThreadContextListener> ListenersOrderedByPriority =>
        [.. _listeners.Values.OrderBy(l => l.Priority)];

    /// <summary>Adds a listener, replacing one with the same priority.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(IThreadContextListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners[listener.Priority] = listener;
    }

    /// <summary>Removes the listener with the given priority.</summary>
    /// <param name="priority">The priority.</param>
    public void RemoveListener(int priority) => _listeners.Remove(priority);

    /// <summary>Removes all listeners that were registered as defaults.</summary>
    public void RemoveDefaultListeners()
    {
        foreach (var priority in GetDefaultListeners().Select(l => l.Priority))
        {
            _listeners.Remove(priority);
        }
    }

    /// <summary>Gets the listener with the given priority.</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>The listener, or <see langword="null"/>.</returns>
    public IThreadContextListener? GetListener(int priority) => _listeners.GetValueOrDefault(priority);
}
