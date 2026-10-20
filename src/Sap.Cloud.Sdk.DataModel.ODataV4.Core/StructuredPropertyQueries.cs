using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Expression;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Gives access to the query that a structured property query delegates to.</summary>
internal interface IStructuredQueryHolder
{
    StructuredQuery DelegateQuery { get; }
}

/// <summary>Shared implementation of the queries on complex and navigation properties.</summary>
/// <typeparam name="TParent">The type that owns the property.</typeparam>
/// <typeparam name="TEntity">The type the property points to.</typeparam>
/// <remarks>Port of the package-private <c>AbstractStructuredPropertyQuery</c>.</remarks>
public abstract class AbstractStructuredPropertyQuery<TParent, TEntity> : IStructuredProperty<TParent, TEntity>, IStructuredQueryHolder
    where TParent : VdmObject
    where TEntity : VdmObject
{
    private protected AbstractStructuredPropertyQuery(StructuredQuery delegateQuery)
    {
        DelegateQuery = delegateQuery;
    }

    /// <summary>The underlying query.</summary>
    internal StructuredQuery DelegateQuery { get; }

    /// <inheritdoc />
    StructuredQuery IStructuredQueryHolder.DelegateQuery => DelegateQuery;

    /// <inheritdoc />
    public string FieldName => DelegateQuery.EntityOrPropertyName;

    /// <inheritdoc />
    public Type EntityType => typeof(TParent);

    internal string EncodedQueryString => DelegateQuery.EncodedQueryString;

    internal string QueryString => DelegateQuery.QueryString;

    private protected void SelectCore(IEnumerable<IProperty<TEntity>> fields)
    {
        foreach (var field in fields)
        {
            if (field is ISimpleProperty<TEntity> or IComplexPropertyMarker)
            {
                DelegateQuery.Select(field.FieldName);
            }
            else if (field is IStructuredQueryHolder holder)
            {
                if (field is IComplexQueryMarker)
                {
                    foreach (var simpleSelector in holder.DelegateQuery.SimpleSelectors)
                    {
                        DelegateQuery.Select(FieldReference.OfPath(field.FieldName, simpleSelector).FieldName);
                    }

                    foreach (var subQuery in holder.DelegateQuery.ComplexSelectors)
                    {
                        var path = FieldReference.OfPath(field.FieldName, subQuery.EntityOrPropertyName).FieldName;
                        DelegateQuery.Select(
                            StructuredQuery
                                .AsNestedQueryOnProperty(path, ODataProtocol.V4)
                                .Select(subQuery.SimpleSelectors.ToArray())
                                .Select(subQuery.ComplexSelectors.ToArray()));
                    }
                }
                else
                {
                    DelegateQuery.Select(holder.DelegateQuery);
                }
            }
            else if (field is IStructuredPropertyMarker)
            {
                DelegateQuery.Select(StructuredQuery.AsNestedQueryOnProperty(field.FieldName, ODataProtocol.V4));
            }
            else
            {
                throw new InvalidOperationException("Unsupported property type: " + field.GetType().Name);
            }
        }
    }

    /// <inheritdoc />
    public override string ToString() => DelegateQuery.QueryString;
}

/// <summary>Marks complex-property queries, whose selectors are flattened into the parent's path.</summary>
internal interface IComplexQueryMarker
{
}

/// <summary>Marks the descriptors of navigation properties.</summary>
internal interface IStructuredPropertyMarker
{
}

/// <summary>Marks the descriptors of complex properties.</summary>
internal interface IComplexPropertyMarker
{
}

/// <summary>A query on a navigation property that points to a collection: <c>$select</c>, <c>$filter</c>, <c>$top</c>, ...</summary>
/// <typeparam name="TParent">The type that owns the navigation property.</typeparam>
/// <typeparam name="TEntity">The entity type the navigation property points to.</typeparam>
/// <remarks>Port of <c>NavigationPropertyCollectionQuery</c>.</remarks>
public sealed class NavigationPropertyCollectionQuery<TParent, TEntity> : AbstractStructuredPropertyQuery<TParent, TEntity>, INavigationProperty<TParent, TEntity>
    where TParent : VdmObject
    where TEntity : VdmEntity
{
    private NavigationPropertyCollectionQuery(StructuredQuery delegateQuery)
        : base(delegateQuery)
    {
    }

    internal static NavigationPropertyCollectionQuery<TParent, TEntity> OfRootQuery(string fieldName) =>
        new(StructuredQuery.OnEntity(fieldName, ODataProtocol.V4));

    internal static NavigationPropertyCollectionQuery<TParent, TEntity> OfSubQuery(string fieldName) =>
        new(StructuredQuery.AsNestedQueryOnProperty(fieldName, ODataProtocol.V4));

    /// <summary>Selects properties (<c>$select</c>).</summary>
    /// <param name="fields">The properties.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> Select(params IProperty<TEntity>[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        SelectCore(fields);
        return this;
    }

    /// <summary>Filters the entities (<c>$filter</c>); several filters are combined with <c>and</c>.</summary>
    /// <param name="filters">The filters.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> Filter(params IValueBoolean[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        DelegateQuery.Filter(filters);
        return this;
    }

    /// <summary>Limits the number of entities (<c>$top</c>).</summary>
    /// <param name="top">The maximum number of entities.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> Top(int top)
    {
        DelegateQuery.Top(top);
        return this;
    }

    /// <summary>Skips entities (<c>$skip</c>).</summary>
    /// <param name="skip">The number of entities to skip.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> Skip(int skip)
    {
        DelegateQuery.Skip(skip);
        return this;
    }

    /// <summary>Orders the entities (<c>$orderby</c>).</summary>
    /// <param name="ordering">The orderings, most significant first.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> OrderBy(params FieldOrdering<TEntity>[] ordering)
    {
        ArgumentNullException.ThrowIfNull(ordering);
        var expression = FieldOrdering.ToOrderExpression(ordering);
        if (expression is not null)
        {
            DelegateQuery.OrderBy(expression);
        }

        return this;
    }

    /// <summary>Searches for a literal text (<c>$search</c>).</summary>
    /// <param name="search">The text.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> Search(string search)
    {
        DelegateQuery.Search(SearchExpression.GetDoubleQuotedString(search));
        return this;
    }

    /// <summary>Searches for an expression (<c>$search</c>).</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>This query.</returns>
    public NavigationPropertyCollectionQuery<TParent, TEntity> Search(SearchExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        DelegateQuery.Search(expression.Term);
        return this;
    }
}

/// <summary>A query on a navigation property that points to a single entity.</summary>
/// <typeparam name="TParent">The type that owns the navigation property.</typeparam>
/// <typeparam name="TEntity">The entity type the navigation property points to.</typeparam>
/// <remarks>Port of <c>NavigationPropertySingleQuery</c>.</remarks>
public sealed class NavigationPropertySingleQuery<TParent, TEntity> : AbstractStructuredPropertyQuery<TParent, TEntity>, INavigationProperty<TParent, TEntity>
    where TParent : VdmObject
    where TEntity : VdmEntity
{
    private NavigationPropertySingleQuery(StructuredQuery delegateQuery)
        : base(delegateQuery)
    {
    }

    internal static NavigationPropertySingleQuery<TParent, TEntity> OfRootQuery(string fieldName) =>
        new(StructuredQuery.OnEntity(fieldName, ODataProtocol.V4));

    internal static NavigationPropertySingleQuery<TParent, TEntity> OfSubQuery(string fieldName) =>
        new(StructuredQuery.AsNestedQueryOnProperty(fieldName, ODataProtocol.V4));

    /// <summary>Selects properties (<c>$select</c>).</summary>
    /// <param name="fields">The properties.</param>
    /// <returns>This query.</returns>
    public NavigationPropertySingleQuery<TParent, TEntity> Select(params IProperty<TEntity>[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        SelectCore(fields);
        return this;
    }
}

/// <summary>A query on a complex property.</summary>
/// <typeparam name="TOwner">The type that owns the complex property.</typeparam>
/// <typeparam name="TProperty">The complex type.</typeparam>
/// <remarks>Port of <c>ComplexPropertyQuery</c>.</remarks>
public sealed class ComplexPropertyQuery<TOwner, TProperty> : AbstractStructuredPropertyQuery<TOwner, TProperty>, IComplexQueryMarker
    where TOwner : VdmObject
    where TProperty : VdmComplex
{
    private ComplexPropertyQuery(StructuredQuery delegateQuery)
        : base(delegateQuery)
    {
    }

    internal static ComplexPropertyQuery<TOwner, TProperty> OnProperty(string fieldName) =>
        new(StructuredQuery.AsNestedQueryOnProperty(fieldName, ODataProtocol.V4));

    /// <summary>Selects properties of the complex type.</summary>
    /// <param name="fields">The properties.</param>
    /// <returns>This query.</returns>
    public ComplexPropertyQuery<TOwner, TProperty> Select(params IProperty<TProperty>[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        SelectCore(fields);
        return this;
    }
}
