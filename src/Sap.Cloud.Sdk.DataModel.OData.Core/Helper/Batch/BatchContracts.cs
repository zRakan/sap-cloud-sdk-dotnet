using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper.Batch;

/// <summary>A service that can start a batch request.</summary>
/// <typeparam name="TBatch">The type of the batch helper of the service.</typeparam>
/// <remarks>Port of <c>BatchService</c>.</remarks>
public interface IBatchService<out TBatch>
{
    /// <summary>Starts a batch request.</summary>
    /// <returns>The batch helper.</returns>
    TBatch Batch();
}

/// <summary>Ends a change set.</summary>
/// <typeparam name="TBatch">The type of the batch helper.</typeparam>
/// <remarks>Port of <c>FluentHelperBatchEndChangeSet</c>.</remarks>
public interface IFluentHelperBatchEndChangeSet<out TBatch>
{
    /// <summary>Ends the change set and returns to the batch.</summary>
    /// <returns>The batch helper.</returns>
    TBatch EndChangeSet();
}

/// <summary>Operations available in every change set.</summary>
/// <typeparam name="TChangeSet">The type of the change set helper.</typeparam>
/// <remarks>Port of <c>FluentHelperBatchChangeSet</c>.</remarks>
public interface IFluentHelperBatchChangeSet<out TChangeSet>
{
    /// <summary>Adds a function import to the change set. Only function imports that use <c>POST</c> are allowed.</summary>
    /// <param name="functionImport">The function import.</param>
    /// <returns>This change set.</returns>
    TChangeSet AddFunctionImport(IFluentHelperFunction functionImport);
}

/// <summary>Executes a batch request.</summary>
/// <remarks>Port of <c>FluentHelperServiceBatchExecute</c>.</remarks>
public interface IFluentHelperServiceBatchExecute
{
    /// <summary>Executes the batch request.</summary>
    /// <param name="httpClient">The client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response; dispose it when done.</returns>
    Task<IBatchResponse> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default);

    /// <summary>Executes the batch request against a destination; the client is obtained from <see cref="ODataHttpClientProvider"/>.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response; dispose it when done.</returns>
    Task<IBatchResponse> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default);
}

/// <summary>A batch request under construction.</summary>
/// <typeparam name="TBatch">The type of the batch helper itself.</typeparam>
/// <typeparam name="TChangeSet">The type of the change set helper.</typeparam>
/// <remarks>Port of <c>FluentHelperServiceBatch</c>.</remarks>
public interface IFluentHelperServiceBatch<TBatch, out TChangeSet> : IFluentHelperServiceBatchExecute
    where TChangeSet : IFluentHelperBatchEndChangeSet<TBatch>
{
    /// <summary>Starts a change set.</summary>
    /// <returns>The change set helper.</returns>
    TChangeSet BeginChangeSet();

    /// <summary>Adds read operations.</summary>
    /// <param name="readOperations">The read helpers.</param>
    /// <returns>This batch.</returns>
    TBatch AddReadOperations(params IFluentHelperRead[] readOperations);

    /// <summary>Adds read-by-key operations.</summary>
    /// <param name="readByKeyOperations">The read-by-key helpers.</param>
    /// <returns>This batch.</returns>
    TBatch AddReadOperations(params IFluentHelperByKey[] readByKeyOperations);

    /// <summary>Adds function imports that use <c>GET</c>.</summary>
    /// <param name="functionOperations">The function import helpers.</param>
    /// <returns>This batch.</returns>
    TBatch AddReadOperations(params IFluentHelperFunction[] functionOperations);

    /// <summary>Adds a change set with the given create, update and delete operations.</summary>
    /// <param name="modifications">The operations.</param>
    /// <returns>This batch.</returns>
    TBatch AddChangeSet(params IFluentHelperModification[] modifications);
}

/// <summary>The result of one change set of a batch.</summary>
/// <remarks>Port of <c>BatchResponseChangeSet</c>.</remarks>
public interface IBatchResponseChangeSet
{
    /// <summary>Reads the entities that the service returned for the create operations of the change set.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created entities, in the order of the operations.</returns>
    Task<IReadOnlyList<VdmEntity>> GetCreatedEntitiesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The response of a batch request. Dispose it to release the HTTP response.</summary>
/// <remarks>Port of <c>BatchResponse</c> (<c>AutoCloseable</c> becomes <see cref="IDisposable"/>).</remarks>
public interface IBatchResponse : IDisposable
{
    /// <summary>Gets the result of the change set with the given index (counting change sets only).</summary>
    /// <param name="index">The index of the change set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The change set result, or the failure if the change set was rejected or does not exist.</returns>
    Task<Result<IBatchResponseChangeSet>> GetAsync(int index, CancellationToken cancellationToken = default);

    /// <summary>Gets the result of a read operation.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="helper">The helper that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entities.</returns>
    Task<IReadOnlyList<TEntity>> GetReadResultAsync<TEntity>(IFluentHelperRead<TEntity> helper, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity;

    /// <summary>Gets the result of a read-by-key operation.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="helper">The helper that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity.</returns>
    Task<TEntity> GetReadResultAsync<TEntity>(IFluentHelperByKey<TEntity> helper, CancellationToken cancellationToken = default)
        where TEntity : VdmEntity;

    /// <summary>Gets the result of a function import that returns a single value.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="helper">The helper that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value.</returns>
    Task<TResult> GetReadResultAsync<TResult>(ISingleValuedFluentHelperFunction<TResult> helper, CancellationToken cancellationToken = default);

    /// <summary>Gets the result of a function import that returns a collection.</summary>
    /// <typeparam name="TResult">The element type.</typeparam>
    /// <param name="helper">The helper that was added to the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The values.</returns>
    Task<IReadOnlyList<TResult>> GetReadResultAsync<TResult>(ICollectionValuedFluentHelperFunction<TResult> helper, CancellationToken cancellationToken = default);
}
