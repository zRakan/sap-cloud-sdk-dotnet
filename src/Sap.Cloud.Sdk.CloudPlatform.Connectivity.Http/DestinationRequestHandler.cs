using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// A <see cref="DelegatingHandler"/> that targets requests at an HTTP destination: it merges the destination URI into
/// the request URI and adds the destination's headers.
/// </summary>
/// <remarks>
/// <para>
/// Port of Java's <c>ApacheHttpClient5Wrapper.wrapRequest</c>. Per request the handler
/// </para>
/// <list type="number">
/// <item>merges the destination URI with the request URI using <see cref="UriPathMerger"/> and adds the
/// <c>URL.queries.*</c> properties of the destination (see <see cref="DestinationUriQueryMerger"/>);</item>
/// <item>awaits <see cref="IHttpDestinationProperties.GetHeadersAsync"/> for the <em>final</em> URI and adds the headers.</item>
/// </list>
/// <para>
/// Header rules: Java appends the destination headers with <c>addHeader</c>, never replacing a header of the request.
/// The same holds here for multi-valued headers (a further value is added). Headers that HTTP allows only once
/// (for example <c>Authorization</c>, <c>Proxy-Authorization</c>) cannot be sent twice by <see cref="HttpClient"/>; the
/// value set by the caller wins, as it would be the first of the two lines in Java. Content headers (for example
/// <c>Content-Type</c>) are added to the request content, or skipped if there is none or if the content sets them already. Header values are never logged.
/// </para>
/// <para>
/// <see cref="HttpClient"/> resolves a relative request URI against its <see cref="HttpClient.BaseAddress"/> before the
/// handler sees it, replacing the base path for requests like <c>/foo</c>. A request whose authority equals that of the
/// client's base address is therefore treated as relative to the destination (path and query are appended to the path
/// of the destination), which is what Java's wrapper does for relative request URIs.
/// </para>
/// </remarks>
public sealed class DestinationRequestHandler : DelegatingHandler
{
    internal static readonly HttpRequestOptionsKey<IHttpDestinationProperties> DestinationKey =
        new("Sap.Cloud.Sdk.CloudPlatform.Connectivity.Destination");

    private static readonly HashSet<string> SingleValuedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Host", "From", "Referer", "Max-Forwards", "If-Modified-Since",
        "If-Unmodified-Since", "If-Range", "Range", "Date",
    };

    private static readonly HashSet<string> ContentHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Allow", "Content-Disposition", "Content-Encoding", "Content-Language", "Content-Length", "Content-Location",
        "Content-MD5", "Content-Range", "Content-Type", "Expires", "Last-Modified",
    };

    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationRequestHandler>();

    private readonly Func<CancellationToken, ValueTask<IHttpDestinationProperties>> _destinationResolver;
    private readonly Uri? _clientBaseAddress;

    /// <summary>Initializes a new instance of the <see cref="DestinationRequestHandler"/> class for a fixed destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <exception cref="DestinationAccessException">The destination targets an on-premise system but has no proxy configuration.</exception>
    public DestinationRequestHandler(IHttpDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        AssertValid(destination);
        _destinationResolver = _ => ValueTask.FromResult(destination);
        _clientBaseAddress = destination.Uri.IsAbsoluteUri ? destination.Uri : null;
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationRequestHandler"/> class that resolves the destination per request.</summary>
    /// <param name="destinationResolver">Resolves the destination; called for every request (destinations may be tenant-specific).</param>
    /// <param name="clientBaseAddress">
    /// The <see cref="HttpClient.BaseAddress"/> of the client. Requests with the same authority are treated as relative to the destination.
    /// </param>
    public DestinationRequestHandler(
        Func<CancellationToken, ValueTask<IHttpDestinationProperties>> destinationResolver,
        Uri? clientBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(destinationResolver);
        _destinationResolver = destinationResolver;
        _clientBaseAddress = clientBaseAddress;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var destination = await _destinationResolver(cancellationToken).ConfigureAwait(false);
        AssertValid(destination);

        var requestUri = new DestinationUriQueryMerger(destination).Merge(ToDestinationRelative(request.RequestUri));
        request.RequestUri = requestUri;
        request.Options.Set(DestinationKey, destination);

        var headers = await destination.GetHeadersAsync(requestUri, cancellationToken).ConfigureAwait(false);
        foreach (var header in headers)
        {
            AddHeader(request, header);
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(
                    "Added HTTP header with key {Name} originating from a {Type} with target URI {Uri} for new outbound HTTP request.",
                    header.Name,
                    destination.GetType().Name,
                    destination.Uri);
            }
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static void AssertValid(IHttpDestinationProperties destination)
    {
        if (destination.ProxyType == ProxyType.OnPremise && destination.ProxyConfiguration is null)
        {
            throw new DestinationAccessException(
                "Unable to create an HttpClient from the provided destination. "
                + "The destination is supposed to target an on-premise system but lacks the correct proxy configuration. "
                + "Please check the application logs for further details.");
        }
    }

    private Uri? ToDestinationRelative(Uri? requestUri)
    {
        if (requestUri is null || !requestUri.IsAbsoluteUri || _clientBaseAddress is null || !_clientBaseAddress.IsAbsoluteUri)
        {
            return requestUri;
        }
        var sameAuthority =
            string.Equals(requestUri.Scheme, _clientBaseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(requestUri.Host, _clientBaseAddress.Host, StringComparison.OrdinalIgnoreCase)
            && requestUri.Port == _clientBaseAddress.Port;
        return sameAuthority ? new Uri(requestUri.PathAndQuery, UriKind.Relative) : requestUri;
    }

    private static void AddHeader(HttpRequestMessage request, Header header)
    {
        var value = header.Value ?? string.Empty;
        if (ContentHeaders.Contains(header.Name))
        {
            // Content headers are mostly single-valued (Content-Type, Content-Length): the caller's value wins.
            if (request.Content is null
                || request.Content.Headers.Contains(header.Name)
                || !request.Content.Headers.TryAddWithoutValidation(header.Name, value))
            {
                if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Skipped content header {Name}: the request has no content, already sets it or does not accept it.", header.Name);
            }
            }
            return;
        }
        if (SingleValuedHeaders.Contains(header.Name) && request.Headers.Contains(header.Name))
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Skipped header {Name}: the request already contains it and it can only be sent once.", header.Name);
            }
            return;
        }
        if (!request.Headers.TryAddWithoutValidation(header.Name, value))
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Skipped header {Name}: it is not a valid request header.", header.Name);
            }
        }
    }
}
