#pragma warning disable CA1000 // Static factory methods on generic types mirror the Java static factories (Of, Asc, Desc, OfAsync).
#pragma warning disable CA1711 // Type names mirror the Java API (Collection suffix).
#pragma warning disable CA1715 // Nested descriptor interfaces are named after the EDM types, as in Java.
#pragma warning disable CA1716 // Member and parameter names mirror the Java API (Select, In, Get, Function, Property).
#pragma warning disable CA1720 // Nested descriptor interfaces are named after the EDM types, as in Java.

using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Expression;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Descriptors of complex properties, with <c>Single</c> and <c>Collection</c> cardinality.</summary>
/// <remarks>
/// Port of the interface <c>ComplexProperty</c> and its nested classes. Create instances with
/// <c>ComplexProperty.Single&lt;Person, Location&gt;.Of("HomeAddress")</c>.
/// </remarks>
public static class ComplexProperty
{
    /// <summary>A complex property that holds one value.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    /// <typeparam name="TValue">The complex type.</typeparam>
    public interface Single<TEntity, TValue> : IComplexProperty<TEntity, TValue>, IFilterableComparisonAbsolute
        where TEntity : VdmObject
        where TValue : VdmComplex
    {
        /// <summary>The complex type.</summary>
        Type ItemType { get; }

        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Single<TEntity, TValue> Of(string fieldName) => new SingleProperty<TEntity, TValue>(fieldName);

        /// <summary>Selects properties of the complex value (<c>$select=Address/City</c>).</summary>
        /// <param name="fields">The properties of the complex type.</param>
        /// <returns>The query that can be passed to <c>Select</c>.</returns>
        ComplexPropertyQuery<TEntity, TValue> Select(params IProperty<TValue>[] fields) =>
            ComplexPropertyQuery<TEntity, TValue>.OnProperty(FieldName).Select(fields);

        /// <summary>Tests an enum flag (<c>has</c>) against an enum member name.</summary>
        /// <param name="operand">The member name.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean Has(string operand) => FilterExpressionLogical.Has(this, IValueEnumMember.Literal(operand));

        /// <summary>Tests an enum flag (<c>has</c>) against an enum property.</summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="operand">The enum property.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean Has<TEnum>(SimpleProperty.Enum<TEntity, TEnum> operand)
            where TEnum : VdmEnum
        {
            ArgumentNullException.ThrowIfNull(operand);
            return FilterExpressionLogical.Has(this, operand);
        }

        /// <summary>Tests whether the value is one of the values of a collection.</summary>
        /// <param name="operand">The collection.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean In(IFilterableArray operand) => FilterExpressionLogical.In(this, operand);
    }

    /// <summary>A complex property that holds a collection of values.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    /// <typeparam name="TValue">The complex type.</typeparam>
    public interface Collection<TEntity, TValue> : IComplexProperty<TEntity, TValue>, IValueArray
        where TEntity : VdmObject
        where TValue : VdmComplex
    {
        /// <summary>The complex type.</summary>
        Type ItemType { get; }

        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Collection<TEntity, TValue> Of(string fieldName) => new CollectionProperty<TEntity, TValue>(fieldName);

        /// <summary>Selects properties of the complex values.</summary>
        /// <param name="fields">The properties of the complex type.</param>
        /// <returns>The query that can be passed to <c>Select</c>.</returns>
        ComplexPropertyQuery<TEntity, TValue> Select(params IProperty<TValue>[] fields) =>
            ComplexPropertyQuery<TEntity, TValue>.OnProperty(FieldName).Select(fields);

        /// <summary>Tests that all items satisfy the condition (<c>all(a:...)</c>).</summary>
        /// <param name="operand">The condition.</param>
        /// <returns>The filter expression.</returns>
        new IValueBoolean All(IValueBoolean operand) =>
            FilterExpressionArray.All(this, operand, CollectionLambda.Predicate(typeof(TValue)));

        /// <summary>Tests that any item satisfies the condition (<c>any(a:...)</c>).</summary>
        /// <param name="operand">The condition.</param>
        /// <returns>The filter expression.</returns>
        new IValueBoolean Any(IValueBoolean operand) =>
            FilterExpressionArray.Any(this, operand, CollectionLambda.Predicate(typeof(TValue)));
    }

    private sealed class SingleProperty<TEntity, TValue>(string fieldName) : PropertyBase<TEntity>(fieldName), Single<TEntity, TValue>, IComplexPropertyMarker
        where TEntity : VdmObject
        where TValue : VdmComplex
    {
        public Type ItemType => typeof(TValue);
    }

    private sealed class CollectionProperty<TEntity, TValue>(string fieldName) : PropertyBase<TEntity>(fieldName), Collection<TEntity, TValue>, IComplexPropertyMarker
        where TEntity : VdmObject
        where TValue : VdmComplex
    {
        public Type ItemType => typeof(TValue);
    }
}

/// <summary>Descriptors of navigation properties, with <c>Single</c> and <c>Collection</c> cardinality.</summary>
/// <remarks>
/// Port of the interface <c>NavigationProperty</c> and its nested classes. Create instances with
/// <c>NavigationProperty.Collection&lt;Person, Trip&gt;.Of("Trips")</c>. The members of Java's
/// <c>NavigationPropertyCollection</c> base class (<c>select</c>, <c>filter</c>, <c>top</c>, ...) are default interface
/// members. Each of them returns a fresh sub-query that can be passed to <c>Select</c> of a parent request to expand
/// the navigation property with options.
/// </remarks>
public static class NavigationProperty
{
    /// <summary>A navigation property that points to a collection of entities.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    /// <typeparam name="TValue">The entity type the property points to.</typeparam>
    public interface Collection<TEntity, TValue> : INavigationProperty<TEntity, TValue>, IValueArray
        where TEntity : VdmObject
        where TValue : VdmEntity
    {
        /// <summary>The entity type the property points to.</summary>
        Type ItemType { get; }

        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Collection<TEntity, TValue> Of(string fieldName) => new CollectionProperty<TEntity, TValue>(fieldName);

        /// <summary>Expands the property and selects properties of the target entities.</summary>
        /// <param name="fields">The properties.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> Select(params IProperty<TValue>[] fields) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).Select(fields);

        /// <summary>Expands the property and filters the target entities.</summary>
        /// <param name="filters">The filters.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> Filter(params IValueBoolean[] filters) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).Filter(filters);

        /// <summary>Expands the property and limits the number of target entities.</summary>
        /// <param name="top">The maximum number of entities.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> Top(int top) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).Top(top);

        /// <summary>Expands the property and skips target entities.</summary>
        /// <param name="skip">The number of entities to skip.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> Skip(int skip) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).Skip(skip);

        /// <summary>Expands the property and orders the target entities.</summary>
        /// <param name="ordering">The orderings.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> OrderBy(params FieldOrdering<TValue>[] ordering) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).OrderBy(ordering);

        /// <summary>Expands the property and searches the target entities.</summary>
        /// <param name="search">The text.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> Search(string search) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).Search(search);

        /// <summary>Expands the property and searches the target entities.</summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertyCollectionQuery<TEntity, TValue> Search(SearchExpression expression) =>
            NavigationPropertyCollectionQuery<TEntity, TValue>.OfSubQuery(FieldName).Search(expression);

        /// <summary>Tests that all target entities satisfy the condition (<c>Friends/all(a:...)</c>).</summary>
        /// <param name="operand">The condition.</param>
        /// <returns>The filter expression.</returns>
        new IValueBoolean All(IValueBoolean operand) =>
            FilterExpressionArray.All(this, operand, CollectionLambda.Predicate(typeof(TValue)));

        /// <summary>Tests that any target entity satisfies the condition (<c>Friends/any(a:...)</c>).</summary>
        /// <param name="operand">The condition.</param>
        /// <returns>The filter expression.</returns>
        new IValueBoolean Any(IValueBoolean operand) =>
            FilterExpressionArray.Any(this, operand, CollectionLambda.Predicate(typeof(TValue)));
    }

    /// <summary>A navigation property that points to one entity.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    /// <typeparam name="TValue">The entity type the property points to.</typeparam>
    public interface Single<TEntity, TValue> : INavigationProperty<TEntity, TValue>, IFilterableComparisonAbsolute
        where TEntity : VdmObject
        where TValue : VdmEntity
    {
        /// <summary>The entity type the property points to.</summary>
        Type ItemType { get; }

        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Single<TEntity, TValue> Of(string fieldName) => new SingleProperty<TEntity, TValue>(fieldName);

        /// <summary>Expands the property and selects properties of the target entity.</summary>
        /// <param name="fields">The properties.</param>
        /// <returns>The sub-query.</returns>
        NavigationPropertySingleQuery<TEntity, TValue> Select(params IProperty<TValue>[] fields) =>
            NavigationPropertySingleQuery<TEntity, TValue>.OfSubQuery(FieldName).Select(fields);

        /// <summary>Tests an enum flag (<c>has</c>) against an enum member name.</summary>
        /// <param name="operand">The member name.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean Has(string operand) => FilterExpressionLogical.Has(this, IValueEnumMember.Literal(operand));

        /// <summary>Tests whether the value is one of the values of a collection.</summary>
        /// <param name="operand">The collection.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean In(IFilterableArray operand) => FilterExpressionLogical.In(this, operand);
    }

    private sealed class CollectionProperty<TEntity, TValue>(string fieldName) : PropertyBase<TEntity>(fieldName), Collection<TEntity, TValue>, IStructuredPropertyMarker
        where TEntity : VdmObject
        where TValue : VdmEntity
    {
        public Type ItemType => typeof(TValue);
    }

    private sealed class SingleProperty<TEntity, TValue>(string fieldName) : PropertyBase<TEntity>(fieldName), Single<TEntity, TValue>, IStructuredPropertyMarker
        where TEntity : VdmObject
        where TValue : VdmEntity
    {
        public Type ItemType => typeof(TValue);
    }
}
