#if SAP_SDK_HTTP_TESTS
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;

namespace Sap.Cloud.Sdk.TestSupport;

/// <summary>
/// An <see cref="IDestinationHttpClientFactory"/> that runs the real <see cref="DestinationRequestHandler"/> on top of a
/// recording fake transport. Install with <see cref="Install"/>; dispose to restore the default factory.
/// </summary>
internal sealed class RecordingDestinationHttpClientFactory : IDestinationHttpClientFactory, IDisposable
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    private RecordingDestinationHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<(HttpMethod Method, Uri Uri, string? Authorization)> Requests { get; } = [];

    public static RecordingDestinationHttpClientFactory Install(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var factory = new RecordingDestinationHttpClientFactory(respond);
        HttpClientAccessor.HttpClientFactory = factory;
        return factory;
    }

    public HttpClient CreateHttpClient(IHttpDestinationProperties destination) =>
        new(new DestinationRequestHandler(destination) { InnerHandler = new Recorder(this) }) { BaseAddress = destination.Uri };

    public HttpClient CreateHttpClient() => new(new Recorder(this));

    public void Dispose() => HttpClientAccessor.HttpClientFactory = null!;

    private sealed class Recorder(RecordingDestinationHttpClientFactory owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (owner.Requests)
            {
                owner.Requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
            }
            return Task.FromResult(owner._respond(request));
        }
    }
}
#endif
