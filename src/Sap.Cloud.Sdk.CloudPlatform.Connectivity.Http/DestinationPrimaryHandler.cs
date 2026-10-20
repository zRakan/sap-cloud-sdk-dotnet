namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// The primary handler of clients created by <c>AddSapDestination</c>. It forwards each request to the cached
/// <see cref="SocketsHttpHandler"/> of the destination that <see cref="DestinationRequestHandler"/> resolved for it.
/// </summary>
/// <remarks>
/// The destination of such clients is only known at request time (and may depend on the tenant), so the
/// destination-specific handler cannot be chosen when the pipeline is built. The cached handlers are owned by the
/// factory and never disposed here.
/// </remarks>
internal sealed class DestinationPrimaryHandler(DefaultDestinationHttpClientFactory factory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.TryGetValue(DestinationRequestHandler.DestinationKey, out var destination);
        var handler = factory.GetPrimaryHandler(destination);
        // Not disposed: the invoker does not own the cached handler.
#pragma warning disable CA2000 // Disposal of the invoker would dispose nothing (disposeHandler: false).
        var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
#pragma warning restore CA2000
        return invoker.SendAsync(request, cancellationToken);
    }
}
