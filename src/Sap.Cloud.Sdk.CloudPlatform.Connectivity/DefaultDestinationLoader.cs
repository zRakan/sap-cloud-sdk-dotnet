using System.Collections.Concurrent;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>A loader for destinations that the application registers programmatically, keyed by their <c>Name</c> property.</summary>
public sealed class DefaultDestinationLoader : IDestinationLoader
{
    private readonly ConcurrentDictionary<string, IDestination> _destinations = new(StringComparer.Ordinal);

    /// <summary>Registers a destination, replacing one with the same name.</summary>
    /// <param name="destination">The destination; it needs a non-empty <c>Name</c> property.</param>
    /// <returns>This loader.</returns>
    /// <exception cref="ArgumentException">The destination has no name.</exception>
    public DefaultDestinationLoader RegisterDestination(IDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.TryGet(DestinationProperty.Name, out var name) || string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The supplied destination is lacking a name to be identified by.", nameof(destination));
        }
        _destinations[name] = destination;
        return this;
    }

    /// <inheritdoc/>
    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        return _destinations.TryGetValue(destinationName, out var destination)
            ? Result<IDestination>.Success(destination)
            : Result<IDestination>.Failure(new DestinationNotFoundException(destinationName));
    }

    /// <inheritdoc/>
    public Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetDestination(destinationName, options));
}
