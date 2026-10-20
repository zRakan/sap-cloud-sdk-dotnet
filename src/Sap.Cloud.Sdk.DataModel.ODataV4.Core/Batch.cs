using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Combines several requests to one <c>$batch</c> request: reads and change sets.</summary>
/// <remarks>
/// Port of <c>BatchRequestBuilder</c>. Java lets subclasses override <c>getUuidProvider()</c> to get predictable
/// identifiers; here the provider is a constructor argument.
/// </remarks>
public class BatchRequestBuilder : RequestBuilderBase<BatchRequestBuilder, BatchResponse>, IModificationRequestBuilder<BatchResponse>
{
    private readonly ODataRequestBatch _delegate;
    private readonly Dictionary<IRequestBuilder, ODataRequestGeneric> _requestMapping = new(ReferenceEqualityComparer.Instance);

    /// <summary>Initializes a new instance of the <see cref="BatchRequestBuilder"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="uuidProvider">Creates the identifiers of batch and change set boundaries; defaults to random GUIDs.</param>
    public BatchRequestBuilder(string servicePath, Func<Guid>? uuidProvider = null)
        : base(servicePath, ODataResourcePath.Of("$batch"))
    {
        _delegate = new ODataRequestBatch(servicePath, ODataProtocol.V4, uuidProvider ?? Guid.NewGuid);
        CsrfTokenEnabled = true;
    }

    internal IReadOnlyDictionary<IRequestBuilder, ODataRequestGeneric> RequestMapping => _requestMapping;

    /// <summary>Adds read requests (get all, get by key, count, functions) outside of any change set.</summary>
    /// <param name="operations">The requests.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">A request is not a read request.</exception>
    public BatchRequestBuilder AddReadOperations(params IReadRequestBuilder[] operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        foreach (var operation in operations)
        {
            var request = operation.ToRequest();
            _requestMapping[operation] = request;
            switch (request)
            {
                case ODataRequestRead read:
                    _delegate.AddRead(read);
                    break;
                case ODataRequestReadByKey readByKey:
                    _delegate.AddReadByKey(readByKey);
                    break;
                case ODataRequestFunction function:
                    _delegate.AddFunction(function);
                    break;
                default:
                    throw new ArgumentException(
                        "Failed to add unknown type of read operation to OData batch request: " + operation.GetType().Name,
                        nameof(operations));
            }
        }

        return this;
    }

    /// <summary>Adds a change set: modifying requests that succeed or fail together.</summary>
    /// <param name="operations">The requests (create, update, delete, actions).</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">A request is not a modifying request.</exception>
    public BatchRequestBuilder AddChangeset(params IModificationRequestBuilder[] operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var changeset = _delegate.BeginChangeset();
        foreach (var operation in operations)
        {
            var request = operation.ToRequest();
            _requestMapping[operation] = request;
            switch (request)
            {
                case ODataRequestCreate create:
                    changeset.AddCreate(create);
                    break;
                case ODataRequestUpdate update:
                    changeset.AddUpdate(update);
                    break;
                case ODataRequestDelete delete:
                    changeset.AddDelete(delete);
                    break;
                case ODataRequestAction action:
                    changeset.AddAction(action);
                    break;
                default:
                    throw new ArgumentException(
                        "Failed to add unknown type of modifying operation to OData batch request: " + operation.GetType().Name,
                        nameof(operations));
            }
        }

        changeset.EndChangeset();
        return this;
    }

    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This builder.</returns>
    public BatchRequestBuilder WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return this;
    }

    IModificationRequestBuilder<BatchResponse> IModificationRequestBuilder<BatchResponse>.WithoutCsrfToken() => WithoutCsrfToken();

    /// <inheritdoc />
    public override ODataRequestBatch ToRequest() => Configure(_delegate);

    /// <inheritdoc />
    public override async Task<BatchResponse> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await Configure(_delegate).ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return new BatchResponse(response, _requestMapping);
    }
}

/// <summary>The response to a batch request; dispose it when done.</summary>
/// <remarks>
/// Port of <c>BatchResponse</c>. Reading the result of a single request deserializes its payload and is
/// asynchronous.
/// </remarks>
public sealed class BatchResponse : IDisposable
{
    private readonly ODataRequestResultMultipartGeneric _result;
    private readonly IReadOnlyDictionary<IRequestBuilder, ODataRequestGeneric> _requestMapping;

    internal BatchResponse(ODataRequestResultMultipartGeneric result, IReadOnlyDictionary<IRequestBuilder, ODataRequestGeneric> requestMapping)
    {
        _result = result;
        _requestMapping = requestMapping;
    }

    /// <summary>Wraps a protocol-level batch result, for example one obtained by executing <c>ToRequest()</c> directly.</summary>
    /// <param name="response">The protocol-level result.</param>
    /// <param name="initialRequest">The builder that created the request.</param>
    /// <returns>The typed response.</returns>
    public static BatchResponse Of(ODataRequestResultMultipartGeneric response, BatchRequestBuilder initialRequest)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(initialRequest);
        return new BatchResponse(response, initialRequest.RequestMapping);
    }

    /// <summary>The HTTP status code of the batch response itself.</summary>
    public int ResponseStatusCode => _result.StatusCode;

    /// <summary>The headers of the batch response itself.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResponseHeaders => _result.GetAllHeaderValues();

    private Task<ODataRequestResultGeneric> GetResultAsync(IRequestBuilder operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!_requestMapping.TryGetValue(operation, out var request))
        {
            throw new ArgumentException("The operation was not added to the batch request.", nameof(operation));
        }

        return _result.GetResultAsync(request, cancellationToken);
    }

    /// <summary>Gets the entities returned by a get-all request.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="operation">The request that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entities.</returns>
    public async Task<IReadOnlyList<T>> GetReadResultAsync<T>(GetAllRequestBuilder<T> operation, CancellationToken cancellationToken = default)
        where T : VdmEntity =>
        await (await GetResultAsync(operation, cancellationToken).ConfigureAwait(false)).AsListAsync<T>(cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the values returned by a function that returns a collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="operation">The request that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The values.</returns>
    public async Task<IReadOnlyList<T>> GetReadResultAsync<T>(CollectionValueFunctionRequestBuilder<T> operation, CancellationToken cancellationToken = default) =>
        await (await GetResultAsync(operation, cancellationToken).ConfigureAwait(false)).AsListAsync<T>(cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the entity returned by a get-by-key request.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="operation">The request that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity.</returns>
    public async Task<T> GetReadResultAsync<T>(GetByKeyRequestBuilder<T> operation, CancellationToken cancellationToken = default)
        where T : VdmEntity
    {
        var result = await GetResultAsync(operation, cancellationToken).ConfigureAwait(false);
        var entity = await result.AsSingleAsync<T>(null, cancellationToken).ConfigureAwait(false);
        if (result.VersionIdentifierFromHeader is { } versionIdentifier)
        {
            entity.VersionIdentifier = versionIdentifier;
        }

        return entity;
    }

    /// <summary>Gets the value returned by a function that returns one value.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The request that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value.</returns>
    public async Task<T> GetReadResultAsync<T>(SingleValueFunctionRequestBuilder<T> operation, CancellationToken cancellationToken = default) =>
        await (await GetResultAsync(operation, cancellationToken).ConfigureAwait(false)).AsSingleAsync<T>(null, cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the number returned by a count request.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="operation">The request that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count.</returns>
    public async Task<long> GetReadResultAsync<T>(CountRequestBuilder<T> operation, CancellationToken cancellationToken = default)
        where T : VdmEntity =>
        await (await GetResultAsync(operation, cancellationToken).ConfigureAwait(false)).AsSingleAsync<long>(null, cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the response to a create request.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="operation">The request that was added to a change set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<ModificationResponse<TEntity>> GetModificationResultAsync<TEntity>(CreateRequestBuilder<TEntity> operation, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity =>
        await ModificationResponse<TEntity>.OfAsync(
            await GetResultAsync(operation, cancellationToken).ConfigureAwait(false),
            operation.Entity,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the response to an update request.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="operation">The request that was added to a change set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<ModificationResponse<TEntity>> GetModificationResultAsync<TEntity>(UpdateRequestBuilder<TEntity> operation, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity =>
        await ModificationResponse<TEntity>.OfAsync(
            await GetResultAsync(operation, cancellationToken).ConfigureAwait(false),
            operation.Entity,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the response to a delete request.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="operation">The request that was added to a change set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<ModificationResponse<TEntity>> GetModificationResultAsync<TEntity>(DeleteRequestBuilder<TEntity> operation, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity =>
        await ModificationResponse<TEntity>.OfAsync(
            await GetResultAsync(operation, cancellationToken).ConfigureAwait(false),
            operation.Entity,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the response to an action that returns one value.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The request that was added to a change set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<ActionResponseSingle<T>> GetModificationResultAsync<T>(SingleValueActionRequestBuilder<T> operation, CancellationToken cancellationToken = default) =>
        await ActionResponseSingle<T>.OfAsync(await GetResultAsync(operation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the response to an action that returns a collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="operation">The request that was added to a change set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<ActionResponseCollection<T>> GetModificationResultAsync<T>(CollectionValueActionRequestBuilder<T> operation, CancellationToken cancellationToken = default) =>
        await ActionResponseCollection<T>.OfAsync(await GetResultAsync(operation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void Dispose() => _result.Dispose();
}
