using System.Net;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string Body);

/// <summary>Records requests and answers them with a configurable function.</summary>
internal sealed class FakeHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, headers, body);
        Requests.Add(recorded);
        return respond(recorded);
    }

    public static HttpResponseMessage Token(string accessToken, long expiresIn = 3600) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($$"""{"access_token":"{{accessToken}}","expires_in":{{expiresIn}},"token_type":"bearer"}"""),
    };
}

internal sealed class ManualTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class FormBody
{
    public static Dictionary<string, string> Parse(string body) => body.Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2))
        .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
}
