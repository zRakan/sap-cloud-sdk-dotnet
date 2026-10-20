using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>An HTTP response that is one part of a multipart batch response.</summary>
internal sealed partial class MultipartHttpResponse : ODataHttpResponse
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<MultipartHttpResponse>();

    private MultipartHttpResponse(
        int statusCode,
        string? reasonPhrase,
        IReadOnlyList<KeyValuePair<string, string>> headers,
        byte[] content,
        int? contentId)
        : base(statusCode, reasonPhrase, headers, content)
    {
        ContentId = contentId;
    }

    /// <summary>The <c>Content-ID</c> of the batch item this response belongs to.</summary>
    internal int? ContentId { get; }

    [GeneratedRegex(@"^HTTP/(\d).(\d) (\d+) (.*)", RegexOptions.CultureInvariant)]
    private static partial Regex StatusLinePattern();

    [GeneratedRegex(@"^Content-ID:\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ContentIdPattern();

    [GeneratedRegex(@"\r\n|[\n\u000B\u000C\r\u0085\u2028\u2029]", RegexOptions.CultureInvariant)]
    private static partial Regex NewLinePattern();

    internal static MultipartHttpResponse OfHttpContent(MultipartParser.Entry entry)
    {
        var contentIdMatch = ContentIdPattern().Match(entry.Meta);
        int? contentId = contentIdMatch.Success ? int.Parse(contentIdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;

        var lines = SplitLines(entry.Payload);
        var (statusCode, reason) = GetStatusLine(lines[0]);
        var payload = new StringBuilder();
        var header = new StringBuilder();
        var isHeaders = true;
        for (var i = 1; i < lines.Count; i++)
        {
            if (isHeaders)
            {
                if (lines[i].Length == 0)
                {
                    isHeaders = false;
                }
                else
                {
                    header.Append(lines[i]).Append('\n');
                }
            }
            else
            {
                payload.Append(lines[i]).Append('\n');
            }
        }
        var headers = GetHeadersFromString(header.ToString());
        var contentType = headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
        var encoding = ODataHttpResponse.EncodingOf(contentType);
        return new MultipartHttpResponse(statusCode, reason, headers, encoding.GetBytes(payload.ToString()), contentId);
    }

    /// <summary>Splits like Java's <c>Pattern.split("\\R")</c>: trailing empty strings are removed.</summary>
    internal static List<string> SplitLines(string text)
    {
        var parts = NewLinePattern().Split(text).ToList();
        if (parts.Count > 1)
        {
            while (parts.Count > 1 && parts[^1].Length == 0)
            {
                parts.RemoveAt(parts.Count - 1);
            }
            if (parts.Count == 1 && parts[0].Length == 0)
            {
                parts.Clear();
                parts.Add(string.Empty);
            }
        }
        return parts;
    }

    internal static List<KeyValuePair<string, string>> GetHeadersFromString(string headerString)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var headerLine in SplitLines(headerString.Trim()))
        {
            if (headerLine.Length == 0)
            {
                continue;
            }
            var split = headerLine.Split(':', 2);
            result.Add(new(split[0].Trim(), split.Length > 1 ? split[1].Trim() : string.Empty));
        }
        return result;
    }

    internal static MediaTypeHeaderValue? GetContentType(IEnumerable<KeyValuePair<string, string>> headers)
    {
        foreach (var header in headers.Where(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            if (MediaTypeHeaderValue.TryParse(header.Value, out var contentType))
            {
                return contentType;
            }
        }
        return null;
    }

    private static (int StatusCode, string? Reason) GetStatusLine(string firstLine)
    {
        var m = StatusLinePattern().Match(firstLine);
        if (m.Success)
        {
            return (int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups[4].Value);
        }
        Log.Error("Failed to construct status line for HTTP protocol response: {FirstLine}", firstLine);
        return (0, "Unknown");
    }
}

/// <summary>Reads lines of a multipart body up to delimiters.</summary>
internal sealed class MultipartParserReader(TextReader reader, string delimiter)
{
    private readonly string _delimiterEnd = delimiter + "--";

    internal bool IsFinished { get; private set; }

    internal string UntilDelimiter() => ReadWhile(line => line != delimiter && line != _delimiterEnd);

    internal string UntilPayload() => ReadWhile(s => s is not null && s.Length > 0);

    private string ReadWhile(Func<string, bool> recordFlag)
    {
        var sb = new StringBuilder();
        while (!IsFinished && reader.ReadLine() is { } line)
        {
            if (!recordFlag(line))
            {
                if (_delimiterEnd == line)
                {
                    IsFinished = true;
                }
                return sb.ToString();
            }
            sb.Append(line).Append('\n');
        }
        IsFinished = true;
        return sb.ToString();
    }
}

/// <summary>Parses a multipart/mixed batch response body, including nested change sets.</summary>
internal sealed class MultipartParser
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<MultipartParser>();
    private const string MultipartMixedMimeType = "multipart/mixed";

    private readonly TextReader _reader;
    private readonly string _delimiter;

    private MultipartParser(TextReader reader, string delimiter)
    {
        _reader = reader;
        _delimiter = delimiter;
    }

    internal static MultipartParser OfHttpResponse(ODataHttpResponse httpResponse)
    {
        var content = httpResponse.ReadBufferedText()
            ?? throw new InvalidOperationException("HTTP response does not contain a content.");
        var contentType = MultipartHttpResponse.GetContentType(httpResponse.Headers);
        var delimiter = GetDelimiterFromMediaType(contentType)
            ?? throw new InvalidOperationException("No delimiter found in HTTP header.");
        if (!string.Equals(contentType?.MediaType, MultipartMixedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("Unexpected value in HTTP header \"Content-Type\" of OData batch response: {ContentType}", contentType);
        }
        return OfText(content, delimiter);
    }

    internal static MultipartParser OfText(string content, string delimiter) => new(new StringReader(content), delimiter);

    internal List<List<T>> ToList<T>(Func<Entry, T> transformation)
    {
        var result = new List<List<T>>();
        var batchRead = new MultipartParserReader(_reader, _delimiter);
        batchRead.UntilDelimiter();
        while (!batchRead.IsFinished)
        {
            var segmentHead = batchRead.UntilPayload();
            var maybeChangesetBoundary = GetDelimiterFromString(segmentHead);
            if (maybeChangesetBoundary is not null)
            {
                // multiple responses in changeset
                result.Add(GetChangeset(maybeChangesetBoundary, batchRead).Select(transformation).ToList());
            }
            else
            {
                // single response
                var content = batchRead.UntilDelimiter();
                result.Add([transformation(new Entry(segmentHead, content))]);
            }
        }
        _reader.Dispose();
        return result;
    }

    private List<Entry> GetChangeset(string changesetDelimiter, MultipartParserReader batchRead)
    {
        var entries = new List<Entry>();
        var changesetRead = new MultipartParserReader(_reader, changesetDelimiter);
        changesetRead.UntilDelimiter();
        while (!changesetRead.IsFinished)
        {
            var subSegmentHead = changesetRead.UntilPayload();
            var content = changesetRead.UntilDelimiter();
            if (changesetRead.IsFinished)
            {
                batchRead.UntilDelimiter(); // position reader to next batch delimiter
            }
            entries.Add(new Entry(subSegmentHead, content));
        }
        return entries;
    }

    private static string? GetDelimiterFromString(string segmentHead)
    {
        var segmentHeaders = MultipartHttpResponse.GetHeadersFromString(segmentHead);
        return GetDelimiterFromMediaType(MultipartHttpResponse.GetContentType(segmentHeaders));
    }

    private static string? GetDelimiterFromMediaType(MediaTypeHeaderValue? contentType)
    {
        var boundary = contentType?.Parameters.FirstOrDefault(p => string.Equals(p.Name, "boundary", StringComparison.OrdinalIgnoreCase))?.Value;
        return boundary is null ? null : "--" + boundary.Trim('"');
    }

    /// <summary>One part of a multipart body.</summary>
    /// <param name="Meta">The part's header block (until the first empty line).</param>
    /// <param name="Payload">The part's content.</param>
    internal sealed record Entry(string Meta, string Payload);
}

/// <summary>The result of an executed batch request.</summary>
public interface IODataRequestResultMultipart
{
    /// <summary>Gets the result of one request of the batch.</summary>
    /// <param name="request">The original request object that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of that request.</returns>
    Task<ODataRequestResultGeneric> GetResultAsync(ODataRequestGeneric request, CancellationToken cancellationToken = default);
}

/// <summary>The result of an executed <see cref="ODataRequestBatch"/>.</summary>
public sealed class ODataRequestResultMultipartGeneric : IODataRequestResultMultipart, IODataRequestResult, IDisposable
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataRequestResultMultipartGeneric>();

    private readonly ODataRequestBatch _batchRequest;
    private readonly Lazy<(IReadOnlyList<IReadOnlyList<ODataHttpResponse>>? Responses, Exception? Failure)> _batchResponses;

    internal ODataRequestResultMultipartGeneric(ODataRequestBatch oDataRequest, ODataHttpResponse httpResponse)
    {
        _batchRequest = oDataRequest;
        HttpResponse = httpResponse;
        _batchResponses = new Lazy<(IReadOnlyList<IReadOnlyList<ODataHttpResponse>>?, Exception?)>(LoadBatchResponses);
    }

    /// <summary>The batch request.</summary>
    public ODataRequestBatch ODataRequest => _batchRequest;

    /// <inheritdoc />
    ODataRequestGeneric IODataRequestResult.ODataRequest => _batchRequest;

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

    /// <inheritdoc />
    public async Task<ODataRequestResultGeneric> GetResultAsync(ODataRequestGeneric request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var responsePosition = ODataRequestBatch.GetBatchItemPosition(_batchRequest, request)
            ?? throw new ArgumentException(
                "Incorrect API usage. Please pass the original OData request reference that was handled as batch request item.",
                nameof(request));
        Log.Debug("Looking for request {Request} in batch response at position {Position}", request, responsePosition);

        var batchResponseItems = GetBatchedResponses();
        if (responsePosition.Index >= batchResponseItems.Count)
        {
            throw new ODataResponseException(
                _batchRequest,
                HttpResponse,
                $"Unable to extract batch response item at position {responsePosition.Index + 1}. The response contains only {batchResponseItems.Count} items.",
                null);
        }
        var subResponses = batchResponseItems[responsePosition.Index];
        var isSingleResponse = responsePosition.NestedIndex is null || responsePosition.NestedIndex >= subResponses.Count;
        var response = subResponses[isSingleResponse ? 0 : responsePosition.NestedIndex!.Value];

        var result = new ODataRequestResultGeneric(_batchRequest, response);
        await ODataHealthyResponseValidator.RequireHealthyResponseAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Gets the parsed responses of the batch: one list per batch item (a change set yields several responses).</summary>
    /// <returns>The responses.</returns>
    /// <exception cref="ODataResponseException">The batch response cannot be parsed.</exception>
    public IReadOnlyList<IReadOnlyList<ODataHttpResponse>> GetBatchedResponses()
    {
        var (responses, failure) = _batchResponses.Value;
        return responses ?? throw new ODataResponseException(
            _batchRequest,
            HttpResponse,
            "Failed to read " + _batchRequest.Protocol + " batch response.",
            failure);
    }

#pragma warning disable CA1031 // As in Java, a parsing failure is deferred and reported as ODataResponseException.
    private (IReadOnlyList<IReadOnlyList<ODataHttpResponse>>? Responses, Exception? Failure) LoadBatchResponses()
    {
        try
        {
            var parser = MultipartParser.OfHttpResponse(HttpResponse);
            var list = parser.ToList(entry => (ODataHttpResponse)MultipartHttpResponse.OfHttpContent(entry));
            return (list.Select(l => (IReadOnlyList<ODataHttpResponse>)l).ToList(), null);
        }
        catch (Exception e)
        {
            return (null, e);
        }
    }
#pragma warning restore CA1031

    /// <summary>Releases the HTTP response.</summary>
    public void Dispose() => HttpResponse.Dispose();
}
