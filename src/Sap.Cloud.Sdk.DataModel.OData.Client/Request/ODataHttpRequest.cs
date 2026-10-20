using System.Text;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

internal static class HttpRequestFactory
{
    /// <summary>
    /// Resolves a relative request URI against the HTTP client's base address by appending it to the base path, the way
    /// the Java SDK appends the request path to the destination URI.
    /// </summary>
    internal static Uri ResolveUri(HttpClient httpClient, Uri relative)
    {
        if (relative.IsAbsoluteUri)
        {
            return relative;
        }
        if (httpClient.BaseAddress is null)
        {
            return relative;
        }
        var basePart = httpClient.BaseAddress.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return new Uri(basePart + relative.OriginalString, UriKind.Absolute);
    }
}

internal sealed class ODataHttpRequest
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataHttpRequest>();

    private static readonly HashSet<string> ContentHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type",
        "Content-Length",
        "Content-Encoding",
        "Content-Language",
        "Content-Location",
        "Content-MD5",
        "Content-Range",
        "Content-Disposition",
        "Expires",
        "Last-Modified",
        "Allow",
    };

    private readonly ODataRequestGeneric _odataRequest;
    private readonly HttpClient _httpClient;
    private readonly byte[]? _requestBody;
    private readonly string? _defaultContentType;

    private ODataHttpRequest(ODataRequestGeneric odataRequest, HttpClient httpClient, byte[]? requestBody, string? defaultContentType)
    {
        _odataRequest = odataRequest;
        _httpClient = httpClient;
        _requestBody = requestBody;
        _defaultContentType = defaultContentType;
    }

    internal static ODataHttpRequest WithoutBody(ODataRequestGeneric requestGeneric, HttpClient httpClient) =>
        new(requestGeneric, httpClient, null, null);

    internal static ODataHttpRequest ForBodyJson(ODataRequestGeneric requestGeneric, HttpClient httpClient, string json) =>
        new(requestGeneric, httpClient, Encoding.UTF8.GetBytes(json), "application/json");

    internal static ODataHttpRequest ForBodyText(ODataRequestGeneric requestGeneric, HttpClient httpClient, string text) =>
        new(requestGeneric, httpClient, Encoding.UTF8.GetBytes(text), "text/plain; charset=UTF-8");

    internal Task<ODataHttpResponse> RequestGetAsync(CancellationToken cancellationToken) =>
        RequestResourceAsync(HttpMethod.Get, cancellationToken);

    internal Task<ODataHttpResponse> RequestPostAsync(CancellationToken cancellationToken) =>
        RequestResourceAsync(HttpMethod.Post, cancellationToken);

    internal Task<ODataHttpResponse> RequestPatchAsync(CancellationToken cancellationToken) =>
        RequestResourceAsync(HttpMethod.Patch, cancellationToken);

    internal Task<ODataHttpResponse> RequestPutAsync(CancellationToken cancellationToken) =>
        RequestResourceAsync(HttpMethod.Put, cancellationToken);

    internal Task<ODataHttpResponse> RequestDeleteAsync(CancellationToken cancellationToken) =>
        RequestResourceAsync(HttpMethod.Delete, cancellationToken);

    private async Task<ODataHttpResponse> RequestResourceAsync(HttpMethod method, CancellationToken cancellationToken)
    {
        using var httpRequest = CreateHttpRequest(method);
        foreach (var listener in _odataRequest.GetListeners())
        {
            listener.ListenOnRequest(httpRequest);
        }
        try
        {
            var message = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return new ODataHttpResponse(message);
        }
        catch (HttpRequestException e)
        {
            Log.Debug(e, "Connection could not be established.");
            throw new ODataConnectionException(_odataRequest, httpRequest, "Connection could not be established.", e);
        }
        catch (IOException e)
        {
            Log.Debug(e, "Connection was aborted.");
            throw new ODataConnectionException(_odataRequest, httpRequest, "Connection was aborted.", e);
        }
#pragma warning disable CA1031 // As in Java, any other failure to send the request is reported as connection failure.
        catch (Exception e) when (e is not OperationCanceledException and not ODataException)
        {
            Log.Debug(e, "Connection failed.");
            throw new ODataConnectionException(_odataRequest, httpRequest, "Connection failed.", e);
        }
#pragma warning restore CA1031
    }

    private HttpRequestMessage CreateHttpRequest(HttpMethod method)
    {
        var uri = HttpRequestFactory.ResolveUri(_httpClient, _odataRequest.GetRelativeUri());
        var httpRequest = new HttpRequestMessage(method, uri);
        ByteArrayContent? content = null;
        if (_requestBody is not null)
        {
            content = new ByteArrayContent(_requestBody);
            httpRequest.Content = content;
        }
        else if (method != HttpMethod.Get && method != HttpMethod.Delete && method != HttpMethod.Head)
        {
            Log.Warn("The HTTP request {Method} {Uri} was expecting an entity, but none was provided.", method, uri);
        }

        var hasContentType = false;
        foreach (var (name, values) in _odataRequest.GetHeaders())
        {
            foreach (var value in values)
            {
                if (content is not null && ContentHeaders.Contains(name))
                {
                    hasContentType |= string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase);
                    content.Headers.TryAddWithoutValidation(name, value);
                }
                else if (!ContentHeaders.Contains(name))
                {
                    httpRequest.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }
        if (content is not null && !hasContentType && _defaultContentType is not null)
        {
            content.Headers.TryAddWithoutValidation("Content-Type", _defaultContentType);
        }
        return httpRequest;
    }
}
