using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

// These CSRF types intentionally stay separate from the public ones in Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http:
// the OData client resolves the service path exactly like its OData requests (HttpRequestFactory.ResolveUri, keeping the
// base address path), whereas the HTTP layer's retriever relies on HttpClient's own resolution, which drops the base path
// for absolute paths when used with a plain HttpClient.

/// <summary>A CSRF token.</summary>
/// <param name="Token">The token value.</param>
internal sealed record CsrfToken(string Token)
{
    /// <summary>Hides the token value.</summary>
    /// <returns>A redacted description.</returns>
    public override string ToString() => "CsrfToken(Token=(hidden))";
}

/// <summary>Thrown when a CSRF token cannot be retrieved.</summary>
internal sealed class CsrfTokenRetrievalException : Exception
{
    public CsrfTokenRetrievalException(string? message)
        : base(message)
    {
    }

    public CsrfTokenRetrievalException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Allows a request URI to be merged with query parameters that the HTTP client adds to every request.</summary>
internal interface IUriQueryMerger
{
    /// <summary>Merges the client's query parameters into the URI.</summary>
    /// <param name="requestUri">The request URI.</param>
    /// <returns>The merged URI.</returns>
    Uri MergeRequestUri(Uri requestUri);
}

/// <summary>Retrieves CSRF tokens from a service.</summary>
internal interface ICsrfTokenRetriever
{
    /// <summary>Whether CSRF token retrieval is enabled.</summary>
    bool IsEnabled => true;

    /// <summary>Retrieves a token.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="servicePath">The service path.</param>
    /// <param name="headers">Additional headers to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token.</returns>
    Task<CsrfToken> RetrieveCsrfTokenAsync(
        HttpClient httpClient,
        string servicePath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        CancellationToken cancellationToken = default);
}

/// <summary>A retriever that is disabled: requests made with it do not fetch a token.</summary>
internal sealed class DisabledCsrfTokenRetriever : ICsrfTokenRetriever
{
    internal static DisabledCsrfTokenRetriever Instance { get; } = new();

    public bool IsEnabled => false;

    public Task<CsrfToken> RetrieveCsrfTokenAsync(
        HttpClient httpClient,
        string servicePath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The disabled CSRF token retriever cannot retrieve an CSRF token.");
}

/// <summary>
/// Retrieves a CSRF token with an HTTP <c>HEAD</c> request to the service path carrying <c>x-csrf-token: fetch</c>.
/// </summary>
internal sealed class DefaultCsrfTokenRetriever : ICsrfTokenRetriever
{
    internal const string XCsrfTokenHeaderKey = "x-csrf-token";
    private const string XCsrfTokenHeaderFetchValue = "fetch";
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultCsrfTokenRetriever>();

    public bool IsEnabled => true;

    public Task<CsrfToken> RetrieveCsrfTokenAsync(
        HttpClient httpClient,
        string servicePath,
        CancellationToken cancellationToken = default) =>
        RetrieveCsrfTokenAsync(httpClient, servicePath, new Dictionary<string, IReadOnlyList<string>>(), cancellationToken);

    public async Task<CsrfToken> RetrieveCsrfTokenAsync(
        HttpClient httpClient,
        string servicePath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(servicePath);
        ArgumentNullException.ThrowIfNull(headers);
        try
        {
            var csrfHeader = await RetrieveCsrfTokenResponseHeaderAsync(httpClient, servicePath, headers, cancellationToken)
                .ConfigureAwait(false);
            var token = ConvertHeaderToCsrfToken(csrfHeader);
            Log.Debug("Successfully retrieved CSRF token from service path {ServicePath}", servicePath);
            return token;
        }
        catch (KeyNotFoundException e)
        {
            var msgError = $"CSRF token retrieval failed: The server did not respond with the {XCsrfTokenHeaderKey} header.";
            Log.Debug(
                "{Error} The subsequent request to service path {ServicePath} is bound to fail if the OData service requires a CSRF token in request header.",
                msgError,
                servicePath);
            throw new CsrfTokenRetrievalException(msgError, e);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            const string MsgError = "CSRF token retrieval failed: The HTTP request was not successful.";
            Log.Debug(e, "{Error} The affected service path is: {ServicePath}", MsgError, servicePath);
            throw new CsrfTokenRetrievalException(MsgError, e);
        }
    }

    private static async Task<string> RetrieveCsrfTokenResponseHeaderAsync(
        HttpClient httpClient,
        string servicePath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, HttpRequestFactory.ResolveUri(httpClient, new Uri(servicePath, UriKind.Relative)));
        request.Headers.TryAddWithoutValidation(XCsrfTokenHeaderKey, XCsrfTokenHeaderFetchValue);
        foreach (var (name, values) in headers)
        {
            foreach (var value in values)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is > 0)
        {
            Log.Warn("The HTTP HEAD request for CSRF token retrieval resulted in an unexpected response payload.");
        }
        if (!response.Headers.TryGetValues(XCsrfTokenHeaderKey, out var tokens) || tokens.FirstOrDefault() is not { } token)
        {
            throw new KeyNotFoundException("No CSRF token could be found in the response header.");
        }
        return token;
    }

    private static CsrfToken ConvertHeaderToCsrfToken(string csrfHeader) =>
        new(new string(csrfHeader.Where(c => c is >= ' ' and <= '~').ToArray()));
}
