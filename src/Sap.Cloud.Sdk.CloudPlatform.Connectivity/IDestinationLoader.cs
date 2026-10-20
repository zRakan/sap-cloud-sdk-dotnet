namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Loads destinations by name.</summary>
/// <remarks>
/// <para>
/// Port of <c>DestinationLoader</c>. Lookups of the loaders in this module are in-memory and synchronous. A loader that
/// performs I/O (the BTP destination service) overrides <see cref="TryGetDestinationAsync"/>; by default the asynchronous
/// method calls the synchronous one, so purely in-memory loaders only implement <see cref="TryGetDestination"/>.
/// </para>
/// <para>
/// A failure with <see cref="Exceptions.DestinationNotFoundException"/> means "not found here"; a
/// <see cref="DestinationLoaderChain"/> then asks the next loader. Any other failure ends the lookup.
/// </para>
/// <para>
/// Overloads without options are the extension methods of <see cref="DestinationLoaderExtensions"/>.
/// </para>
/// </remarks>
public interface IDestinationLoader
{
    /// <summary>Looks up a destination.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Loader specific options.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options);

    /// <summary>Looks up a destination asynchronously. By default this calls <see cref="TryGetDestination"/>.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Loader specific options.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetDestination(destinationName, options));
}

/// <summary>Overloads of <see cref="IDestinationLoader"/> without options.</summary>
public static class DestinationLoaderExtensions
{
    /// <summary>Looks up a destination with default options.</summary>
    /// <param name="loader">The loader.</param>
    /// <param name="destinationName">The name.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public static Result<IDestination> TryGetDestination(this IDestinationLoader loader, string destinationName)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return loader.TryGetDestination(destinationName, DestinationOptions.CreateBuilder().Build());
    }

    /// <summary>Looks up a destination asynchronously with default options.</summary>
    /// <param name="loader">The loader.</param>
    /// <param name="destinationName">The name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public static Task<Result<IDestination>> TryGetDestinationAsync(
        this IDestinationLoader loader,
        string destinationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return loader.TryGetDestinationAsync(destinationName, DestinationOptions.CreateBuilder().Build(), cancellationToken);
    }
}
