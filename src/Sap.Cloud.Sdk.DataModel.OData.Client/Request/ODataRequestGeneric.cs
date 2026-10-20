using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>Executes an OData request.</summary>
public interface IODataRequestExecutable
{
    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">
    /// The HTTP client. Its <see cref="HttpClient.BaseAddress"/> (the destination URI) is the prefix of the request
    /// path, which consists of the service path, the resource path and the query.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ODataConnectionException">The request could not be sent.</exception>
    /// <exception cref="ODataResponseException">The response indicates an error.</exception>
    Task<IODataRequestResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default);
}

/// <summary>Observes the HTTP requests of an OData request.</summary>
/// <remarks>Port of <c>ODataRequestListener</c>; the Apache <c>HttpRequestBase</c> is a <see cref="HttpRequestMessage"/>.</remarks>
public interface IODataRequestListener
{
    /// <summary>Called right before the HTTP request is sent.</summary>
    /// <param name="request">The HTTP request.</param>
    void ListenOnRequest(HttpRequestMessage request);

    /// <summary>Called when sending the request failed.</summary>
    /// <param name="exception">The error.</param>
    void ListenOnRequestError(Exception exception);

    /// <summary>Called when parsing the response failed.</summary>
    /// <param name="exception">The error.</param>
    void ListenOnParsingError(Exception exception);
}

/// <summary>Base class of all OData requests.</summary>
public abstract class ODataRequestGeneric : IODataRequestExecutable
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataRequestGeneric>();
    private const string DefaultAccept = "application/json";

    private readonly SortedDictionary<string, List<string>> _headers = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> _queryParameters = new(StringComparer.Ordinal);
    private readonly List<IODataRequestListener> _listeners = [];

    internal ODataRequestGeneric(string servicePath, ODataResourcePath resourcePath, IODataProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(servicePath);
        ArgumentNullException.ThrowIfNull(resourcePath);
        ArgumentNullException.ThrowIfNull(protocol);
        Protocol = protocol;
        ServicePath = servicePath;
        ResourcePath = resourcePath;
        _headers.TryAdd("Accept", [DefaultAccept]);
    }

    /// <summary>The service path.</summary>
    public string ServicePath { get; }

    /// <summary>The protocol.</summary>
    public IODataProtocol Protocol { get; }

    /// <summary>The resource path.</summary>
    protected internal ODataResourcePath ResourcePath { get; }

    /// <summary>The registered listeners.</summary>
    protected IReadOnlyList<IODataRequestListener> Listeners => _listeners;

    /// <summary>The query parameters added with <see cref="AddQueryParameter"/>.</summary>
    protected IReadOnlyDictionary<string, string> QueryParameters => _queryParameters;

    internal ICsrfTokenRetriever? CsrfTokenRetriever { get; set; }

    /// <summary>
    /// Merges query parameters that the HTTP client adds to every request (set by the connectivity layer);
    /// used to remove duplicates from next links.
    /// </summary>
    internal IUriQueryMerger? UriQueryMerger { get; set; }

    internal Func<ODataRequestGeneric, ODataHttpResponse, HttpClient?, ODataRequestResultGeneric> RequestResultFactory { get; set; } =
        ODataRequestResultFactories.WithBuffer;

    /// <summary>Gets the relative URI of the request (service path, resource path and query).</summary>
    /// <param name="strategy">The encoding strategy.</param>
    /// <returns>The relative URI.</returns>
    public abstract Uri GetRelativeUri(UriEncodingStrategy strategy);

    /// <summary>Gets the relative URI of the request using the regular encoding.</summary>
    /// <returns>The relative URI.</returns>
    public Uri GetRelativeUri() => GetRelativeUri(UriEncodingStrategy.Regular);

    /// <summary>Gets the query string of the request, including additional query parameters.</summary>
    /// <returns>The query string without the leading <c>?</c>.</returns>
    public virtual string GetRequestQuery() => string.Join("&", _queryParameters.Select(p => p.Key + "=" + p.Value));

    /// <summary>Registers a listener.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(IODataRequestListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);
    }

    /// <summary>Sets a header, replacing existing values.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The value.</param>
    public void SetHeader(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _headers[key] = [value];
    }

    /// <summary>Sets a header with several values, replacing existing values.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="values">The values.</param>
    public void SetHeader(string key, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(values);
        _headers[key] = [.. values];
    }

    /// <summary>Adds a header value.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The value.</param>
    public void AddHeader(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_headers.TryGetValue(key, out var values))
        {
            _headers[key] = values = [];
        }
        values.Add(value);
    }

    /// <summary>Adds a header unless a header of that name is already present.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The value.</param>
    public void AddHeaderIfAbsent(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _headers.TryAdd(key, [value]);
    }

    /// <summary>Adds (or replaces) a query parameter that is appended to the request query.</summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The (already encoded) value.</param>
    public void AddQueryParameter(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _queryParameters[key] = value;
    }

    /// <summary>
    /// Enables or disables the retrieval of a CSRF token before the request is sent. Requests other than reads fetch a
    /// token by default; read requests do not.
    /// </summary>
    /// <param name="enabled">Whether a token is fetched.</param>
    public void UseCsrfTokenRetrieval(bool enabled) =>
        CsrfTokenRetriever = enabled ? new DefaultCsrfTokenRetriever() : DisabledCsrfTokenRetriever.Instance;

    /// <summary>Gets a snapshot of the request headers, ordered by name.</summary>
    /// <returns>The headers.</returns>
    public virtual IReadOnlyDictionary<string, IReadOnlyList<string>> GetHeaders()
    {
        var copy = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, values) in _headers)
        {
            copy[key] = values.ToList();
        }
        return copy;
    }

    /// <inheritdoc />
    async Task<IODataRequestResult> IODataRequestExecutable.ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteCoreAsync(httpClient, cancellationToken).ConfigureAwait(false);

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    private protected abstract Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken);

    internal async Task<ODataRequestResultGeneric> TryExecuteAsync(
        ODataHttpRequest request,
        Func<ODataHttpRequest, CancellationToken, Task<ODataHttpResponse>> httpOperation,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var response = await httpOperation(request, cancellationToken).ConfigureAwait(false);
        var result = RequestResultFactory(this, response, httpClient);
        await result.BufferIfRequiredAsync(cancellationToken).ConfigureAwait(false);
        await ODataHealthyResponseValidator.RequireHealthyResponseAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    internal async Task<ODataRequestResultGeneric> TryExecuteWithCsrfTokenAsync(
        HttpClient httpClient,
        ODataHttpRequest request,
        Func<ODataHttpRequest, CancellationToken, Task<ODataHttpResponse>> httpOperation,
        CancellationToken cancellationToken)
    {
        var retriever = CsrfTokenRetriever ?? new DefaultCsrfTokenRetriever();
        if (!retriever.IsEnabled || HasHeader(DefaultCsrfTokenRetriever.XCsrfTokenHeaderKey))
        {
            Log.Debug("CSRF token already present, skipping retrieval.");
            return await TryExecuteAsync(request, httpOperation, httpClient, cancellationToken).ConfigureAwait(false);
        }

        CsrfTokenRetrievalException? csrfFailure = null;
        try
        {
            var token = await retriever.RetrieveCsrfTokenAsync(httpClient, ServicePath, GetHeaders(), cancellationToken)
                .ConfigureAwait(false);
            AddHeader(DefaultCsrfTokenRetriever.XCsrfTokenHeaderKey, token.Token);
        }
        catch (CsrfTokenRetrievalException e)
        {
            csrfFailure = e;
        }

        try
        {
            return await TryExecuteAsync(request, httpOperation, httpClient, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (csrfFailure is not null && e is not OperationCanceledException)
        {
            // Java: addSuppressed. Keep the failed token retrieval available for diagnostics.
            e.Data["CsrfTokenRetrievalFailure"] = csrfFailure;
            throw;
        }
    }

    internal bool HasHeader(string name) => _headers.Keys.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

    internal void AddVersionIdentifierToHeaderIfPresent(string? versionIdentifier)
    {
        if (versionIdentifier is not null)
        {
            AddHeaderIfAbsent("If-Match", versionIdentifier);
        }
        else
        {
            Log.Debug(
                "Version identifier for {Request} is either not defined on the entity or is explicitly ignored.",
                GetType().Name);
        }
    }

    internal static string Combine(string first, string second)
    {
        if (first.Length > 0 && second.Length > 0)
        {
            return first + "&" + second;
        }
        return first + second;
    }

    internal void RemoveHeader(string name) => _headers.Remove(name);

    internal IReadOnlyList<IODataRequestListener> GetListeners() => _listeners;
}
