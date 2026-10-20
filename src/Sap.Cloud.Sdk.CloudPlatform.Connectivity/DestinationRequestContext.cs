namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The context in which headers for a request to a destination are created.</summary>
/// <param name="Destination">The destination.</param>
/// <param name="RequestUri">The URI of the request.</param>
public sealed record DestinationRequestContext(IHttpDestination Destination, Uri RequestUri);
