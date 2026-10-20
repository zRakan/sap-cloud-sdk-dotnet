using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper.Batch;

/// <summary>Gives the batch helper access to the operations of a change set helper.</summary>
internal interface IMutableChangeSet
{
    void AddModification(IFluentHelperModification modification);
}

/// <summary>Reads the entity a create helper receives from the service.</summary>
internal interface ICreatedEntityReader
{
    VdmEntity ReadCreatedEntity(ODataRequestResultGeneric result);
}

/// <summary>Base class of the generated change set helper of a service.</summary>
/// <typeparam name="TBatch">The batch helper interface of the service.</typeparam>
/// <typeparam name="TChangeSet">The change set helper interface of the service.</typeparam>
/// <remarks>Port of <c>BatchChangeSetFluentHelperBasic</c>.</remarks>
public abstract class BatchChangeSetFluentHelperBasic<TBatch, TChangeSet> : IFluentHelperBatchEndChangeSet<TBatch>, IMutableChangeSet
    where TBatch : IFluentHelperServiceBatch<TBatch, TChangeSet>
    where TChangeSet : IFluentHelperBatchEndChangeSet<TBatch>
{
    private readonly TBatch _typedBatchFluentHelper;
    private readonly BatchFluentHelperBasic<TBatch, TChangeSet> _basicBatchFluentHelper;
    private readonly List<BatchRequestChangeSetOperation> _operations = [];

    /// <summary>Initializes a new instance of the <see cref="BatchChangeSetFluentHelperBasic{TBatch, TChangeSet}"/> class.</summary>
    /// <param name="typedBatchFluentHelper">The batch helper of the service, returned by <see cref="EndChangeSet"/>.</param>
    /// <param name="basicBatchFluentHelper">The same batch helper as its base class.</param>
    protected BatchChangeSetFluentHelperBasic(TBatch typedBatchFluentHelper, BatchFluentHelperBasic<TBatch, TChangeSet> basicBatchFluentHelper)
    {
        ArgumentNullException.ThrowIfNull(basicBatchFluentHelper);
        _typedBatchFluentHelper = typedBatchFluentHelper;
        _basicBatchFluentHelper = basicBatchFluentHelper;
    }

    /// <summary>This helper as <typeparamref name="TChangeSet"/>.</summary>
    protected abstract TChangeSet This { get; }

    /// <summary>Adds a delete operation using the helper that <paramref name="serviceMethod"/> creates for the entity.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="serviceMethod">The method of the service that creates the helper.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <returns>This change set.</returns>
    protected TChangeSet AddRequestDelete<TEntity>(Func<TEntity, IFluentHelperDelete<TEntity>> serviceMethod, TEntity entity)
        where TEntity : VdmEntity
    {
        ArgumentNullException.ThrowIfNull(serviceMethod);
        return AddRequest(serviceMethod(entity));
    }

    /// <summary>Adds a delete operation.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="deleteRequest">The helper.</param>
    /// <returns>This change set.</returns>
    protected TChangeSet AddRequest<TEntity>(IFluentHelperDelete<TEntity> deleteRequest)
        where TEntity : VdmEntity
    {
        ArgumentNullException.ThrowIfNull(deleteRequest);
        var request = (ODataRequestDelete)deleteRequest.ToRequest();
        return Add(deleteRequest, request, changeSet => changeSet.AddDelete(request), null);
    }

    /// <summary>Adds a create operation using the helper that <paramref name="serviceMethod"/> creates for the entity.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="serviceMethod">The method of the service that creates the helper.</param>
    /// <param name="entity">The entity to create.</param>
    /// <returns>This change set.</returns>
    protected TChangeSet AddRequestCreate<TEntity>(Func<TEntity, IFluentHelperCreate<TEntity>> serviceMethod, TEntity entity)
        where TEntity : VdmEntity
    {
        ArgumentNullException.ThrowIfNull(serviceMethod);
        return AddRequest(serviceMethod(entity));
    }

    /// <summary>Adds a create operation.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="createRequest">The helper.</param>
    /// <returns>This change set.</returns>
    protected TChangeSet AddRequest<TEntity>(IFluentHelperCreate<TEntity> createRequest)
        where TEntity : VdmEntity
    {
        ArgumentNullException.ThrowIfNull(createRequest);
        var request = (ODataRequestCreate)createRequest.ToRequest();
        return Add(createRequest, request, changeSet => changeSet.AddCreate(request), result => ODataResponseReader.ReadSingleVdm<TEntity>(result, null));
    }

    /// <summary>Adds an update operation using the helper that <paramref name="serviceMethod"/> creates for the entity.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="serviceMethod">The method of the service that creates the helper.</param>
    /// <param name="entity">The entity to update.</param>
    /// <returns>This change set.</returns>
    protected TChangeSet AddRequestUpdate<TEntity>(Func<TEntity, IFluentHelperUpdate<TEntity>> serviceMethod, TEntity entity)
        where TEntity : VdmEntity
    {
        ArgumentNullException.ThrowIfNull(serviceMethod);
        return AddRequest(serviceMethod(entity));
    }

    /// <summary>Adds an update operation.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="updateRequest">The helper.</param>
    /// <returns>This change set.</returns>
    protected TChangeSet AddRequest<TEntity>(IFluentHelperUpdate<TEntity> updateRequest)
        where TEntity : VdmEntity
    {
        ArgumentNullException.ThrowIfNull(updateRequest);
        var request = (ODataRequestUpdate)updateRequest.ToRequest();
        return Add(updateRequest, request, changeSet => changeSet.AddUpdate(request), null);
    }

    /// <summary>Adds a function import (which must use <c>POST</c>) to the change set.</summary>
    /// <param name="functionImport">The function import.</param>
    /// <returns>This change set.</returns>
    public TChangeSet AddFunctionImport(IFluentHelperFunction functionImport)
    {
        ArgumentNullException.ThrowIfNull(functionImport);
        var request = functionImport.ToRequest();
        if (request is not ODataRequestAction action)
        {
            throw new InvalidOperationException(
                "Request for function imports in batch change sets must be "
                + nameof(ODataRequestAction)
                + ", but was "
                + request.GetType().Name
                + ". Only function imports using HTTP POST are allowed.");
        }
        return Add(functionImport, request, changeSet => changeSet.AddAction(action), null);
    }

    /// <inheritdoc />
    public TBatch EndChangeSet()
    {
        _basicBatchFluentHelper.AddChangeSet(new BatchRequestChangeSet(_operations));
        return _typedBatchFluentHelper;
    }

    void IMutableChangeSet.AddModification(IFluentHelperModification modification)
    {
        ArgumentNullException.ThrowIfNull(modification);
        switch (modification.ToRequest())
        {
            case ODataRequestCreate create:
                Func<ODataRequestResultGeneric, VdmEntity>? reader = null;
                if (modification is ICreatedEntityReader createdEntityReader)
                {
                    reader = createdEntityReader.ReadCreatedEntity;
                }
                Add(modification, create, changeSet => changeSet.AddCreate(create), reader);
                break;
            case ODataRequestUpdate update:
                Add(modification, update, changeSet => changeSet.AddUpdate(update), null);
                break;
            case ODataRequestDelete delete:
                Add(modification, delete, changeSet => changeSet.AddDelete(delete), null);
                break;
            default:
                throw new ArgumentException(
                    "Failed to add unknown type of modifying operation to OData batch request: " + modification.GetType().Name,
                    nameof(modification));
        }
    }

    private TChangeSet Add(
        object helper,
        ODataRequestGeneric request,
        Action<ODataRequestBatch.Changeset> consumer,
        Func<ODataRequestResultGeneric, VdmEntity>? createdEntityReader)
    {
        _basicBatchFluentHelper.MapRequest(helper, request);
        _operations.Add(new BatchRequestChangeSetOperation(consumer, helper, createdEntityReader));
        return This;
    }
}
