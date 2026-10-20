using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper.Batch;

/// <summary>An operation (read or change set) of a batch request.</summary>
internal interface IBatchRequestOperation
{
    void AddToRequestBuilder(ODataRequestBatch requestBatch);
}

/// <summary>A read operation of a batch request.</summary>
internal sealed class BatchRequestRead(ODataRequestGeneric request) : IBatchRequestOperation
{
    public void AddToRequestBuilder(ODataRequestBatch requestBatch)
    {
        switch (request)
        {
            case ODataRequestRead read:
                requestBatch.AddRead(read);
                break;
            case ODataRequestReadByKey readByKey:
                requestBatch.AddReadByKey(readByKey);
                break;
            case ODataRequestFunction function:
                requestBatch.AddFunction(function);
                break;
            default:
                throw new InvalidOperationException("Unexpected read request: " + request.GetType().Name);
        }
    }
}

/// <summary>One operation of a change set, with the helper it originates from and the reader of its result.</summary>
internal sealed record BatchRequestChangeSetOperation(
    Action<ODataRequestBatch.Changeset> ChangeSetConsumer,
    object FluentHelper,
    Func<ODataRequestResultGeneric, VdmEntity>? CreatedEntityReader);

/// <summary>A change set of a batch request.</summary>
internal sealed class BatchRequestChangeSet(IReadOnlyList<BatchRequestChangeSetOperation> operations) : IBatchRequestOperation
{
    internal IReadOnlyList<BatchRequestChangeSetOperation> Operations => operations;

    public void AddToRequestBuilder(ODataRequestBatch requestBatch)
    {
        var requestChangeset = requestBatch.BeginChangeset();
        foreach (var operation in operations)
        {
            operation.ChangeSetConsumer(requestChangeset);
        }
        requestChangeset.EndChangeset();
    }
}

/// <summary>
/// Base class of the generated batch helper of a service: collects read operations and change sets and executes them as one
/// <c>$batch</c> request.
/// </summary>
/// <typeparam name="TBatch">The batch helper interface of the service.</typeparam>
/// <typeparam name="TChangeSet">The change set helper interface of the service.</typeparam>
/// <remarks>Port of <c>BatchFluentHelperBasic</c>. The legacy request mapping by index is not ported.</remarks>
public abstract class BatchFluentHelperBasic<TBatch, TChangeSet> : IFluentHelperServiceBatch<TBatch, TChangeSet>
    where TBatch : IFluentHelperServiceBatch<TBatch, TChangeSet>
    where TChangeSet : IFluentHelperBatchEndChangeSet<TBatch>
{
    private readonly List<IBatchRequestOperation> _requestParts = [];
    private readonly Dictionary<object, ODataRequestGeneric> _requestMapping = new(ReferenceEqualityComparer.Instance);
    private bool _skipCsrfTokenRetrieval;

    /// <summary>The path of the OData service.</summary>
    protected abstract string ServicePathForBatchRequest { get; }

    /// <summary>This helper as <typeparamref name="TBatch"/>.</summary>
    protected abstract TBatch This { get; }

    internal Func<Guid> UuidProvider { get; set; } = Guid.NewGuid;

    internal void MapRequest(object helper, ODataRequestGeneric request) => _requestMapping[helper] = request;

    internal void AddChangeSet(BatchRequestChangeSet changeSet) => _requestParts.Add(changeSet);

    /// <inheritdoc />
    public abstract TChangeSet BeginChangeSet();

    /// <summary>Builds the batch request.</summary>
    /// <returns>The request.</returns>
    public ODataRequestBatch ToRequest()
    {
        var requestBatch = new ODataRequestBatch(ServicePathForBatchRequest, ODataProtocol.V2, UuidProvider);
        if (_skipCsrfTokenRetrieval)
        {
            requestBatch.UseCsrfTokenRetrieval(false);
        }
        foreach (var part in _requestParts)
        {
            part.AddToRequestBuilder(requestBatch);
        }
        return requestBatch;
    }

    /// <inheritdoc />
    public Task<IBatchResponse> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return ExecuteCoreAsync(ODataExecutionTarget.ForClient(httpClient), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IBatchResponse> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ExecuteCoreAsync(ODataExecutionTarget.ForDestination(destination), cancellationToken);
    }

    private async Task<IBatchResponse> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var result = await ToRequest().ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        return new DefaultBatchResponseResult(_requestParts, _requestMapping, result);
    }

    /// <inheritdoc />
    public TBatch AddReadOperations(params IFluentHelperRead[] readOperations)
    {
        ArgumentNullException.ThrowIfNull(readOperations);
        foreach (var operation in readOperations)
        {
            AddRead(operation, operation.ToRequest());
        }
        return This;
    }

    /// <inheritdoc />
    public TBatch AddReadOperations(params IFluentHelperByKey[] readByKeyOperations)
    {
        ArgumentNullException.ThrowIfNull(readByKeyOperations);
        foreach (var operation in readByKeyOperations)
        {
            AddRead(operation, operation.ToRequest());
        }
        return This;
    }

    /// <inheritdoc />
    public TBatch AddReadOperations(params IFluentHelperFunction[] functionOperations)
    {
        ArgumentNullException.ThrowIfNull(functionOperations);
        foreach (var operation in functionOperations)
        {
            var request = operation.ToRequest();
            if (request is ODataRequestAction)
            {
                throw new InvalidOperationException(
                    "Request for function imports while adding read operations must be "
                    + nameof(ODataRequestFunction)
                    + ", but was "
                    + request.GetType().Name
                    + ". Only function imports using HTTP GET are allowed.");
            }
            AddRead(operation, request);
        }
        return This;
    }

    private void AddRead(object helper, ODataRequestGeneric request)
    {
        _requestMapping[helper] = request;
        _requestParts.Add(new BatchRequestRead(request));
    }

    /// <inheritdoc />
    public TBatch AddChangeSet(params IFluentHelperModification[] modifications)
    {
        ArgumentNullException.ThrowIfNull(modifications);
        var changeSet = BeginChangeSet();
        if (changeSet is not IMutableChangeSet mutableChangeSet)
        {
            throw new NotSupportedException(
                $"{GetType().FullName}.{nameof(BeginChangeSet)} does not return an instance of type {typeof(BatchChangeSetFluentHelperBasic<,>).FullName}. "
                + $"To fix this issue, you may either implement {nameof(AddChangeSet)} yourself, or use the default implementation of {nameof(BeginChangeSet)}.");
        }
        foreach (var modification in modifications)
        {
            mutableChangeSet.AddModification(modification);
        }
        changeSet.EndChangeSet();
        return This;
    }

    /// <summary>Does not fetch a CSRF token before the batch request.</summary>
    /// <returns>This batch.</returns>
    public TBatch WithoutCsrfToken()
    {
        _skipCsrfTokenRetrieval = true;
        return This;
    }
}
