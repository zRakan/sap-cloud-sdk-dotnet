using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.Result;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>The result of an executed OData request: the request and the HTTP response.</summary>
public interface IODataRequestResult
{
    /// <summary>The request that produced this result.</summary>
    ODataRequestGeneric ODataRequest { get; }

    /// <summary>The HTTP response.</summary>
    ODataHttpResponse HttpResponse { get; }

    /// <summary>The HTTP status code.</summary>
    int StatusCode => HttpResponse.StatusCode;

    /// <summary>The names of the response headers (case-insensitively distinct).</summary>
    IEnumerable<string> HeaderNames => GetAllHeaderValues().Keys;

    /// <summary>Gets the values of a response header.</summary>
    /// <param name="headerName">The header name (case-insensitive).</param>
    /// <returns>The values; empty if the header is absent.</returns>
    IEnumerable<string> GetHeaderValues(string headerName) =>
        GetAllHeaderValues().TryGetValue(headerName, out var values) ? values : [];

    /// <summary>Gets all response headers, keyed case-insensitively.</summary>
    /// <returns>The headers.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetAllHeaderValues() => HttpResponse.GetAllHeaderValues();
}

/// <summary>Deserialization of the payload of an OData response.</summary>
public interface IODataRequestResultDeserializable
{
    /// <summary>Deserializes the response as a single object (entity or primitive).</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="resultExtractor">Optionally selects the JSON element to deserialize; by default the standard result location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The object.</returns>
    Task<T> AsSingleAsync<T>(Func<System.Text.Json.JsonElement, System.Text.Json.JsonElement>? resultExtractor = null, CancellationToken cancellationToken = default);

    /// <summary>Deserializes the response as list of objects (entities or primitives).</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list.</returns>
    Task<IReadOnlyList<T>> AsListAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>Streams the entities of the result set.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The elements.</returns>
    IAsyncEnumerable<IResultElement> StreamElementsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the inline count.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count.</returns>
    Task<long> GetInlineCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Deserializes the response as a map of property names to values.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The map.</returns>
    Task<IReadOnlyDictionary<string, object?>> AsMapAsync(CancellationToken cancellationToken = default);

    /// <summary>Deserializes the response as list of maps.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of maps.</returns>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> AsListOfMapsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Access to further pages of a paged result set.</summary>
public interface IODataRequestResultPagination
{
    /// <summary>The request that produced this page.</summary>
    ODataRequestGeneric ODataRequest { get; }

    /// <summary>Gets the link to the next page.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The link, or <see langword="null"/> if this is the last page.</returns>
    Task<string?> GetNextLinkAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests the next page.</summary>
    /// <param name="httpClient">The HTTP client; defaults to the client that produced this page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next page or the failure.</returns>
    Task<Result<ODataRequestResultGeneric>> TryGetNextPageAsync(HttpClient? httpClient = null, CancellationToken cancellationToken = default);
}
