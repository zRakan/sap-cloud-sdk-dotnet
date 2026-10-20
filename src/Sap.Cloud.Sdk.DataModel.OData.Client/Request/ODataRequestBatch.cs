using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>A <c>$batch</c> request: reads and change sets that are sent in one multipart HTTP request.</summary>
public class ODataRequestBatch : ODataRequestGeneric
{
    private const string BatchNewline = "\r\n";
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataRequestBatch>();

    private readonly List<BatchItem> _requests = [];
    private readonly Func<Guid> _uuidProvider;
    private readonly Guid _batchUuid;
    private int _contentId = 1;

    /// <summary>Initializes a new instance of the <see cref="ODataRequestBatch"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestBatch(string servicePath, IODataProtocol protocol)
        : this(servicePath, protocol, Guid.NewGuid)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestBatch"/> class with a custom source of boundary identifiers.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="protocol">The protocol.</param>
    /// <param name="uuidProvider">Supplies the identifiers used in the multipart boundaries.</param>
    public ODataRequestBatch(string servicePath, IODataProtocol protocol, Func<Guid> uuidProvider)
        : base(servicePath, ODataResourcePath.Of("$batch"), protocol)
    {
        ArgumentNullException.ThrowIfNull(uuidProvider);
        _uuidProvider = uuidProvider;
        _batchUuid = uuidProvider();
        RemoveHeader("Accept"); // batch request does not require Accept header
        RequestResultFactory = ODataRequestResultFactories.WithoutBuffer;
    }

    internal IReadOnlyList<BatchItem> Requests => _requests;

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <summary>Adds a read request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>This batch.</returns>
    public ODataRequestBatch AddRead(ODataRequestRead request)
    {
        _requests.Add(new BatchItemSingle(this, request, "GET", null));
        return this;
    }

    /// <summary>Adds a read-by-key request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>This batch.</returns>
    public ODataRequestBatch AddReadByKey(ODataRequestReadByKey request)
    {
        _requests.Add(new BatchItemSingle(this, request, "GET", null));
        return this;
    }

    /// <summary>Adds a function request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>This batch.</returns>
    public ODataRequestBatch AddFunction(ODataRequestFunction request)
    {
        _requests.Add(new BatchItemSingle(this, request, "GET", null));
        return this;
    }

    /// <summary>Starts a change set.</summary>
    /// <returns>The change set; call <see cref="Changeset.EndChangeset"/> to add it to this batch.</returns>
    public Changeset BeginChangeset() => new(this);

    /// <summary>Executes the batch request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The multipart result.</returns>
    public async Task<ODataRequestResultMultipartGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var retriever = CsrfTokenRetriever ?? new DefaultCsrfTokenRetriever();
        if (!retriever.IsEnabled || HasHeader(DefaultCsrfTokenRetriever.XCsrfTokenHeaderKey))
        {
            Log.Debug("CSRF token already present, skipping retrieval.");
            return await TryExecuteBatchAsync(httpClient, cancellationToken).ConfigureAwait(false);
        }

        CsrfTokenRetrievalException? csrfFailure = null;
        try
        {
            var token = await retriever.RetrieveCsrfTokenAsync(httpClient, ServicePath, GetHeaders(), cancellationToken).ConfigureAwait(false);
            AddHeader(DefaultCsrfTokenRetriever.XCsrfTokenHeaderKey, token.Token);
        }
        catch (CsrfTokenRetrievalException e)
        {
            csrfFailure = e;
        }

        try
        {
            return await TryExecuteBatchAsync(httpClient, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (csrfFailure is not null && e is not OperationCanceledException)
        {
            e.Data["CsrfTokenRetrievalFailure"] = csrfFailure;
            throw;
        }
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);

    private async Task<ODataRequestResultMultipartGeneric> TryExecuteBatchAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var requestBody = GetBatchRequestBody();
        var request = ODataHttpRequest.ForBodyText(this, httpClient, requestBody);
        var response = await request.RequestPostAsync(cancellationToken).ConfigureAwait(false);
        await response.BufferAsync(cancellationToken).ConfigureAwait(false);
        var result = new ODataRequestResultMultipartGeneric(this, response);
        await ODataHealthyResponseValidator.RequireHealthyResponseAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, IReadOnlyList<string>> GetHeaders()
    {
        var headers = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (name, values) in base.GetHeaders())
        {
            headers[name] = values;
        }
        foreach (var (key, value) in GetBatchHeaders())
        {
            headers[key] = [value];
        }
        return headers;
    }

    private Dictionary<string, string> GetBatchHeaders() => new()
    {
        ["Content-Type"] = "multipart/mixed;boundary=batch_" + _batchUuid.ToString("D"),
        ["OData-Version"] = Protocol.ProtocolVersion,
    };

    internal string GetBatchRequestBody()
    {
        var batchDelimiter = "--batch_" + _batchUuid.ToString("D");
        var batchDelimiterEnd = batchDelimiter + "--";
        var resultLines = new List<string>();
        foreach (var item in _requests)
        {
            resultLines.Add(batchDelimiter);
            resultLines.AddRange(item.GetLines());
        }
        resultLines.Add(batchDelimiterEnd);
        resultLines.Add(string.Empty);
        return string.Join(BatchNewline, resultLines);
    }

    internal string GetEncodedServicePath(UriEncodingStrategy encodingStrategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, string.Empty, null, encodingStrategy).OriginalString;

    internal static (int Index, int? NestedIndex)? GetBatchItemPosition(ODataRequestBatch batchRequest, ODataRequestGeneric singleRequest)
    {
        var batchItems = batchRequest.Requests;
        for (var i = 0; i < batchItems.Count; i++)
        {
            switch (batchItems[i])
            {
                case BatchItemChangeset changeset:
                    for (var p = 0; p < changeset.Requests.Count; p++)
                    {
                        if (ReferenceEquals(singleRequest, changeset.Requests[p].Request))
                        {
                            return (i, p);
                        }
                    }
                    break;
                case BatchItemSingle single when ReferenceEquals(singleRequest, single.Request):
                    return (i, null);
                default:
                    break;
            }
        }
        return null;
    }

    /// <summary>A change set: requests that are executed atomically.</summary>
    public sealed class Changeset
    {
        private readonly ODataRequestBatch _originalRequest;
        private readonly List<BatchItemSingle> _queries = [];

        internal Changeset(ODataRequestBatch originalRequest)
        {
            _originalRequest = originalRequest;
        }

        /// <summary>Adds a create request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>This change set.</returns>
        public Changeset AddCreate(ODataRequestCreate request)
        {
            ArgumentNullException.ThrowIfNull(request);
            _queries.Add(new BatchItemSingle(_originalRequest, request, "POST", () => request.SerializedEntity));
            return this;
        }

        /// <summary>Adds an update request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>This change set.</returns>
        public Changeset AddUpdate(ODataRequestUpdate request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.AddVersionIdentifierToHeaderIfPresent(request.VersionIdentifier);
            _queries.Add(new BatchItemSingle(_originalRequest, request, request.HttpMethodName, () => request.SerializedEntity));
            return this;
        }

        /// <summary>Adds a delete request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>This change set.</returns>
        public Changeset AddDelete(ODataRequestDelete request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.AddVersionIdentifierToHeaderIfPresent(request.VersionIdentifier);
            _queries.Add(new BatchItemSingle(_originalRequest, request, "DELETE", null));
            return this;
        }

        /// <summary>Adds an action request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>This change set.</returns>
        public Changeset AddAction(ODataRequestAction request)
        {
            ArgumentNullException.ThrowIfNull(request);
            _queries.Add(new BatchItemSingle(_originalRequest, request, "POST", () => request.ActionParameters));
            return this;
        }

        /// <summary>Adds this change set to the batch.</summary>
        /// <returns>The batch.</returns>
        public ODataRequestBatch EndChangeset()
        {
            var changeSetId = _originalRequest._uuidProvider();
            _originalRequest._requests.Add(new BatchItemChangeset(changeSetId, _queries));
            return _originalRequest;
        }
    }

    internal abstract class BatchItem
    {
        internal abstract List<string> GetLines();
    }

    internal sealed class BatchItemSingle : BatchItem
    {
        private readonly string _resourcePath;
        private readonly string _httpMethod;
        private readonly Func<string>? _payload;

        internal BatchItemSingle(ODataRequestBatch requestBatch, ODataRequestGeneric requestSingle, string httpMethod, Func<string>? payload)
        {
            ArgumentNullException.ThrowIfNull(requestSingle);
            var encodedRelativeUriSingleRequest = requestSingle.GetRelativeUri(UriEncodingStrategy.Batch).OriginalString;
            var encodedServicePathBatchRequest = requestBatch.GetEncodedServicePath(UriEncodingStrategy.Batch);
            AssertSingleAndBatchRequestAreConsistent(
                requestBatch,
                requestSingle,
                encodedRelativeUriSingleRequest,
                encodedServicePathBatchRequest);
            ContentId = Interlocked.Increment(ref requestBatch._contentId) - 1;
            Request = requestSingle;
            _resourcePath = RemoveStart(encodedRelativeUriSingleRequest, encodedServicePathBatchRequest);
            _httpMethod = httpMethod;
            _payload = payload;
        }

        internal int ContentId { get; }

        internal ODataRequestGeneric Request { get; }

        private static string RemoveStart(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;

        private static void AssertSingleAndBatchRequestAreConsistent(
            ODataRequestBatch requestBatch,
            ODataRequestGeneric requestSingle,
            string encodedRelativeUriSingleRequest,
            string encodedServicePathBatchRequest)
        {
            if (!encodedRelativeUriSingleRequest.StartsWith(encodedServicePathBatchRequest, StringComparison.Ordinal))
            {
                throw new ODataRequestException(
                    requestBatch,
                    "Batch request contains requests to different service paths (batch request: "
                    + encodedServicePathBatchRequest
                    + ", single request: "
                    + encodedRelativeUriSingleRequest,
                    null);
            }
            if (!requestSingle.Protocol.IsEqualTo(requestBatch.Protocol))
            {
                throw new ODataRequestException(requestBatch, "Batch request contains requests with different protocol versions,", null);
            }
        }

        internal override List<string> GetLines()
        {
            var lines = new List<string>
            {
                "Content-Type: application/http",
                "Content-Transfer-Encoding: binary",
                "Content-ID: " + ContentId,
                string.Empty,
                $"{_httpMethod} {_resourcePath} HTTP/1.1",
            };
            foreach (var (key, values) in Request.GetHeaders())
            {
                lines.AddRange(values.Select(v => key + ": " + v));
            }
            lines.Add(string.Empty);
            if (_payload is not null)
            {
                lines.Add(_payload());
            }
            lines.Add(string.Empty);
            return lines;
        }
    }

    internal sealed class BatchItemChangeset(Guid changeSetId, IEnumerable<BatchItemSingle> requests) : BatchItem
    {
        internal IReadOnlyList<BatchItemSingle> Requests { get; } = requests.ToList();

        internal override List<string> GetLines()
        {
            var changesetDelimiter = "--changeset_" + changeSetId.ToString("D");
            var changesetDelimiterEnd = changesetDelimiter + "--";
            var lines = new List<string>
            {
                "Content-Type: multipart/mixed;boundary=changeset_" + changeSetId.ToString("D"),
                string.Empty,
            };
            foreach (var request in Requests)
            {
                lines.Add(changesetDelimiter);
                lines.AddRange(request.GetLines());
            }
            lines.Add(changesetDelimiterEnd);
            lines.Add(string.Empty);
            return lines;
        }
    }
}
