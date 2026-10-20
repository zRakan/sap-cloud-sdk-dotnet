using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper.Batch;

/// <summary>The default <see cref="IBatchResponse"/>.</summary>
/// <remarks>Port of <c>DefaultBatchResponseResult</c> and <c>DefaultBatchResponseChangeSet</c>.</remarks>
internal sealed class DefaultBatchResponseResult(
    IReadOnlyList<IBatchRequestOperation> requestParts,
    IReadOnlyDictionary<object, ODataRequestGeneric> requestMapping,
    ODataRequestResultMultipartGeneric result) : IBatchResponse
{
    public async Task<Result<IBatchResponseChangeSet>> GetAsync(int index, CancellationToken cancellationToken = default)
    {
        var requests = requestParts.OfType<BatchRequestChangeSet>().Skip(index).FirstOrDefault();
        if (requests is null)
        {
            return Result<IBatchResponseChangeSet>.Failure(
                new ArgumentException("Unable to find changeset " + index + " in batch request.", nameof(index)));
        }

        // Perform a health check for the first operation response in the change set.
        var request = requestMapping[requests.Operations[0].FluentHelper];
        try
        {
            await result.GetResultAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result<IBatchResponseChangeSet>.Failure(e);
        }
        return Result<IBatchResponseChangeSet>.Success(new ChangeSetResult(requests.Operations, requestMapping, result));
    }

    public async Task<IReadOnlyList<TEntity>> GetReadResultAsync<TEntity>(IFluentHelperRead<TEntity> helper, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity
    {
        var response = await result.GetResultAsync(requestMapping[helper], cancellationToken).ConfigureAwait(false);
        return await ODataResponseReader.ReadListAsync<TEntity>(response, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TEntity> GetReadResultAsync<TEntity>(IFluentHelperByKey<TEntity> helper, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity
    {
        var response = await result.GetResultAsync(requestMapping[helper], cancellationToken).ConfigureAwait(false);
        return await ODataResponseReader.ReadSingleAsync<TEntity>(response, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> GetReadResultAsync<TResult>(ISingleValuedFluentHelperFunction<TResult> helper, CancellationToken cancellationToken = default)
    {
        var request = requestMapping[helper];
        var response = await result.GetResultAsync(request, cancellationToken).ConfigureAwait(false);
        return await ODataResponseReader.ReadSingleAsync<TResult>(response, FunctionNameOf(request), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TResult>> GetReadResultAsync<TResult>(ICollectionValuedFluentHelperFunction<TResult> helper, CancellationToken cancellationToken = default)
    {
        var request = requestMapping[helper];
        var response = await result.GetResultAsync(request, cancellationToken).ConfigureAwait(false);
        return await ODataResponseReader.ReadListAsync<TResult>(response, FunctionNameOf(request), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The name of the function import: the last path segment of the request.</summary>
    private static string FunctionNameOf(ODataRequestGeneric request)
    {
        var path = request.GetRelativeUri().OriginalString;
        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            path = path[..query];
        }
        return path[(path.LastIndexOf('/') + 1)..];
    }

    public void Dispose() => result.Dispose();

    private sealed class ChangeSetResult(
        IReadOnlyList<BatchRequestChangeSetOperation> operations,
        IReadOnlyDictionary<object, ODataRequestGeneric> requestMapping,
        ODataRequestResultMultipartGeneric result) : IBatchResponseChangeSet
    {
        public async Task<IReadOnlyList<VdmEntity>> GetCreatedEntitiesAsync(CancellationToken cancellationToken = default)
        {
            var entities = new List<VdmEntity>();
            foreach (var operation in operations.Where(o => o.CreatedEntityReader is not null))
            {
                var response = await result.GetResultAsync(requestMapping[operation.FluentHelper], cancellationToken).ConfigureAwait(false);
                entities.Add(operation.CreatedEntityReader!(response));
            }
            return entities;
        }
    }
}
