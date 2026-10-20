using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// A <see cref="DelegatingHandler"/> that fetches a CSRF token for state-changing requests and adds it to them.
/// </summary>
/// <remarks>
/// <para>
/// Port of Java's <c>CsrfTokenInterceptor</c> (httpclient5 module). For <c>POST</c>, <c>PUT</c>, <c>PATCH</c> and
/// <c>DELETE</c> requests that carry no <c>X-CSRF-Token</c> header, a <c>HEAD</c> request with
/// <c>X-CSRF-Token: Fetch</c> is sent to the service root derived from the request URI (see
/// <see cref="DeriveServiceRootUri"/>); the token from the response (non-printable characters removed) is added to
/// the request. If no token is returned or the <c>HEAD</c> request fails, a warning is logged and the request is sent
/// without token. Like in Java there is no retry on <c>403</c>.
/// </para>
/// <para>
/// Differences to Java: the interceptor there uses the plain HTTP client, so its <c>HEAD</c> request lacks the
/// destination's headers. This handler is placed behind <see cref="DestinationRequestHandler"/> and copies the headers
/// of the original request (for example <c>Authorization</c>) to the <c>HEAD</c> request, which SAP systems require.
/// Cookies set by the <c>HEAD</c> response are sent with the actual request by the primary handler's cookie container.
/// </para>
/// </remarks>
public sealed partial class CsrfTokenHandler : DelegatingHandler
{
    /// <summary>The name of the CSRF token header.</summary>
    public const string CsrfTokenHeaderName = "x-csrf-token";

    private const string FetchValue = "fetch";
    private static readonly ILogger Log = SdkLogging.CreateLogger<CsrfTokenHandler>();
    private static readonly HashSet<HttpMethod> MutatingMethods = [HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!MutatingMethods.Contains(request.Method))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        if (request.Headers.Contains(CsrfTokenHeaderName))
        {
            Log.LogDebug("CSRF token already present in request, skipping retrieval.");
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        if (request.RequestUri is { IsAbsoluteUri: true } requestUri)
        {
            var token = await TryFetchTokenAsync(request, requestUri, cancellationToken).ConfigureAwait(false);
            if (token is not null)
            {
                Log.LogDebug("Successfully retrieved CSRF token, adding to request.");
                request.Headers.TryAddWithoutValidation(CsrfTokenHeaderName, token);
            }
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> TryFetchTokenAsync(HttpRequestMessage request, Uri requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, DeriveServiceRootUri(requestUri));
            foreach (var header in request.Headers)
            {
                head.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            head.Headers.Remove(CsrfTokenHeaderName);
            head.Headers.TryAddWithoutValidation(CsrfTokenHeaderName, FetchValue);
            foreach (var option in request.Options)
            {
                head.Options.TryAdd(option.Key, option.Value);
            }

            using var response = await base.SendAsync(head, cancellationToken).ConfigureAwait(false);
            if (response.Headers.TryGetValues(CsrfTokenHeaderName, out var values))
            {
                var value = values.FirstOrDefault();
                if (value is not null)
                {
                    return NonPrintableChars().Replace(value, string.Empty);
                }
            }
            Log.LogWarning(
                "Target system did not respond with a {Header} header. The subsequent request may fail if a CSRF token is required.",
                CsrfTokenHeaderName);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log.LogWarning(
                e,
                "CSRF token retrieval failed: the HEAD request was not successful. The subsequent request may fail if a CSRF token is required.");
            return null;
        }
    }

    /// <summary>
    /// Derives the service root URI from the full request URI. The service root is the path prefix up to and including
    /// the slash that precedes the first OData resource segment: the slash before the first <c>(</c> if there is a key
    /// predicate, otherwise the last path segment is stripped. Query and fragment are dropped.
    /// </summary>
    /// <param name="requestUri">The absolute request URI.</param>
    /// <returns>The service root URI.</returns>
    /// <example><c>/service/Entity('key')/Nav(42)</c> and <c>/service/$batch</c> both yield <c>/service/</c>.</example>
    public static Uri DeriveServiceRootUri(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        var path = requestUri.AbsolutePath;
        string servicePath;
        var firstParen = path.IndexOf('(', StringComparison.Ordinal);
        if (firstParen > 0)
        {
            var slashBeforeEntity = path.LastIndexOf('/', firstParen);
            servicePath = slashBeforeEntity >= 0 ? path[..(slashBeforeEntity + 1)] : path;
        }
        else
        {
            var lastSlash = path.LastIndexOf('/');
            servicePath = lastSlash >= 0 && lastSlash < path.Length - 1 ? path[..(lastSlash + 1)] : path;
        }
        return new Uri(requestUri.GetLeftPart(UriPartial.Authority) + servicePath, UriKind.Absolute);
    }

    [GeneratedRegex("[^ -~]")]
    private static partial Regex NonPrintableChars();
}
