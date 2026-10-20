using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Asks several loaders in order. The first success wins. A <see cref="DestinationNotFoundException"/> moves on to the
/// next loader; any other failure ends the lookup. If no loader finds the destination, the result is a
/// <see cref="DestinationNotFoundException"/> whose inner <see cref="AggregateException"/> holds the individual failures.
/// </summary>
public sealed class DestinationLoaderChain : IDestinationLoader
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationLoaderChain>();

    private readonly Lock _gate = new();
    private IReadOnlyList<IDestinationLoader> _loaders;

    /// <summary>Creates a chain.</summary>
    /// <param name="loaders">The loaders, in the order in which they are asked.</param>
    /// <exception cref="ArgumentException">No loaders are given.</exception>
    public DestinationLoaderChain(IEnumerable<IDestinationLoader> loaders)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        _loaders = [.. loaders];
        if (_loaders.Count == 0)
        {
            throw new ArgumentException("The given set of loaders must not be empty.", nameof(loaders));
        }
    }

    /// <summary>The loaders in the order in which they are asked.</summary>
    public IReadOnlyList<IDestinationLoader> Loaders => _loaders;

    /// <summary>Starts a chain with the given loader.</summary>
    /// <param name="firstLoader">The first loader.</param>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder(IDestinationLoader firstLoader) => new Builder().Append(firstLoader);

    /// <inheritdoc/>
    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        ArgumentNullException.ThrowIfNull(options);
        var suppressed = new List<Exception>();
        foreach (var loader in _loaders)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Delegating destination lookup for destination {Name} to the destination loader {Loader}.", destinationName, loader.GetType());
            }
            if (Evaluate(loader, destinationName, loader.TryGetDestination(destinationName, options), suppressed) is { } done)
            {
                return done;
            }
        }
        return NotFound(destinationName, suppressed);
    }

    /// <inheritdoc/>
    public async Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        ArgumentNullException.ThrowIfNull(options);
        var suppressed = new List<Exception>();
        foreach (var loader in _loaders)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Delegating destination lookup for destination {Name} to the destination loader {Loader}.", destinationName, loader.GetType());
            }
            var result = await loader.TryGetDestinationAsync(destinationName, options, cancellationToken).ConfigureAwait(false);
            if (Evaluate(loader, destinationName, result, suppressed) is { } done)
            {
                return done;
            }
        }
        return NotFound(destinationName, suppressed);
    }

    /// <summary>Lists the loader types.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() =>
        $"DestinationLoaderChain(loaders=[{string.Join(", ", _loaders.Select(l => l.GetType().Name))}])";

    internal void AppendLoader(IDestinationLoader loader)
    {
        lock (_gate)
        {
            _loaders = [.. _loaders, loader];
        }
    }

    internal void PrependLoader(IDestinationLoader loader)
    {
        lock (_gate)
        {
            _loaders = [loader, .. _loaders];
        }
    }

    private static Result<IDestination>? Evaluate(
        IDestinationLoader loader,
        string destinationName,
        Result<IDestination> result,
        List<Exception> suppressed)
    {
        if (result.IsSuccess)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Destination loader {Loader} successfully loaded destination {Name}.", loader.GetType(), destinationName);
            }
            return result;
        }
        var cause = result.Exception;
        if (!DefaultServiceBindingDestinationLoaderChain.HasCause<DestinationNotFoundException>(cause))
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(cause, "Destination loader {Loader} returned an exception when loading destination {Name}:", loader.GetType(), destinationName);
            }
            return result;
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("No destination with name '{Name}' was found in destination loader {Loader}.", destinationName, loader.GetType());
        }
        suppressed.Add(cause);
        return null;
    }

    private static Result<IDestination> NotFound(string destinationName, List<Exception> suppressed) =>
        Result<IDestination>.Failure(
            new DestinationNotFoundException(
                destinationName,
                "Destination could not be found in any of the registered loaders. Check the inner exceptions and the logs above to see which loaders were queried.",
                new AggregateException(suppressed)));

    /// <summary>Builds <see cref="DestinationLoaderChain"/>s.</summary>
    public sealed class Builder
    {
        private readonly List<IDestinationLoader> _loaders = [];

        internal Builder()
        {
        }

        /// <summary>Adds a loader at the end of the chain.</summary>
        /// <param name="nextLoader">The loader.</param>
        /// <returns>This builder.</returns>
        public Builder Append(IDestinationLoader nextLoader)
        {
            ArgumentNullException.ThrowIfNull(nextLoader);
            _loaders.Add(nextLoader);
            return this;
        }

        /// <summary>Creates the chain.</summary>
        /// <returns>The chain.</returns>
        public DestinationLoaderChain Build() => new(_loaders);
    }
}
