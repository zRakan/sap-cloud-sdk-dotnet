using System.Text;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;

/// <summary>An HTTP request as seen by the fake handler.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyList<KeyValuePair<string, string>> Headers, string? Body)
{
    /// <summary>The path and query exactly as constructed (not normalized by <see cref="System.Uri"/>).</summary>
    public string PathAndQuery
    {
        get
        {
            var original = Uri.OriginalString;
            var start = original.IndexOf('/', original.IndexOf("//", StringComparison.Ordinal) + 2);
            return start < 0 ? "/" : original[start..];
        }
    }

    public IReadOnlyList<string> GetHeader(string name) =>
        Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();

    public string? GetFirstHeader(string name) => GetHeader(name) is [var first, ..] ? first : null;

    public bool HasHeader(string name) => GetHeader(name).Count > 0;
}

/// <summary>Records requests and answers them with canned responses; no network involved.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, HttpResponseMessage> _responder;

    public FakeHttpHandler(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public FakeHttpHandler(HttpResponseMessage response)
        : this(_ => response)
    {
    }

    public List<RecordedRequest> Requests { get; } = [];

    public HttpClient CreateClient(string baseAddress = "http://localhost:8080") =>
        new(this, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v))).ToList();
        if (request.Content is not null)
        {
            headers.AddRange(request.Content.Headers.SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v))));
        }
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, headers, body);
        Requests.Add(recorded);
        return _responder(recorded);
    }
}

internal static class Responses
{
    public static HttpResponseMessage Json(string body, int status = 200) =>
        new((System.Net.HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Text(string body, string contentType = "text/plain; charset=utf-8", int status = 200)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
        response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        return response;
    }

    public static HttpResponseMessage Empty(int status = 200) =>
        new((System.Net.HttpStatusCode)status) { Content = new ByteArrayContent([]) };

    public static HttpResponseMessage NoContent() => new(System.Net.HttpStatusCode.NoContent);

    public static HttpResponseMessage WithHeader(this HttpResponseMessage response, string name, string value)
    {
        response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }
}
