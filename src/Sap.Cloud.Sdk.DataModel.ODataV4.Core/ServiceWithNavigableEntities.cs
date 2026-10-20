#pragma warning disable CA1711 // Type names mirror the Java API (Collection suffix).
#pragma warning disable CA1716 // Member and parameter names mirror the Java API (Select, In, Get, Function, Property).

using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Operations that can be applied to an entity collection: select one entity, call bound operations.</summary>
/// <remarks>Port of <c>OperationsOnEntityCollections</c>.</remarks>
public interface IOperationsOnEntityCollections
{
    /// <summary>Navigates to one entity of the collection.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity; its key identifies it.</param>
    /// <returns>The navigable entity.</returns>
    INavigableEntitySingle<TEntity> ForEntity<TEntity>(TEntity entity)
        where TEntity : VdmEntity<TEntity>;

    /// <summary>Applies a function that returns one entity and continues from that entity.</summary>
    /// <typeparam name="TEntity">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result entity type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The navigable result entity.</returns>
    INavigableEntitySingle<TResult> WithFunction<TEntity, TResult>(BoundFunction.CollectionToSingleEntityComposable<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>
        where TResult : VdmEntity<TResult>;

    /// <summary>Applies a function that returns entities and continues from that collection.</summary>
    /// <typeparam name="TEntity">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result entity type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The navigable result collection.</returns>
    INavigableEntityCollection<TResult> WithFunction<TEntity, TResult>(BoundFunction.CollectionToCollectionEntityComposable<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>
        where TResult : VdmEntity<TResult>;

    /// <summary>Invokes an action bound to the collection that returns a collection.</summary>
    /// <typeparam name="TEntity">The entity type the action is bound to.</typeparam>
    /// <typeparam name="TResult">The item type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The request builder.</returns>
    CollectionValueActionRequestBuilder<TResult> ApplyAction<TEntity, TResult>(BoundAction.CollectionToCollection<TEntity, TResult> action)
        where TEntity : VdmEntity<TEntity>;

    /// <summary>Invokes an action bound to the collection that returns one value.</summary>
    /// <typeparam name="TEntity">The entity type the action is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The request builder.</returns>
    SingleValueActionRequestBuilder<TResult> ApplyAction<TEntity, TResult>(BoundAction.CollectionToSingle<TEntity, TResult> action)
        where TEntity : VdmEntity<TEntity>;

    /// <summary>Invokes a function bound to the collection that returns one value.</summary>
    /// <typeparam name="TEntity">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The request builder.</returns>
    SingleValueFunctionRequestBuilder<TResult> ApplyFunction<TEntity, TResult>(BoundFunction.CollectionToSingle<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>;

    /// <summary>Invokes a function bound to the collection that returns a collection.</summary>
    /// <typeparam name="TEntity">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The item type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The request builder.</returns>
    CollectionValueFunctionRequestBuilder<TResult> ApplyFunction<TEntity, TResult>(BoundFunction.CollectionToCollection<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>;
}

/// <summary>An entity collection (an entity set or the target of a navigation property) with its operations.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>NavigableEntityCollection</c>.</remarks>
public interface INavigableEntityCollection<TEntity> : IOperationsOnEntityCollections
    where TEntity : VdmEntity<TEntity>
{
    /// <summary>Reads all entities.</summary>
    /// <returns>The request builder.</returns>
    GetAllRequestBuilder<TEntity> GetAll();

    /// <summary>Creates an entity.</summary>
    /// <param name="item">The entity.</param>
    /// <returns>The request builder.</returns>
    CreateRequestBuilder<TEntity> Create(TEntity item);

    /// <summary>Counts the entities.</summary>
    /// <returns>The request builder.</returns>
    CountRequestBuilder<TEntity> Count();
}

/// <summary>One entity with its operations.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>NavigableEntitySingle</c>.</remarks>
public interface INavigableEntitySingle<TEntity>
    where TEntity : VdmEntity<TEntity>
{
    /// <summary>Reads the entity.</summary>
    /// <returns>The request builder.</returns>
    GetByKeyRequestBuilder<TEntity> Get();

    /// <summary>Updates the entity.</summary>
    /// <param name="item">The entity with the changes.</param>
    /// <returns>The request builder.</returns>
    UpdateRequestBuilder<TEntity> Update(TEntity item);

    /// <summary>Deletes the entity.</summary>
    /// <returns>The request builder.</returns>
    DeleteRequestBuilder<TEntity> Delete();

    /// <summary>Navigates to the entities of a navigation property.</summary>
    /// <typeparam name="TNavigation">The target entity type.</typeparam>
    /// <param name="property">The navigation property.</param>
    /// <returns>The navigable target collection.</returns>
    INavigableEntityCollection<TNavigation> NavigateTo<TNavigation>(NavigationProperty.Collection<TEntity, TNavigation> property)
        where TNavigation : VdmEntity<TNavigation>;

    /// <summary>Navigates to the entity of a navigation property.</summary>
    /// <typeparam name="TNavigation">The target entity type.</typeparam>
    /// <param name="property">The navigation property.</param>
    /// <returns>The navigable target entity.</returns>
    INavigableEntitySingle<TNavigation> NavigateTo<TNavigation>(NavigationProperty.Single<TEntity, TNavigation> property)
        where TNavigation : VdmEntity<TNavigation>;

    /// <summary>Invokes a function bound to the entity that returns one value.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The request builder.</returns>
    SingleValueFunctionRequestBuilder<TResult> ApplyFunction<TResult>(BoundFunction.SingleToSingle<TEntity, TResult> function);

    /// <summary>Invokes a function bound to the entity that returns a collection.</summary>
    /// <typeparam name="TResult">The item type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The request builder.</returns>
    CollectionValueFunctionRequestBuilder<TResult> ApplyFunction<TResult>(BoundFunction.SingleToCollection<TEntity, TResult> function);

    /// <summary>Applies a function that returns one entity and continues from that entity.</summary>
    /// <typeparam name="TResult">The result entity type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The navigable result entity.</returns>
    INavigableEntitySingle<TResult> WithFunction<TResult>(BoundFunction.SingleToSingleEntityComposable<TEntity, TResult> function)
        where TResult : VdmEntity<TResult>;

    /// <summary>Applies a function that returns entities and continues from that collection.</summary>
    /// <typeparam name="TResult">The result entity type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The navigable result collection.</returns>
    INavigableEntityCollection<TResult> WithFunction<TResult>(BoundFunction.SingleToCollectionEntityComposable<TEntity, TResult> function)
        where TResult : VdmEntity<TResult>;

    /// <summary>Invokes an action bound to the entity that returns one value.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The request builder.</returns>
    SingleValueActionRequestBuilder<TResult> ApplyAction<TResult>(BoundAction.SingleToSingle<TEntity, TResult> action);

    /// <summary>Invokes an action bound to the entity that returns a collection.</summary>
    /// <typeparam name="TResult">The item type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The request builder.</returns>
    CollectionValueActionRequestBuilder<TResult> ApplyAction<TResult>(BoundAction.SingleToCollection<TEntity, TResult> action);
}

/// <summary>A generated service: it knows its service path and provides the navigable entity API on top of the entity sets.</summary>
/// <remarks>Port of the interface <c>ServiceWithNavigableEntities</c>; see <see cref="ServiceWithNavigableEntities"/>.</remarks>
public interface IServiceWithNavigableEntities : IOperationsOnEntityCollections
{
    /// <summary>The service path, for example <c>/odata/v4/TripPinServiceRW</c>.</summary>
    string ServicePath { get; }
}

/// <summary>
/// Base class of generated services. It implements <see cref="IServiceWithNavigableEntities"/> with public members, so
/// that <c>service.ForEntity(person)</c> works on the class (C# default interface members are only visible through
/// the interface type).
/// </summary>
/// <remarks>Port of the default methods of <c>ServiceWithNavigableEntities</c>.</remarks>
public abstract class ServiceWithNavigableEntities : IServiceWithNavigableEntities
{
    /// <summary>Initializes a new instance of the <see cref="ServiceWithNavigableEntities"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    protected ServiceWithNavigableEntities(string servicePath)
    {
        ArgumentNullException.ThrowIfNull(servicePath);
        ServicePath = servicePath;
    }

    /// <inheritdoc />
    public string ServicePath { get; }

    /// <inheritdoc />
    public INavigableEntitySingle<TEntity> ForEntity<TEntity>(TEntity entity)
        where TEntity : VdmEntity<TEntity>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is not IVdmEntitySet)
        {
            throw new InvalidOperationException($"Entity type {entity.GetType().Name} must implement {nameof(IVdmEntitySet)}.");
        }

        return new NavigableEntitySingle<TEntity>(ServicePath, VdmEntityUtil.OfEntity(entity), entity);
    }

    /// <inheritdoc />
    public INavigableEntitySingle<TResult> WithFunction<TEntity, TResult>(BoundFunction.CollectionToSingleEntityComposable<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>
        where TResult : VdmEntity<TResult>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntitySingle<TResult>(
            ServicePath,
            BoundOperationPath.OfBoundOperation(function).AddSegment(function.QualifiedName, function.Parameters),
            null);
    }

    /// <inheritdoc />
    public INavigableEntityCollection<TResult> WithFunction<TEntity, TResult>(BoundFunction.CollectionToCollectionEntityComposable<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>
        where TResult : VdmEntity<TResult>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntityCollection<TResult>(
            ServicePath,
            BoundOperationPath.OfBoundOperation(function).AddSegment(function.QualifiedName, function.Parameters));
    }

    /// <inheritdoc />
    public CollectionValueActionRequestBuilder<TResult> ApplyAction<TEntity, TResult>(BoundAction.CollectionToCollection<TEntity, TResult> action)
        where TEntity : VdmEntity<TEntity>
    {
        ArgumentNullException.ThrowIfNull(action);
        return new NavigableEntityCollection<TEntity>(ServicePath, BoundOperationPath.OfBoundOperation(action)).ApplyAction(action);
    }

    /// <inheritdoc />
    public SingleValueActionRequestBuilder<TResult> ApplyAction<TEntity, TResult>(BoundAction.CollectionToSingle<TEntity, TResult> action)
        where TEntity : VdmEntity<TEntity>
    {
        ArgumentNullException.ThrowIfNull(action);
        return new NavigableEntityCollection<TEntity>(ServicePath, BoundOperationPath.OfBoundOperation(action)).ApplyAction(action);
    }

    /// <inheritdoc />
    public SingleValueFunctionRequestBuilder<TResult> ApplyFunction<TEntity, TResult>(BoundFunction.CollectionToSingle<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntityCollection<TEntity>(ServicePath, BoundOperationPath.OfBoundOperation(function)).ApplyFunction(function);
    }

    /// <inheritdoc />
    public CollectionValueFunctionRequestBuilder<TResult> ApplyFunction<TEntity, TResult>(BoundFunction.CollectionToCollection<TEntity, TResult> function)
        where TEntity : VdmEntity<TEntity>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntityCollection<TEntity>(ServicePath, BoundOperationPath.OfBoundOperation(function)).ApplyFunction(function);
    }
}

/// <summary>Resource paths of bound operations.</summary>
internal static class BoundOperationPath
{
    /// <summary>The path of the entity set the operation is bound to.</summary>
    internal static ODataResourcePath OfBoundOperation<TBinding, TResult>(IBoundOperation<TBinding, TResult> operation)
        where TBinding : VdmEntity<TBinding>
    {
        _ = operation;
        return new ODataResourcePath().AddSegment(VdmEntityUtil.GetEntityCollection<TBinding>());
    }
}

/// <summary>Implementation of <see cref="INavigableEntitySingle{TEntity}"/>.</summary>
internal sealed class NavigableEntitySingle<TEntity>(string servicePath, ODataResourcePath entityPath, TEntity? maybeEntity) : INavigableEntitySingle<TEntity>
    where TEntity : VdmEntity<TEntity>
{
    public GetByKeyRequestBuilder<TEntity> Get() => new(servicePath, entityPath);

    public UpdateRequestBuilder<TEntity> Update(TEntity item) => new(servicePath, entityPath, item);

    public DeleteRequestBuilder<TEntity> Delete() => new(servicePath, entityPath, maybeEntity ?? VdmEntityUtil.NewInstance<TEntity>());

    public INavigableEntityCollection<TNavigation> NavigateTo<TNavigation>(NavigationProperty.Collection<TEntity, TNavigation> property)
        where TNavigation : VdmEntity<TNavigation>
    {
        ArgumentNullException.ThrowIfNull(property);
        return new NavigableEntityCollection<TNavigation>(servicePath, entityPath.Copy().AddSegment(property.FieldName));
    }

    public INavigableEntitySingle<TNavigation> NavigateTo<TNavigation>(NavigationProperty.Single<TEntity, TNavigation> property)
        where TNavigation : VdmEntity<TNavigation>
    {
        ArgumentNullException.ThrowIfNull(property);
        return new NavigableEntitySingle<TNavigation>(servicePath, entityPath.Copy().AddSegment(property.FieldName), null);
    }

    public SingleValueFunctionRequestBuilder<TResult> ApplyFunction<TResult>(BoundFunction.SingleToSingle<TEntity, TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters));
    }

    public CollectionValueFunctionRequestBuilder<TResult> ApplyFunction<TResult>(BoundFunction.SingleToCollection<TEntity, TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters));
    }

    public INavigableEntitySingle<TResult> WithFunction<TResult>(BoundFunction.SingleToSingleEntityComposable<TEntity, TResult> function)
        where TResult : VdmEntity<TResult>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntitySingle<TResult>(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters), null);
    }

    public INavigableEntityCollection<TResult> WithFunction<TResult>(BoundFunction.SingleToCollectionEntityComposable<TEntity, TResult> function)
        where TResult : VdmEntity<TResult>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntityCollection<TResult>(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters));
    }

    public SingleValueActionRequestBuilder<TResult> ApplyAction<TResult>(BoundAction.SingleToSingle<TEntity, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var builder = new SingleValueActionRequestBuilder<TResult>(servicePath, entityPath.Copy().AddSegment(action.QualifiedName), action.Parameters);
        if (maybeEntity?.VersionIdentifier is { } eTag)
        {
            builder.WithHeader("If-Match", eTag);
        }

        return builder;
    }

    public CollectionValueActionRequestBuilder<TResult> ApplyAction<TResult>(BoundAction.SingleToCollection<TEntity, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var builder = new CollectionValueActionRequestBuilder<TResult>(servicePath, entityPath.Copy().AddSegment(action.QualifiedName), action.Parameters);
        if (maybeEntity?.VersionIdentifier is { } eTag)
        {
            builder.WithHeader("If-Match", eTag);
        }

        return builder;
    }
}

/// <summary>Implementation of <see cref="INavigableEntityCollection{TEntity}"/>.</summary>
internal sealed class NavigableEntityCollection<TEntity>(string servicePath, ODataResourcePath entityPath) : INavigableEntityCollection<TEntity>
    where TEntity : VdmEntity<TEntity>
{
    public INavigableEntitySingle<TItem> ForEntity<TItem>(TItem entity)
        where TItem : VdmEntity<TItem>
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new NavigableEntitySingle<TItem>(servicePath, entityPath.Copy().AddParameterToLastSegment(entity.GetKey()), entity);
    }

    public GetAllRequestBuilder<TEntity> GetAll() => new(servicePath, entityPath);

    public CreateRequestBuilder<TEntity> Create(TEntity item) => new(servicePath, entityPath, item);

    public CountRequestBuilder<TEntity> Count() => new(servicePath, entityPath);

    public INavigableEntitySingle<TResult> WithFunction<TBinding, TResult>(BoundFunction.CollectionToSingleEntityComposable<TBinding, TResult> function)
        where TBinding : VdmEntity<TBinding>
        where TResult : VdmEntity<TResult>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntitySingle<TResult>(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters), null);
    }

    public INavigableEntityCollection<TResult> WithFunction<TBinding, TResult>(BoundFunction.CollectionToCollectionEntityComposable<TBinding, TResult> function)
        where TBinding : VdmEntity<TBinding>
        where TResult : VdmEntity<TResult>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new NavigableEntityCollection<TResult>(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters));
    }

    public CollectionValueActionRequestBuilder<TResult> ApplyAction<TBinding, TResult>(BoundAction.CollectionToCollection<TBinding, TResult> action)
        where TBinding : VdmEntity<TBinding>
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(servicePath, entityPath.Copy().AddSegment(action.QualifiedName), action.Parameters);
    }

    public SingleValueActionRequestBuilder<TResult> ApplyAction<TBinding, TResult>(BoundAction.CollectionToSingle<TBinding, TResult> action)
        where TBinding : VdmEntity<TBinding>
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(servicePath, entityPath.Copy().AddSegment(action.QualifiedName), action.Parameters);
    }

    public SingleValueFunctionRequestBuilder<TResult> ApplyFunction<TBinding, TResult>(BoundFunction.CollectionToSingle<TBinding, TResult> function)
        where TBinding : VdmEntity<TBinding>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters));
    }

    public CollectionValueFunctionRequestBuilder<TResult> ApplyFunction<TBinding, TResult>(BoundFunction.CollectionToCollection<TBinding, TResult> function)
        where TBinding : VdmEntity<TBinding>
    {
        ArgumentNullException.ThrowIfNull(function);
        return new(servicePath, entityPath.Copy().AddSegment(function.QualifiedName, function.Parameters));
    }
}
