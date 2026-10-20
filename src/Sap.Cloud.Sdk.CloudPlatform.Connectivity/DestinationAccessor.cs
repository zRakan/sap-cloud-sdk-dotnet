using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// The entry point to look up destinations by name.
/// </summary>
/// <remarks>
/// <para>
/// By default, destinations are loaded by a <see cref="DestinationLoaderChain"/> that first asks an
/// <see cref="EnvVarDestinationLoader"/> and then the loaders registered with <see cref="RegisterDefaultLoader"/> (Java:
/// <c>FacadeLocator</c>; the destination service module registers its loader this way). The default is created on first
/// use. Replace it with <see cref="Loader"/>.
/// </para>
/// <para>
/// Lookups through the loaders of this module are in-memory; the <c>…Async</c> methods exist so that loaders that perform
/// I/O can be used without blocking.
/// </para>
/// </remarks>
public static class DestinationAccessor
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(DestinationAccessor).FullName!);
    private static readonly Lock Gate = new();

    private static IDestinationLoader? _loader;
    private static bool _isAutoCreated;
    private static ImmutableList<IDestinationLoader> _defaultSecondaryLoaders = [];

    /// <summary>
    /// The loader used for lookups. Setting <see langword="null"/> restores the default chain.
    /// </summary>
    public static IDestinationLoader Loader
    {
        get
        {
            lock (Gate)
            {
                return _loader ??= CreateDefaultLoader();
            }
        }
        set
        {
            lock (Gate)
            {
                if (value is null)
                {
                    if (Log.IsEnabled(LogLevel.Information))
                    {
                        Log.LogInformation("Resetting the current {Type} used to the default {Default}.", nameof(IDestinationLoader), nameof(DestinationLoaderChain));
                    }
                    _loader = null;
                    _isAutoCreated = false;
                }
                else
                {
                    if (Log.IsEnabled(LogLevel.Information))
                    {
                        Log.LogInformation("Setting the current {Type} used to a custom instance of {Custom}.", nameof(IDestinationLoader), value.GetType());
                    }
                    _loader = value;
                    _isAutoCreated = false;
                }
            }
        }
    }

    /// <summary>Registers a loader that the default chain asks after the environment variable loader.</summary>
    /// <param name="loader">The loader.</param>
    /// <remarks>If the default chain has already been created and not been replaced, it is re-created.</remarks>
    public static void RegisterDefaultLoader(IDestinationLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Gate)
        {
            if (!_defaultSecondaryLoaders.Contains(loader))
            {
                _defaultSecondaryLoaders = _defaultSecondaryLoaders.Add(loader);
                ResetAutoCreated();
            }
        }
    }

    /// <summary>Removes a loader registered with <see cref="RegisterDefaultLoader"/>.</summary>
    /// <param name="loader">The loader.</param>
    /// <returns><see langword="true"/> if it was registered.</returns>
    public static bool UnregisterDefaultLoader(IDestinationLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Gate)
        {
            var updated = _defaultSecondaryLoaders.Remove(loader);
            var removed = updated.Count != _defaultSecondaryLoaders.Count;
            _defaultSecondaryLoaders = updated;
            ResetAutoCreated();
            return removed;
        }
    }

    /// <summary>Gets a destination.</summary>
    /// <param name="destinationName">The name.</param>
    /// <returns>The destination.</returns>
    /// <exception cref="DestinationNotFoundException">There is no such destination.</exception>
    /// <exception cref="DestinationAccessException">The destination cannot be loaded.</exception>
    public static IDestination GetDestination(string destinationName) =>
        GetDestination(destinationName, DestinationOptions.CreateBuilder().Build());

    /// <summary>Gets a destination.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Loader specific options.</param>
    /// <returns>The destination.</returns>
    /// <exception cref="DestinationNotFoundException">There is no such destination.</exception>
    /// <exception cref="DestinationAccessException">The destination cannot be loaded.</exception>
    public static IDestination GetDestination(string destinationName, DestinationOptions options) =>
        TryGetDestination(destinationName, options).GetValueOrThrow(failure => Translate(destinationName, failure));

    /// <summary>Gets a destination.</summary>
    /// <param name="destinationName">The name.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public static Result<IDestination> TryGetDestination(string destinationName) =>
        TryGetDestination(destinationName, DestinationOptions.CreateBuilder().Build());

    /// <summary>Gets a destination.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Loader specific options.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public static Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        ArgumentNullException.ThrowIfNull(options);
        return Loader.TryGetDestination(destinationName, options);
    }

    /// <summary>Gets a destination asynchronously.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination.</returns>
    /// <exception cref="DestinationNotFoundException">There is no such destination.</exception>
    /// <exception cref="DestinationAccessException">The destination cannot be loaded.</exception>
    public static Task<IDestination> GetDestinationAsync(string destinationName, CancellationToken cancellationToken = default) =>
        GetDestinationAsync(destinationName, DestinationOptions.CreateBuilder().Build(), cancellationToken);

    /// <summary>Gets a destination asynchronously.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Loader specific options.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination.</returns>
    /// <exception cref="DestinationNotFoundException">There is no such destination.</exception>
    /// <exception cref="DestinationAccessException">The destination cannot be loaded.</exception>
    public static async Task<IDestination> GetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await TryGetDestinationAsync(destinationName, options, cancellationToken).ConfigureAwait(false);
        return result.GetValueOrThrow(failure => Translate(destinationName, failure));
    }

    /// <summary>Gets a destination asynchronously.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public static Task<Result<IDestination>> TryGetDestinationAsync(string destinationName, CancellationToken cancellationToken = default) =>
        TryGetDestinationAsync(destinationName, DestinationOptions.CreateBuilder().Build(), cancellationToken);

    /// <summary>Gets a destination asynchronously.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Loader specific options.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public static Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        ArgumentNullException.ThrowIfNull(options);
        return Loader.TryGetDestinationAsync(destinationName, options, cancellationToken);
    }

    /// <summary>Adds a loader at the end of the current chain.</summary>
    /// <param name="newLoader">The loader.</param>
    public static void AppendDestinationLoader(IDestinationLoader newLoader)
    {
        ArgumentNullException.ThrowIfNull(newLoader);
        lock (Gate)
        {
            var current = Loader;
            if (ReferenceEquals(current, newLoader))
            {
                return;
            }
            if (current is DestinationLoaderChain chain)
            {
                chain.AppendLoader(newLoader);
                _isAutoCreated = false;
            }
            else
            {
                Loader = DestinationLoaderChain.CreateBuilder(current).Append(newLoader).Build();
            }
        }
    }

    /// <summary>Adds a loader at the start of the current chain.</summary>
    /// <param name="newLoader">The loader.</param>
    public static void PrependDestinationLoader(IDestinationLoader newLoader)
    {
        ArgumentNullException.ThrowIfNull(newLoader);
        lock (Gate)
        {
            var current = Loader;
            if (ReferenceEquals(current, newLoader))
            {
                return;
            }
            if (current is DestinationLoaderChain chain)
            {
                chain.PrependLoader(newLoader);
                _isAutoCreated = false;
            }
            else
            {
                Loader = DestinationLoaderChain.CreateBuilder(newLoader).Append(current).Build();
            }
        }
    }

    private static DestinationLoaderChain CreateDefaultLoader()
    {
        if (Log.IsEnabled(LogLevel.Information))
        {
            Log.LogInformation(
                "Creating a new DestinationLoaderChain with {First} as the primary {Type} implementation.",
                nameof(EnvVarDestinationLoader),
                nameof(IDestinationLoader));
        }
        var builder = DestinationLoaderChain.CreateBuilder(new EnvVarDestinationLoader());
        foreach (var secondary in _defaultSecondaryLoaders)
        {
            if (Log.IsEnabled(LogLevel.Information))
            {
                Log.LogInformation("Using an instance of {Loader} as a secondary {Type} implementation.", secondary.GetType(), nameof(IDestinationLoader));
            }
            builder.Append(secondary);
        }
        _isAutoCreated = true;
        return builder.Build();
    }

    private static void ResetAutoCreated()
    {
        if (_isAutoCreated)
        {
            _loader = null;
            _isAutoCreated = false;
        }
    }

    private static Exception Translate(string destinationName, Exception failure) =>
        failure is DestinationAccessException or DestinationNotFoundException
            ? failure
            : new DestinationAccessException($"Failed to get destination with name '{destinationName}'.", failure);
}
