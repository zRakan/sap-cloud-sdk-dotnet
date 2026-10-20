namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// A prepared retrieval of a single destination: the (possibly multi-step) operation, and the identity on whose behalf the
/// first request is sent.
/// </summary>
/// <param name="Result">Performs the retrieval.</param>
/// <param name="OnBehalfOf">On whose behalf the first request is sent; determines the tenant of the destination.</param>
internal sealed record DestinationRetrieval(
    Func<CancellationToken, Task<DestinationServiceV1Response>> Result,
    OnBehalfOf OnBehalfOf)
{
    public Task<DestinationServiceV1Response> GetAsync(CancellationToken cancellationToken) => Result(cancellationToken);
}
