using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>Strategy for updating an entity.</summary>
public enum UpdateStrategy
{
    /// <summary>Replace the whole entity with <c>PUT</c>.</summary>
    ReplaceWithPut,

    /// <summary>Modify the given properties with <c>PATCH</c>.</summary>
    ModifyWithPatch,

    /// <summary>Modify with <c>PATCH</c>, sending a recursive delta.</summary>
    ModifyWithPatchRecursiveDelta,

    /// <summary>Modify with <c>PATCH</c>, sending the recursive full entity.</summary>
    ModifyWithPatchRecursiveFull,
}

/// <summary>Strategy for sending the entity's version identifier (ETag) in the <c>If-Match</c> header.</summary>
public enum ETagSubmissionStrategy
{
    /// <summary>Send the ETag of the entity if it has one.</summary>
    SubmitEtagFromEntity,

    /// <summary>Send no ETag.</summary>
    SubmitNoEtag,

    /// <summary>Send <c>*</c>, which matches any ETag.</summary>
    SubmitAnyMatchEtag,
}

/// <summary>Helpers for <see cref="ETagSubmissionStrategy"/>.</summary>
public static class ETagSubmissionStrategyExtensions
{
    /// <summary>Gets the <c>If-Match</c> header value for the strategy.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="maybeVersionIdentifier">The version identifier of the entity, if any.</param>
    /// <returns>The header value, or <see langword="null"/> if no header is sent.</returns>
    public static string? GetHeaderFromVersionIdentifier(this ETagSubmissionStrategy strategy, string? maybeVersionIdentifier) =>
        strategy switch
        {
            ETagSubmissionStrategy.SubmitAnyMatchEtag => "*",
            ETagSubmissionStrategy.SubmitNoEtag => null,
            _ => string.IsNullOrEmpty(maybeVersionIdentifier) ? null : maybeVersionIdentifier,
        };
}

/// <summary>A read request for an entity set (or any collection resource).</summary>
public class ODataRequestRead : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestRead"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestRead(string servicePath, string entityName, string? encodedQuery, IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(entityName), encodedQuery, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestRead"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The resource path.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestRead(string servicePath, ODataResourcePath entityPath, string? encodedQuery, IODataProtocol protocol)
        : base(servicePath, entityPath, protocol)
    {
        QueryString = encodedQuery ?? string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestRead"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The path to the parent resource.</param>
    /// <param name="query">The structured query; its entity name is appended to the path.</param>
    public ODataRequestRead(string servicePath, ODataResourcePath entityPath, StructuredQuery query)
        : this(
            servicePath,
            Required(entityPath).Copy().AddSegment(Required(query).EntityOrPropertyName),
            query.EncodedQueryString,
            query.Protocol)
    {
    }

    /// <summary>The percent-encoded query given at construction.</summary>
    public string QueryString { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <inheritdoc />
    public override string GetRequestQuery() => Combine(QueryString, base.GetRequestQuery());

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.WithoutBody(this, httpClient);
        return CsrfTokenRetriever is null
            ? await TryExecuteAsync(request, static (r, ct) => r.RequestGetAsync(ct), httpClient, cancellationToken).ConfigureAwait(false)
            : await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestGetAsync(ct), cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>Makes this request return an unbuffered result whose body is streamed.</summary>
    /// <returns>An executable that returns <see cref="ODataRequestResultResource"/>.</returns>
    public IODataRequestResourceExecutable WithoutResponseBuffering()
    {
        RequestResultFactory = ODataRequestResultFactories.WithoutBuffer;
        return new ResourceExecutable(this);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);

    internal static T Required<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    private sealed class ResourceExecutable(ODataRequestRead request) : IODataRequestResourceExecutable
    {
        public async Task<ODataRequestResultResource> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default) =>
            (ODataRequestResultResource)await request.ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>A request that counts the entities of an entity set (<c>/$count</c>).</summary>
public class ODataRequestCount : ODataRequestRead
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestCount"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestCount(string servicePath, string entityName, string? encodedQuery, IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(entityName), encodedQuery, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestCount"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="resourcePath">The resource path.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestCount(string servicePath, ODataResourcePath resourcePath, string? encodedQuery, IODataProtocol protocol)
        : base(servicePath, Required(resourcePath).Copy().AddSegment("$count"), encodedQuery, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestCount"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="resourcePath">The path to the parent resource.</param>
    /// <param name="query">The structured query; its entity name is appended to the path.</param>
    public ODataRequestCount(string servicePath, ODataResourcePath resourcePath, StructuredQuery query)
        : this(
            servicePath,
            Required(resourcePath).Copy().AddSegment(Required(query).EntityOrPropertyName),
            query.EncodedQueryString,
            query.Protocol)
    {
    }
}

/// <summary>A request that reads a single entity by its key.</summary>
public class ODataRequestReadByKey : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestReadByKey"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="entityKey">The entity key.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestReadByKey(
        string servicePath,
        string entityName,
        ODataEntityKey entityKey,
        string? encodedQuery,
        IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(entityName, entityKey), encodedQuery, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestReadByKey"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The resource path including the key.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestReadByKey(string servicePath, ODataResourcePath entityPath, string? encodedQuery, IODataProtocol protocol)
        : base(servicePath, entityPath, protocol)
    {
        QueryString = encodedQuery ?? string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestReadByKey"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The resource path whose last segment receives the key.</param>
    /// <param name="entityKey">The entity key.</param>
    /// <param name="query">The structured query.</param>
    public ODataRequestReadByKey(string servicePath, ODataResourcePath entityPath, ODataEntityKey entityKey, StructuredQuery query)
        : this(
            servicePath,
            ODataRequestRead.Required(entityPath).Copy().AddParameterToLastSegment(entityKey),
            ODataRequestRead.Required(query).EncodedQueryString,
            query.Protocol)
    {
    }

    /// <summary>The percent-encoded query given at construction.</summary>
    public string QueryString { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <inheritdoc />
    public override string GetRequestQuery() => Combine(QueryString, base.GetRequestQuery());

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.WithoutBody(this, httpClient);
        return CsrfTokenRetriever is null
            ? await TryExecuteAsync(request, static (r, ct) => r.RequestGetAsync(ct), httpClient, cancellationToken).ConfigureAwait(false)
            : await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestGetAsync(ct), cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>Makes this request return an unbuffered result whose body is streamed.</summary>
    /// <returns>An executable that returns <see cref="ODataRequestResultResource"/>.</returns>
    public IODataRequestResourceExecutable WithoutResponseBuffering()
    {
        RequestResultFactory = ODataRequestResultFactories.WithoutBuffer;
        return new ResourceExecutable(this);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);

    private sealed class ResourceExecutable(ODataRequestReadByKey request) : IODataRequestResourceExecutable
    {
        public async Task<ODataRequestResultResource> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default) =>
            (ODataRequestResultResource)await request.ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>A request that creates an entity.</summary>
public class ODataRequestCreate : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestCreate"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="serializedEntity">The JSON payload.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestCreate(string servicePath, string entityName, string serializedEntity, IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(entityName), serializedEntity, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestCreate"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The resource path.</param>
    /// <param name="serializedEntity">The JSON payload.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestCreate(string servicePath, ODataResourcePath entityPath, string serializedEntity, IODataProtocol protocol)
        : base(servicePath, entityPath, protocol)
    {
        SerializedEntity = serializedEntity;
        AddHeaderIfAbsent("Content-Type", "application/json");
    }

    /// <summary>The JSON payload.</summary>
    public string SerializedEntity { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.ForBodyJson(this, httpClient, SerializedEntity);
        return await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestPostAsync(ct), cancellationToken)
            .ConfigureAwait(false);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
}

/// <summary>A request that updates an entity.</summary>
public class ODataRequestUpdate : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestUpdate"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="entityKey">The entity key.</param>
    /// <param name="serializedEntity">The JSON payload.</param>
    /// <param name="updateStrategy">The update strategy.</param>
    /// <param name="versionIdentifier">The version identifier (ETag) for the <c>If-Match</c> header, or <see langword="null"/>.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestUpdate(
        string servicePath,
        string entityName,
        ODataEntityKey entityKey,
        string serializedEntity,
        UpdateStrategy updateStrategy,
        string? versionIdentifier,
        IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(entityName, entityKey), serializedEntity, updateStrategy, versionIdentifier, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestUpdate"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The resource path including the key.</param>
    /// <param name="serializedEntity">The JSON payload.</param>
    /// <param name="updateStrategy">The update strategy.</param>
    /// <param name="versionIdentifier">The version identifier (ETag) for the <c>If-Match</c> header, or <see langword="null"/>.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestUpdate(
        string servicePath,
        ODataResourcePath entityPath,
        string serializedEntity,
        UpdateStrategy updateStrategy,
        string? versionIdentifier,
        IODataProtocol protocol)
        : base(servicePath, entityPath, protocol)
    {
        SerializedEntity = serializedEntity;
        UpdateStrategy = updateStrategy;
        VersionIdentifier = versionIdentifier;
        AddHeaderIfAbsent("Content-Type", "application/json");
    }

    /// <summary>The JSON payload.</summary>
    public string SerializedEntity { get; }

    /// <summary>The update strategy.</summary>
    public UpdateStrategy UpdateStrategy { get; set; }

    /// <summary>The version identifier (ETag) sent as <c>If-Match</c>.</summary>
    public string? VersionIdentifier { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    internal string HttpMethodName => UpdateStrategy switch
    {
        UpdateStrategy.ModifyWithPatch or UpdateStrategy.ModifyWithPatchRecursiveDelta or UpdateStrategy.ModifyWithPatchRecursiveFull => "PATCH",
        UpdateStrategy.ReplaceWithPut => "PUT",
        _ => throw new InvalidOperationException("Unexpected update Strategy: " + UpdateStrategy),
    };

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.ForBodyJson(this, httpClient, SerializedEntity);
        AddVersionIdentifierToHeaderIfPresent(VersionIdentifier);
        return HttpMethodName == "PATCH"
            ? await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestPatchAsync(ct), cancellationToken)
                .ConfigureAwait(false)
            : await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestPutAsync(ct), cancellationToken)
                .ConfigureAwait(false);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
}

/// <summary>A request that deletes an entity.</summary>
public class ODataRequestDelete : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestDelete"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="entityKey">The entity key.</param>
    /// <param name="versionIdentifier">The version identifier (ETag) for the <c>If-Match</c> header, or <see langword="null"/>.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestDelete(
        string servicePath,
        string entityName,
        ODataEntityKey entityKey,
        string? versionIdentifier,
        IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(entityName, entityKey), versionIdentifier, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestDelete"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityPath">The resource path including the key.</param>
    /// <param name="versionIdentifier">The version identifier (ETag) for the <c>If-Match</c> header, or <see langword="null"/>.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestDelete(string servicePath, ODataResourcePath entityPath, string? versionIdentifier, IODataProtocol protocol)
        : base(servicePath, entityPath, protocol)
    {
        VersionIdentifier = versionIdentifier;
    }

    /// <summary>The version identifier (ETag) sent as <c>If-Match</c>.</summary>
    public string? VersionIdentifier { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.WithoutBody(this, httpClient);
        AddVersionIdentifierToHeaderIfPresent(VersionIdentifier);
        return await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestDeleteAsync(ct), cancellationToken)
            .ConfigureAwait(false);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
}

/// <summary>A request that invokes a function (import).</summary>
public class ODataRequestFunction : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestFunction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestFunction(string servicePath, string functionName, ODataFunctionParameters parameters, IODataProtocol protocol)
        : this(
            servicePath,
            protocol.IsEqualTo(ODataProtocol.V2) ? ODataResourcePath.Of(functionName) : ODataResourcePath.Of(functionName, parameters),
            protocol.IsEqualTo(ODataProtocol.V2) ? parameters.ToEncodedString() : null,
            protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestFunction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="functionPath">The resource path of the function.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="query">An additional percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestFunction(
        string servicePath,
        ODataResourcePath functionPath,
        ODataFunctionParameters parameters,
        string? query,
        IODataProtocol protocol)
        : this(
            servicePath,
            AppendResourcePathWithParameters(functionPath, parameters, protocol),
            AppendQueryWithParameters(query, parameters, protocol),
            protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestFunction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="functionPath">The resource path of the function (including parameters).</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestFunction(string servicePath, ODataResourcePath functionPath, string? encodedQuery, IODataProtocol protocol)
        : base(servicePath, functionPath, protocol)
    {
        Query = encodedQuery ?? string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestFunction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="functionPath">The path to the parent resource.</param>
    /// <param name="structQuery">The structured query; its entity name is appended to the path.</param>
    public ODataRequestFunction(string servicePath, ODataResourcePath functionPath, StructuredQuery structQuery)
        : base(
            servicePath,
            ODataRequestRead.Required(functionPath).Copy().AddSegment(ODataRequestRead.Required(structQuery).EntityOrPropertyName),
            structQuery.Protocol)
    {
        Query = structQuery.EncodedQueryString;
    }

    /// <summary>The percent-encoded query given at construction.</summary>
    public string Query { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <inheritdoc />
    public override string GetRequestQuery() => Combine(Query, base.GetRequestQuery());

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.WithoutBody(this, httpClient);
        return CsrfTokenRetriever is null
            ? await TryExecuteAsync(request, static (r, ct) => r.RequestGetAsync(ct), httpClient, cancellationToken).ConfigureAwait(false)
            : await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestGetAsync(ct), cancellationToken)
                .ConfigureAwait(false);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);

    private static ODataResourcePath AppendResourcePathWithParameters(
        ODataResourcePath path,
        ODataFunctionParameters parameters,
        IODataProtocol protocol) =>
        protocol.IsEqualTo(ODataProtocol.V2) ? path : path.Copy().AddParameterToLastSegment(parameters);

    private static string? AppendQueryWithParameters(string? query, ODataFunctionParameters parameters, IODataProtocol protocol)
    {
        if (protocol.IsEqualTo(ODataProtocol.V4))
        {
            return query;
        }
        var encodedParams = parameters.ToEncodedString();
        return string.IsNullOrEmpty(query) ? encodedParams : encodedParams + "&" + query;
    }
}

/// <summary>A request that invokes an action.</summary>
public class ODataRequestAction : ODataRequestGeneric
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestAction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="actionName">The action name.</param>
    /// <param name="actionParameters">The JSON payload with the parameters.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestAction(string servicePath, string actionName, string? actionParameters, IODataProtocol protocol)
        : this(servicePath, ODataResourcePath.Of(actionName), actionParameters, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestAction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="actionPath">The resource path of the action.</param>
    /// <param name="actionParameters">The JSON payload with the parameters.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestAction(string servicePath, ODataResourcePath actionPath, string? actionParameters, IODataProtocol protocol)
        : this(servicePath, actionPath, actionParameters, null, protocol)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestAction"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="actionPath">The resource path of the action.</param>
    /// <param name="actionParameters">The JSON payload with the parameters.</param>
    /// <param name="encodedQuery">The percent-encoded query.</param>
    /// <param name="protocol">The protocol.</param>
    public ODataRequestAction(
        string servicePath,
        ODataResourcePath actionPath,
        string? actionParameters,
        string? encodedQuery,
        IODataProtocol protocol)
        : base(servicePath, actionPath, protocol)
    {
        ActionParameters = actionParameters ?? "{}";
        Query = encodedQuery ?? string.Empty;
        AddHeaderIfAbsent("Content-Type", "application/json");
    }

    /// <summary>The JSON payload with the parameters.</summary>
    public string ActionParameters { get; }

    /// <summary>The percent-encoded query given at construction.</summary>
    public string Query { get; }

    /// <inheritdoc />
    public override Uri GetRelativeUri(UriEncodingStrategy strategy) =>
        ODataUriFactory.CreateAndEncodeUri(ServicePath, ResourcePath, GetRequestQuery(), strategy);

    /// <inheritdoc />
    public override string GetRequestQuery() => Combine(Query, base.GetRequestQuery());

    /// <summary>Executes the request.</summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ODataRequestResultGeneric> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var request = ODataHttpRequest.ForBodyJson(this, httpClient, ActionParameters);
        return await TryExecuteWithCsrfTokenAsync(httpClient, request, static (r, ct) => r.RequestPostAsync(ct), cancellationToken)
            .ConfigureAwait(false);
    }

    private protected override async Task<IODataRequestResult> ExecuteCoreAsync(HttpClient httpClient, CancellationToken cancellationToken) =>
        await ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
}
