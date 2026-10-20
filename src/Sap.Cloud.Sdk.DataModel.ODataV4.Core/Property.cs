using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Expression;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>A property of the entity or complex type <typeparamref name="TEntity"/>, usable in <c>$select</c>.</summary>
/// <typeparam name="TEntity">The type that owns the property.</typeparam>
public interface IProperty<TEntity> : IFieldReference, IEntityReference<TEntity>
{
}

/// <summary>A simple (primitive, enum or collection of those) property; it can be ordered by.</summary>
/// <typeparam name="TEntity">The type that owns the property.</typeparam>
public interface ISimpleProperty<TEntity> : IProperty<TEntity>
{
    /// <summary>Orders ascending by this property.</summary>
    /// <returns>The ordering.</returns>
    FieldOrdering<TEntity> Asc() => FieldOrdering<TEntity>.Asc(this);

    /// <summary>Orders descending by this property.</summary>
    /// <returns>The ordering.</returns>
    FieldOrdering<TEntity> Desc() => FieldOrdering<TEntity>.Desc(this);
}

/// <summary>A complex or navigation property: it points to another structure.</summary>
/// <typeparam name="TEntity">The type that owns the property.</typeparam>
/// <typeparam name="TTarget">The type the property points to.</typeparam>
/// <remarks>Port of the package-private <c>StructuredProperty</c>.</remarks>
public interface IStructuredProperty<TEntity, TTarget> : IProperty<TEntity>
    where TEntity : VdmObject
    where TTarget : VdmObject
{
}

/// <summary>A complex property.</summary>
/// <typeparam name="TEntity">The type that owns the property.</typeparam>
/// <typeparam name="TTarget">The complex type.</typeparam>
public interface IComplexProperty<TEntity, TTarget> : IStructuredProperty<TEntity, TTarget>
    where TEntity : VdmObject
    where TTarget : VdmComplex
{
}

/// <summary>A navigation property.</summary>
/// <typeparam name="TEntity">The type that owns the property.</typeparam>
/// <typeparam name="TTarget">The entity type the property points to.</typeparam>
public interface INavigationProperty<TEntity, TTarget> : IStructuredProperty<TEntity, TTarget>
    where TEntity : VdmObject
    where TTarget : VdmEntity
{
}

/// <summary>Shared implementation of the property descriptors.</summary>
internal abstract class PropertyBase<TEntity>(string fieldName) : IProperty<TEntity>
{
    public string FieldName { get; } = fieldName;

    public Type EntityType => typeof(TEntity);

    public override string ToString() => FieldName;
}
