namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>
/// Creates fluent helpers for arbitrary entity types without generated helper classes.
/// </summary>
/// <remarks>
/// Port of <c>FluentHelperFactory</c> (package-private in Java, <see langword="internal"/> here). Java returns anonymous subclasses; this
/// factory returns small sealed subclasses. Used by tests and by generated code that needs a helper on the fly.
/// </remarks>
internal sealed class FluentHelperFactory
{
    private readonly string _servicePath;

    private FluentHelperFactory(string servicePath)
    {
        _servicePath = servicePath;
    }

    internal static FluentHelperFactory WithServicePath(string servicePath)
    {
        ArgumentNullException.ThrowIfNull(servicePath);
        return new FluentHelperFactory(servicePath);
    }

    internal ReadByKeyHelper<TEntity, TSelectable> ReadByKey<TEntity, TSelectable>(string entityCollection, IReadOnlyDictionary<string, object?> key)
        where TEntity : VdmEntity
        where TSelectable : IEntitySelectable =>
        new(_servicePath, entityCollection, key);

    internal ReadHelper<TEntity, TSelectable> Read<TEntity, TSelectable>(string entityCollection)
        where TEntity : VdmEntity
        where TSelectable : IEntitySelectable =>
        new(_servicePath, entityCollection);

    internal CreateHelper<TEntity> Create<TEntity>(string entityCollection, TEntity entity)
        where TEntity : VdmEntity =>
        new(_servicePath, entityCollection, entity);

    internal CreateHelper<TEntity> Create<TEntity>(TEntity entity)
        where TEntity : VdmEntity =>
        Create(entity.GetEntityCollection() ?? string.Empty, entity);

    internal DeleteHelper<TEntity> Delete<TEntity>(string entityCollection, TEntity entity)
        where TEntity : VdmEntity =>
        new(_servicePath, entityCollection, entity);

    internal DeleteHelper<TEntity> Delete<TEntity>(TEntity entity)
        where TEntity : VdmEntity =>
        Delete(entity.GetEntityCollection() ?? string.Empty, entity);

    internal UpdateHelper<TEntity> Update<TEntity>(string entityCollection, TEntity entity)
        where TEntity : VdmEntity =>
        new(_servicePath, entityCollection, entity);

    internal UpdateHelper<TEntity> Update<TEntity>(TEntity entity)
        where TEntity : VdmEntity =>
        Update(entity.GetEntityCollection() ?? string.Empty, entity);

    internal SingleFunctionHelper<TObject> FunctionSingle<TObject>(IReadOnlyDictionary<string, object?> parameters, string functionName, HttpMethod method) =>
        new(_servicePath, parameters, functionName, method);

    internal CollectionFunctionHelper<TObject> FunctionMultiple<TObject>(IReadOnlyDictionary<string, object?> parameters, string functionName, HttpMethod method) =>
        new(_servicePath, parameters, functionName, method);

    internal sealed class ReadByKeyHelper<TEntity, TSelectable>(string servicePath, string entityCollection, IReadOnlyDictionary<string, object?> key)
        : FluentHelperByKey<ReadByKeyHelper<TEntity, TSelectable>, TEntity, TSelectable>(servicePath, entityCollection)
        where TEntity : VdmEntity
        where TSelectable : IEntitySelectable
    {
        protected override IReadOnlyDictionary<string, object?> Key => key;
    }

    internal sealed class ReadHelper<TEntity, TSelectable>(string servicePath, string entityCollection)
        : FluentHelperRead<ReadHelper<TEntity, TSelectable>, TEntity, TSelectable>(servicePath, entityCollection)
        where TEntity : VdmEntity
        where TSelectable : IEntitySelectable
    {
    }

    internal sealed class CreateHelper<TEntity>(string servicePath, string entityCollection, TEntity entity)
        : FluentHelperCreate<CreateHelper<TEntity>, TEntity>(servicePath, entityCollection)
        where TEntity : VdmEntity
    {
        protected override TEntity Entity => entity;
    }

    internal sealed class DeleteHelper<TEntity>(string servicePath, string entityCollection, TEntity entity)
        : FluentHelperDelete<DeleteHelper<TEntity>, TEntity>(servicePath, entityCollection)
        where TEntity : VdmEntity
    {
        protected override TEntity Entity => entity;
    }

    internal sealed class UpdateHelper<TEntity>(string servicePath, string entityCollection, TEntity entity)
        : FluentHelperUpdate<UpdateHelper<TEntity>, TEntity>(servicePath, entityCollection)
        where TEntity : VdmEntity
    {
        protected override TEntity Entity => entity;
    }

    internal sealed class SingleFunctionHelper<TObject>(string servicePath, IReadOnlyDictionary<string, object?> parameters, string functionName, HttpMethod method)
        : SingleValuedFluentHelperFunction<SingleFunctionHelper<TObject>, TObject, TObject?>(servicePath)
    {
        protected override IReadOnlyDictionary<string, object?> Parameters => parameters;

        protected override string FunctionName => functionName;

        protected override HttpMethod RequestMethod => method;

        protected override Task<TObject?> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>
            ExecuteSingleAsync(target, cancellationToken);
    }

    internal sealed class CollectionFunctionHelper<TObject>(string servicePath, IReadOnlyDictionary<string, object?> parameters, string functionName, HttpMethod method)
        : CollectionValuedFluentHelperFunction<CollectionFunctionHelper<TObject>, TObject, IReadOnlyList<TObject>>(servicePath)
    {
        protected override IReadOnlyDictionary<string, object?> Parameters => parameters;

        protected override string FunctionName => functionName;

        protected override HttpMethod RequestMethod => method;

        protected override Task<IReadOnlyList<TObject>> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>
            ExecuteMultipleAsync(target, cancellationToken);
    }
}
