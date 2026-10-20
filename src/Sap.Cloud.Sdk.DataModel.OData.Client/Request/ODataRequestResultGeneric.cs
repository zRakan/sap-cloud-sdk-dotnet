using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.Result;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>
/// The result of a (non-batch) OData request. The payload is parsed on demand according to the protocol (V2: <c>d</c> and
/// <c>d.results</c>, V4: root and <c>value</c>).
/// </summary>
public class ODataRequestResultGeneric : IODataRequestResult, IODataRequestResultDeserializable, IODataRequestResultPagination
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataRequestResultGeneric>();

    private readonly ODataResponseDeserializer _deserializer;
    private readonly IODataProtocol _protocol;
    private readonly HttpClient? _httpClient;
    private NumberDeserializationStrategy _numberStrategy = NumberDeserializationStrategy.AsDouble;

    /// <summary>Initializes a new instance of the <see cref="ODataRequestResultGeneric"/> class.</summary>
    /// <param name="oDataRequest">The request.</param>
    /// <param name="httpResponse">The HTTP response.</param>
    public ODataRequestResultGeneric(ODataRequestGeneric oDataRequest, ODataHttpResponse httpResponse)
        : this(oDataRequest, httpResponse, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestResultGeneric"/> class.</summary>
    /// <param name="oDataRequest">The request.</param>
    /// <param name="httpResponse">The HTTP response.</param>
    /// <param name="httpClient">The HTTP client used to query further pages.</param>
    public ODataRequestResultGeneric(ODataRequestGeneric oDataRequest, ODataHttpResponse httpResponse, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(oDataRequest);
        ArgumentNullException.ThrowIfNull(httpResponse);
        ODataRequest = oDataRequest;
        HttpResponse = httpResponse;
        _httpClient = httpClient;
        _protocol = oDataRequest.Protocol;
        _deserializer = new ODataResponseDeserializer(_protocol);
    }

    /// <inheritdoc />
    public ODataRequestGeneric ODataRequest { get; }

    /// <inheritdoc />
    public ODataHttpResponse HttpResponse { get; }

    /// <inheritdoc />
    public int StatusCode => HttpResponse.StatusCode;

    /// <inheritdoc />
    public IEnumerable<string> HeaderNames => GetAllHeaderValues().Keys;

    /// <inheritdoc />
    public IEnumerable<string> GetHeaderValues(string headerName) =>
        GetAllHeaderValues().TryGetValue(headerName, out var values) ? values : [];

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetAllHeaderValues() => HttpResponse.GetAllHeaderValues();

    /// <summary>Whether the HTTP response has a body.</summary>
    public bool HasPayload => HttpResponse.HasEntity;

    /// <summary>The first non-empty <c>ETag</c> response header, if any.</summary>
    public string? VersionIdentifierFromHeader =>
        GetHeaderValues("ETag").FirstOrDefault(v => !string.IsNullOrEmpty(v));

    /// <summary>Chooses how untyped numbers in the payload are deserialized.</summary>
    /// <param name="numberStrategy">The strategy.</param>
    /// <returns>This result.</returns>
    public ODataRequestResultGeneric WithNumberDeserializationStrategy(NumberDeserializationStrategy numberStrategy)
    {
        _numberStrategy = numberStrategy;
        return this;
    }

    /// <summary>Reads the body into memory unless this result streams it.</summary>
    internal virtual Task BufferIfRequiredAsync(CancellationToken cancellationToken) => HttpResponse.BufferAsync(cancellationToken);

    private JsonResultElementFactory GetResultElementFactory() => new(ODataJsonOptions.Get(_numberStrategy));

    private ODataDeserializationException CannotRead(string what)
    {
        Log.Debug("{Protocol} response cannot be read as {What}.", _protocol, what);
        return new ODataDeserializationException(ODataRequest, HttpResponse, "Unable to read " + _protocol + " response.", null);
    }

    private async Task<IResultPrimitive> LoadPrimitiveFromResponseAsync(
        Func<JsonElement, JsonElement> jsonElementExtractor,
        CancellationToken cancellationToken)
    {
        var elementFactory = GetResultElementFactory();
        var result = await HttpEntityReader.ReadAsync(
            this,
            element =>
            {
                var single = _deserializer.GetElementToResultPrimitiveSingle(element);
                return single is null ? null : elementFactory.Create(jsonElementExtractor(single.Value))?.GetAsPrimitive();
            },
            cancellationToken).ConfigureAwait(false);
        return result ?? throw CannotRead("a primitive value");
    }

    private async Task<IResultCollection> LoadPrimitiveCollectionFromResponseAsync(CancellationToken cancellationToken)
    {
        var elementFactory = GetResultElementFactory();
        var result = await HttpEntityReader.ReadAsync(
            this,
            element => _deserializer.GetElementToResultPrimitiveSet(element) is { } set
                ? elementFactory.Create(set)?.GetAsCollection()
                : null,
            cancellationToken).ConfigureAwait(false);
        return result ?? throw CannotRead("set of primitive values");
    }

    private async Task<IResultObject> LoadEntryFromResponseAsync(
        Func<JsonElement, JsonElement> jsonElementExtractor,
        CancellationToken cancellationToken)
    {
        var elementFactory = GetResultElementFactory();
        var result = await HttpEntityReader.ReadAsync(
            this,
            element =>
            {
                var single = _deserializer.GetElementToResultSingle(element);
                return single is null ? null : elementFactory.Create(jsonElementExtractor(single.Value))?.GetAsObject();
            },
            cancellationToken).ConfigureAwait(false);
        return result ?? throw CannotRead("a single entity");
    }

    private async Task<IResultCollection> LoadEntryCollectionFromResponseAsync(CancellationToken cancellationToken)
    {
        var elementFactory = GetResultElementFactory();
        var result = await HttpEntityReader.ReadAsync(
            this,
            element => _deserializer.GetElementToResultSet(element) is { } set
                ? elementFactory.Create(set)?.GetAsCollection()
                : null,
            cancellationToken).ConfigureAwait(false);
        return result ?? throw CannotRead("set of entities");
    }

    /// <summary>Gets the single result object (V2: <c>d</c>, V4: the payload root).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result object.</returns>
    public async Task<IResultElement> GetResultElementAsync(CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        return await LoadEntryFromResponseAsync(e => e, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the entities of the result set.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entities.</returns>
    public async Task<IResultCollection> GetResultElementsAsync(CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        return await LoadEntryCollectionFromResponseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<IResultElement> StreamElementsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var elements = await GetResultElementsAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;
        foreach (var element in elements)
        {
            if (!element.IsResultObject)
            {
                break;
            }
            count++;
            yield return element;
        }
        Log.Debug("Iterated {Count} elements.", count);
    }

    /// <inheritdoc />
    public async Task<T> AsSingleAsync<T>(
        Func<JsonElement, JsonElement>? resultExtractor = null,
        CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        resultExtractor ??= e => e;
        if (IsPrimitiveOrString(typeof(T)))
        {
            if (string.Equals(HttpResponse.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
            {
                return GetPrimitiveObjectFromPlainText<T>();
            }
            return await GetPrimitiveObjectFromJsonAsync<T>(resultExtractor, cancellationToken).ConfigureAwait(false);
        }
        return await GetComplexObjectFromJsonAsync<T>(resultExtractor, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> GetComplexObjectFromJsonAsync<T>(Func<JsonElement, JsonElement> resultExtractor, CancellationToken cancellationToken)
    {
        var resultObject = await LoadEntryFromResponseAsync(resultExtractor, cancellationToken).ConfigureAwait(false);
        try
        {
            return resultObject.AsType<T>();
        }
#pragma warning disable CA1031 // As in Java, any failure is reported as ODataDeserializationException.
        catch (Exception e)
        {
            Log.Debug("Failed to deserialize {Type} from JSON response.", typeof(T));
            throw new ODataDeserializationException(ODataRequest, HttpResponse, "Failed to deserialize a complex object.", e);
        }
#pragma warning restore CA1031
    }

    private async Task<T> GetPrimitiveObjectFromJsonAsync<T>(Func<JsonElement, JsonElement> resultExtractor, CancellationToken cancellationToken)
    {
        var resultPrimitive = await LoadPrimitiveFromResponseAsync(resultExtractor, cancellationToken).ConfigureAwait(false);
        try
        {
            return GetPrimitiveAsType<T>(resultPrimitive);
        }
#pragma warning disable CA1031 // As in Java, any failure is reported as ODataDeserializationException.
        catch (Exception e)
        {
            Log.Debug("Failed to deserialize {Type} from JSON response.", typeof(T));
            throw new ODataDeserializationException(ODataRequest, HttpResponse, "Failed to deserialize a primitive object.", e);
        }
#pragma warning restore CA1031
    }

    private T GetPrimitiveObjectFromPlainText<T>()
    {
        string objectText;
        try
        {
            objectText = HttpResponse.ReadBufferedText()
                ?? throw new InvalidOperationException("The response body has not been read.");
        }
#pragma warning disable CA1031 // As in Java, any failure is reported as ODataDeserializationException.
        catch (Exception e)
        {
            throw new ODataDeserializationException(ODataRequest, HttpResponse, "Failed to parse HTTP response.", e);
        }

        try
        {
            var trimmed = objectText.Trim();
            object? value;
            if (typeof(T) == typeof(string))
            {
                value = trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
                    ? JsonSerializer.Deserialize<string>(trimmed)
                    : trimmed;
            }
            else
            {
                value = JsonSerializer.Deserialize<T>(trimmed);
            }
            return (T?)value ?? throw new ODataDeserializationException(ODataRequest, HttpResponse, "The response is null.", null);
        }
        catch (Exception e) when (e is not ODataException)
        {
            Log.Debug("Failed to deserialize {Type} from text/plain response: {Text}", typeof(T), objectText);
            throw new ODataDeserializationException(ODataRequest, HttpResponse, "Failed to deserialize a primitive object.", e);
        }
#pragma warning restore CA1031
    }

    private static bool IsPrimitiveOrString(Type type) =>
        type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(BigInteger);

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> AsListAsync<T>(CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        var result = IsPrimitiveOrString(typeof(T))
            ? await LoadPrimitiveCollectionFromResponseAsync(cancellationToken).ConfigureAwait(false)
            : await LoadEntryCollectionFromResponseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return result.AsList<T>();
        }
#pragma warning disable CA1031 // As in Java, any failure is reported as ODataDeserializationException.
        catch (Exception e)
        {
            Log.Debug("Failed to parse {Protocol} result to a list of {Type}", _protocol, typeof(T));
            throw new ODataDeserializationException(ODataRequest, HttpResponse, "Failed to parse " + _protocol + " result to a list.", e);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task<long> GetInlineCountAsync(CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        foreach (var path in _protocol.PathToInlineCount.Paths)
        {
            var resultElement = await GetResultElementAsync(path, cancellationToken).ConfigureAwait(false);
            if (resultElement is not null)
            {
                return resultElement.GetAsPrimitive().AsLong();
            }
        }
        throw new ODataDeserializationException(
            ODataRequest,
            HttpResponse,
            "Inline count not found in " + _protocol + " response payload.",
            null);
    }

    /// <inheritdoc />
    public async Task<string?> GetNextLinkAsync(CancellationToken cancellationToken = default)
    {
        Log.Debug("Checking for a next link on current page.");
        AssertNonEmptyPayload();
        foreach (var path in _protocol.PathToNextLink.Paths)
        {
            var resultElement = await GetResultElementAsync(path, cancellationToken).ConfigureAwait(false);
            if (resultElement is not null)
            {
                var nextLink = resultElement.AsString();
                Log.Debug("Found reference to next page: {NextLink}", nextLink);
                return RemoveDuplicateQueryParameters(nextLink);
            }
        }
        Log.Debug("Result does not reference any further pages.");
        return null;
    }

    /// <summary>Gets the link to the delta page (OData V4).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The link, or <see langword="null"/> if there is none.</returns>
    public async Task<string?> GetDeltaLinkAsync(CancellationToken cancellationToken = default)
    {
        Log.Debug("Checking for a delta link on current page.");
        AssertNonEmptyPayload();
        foreach (var path in _protocol.PathToDeltaLink.Paths)
        {
            var resultElement = await GetResultElementAsync(path, cancellationToken).ConfigureAwait(false);
            if (resultElement is not null)
            {
                var link = resultElement.AsString();
                Log.Debug("Found reference to delta page: {DeltaLink}", link);
                return link;
            }
        }
        Log.Debug("Result does not contain a delta reference.");
        return null;
    }

    private async Task<IResultElement?> GetResultElementAsync(JsonPath path, CancellationToken cancellationToken)
    {
        IResultElement? resultElement = await LoadEntryFromResponseAsync(e => e, cancellationToken).ConfigureAwait(false);
        var nodes = path.Nodes;
        for (var i = 0; i < nodes.Count && resultElement is { IsResultObject: true }; i++)
        {
            resultElement = resultElement.GetAsObject().GetElement(nodes[i]);
        }
        return resultElement;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, object?>> AsMapAsync(CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        return await AsSingleAsync<Dictionary<string, object?>>(null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> AsListOfMapsAsync(CancellationToken cancellationToken = default)
    {
        AssertNonEmptyPayload();
        var list = await AsListAsync<Dictionary<string, object?>>(cancellationToken).ConfigureAwait(false);
        return list.Cast<IReadOnlyDictionary<string, object?>>().ToList();
    }

    private static T GetPrimitiveAsType<T>(IResultPrimitive primitive)
    {
        var type = typeof(T);
        object value;
        try
        {
            if (type == typeof(bool))
            {
                value = primitive.AsBoolean();
            }
            else if (type == typeof(byte))
            {
                value = primitive.AsByte();
            }
            else if (type == typeof(short))
            {
                value = primitive.AsShort();
            }
            else if (type == typeof(int))
            {
                value = primitive.AsInteger();
            }
            else if (type == typeof(long))
            {
                value = primitive.AsLong();
            }
            else if (type == typeof(BigInteger))
            {
                value = primitive.AsBigInteger();
            }
            else if (type == typeof(float))
            {
                value = primitive.AsFloat();
            }
            else if (type == typeof(double))
            {
                value = primitive.AsDouble();
            }
            else if (type == typeof(decimal))
            {
                value = primitive.AsBigDecimal();
            }
            else if (type == typeof(char))
            {
                value = primitive.AsCharacter();
            }
            else if (type == typeof(string))
            {
                value = primitive.AsString();
            }
            else
            {
                throw new ArgumentException($"Failed to convert primitive '{primitive.AsString()}' to unsupported type: {type.FullName}.");
            }
        }
        catch (NotSupportedException e)
        {
            throw new ArgumentException($"Failed to convert primitive '{primitive.AsString()}' to type: {type.FullName}.", e);
        }
        return (T)value;
    }

    /// <inheritdoc />
    public async Task<Result<ODataRequestResultGeneric>> TryGetNextPageAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (ODataRequest is not ODataRequestRead request)
        {
            return Result<ODataRequestResultGeneric>.Failure(
                new InvalidOperationException("Pagination is only applicable for read requests."));
        }
        var client = httpClient ?? _httpClient;
        if (client is null)
        {
            return Result<ODataRequestResultGeneric>.Failure(new ODataRequestException(
                request,
                "Unable to access response of next page: HTTP client was not provided when creating this response object.",
                null));
        }

        string nextQuery;
        try
        {
            var nextLink = await GetNextLinkAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Current page of result-set does not reference a next page.");
            nextQuery = GetRawQuery(nextLink);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result<ODataRequestResultGeneric>.Failure(new ODataRequestException(
                request,
                "Unable to extract query parameters for querying next page of result-set.",
                e));
        }

        Log.Debug("Querying {Protocol} service for next page {Query}", _protocol, nextQuery);
        var nextReadRequest = new ODataRequestRead(request.ServicePath, request.ResourcePath, nextQuery, request.Protocol);
        foreach (var (name, values) in request.GetHeaders())
        {
            nextReadRequest.SetHeader(name, values);
        }
        try
        {
            return Result<ODataRequestResultGeneric>.Success(
                await nextReadRequest.ExecuteAsync(client, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result<ODataRequestResultGeneric>.Failure(e);
        }
    }

    /// <summary>Iterates over all pages of the result set, requesting further pages lazily.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entities of each page.</returns>
    /// <exception cref="ODataRequestException">A further page cannot be requested.</exception>
    public async IAsyncEnumerable<IReadOnlyList<T>> IteratePagesAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ODataRequestResultGeneric page = this;
        while (true)
        {
            var elements = await page.GetResultElementsAsync(cancellationToken).ConfigureAwait(false);
            yield return elements.Select(item => item.GetAsObject().AsType<T>()).ToList();
            if (await page.GetNextLinkAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                yield break;
            }
            var next = await page.TryGetNextPageAsync(null, cancellationToken).ConfigureAwait(false);
            if (next.IsFailure)
            {
                throw new ODataRequestException(page.ODataRequest, "Failed to handle next page.", next.Exception);
            }
            page = next.GetValueOrThrow();
        }
    }

    private static string GetRawQuery(string link)
    {
        var fragment = link.IndexOf('#', StringComparison.Ordinal);
        var withoutFragment = fragment >= 0 ? link[..fragment] : link;
        var index = withoutFragment.IndexOf('?', StringComparison.Ordinal);
        return index >= 0 ? withoutFragment[(index + 1)..] : string.Empty;
    }

    private void AssertNonEmptyPayload()
    {
        if (!HasPayload)
        {
            throw new ODataDeserializationException(
                ODataRequest,
                HttpResponse,
                _protocol + " response did not contain any payload.",
                null);
        }
    }

    private string RemoveDuplicateQueryParameters(string nextLink)
    {
        var merger = ODataRequest.UriQueryMerger;
        if (merger is null)
        {
            return nextLink;
        }
        var query = merger.MergeRequestUri(new Uri(string.Empty, UriKind.Relative)).OriginalString;
        var queryStart = query.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return nextLink;
        }
        query = query[(queryStart + 1)..];
        var segments = nextLink.Split('?', 2);
        if (segments.Length < 2)
        {
            return nextLink;
        }
        foreach (var argument in query.Split('&'))
        {
            if (segments[1].Contains(argument, StringComparison.Ordinal))
            {
                segments[1] = segments[1].Replace(argument, string.Empty, StringComparison.Ordinal);
            }
        }
        if (nextLink.Length + 1 == segments[0].Length + segments[1].Length)
        {
            return nextLink;
        }
        segments[1] = System.Text.RegularExpressions.Regex
            .Replace(segments[1], "&&+", "&")
            .Replace("?&", "?", StringComparison.Ordinal);
        if (segments[1].EndsWith('&'))
        {
            segments[1] = segments[1][..^1];
        }
        var updatedLink = segments[0] + "?" + segments[1];
        Log.Debug("Updated reference to next page: {UpdatedLink}", updatedLink);
        return updatedLink;
    }
}

/// <summary>An unbuffered result: the response body is streamed and the result must be disposed.</summary>
/// <remarks>Port of <c>ODataRequestResultResource</c>.</remarks>
public sealed class ODataRequestResultResource : ODataRequestResultGeneric, IDisposable
{
    internal ODataRequestResultResource(ODataRequestGeneric oDataRequest, ODataHttpResponse httpResponse, HttpClient? httpClient)
        : base(oDataRequest, httpResponse, httpClient)
    {
    }

    /// <inheritdoc />
    internal override Task BufferIfRequiredAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Releases the HTTP response and its connection.</summary>
    public void Dispose() => HttpResponse.Dispose();
}

/// <summary>Executes a request and returns an unbuffered result.</summary>
public interface IODataRequestResourceExecutable
{
    /// <summary>Executes the request without buffering the response.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result; dispose it when done.</returns>
    Task<ODataRequestResultResource> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default);
}

internal static class ODataRequestResultFactories
{
    internal static ODataRequestResultGeneric WithBuffer(ODataRequestGeneric request, ODataHttpResponse response, HttpClient? httpClient) =>
        new(request, response, httpClient);

    internal static ODataRequestResultGeneric WithoutBuffer(ODataRequestGeneric request, ODataHttpResponse response, HttpClient? httpClient) =>
        new ODataRequestResultResource(request, response, httpClient);
}
