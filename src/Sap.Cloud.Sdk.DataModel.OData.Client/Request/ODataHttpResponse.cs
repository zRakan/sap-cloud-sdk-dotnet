using System.Text;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>
/// The HTTP response of an OData request: status, headers and (once buffered) the body.
/// </summary>
/// <remarks>
/// Replaces Apache's <c>HttpResponse</c>/<c>StatusLine</c>/<c>Header</c>. The response of an unbuffered request
/// (<see cref="ODataRequestResultResource"/>) streams its body from the connection and must be disposed.
/// </remarks>
public class ODataHttpResponse : IDisposable
{
    private readonly List<KeyValuePair<string, string>> _headers;
    private HttpResponseMessage? _message;
    private byte[]? _buffer;

    /// <summary>Initializes a new instance of the <see cref="ODataHttpResponse"/> class from an HTTP response (body not yet read).</summary>
    /// <param name="message">The response message; ownership is taken.</param>
    internal ODataHttpResponse(HttpResponseMessage message)
    {
        _message = message;
        StatusCode = (int)message.StatusCode;
        ReasonPhrase = message.ReasonPhrase;
        _headers = [];
        foreach (var header in message.Headers)
        {
            foreach (var value in header.Value)
            {
                _headers.Add(new(header.Key, value));
            }
        }
        foreach (var header in message.Content.Headers)
        {
            foreach (var value in header.Value)
            {
                _headers.Add(new(header.Key, value));
            }
        }
        // Like Apache HttpClient: no entity for informational, 204, 205 and 304 responses.
        HasEntity = StatusCode >= 200 && StatusCode is not (204 or 205 or 304);
    }

    /// <summary>Initializes a new instance of the <see cref="ODataHttpResponse"/> class from already available parts.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="reasonPhrase">The reason phrase.</param>
    /// <param name="headers">The headers.</param>
    /// <param name="content">The body, or <see langword="null"/> if the response has no entity.</param>
    public ODataHttpResponse(
        int statusCode,
        string? reasonPhrase,
        IEnumerable<KeyValuePair<string, string>> headers,
        byte[]? content)
    {
        ArgumentNullException.ThrowIfNull(headers);
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        _headers = headers.ToList();
        _buffer = content;
        HasEntity = content is not null;
    }

    /// <summary>The HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>The HTTP reason phrase.</summary>
    public string? ReasonPhrase { get; }

    /// <summary>All response headers (in order of occurrence).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers => _headers;

    /// <summary>Whether the response has an entity (a body).</summary>
    public bool HasEntity { get; }

    /// <summary>The value of the <c>Content-Type</c> header, if any.</summary>
    public string? ContentType => GetFirstHeader("Content-Type");

    /// <summary>The MIME type of the content (without parameters), if any.</summary>
    public string? MimeType
    {
        get
        {
            var contentType = ContentType;
            if (contentType is null)
            {
                return null;
            }
            var end = contentType.IndexOf(';', StringComparison.Ordinal);
            return (end < 0 ? contentType : contentType[..end]).Trim();
        }
    }

    /// <summary>Whether the body has been read into memory.</summary>
    public bool IsBuffered => _buffer is not null;

    /// <summary>Gets the first value of a header (case-insensitive).</summary>
    /// <param name="name">The header name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? GetFirstHeader(string name) =>
        _headers.FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Gets all values of a header (case-insensitive).</summary>
    /// <param name="name">The header name.</param>
    /// <returns>The values.</returns>
    public IReadOnlyList<string> GetHeaderValues(string name) =>
        _headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();

    /// <summary>Gets all headers grouped by name (case-insensitive, ordered by name).</summary>
    /// <returns>The headers.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetAllHeaderValues()
    {
        var result = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in _headers)
        {
            if (!result.TryGetValue(name, out var values))
            {
                result[name] = values = [];
            }
            values.Add(value);
        }
        return result.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Reads the body into memory, if it is not buffered yet.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the body is buffered.</returns>
    public async Task BufferAsync(CancellationToken cancellationToken = default)
    {
        if (_buffer is not null || _message is null)
        {
            return;
        }
        if (HasEntity)
        {
            _buffer = await _message.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        _message.Dispose(); // releases the connection; everything needed is copied
        _message = null;
    }

    /// <summary>Gets the buffered body decoded as text (UTF-8 unless the content type names another charset).</summary>
    /// <returns>The text, or <see langword="null"/> if there is no buffered entity.</returns>
    public string? ReadBufferedText()
    {
        if (_buffer is null)
        {
            return null;
        }
        return GetEncoding().GetString(_buffer);
    }

    /// <summary>Opens the body as stream, for example to read binary media. A buffered body can be opened repeatedly.</summary>
    /// <returns>The stream, or <see langword="null"/> if there is no entity.</returns>
    public Stream? OpenContent()
    {
        if (_buffer is not null)
        {
            return new MemoryStream(_buffer, writable: false);
        }
        return HasEntity ? _message?.Content.ReadAsStream() : null;
    }

    internal Encoding GetEncoding() => EncodingOf(ContentType);

    internal static Encoding EncodingOf(string? contentType)
    {
        var index = contentType?.IndexOf("charset=", StringComparison.OrdinalIgnoreCase) ?? -1;
        if (contentType is not null && index >= 0)
        {
            var name = contentType[(index + 8)..].Split(';')[0].Trim().Trim('"');
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                // unknown charset: fall back to the default
            }
        }
        return Encoding.UTF8;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the underlying HTTP response.</summary>
    /// <param name="disposing">Whether called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _message?.Dispose();
            _message = null;
        }
    }
}
