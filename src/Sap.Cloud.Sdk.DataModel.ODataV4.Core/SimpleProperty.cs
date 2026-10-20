#pragma warning disable CA1000 // Static factory methods on generic types mirror the Java static factories (Of, Asc, Desc, OfAsync).
#pragma warning disable CA1715 // Nested descriptor interfaces are named after the EDM types, as in Java.
#pragma warning disable CA1716 // Member and parameter names mirror the Java API (Select, In, Get, Function, Property).
#pragma warning disable CA1720 // Nested descriptor interfaces are named after the EDM types, as in Java.
#pragma warning disable CA1711 // Type names mirror the Java API (Collection suffix).

using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Expression;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Typed descriptors of simple properties. Each descriptor is a field reference that also offers the filter
/// functions of its EDM type (<c>Name.StartsWith("a")</c>, <c>Age.GreaterThan(3)</c>, ...), and can be passed to
/// <c>Select</c> and ordered by.
/// </summary>
/// <remarks>
/// Port of the interface <c>SimpleProperty</c> and its nested classes. The nested types are interfaces so that the
/// default filter functions of <c>OData.Client</c> are reachable on the declared type of the generated static field;
/// create instances with <c>SimpleProperty.String&lt;Person&gt;.Of("FirstName")</c>. The filter functions return the
/// untyped <see cref="IValueBoolean"/> of <c>OData.Client</c> (Java: <c>FilterableBoolean&lt;EntityT&gt;</c>).
/// </remarks>
public static class SimpleProperty
{
    /// <summary>Creates the property that selects all fields.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The property.</returns>
    public static ISimpleProperty<TEntity> CreateAll<TEntity>() => new AllProperty<TEntity>();

    /// <summary><c>Edm.String</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface String<TEntity> : ISimpleProperty<TEntity>, IValueString
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static String<TEntity> Of(string fieldName) => new StringProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.Boolean</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface Boolean<TEntity> : ISimpleProperty<TEntity>, IValueBoolean
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Boolean<TEntity> Of(string fieldName) => new BooleanProperty<TEntity>(fieldName);
    }

    /// <summary>Floating point and decimal numbers (<c>Edm.Decimal</c>, <c>Edm.Double</c>, <c>Edm.Single</c>).</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface NumericDecimal<TEntity> : ISimpleProperty<TEntity>, IValueNumeric
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static NumericDecimal<TEntity> Of(string fieldName) => new NumericDecimalProperty<TEntity>(fieldName);
    }

    /// <summary>Integral numbers (<c>Edm.Byte</c>, <c>Edm.Int16</c>, <c>Edm.Int32</c>, <c>Edm.Int64</c>, ...).</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface NumericInteger<TEntity> : ISimpleProperty<TEntity>, IValueNumeric
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static NumericInteger<TEntity> Of(string fieldName) => new NumericIntegerProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.Guid</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface Guid<TEntity> : ISimpleProperty<TEntity>, IValueGuid
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Guid<TEntity> Of(string fieldName) => new GuidProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.Binary</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface Binary<TEntity> : ISimpleProperty<TEntity>
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Binary<TEntity> Of(string fieldName) => new BinaryProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.Duration</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface Duration<TEntity> : ISimpleProperty<TEntity>, IValueDuration
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Duration<TEntity> Of(string fieldName) => new DurationProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.DateTimeOffset</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface DateTime<TEntity> : ISimpleProperty<TEntity>, IValueDateTimeOffset
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static DateTime<TEntity> Of(string fieldName) => new DateTimeProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.Date</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface Date<TEntity> : ISimpleProperty<TEntity>, IValueDate
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Date<TEntity> Of(string fieldName) => new DateProperty<TEntity>(fieldName);
    }

    /// <summary><c>Edm.TimeOfDay</c>.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    public interface Time<TEntity> : ISimpleProperty<TEntity>, IValueTimeOfDay
    {
        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Time<TEntity> Of(string fieldName) => new TimeProperty<TEntity>(fieldName);
    }

    /// <summary>A collection of primitive values or enum members, <c>Collection(Edm.String)</c> and the like.</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    /// <typeparam name="TItem">The item type.</typeparam>
    public interface Collection<TEntity, TItem> : ISimpleProperty<TEntity>, IValueArray
    {
        /// <summary>The item type.</summary>
        Type ItemType { get; }

        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <returns>The descriptor.</returns>
        static Collection<TEntity, TItem> Of(string fieldName) => new CollectionProperty<TEntity, TItem>(fieldName);

        /// <summary>Tests that all items satisfy the condition (<c>all(a:...)</c>).</summary>
        /// <param name="operand">The condition.</param>
        /// <returns>The filter expression.</returns>
        new IValueBoolean All(IValueBoolean operand) =>
            FilterExpressionArray.All(this, operand, CollectionLambda.Predicate(typeof(TItem)));

        /// <summary>Tests that any item satisfies the condition (<c>any(a:...)</c>).</summary>
        /// <param name="operand">The condition.</param>
        /// <returns>The filter expression.</returns>
        new IValueBoolean Any(IValueBoolean operand) =>
            FilterExpressionArray.Any(this, operand, CollectionLambda.Predicate(typeof(TItem)));
    }

    /// <summary>An enumeration (<c>Edm.EnumType</c>).</summary>
    /// <typeparam name="TEntity">The type that owns the property.</typeparam>
    /// <typeparam name="TEnum">The generated enumeration type.</typeparam>
    public interface Enum<TEntity, TEnum> : ISimpleProperty<TEntity>, IValueEnumMember
        where TEnum : VdmEnum
    {
        /// <summary>The qualified OData name of the enumeration type.</summary>
        string EnumType { get; }

        /// <summary>Creates the descriptor.</summary>
        /// <param name="fieldName">The OData name.</param>
        /// <param name="enumType">The qualified OData name of the enumeration type, for example <c>Trippin.PersonGender</c>.</param>
        /// <returns>The descriptor.</returns>
        static Enum<TEntity, TEnum> Of(string fieldName, string enumType) => new EnumProperty<TEntity, TEnum>(fieldName, enumType);

        /// <summary>Compares with a member.</summary>
        /// <param name="operand">The member; <see langword="null"/> compares with <c>null</c>.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean EqualTo(TEnum? operand) =>
            FilterExpressionLogical.EqualTo(this, operand is null ? IValueEnumMember.Null : IValueEnumMember.Literal(EnumType, operand.Name));

        /// <summary>Compares with a member.</summary>
        /// <param name="operand">The member; <see langword="null"/> compares with <c>null</c>.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean NotEqualTo(TEnum? operand) =>
            FilterExpressionLogical.NotEqualTo(this, operand is null ? IValueEnumMember.Null : IValueEnumMember.Literal(EnumType, operand.Name));

        /// <summary>Tests whether the property is one of the members.</summary>
        /// <param name="operands">The members.</param>
        /// <returns>The filter expression.</returns>
        IValueBoolean In(params TEnum[] operands)
        {
            ArgumentNullException.ThrowIfNull(operands);
            return FilterExpressionLogical.In(
                this,
                operands.Select(o => (IOperand)IValueEnumMember.Literal(EnumType, o.Name)).ToArray());
        }
    }

    private sealed class AllProperty<TEntity>() : PropertyBase<TEntity>(string.Empty), ISimpleProperty<TEntity>
    {
    }

    private sealed class StringProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), String<TEntity>
    {
    }

    private sealed class BooleanProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), Boolean<TEntity>
    {
    }

    private sealed class NumericDecimalProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), NumericDecimal<TEntity>
    {
    }

    private sealed class NumericIntegerProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), NumericInteger<TEntity>
    {
    }

    private sealed class GuidProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), Guid<TEntity>
    {
    }

    private sealed class BinaryProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), Binary<TEntity>
    {
    }

    private sealed class DurationProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), Duration<TEntity>
    {
    }

    private sealed class DateTimeProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), DateTime<TEntity>
    {
    }

    private sealed class DateProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), Date<TEntity>
    {
    }

    private sealed class TimeProperty<TEntity>(string fieldName) : PropertyBase<TEntity>(fieldName), Time<TEntity>
    {
    }

    private sealed class CollectionProperty<TEntity, TItem>(string fieldName) : PropertyBase<TEntity>(fieldName), Collection<TEntity, TItem>
    {
        public Type ItemType => typeof(TItem);
    }

    private sealed class EnumProperty<TEntity, TEnum>(string fieldName, string enumType) : PropertyBase<TEntity>(fieldName), Enum<TEntity, TEnum>
        where TEnum : VdmEnum
    {
        public string EnumType { get; } = enumType;
    }
}

internal static class CollectionLambda
{
    /// <summary>Selects the field references inside the lambda of <c>any</c>/<c>all</c> that belong to the item type.</summary>
    internal static Func<IFieldReference, bool> Predicate(Type itemType) =>
        field => field is IEntityReference reference && reference.EntityType == itemType;
}
