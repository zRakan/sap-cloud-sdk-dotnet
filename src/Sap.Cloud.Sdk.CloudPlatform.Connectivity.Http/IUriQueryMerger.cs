namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Resolves the request URI for a given relative request URI.</summary>
/// <remarks>
/// Port of <c>UriQueryMerger</c>. Used to determine the destination-contributed query parameters, so that next-link
/// pagination can strip duplicate parameters. Java's functional interface is a regular interface here; see
/// <see cref="DestinationUriQueryMerger"/> for the implementation.
/// </remarks>
public interface IUriQueryMerger
{
    /// <summary>
    /// Returns the fully merged request URI for the given relative request URI: the destination base URL, the query of
    /// the destination URL and the <c>URL.queries.*</c> destination properties are merged into it.
    /// </summary>
    /// <param name="requestUri">The relative request URI (create it with <see cref="UriKind.Relative"/>).</param>
    /// <returns>The merged, absolute request URI.</returns>
    /// <exception cref="Exceptions.DestinationPathsNotMergeableException">The URIs cannot be merged.</exception>
    Uri MergeRequestUri(Uri requestUri);
}
