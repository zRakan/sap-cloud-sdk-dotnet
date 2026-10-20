using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>Sort order of <c>OrderBy</c>.</summary>
/// <remarks>Port of <c>Order</c>.</remarks>
public enum Order
{
    /// <summary>Ascending.</summary>
    Asc,

    /// <summary>Descending.</summary>
    Desc,
}

/// <summary>How <c>FluentHelperUpdate.ModifyingEntity(ModifyPatchStrategy)</c> sends complex properties.</summary>
/// <remarks>Port of <c>ModifyPatchStrategy</c>.</remarks>
public enum ModifyPatchStrategy
{
    /// <summary>Send the changed fields of the entity only; a changed complex property is sent completely.</summary>
    Shallow,

    /// <summary>Send the changed fields of the entity and, recursively, only the changed fields of complex properties.</summary>
    RecursiveDelta,

    /// <summary>Send the changed fields of the entity and complex properties with changes inside completely.</summary>
    RecursiveFull,
}

/// <summary>Something that can be selected from an entity: a field, a navigation property or all fields.</summary>
/// <remarks>Port of the non-generic part of <c>EntitySelectable</c>.</remarks>
public interface IEntitySelectable
{
    /// <summary>The name of the field in the OData service.</summary>
    string FieldName { get; }

    /// <summary>The select clauses this selectable contributes, for example <c>Name</c> or <c>Address/City</c>.</summary>
    IReadOnlyList<string> Selections => [FieldName];
}

/// <summary>Something that can be selected from an entity of type <typeparamref name="TEntity"/>.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>EntitySelectable</c>.</remarks>
#pragma warning disable CA1040 // Marker interface: generated per-entity selectable interfaces derive from it to keep selections type safe.
public interface IEntitySelectable<TEntity> : IEntitySelectable
{
}
#pragma warning restore CA1040

/// <summary>Selects all fields of an entity (<c>*</c>).</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>EntityFieldAll</c>.</remarks>
public class EntityFieldAll<TEntity> : IEntitySelectable<TEntity>, IEquatable<IEntitySelectable>
    where TEntity : VdmObject
{
    /// <inheritdoc />
    public string FieldName => "*";

    /// <inheritdoc />
    public IReadOnlyList<string> Selections => ["*"];

    /// <inheritdoc />
    public override string ToString() => "*";

    /// <inheritdoc />
    public bool Equals(IEntitySelectable? other) => other is not null && other.Selections.SequenceEqual(Selections);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IEntitySelectable other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => "*".GetHashCode(StringComparison.Ordinal);
}

/// <summary>A field of an entity, usable to select it and to build filter expressions and orderings.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TField">The .NET type of the field.</typeparam>
/// <remarks>Port of <c>EntityField</c>. Method names are PascalCase (<c>Eq</c>, <c>Ne</c>, <c>Gt</c>, <c>Ge</c>, <c>Lt</c>, <c>Le</c>, ...).</remarks>
public class EntityField<TEntity, TField> : IEntitySelectable<TEntity>, IEquatable<EntityField<TEntity, TField>>
{
    private readonly IFieldUntyped _fieldUntyped;

    /// <summary>Initializes a new instance of the <see cref="EntityField{TEntity, TField}"/> class.</summary>
    /// <param name="fieldName">The name of the field in the OData service.</param>
    public EntityField(string fieldName)
        : this(fieldName, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EntityField{TEntity, TField}"/> class with a type converter.</summary>
    /// <param name="fieldName">The name of the field in the OData service.</param>
    /// <param name="typeConverter">Converts custom field values; see <see cref="FieldValueConverter.From{TField, TDomain}"/>.</param>
    public EntityField(string fieldName, IFieldValueConverter<TField>? typeConverter)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        FieldName = fieldName;
        TypeConverter = typeConverter;
        _fieldUntyped = FieldReference.Of(fieldName);
    }

    /// <inheritdoc />
    public string FieldName { get; }

    /// <summary>The converter used to get and set this field as custom field, if any.</summary>
    public IFieldValueConverter<TField>? TypeConverter { get; }

    /// <summary>Creates the filter <c>field eq null</c>.</summary>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> EqNull() => new(_fieldUntyped.EqualToNull());

    /// <summary>Creates the filter <c>field eq value</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> Eq(TField? value) => new(_fieldUntyped.EqualTo((object?)value));

    /// <summary>Creates the filter <c>field ne null</c>.</summary>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> NeNull() => new(_fieldUntyped.NotEqualToNull());

    /// <summary>Creates the filter <c>field ne value</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> Ne(TField? value) => new(_fieldUntyped.NotEqualTo((object?)value));

    /// <summary>Creates the filter <c>field gt value</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> Gt(TField? value) => new(_fieldUntyped.GreaterThan((object?)value));

    /// <summary>Creates the filter <c>field ge value</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> Ge(TField? value) => new(_fieldUntyped.GreaterThanEqual((object?)value));

    /// <summary>Creates the filter <c>field lt value</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> Lt(TField? value) => new(_fieldUntyped.LessThan((object?)value));

    /// <summary>Creates the filter <c>field le value</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> Le(TField? value) => new(_fieldUntyped.LessThanEqual((object?)value));

    /// <summary>Creates the filter <c>substringof(value, field)</c>.</summary>
    /// <param name="value">The substring.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> SubstringOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(FilterExpressionString.SubstringOf(IValueString.Literal(value), _fieldUntyped.AsString()));
    }

    /// <summary>Creates the filter <c>endswith(field, value)</c>.</summary>
    /// <param name="value">The suffix.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> EndsWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(FilterExpressionString.EndsWith(_fieldUntyped.AsString(), IValueString.Literal(value)));
    }

    /// <summary>Creates the filter <c>startswith(field, value)</c>.</summary>
    /// <param name="value">The prefix.</param>
    /// <returns>The expression.</returns>
    public ExpressionFluentHelper<TEntity> StartsWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(FilterExpressionString.StartsWith(_fieldUntyped.AsString(), IValueString.Literal(value)));
    }

    /// <inheritdoc />
    public bool Equals(EntityField<TEntity, TField>? other) => other is not null && FieldName == other.FieldName && Equals(TypeConverter, other.TypeConverter);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as EntityField<TEntity, TField>);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(FieldName, TypeConverter);

    /// <inheritdoc />
    public override string ToString() => FieldName;
}

/// <summary>A filter expression on entities of type <typeparamref name="TEntity"/>.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>ExpressionFluentHelper</c>.</remarks>
public class ExpressionFluentHelper<TEntity>
{
    private readonly IValueBoolean _delegateExpression;

    /// <summary>Initializes a new instance of the <see cref="ExpressionFluentHelper{TEntity}"/> class.</summary>
    /// <param name="delegateExpression">The underlying filter expression.</param>
    public ExpressionFluentHelper(IValueBoolean delegateExpression)
    {
        ArgumentNullException.ThrowIfNull(delegateExpression);
        _delegateExpression = delegateExpression;
    }

    /// <summary>Combines this expression with another one using <c>or</c>.</summary>
    /// <param name="disjunctExpression">The other expression.</param>
    /// <returns>The combined expression.</returns>
    public ExpressionFluentHelper<TEntity> Or(ExpressionFluentHelper<TEntity> disjunctExpression)
    {
        ArgumentNullException.ThrowIfNull(disjunctExpression);
        return new(_delegateExpression.Or(disjunctExpression._delegateExpression));
    }

    /// <summary>Combines this expression with another one using <c>and</c>.</summary>
    /// <param name="conjunctExpression">The other expression.</param>
    /// <returns>The combined expression.</returns>
    public ExpressionFluentHelper<TEntity> And(ExpressionFluentHelper<TEntity> conjunctExpression)
    {
        ArgumentNullException.ThrowIfNull(conjunctExpression);
        return new(_delegateExpression.And(conjunctExpression._delegateExpression));
    }

    /// <summary>Negates this expression.</summary>
    /// <returns>The negated expression.</returns>
    public ExpressionFluentHelper<TEntity> Not() => new(_delegateExpression.Not());

    /// <summary>Negates an expression.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The negated expression.</returns>
#pragma warning disable CA1000 // Mirrors the Java API (ExpressionFluentHelper.not(expression)); the entity type is inferred from the argument.
    public static ExpressionFluentHelper<TEntity> Not(ExpressionFluentHelper<TEntity> expression)
#pragma warning restore CA1000
    {
        ArgumentNullException.ThrowIfNull(expression);
        return expression.Not();
    }

    /// <summary>The underlying filter expression; the outer parentheses are removed when it is rendered.</summary>
    internal IValueBoolean DelegateExpressionWithoutOuterParentheses => new WithoutOuterParentheses(_delegateExpression);

    private sealed class WithoutOuterParentheses(IValueBoolean expression) : IValueBoolean
    {
        public string GetExpression(IODataProtocol protocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>> prefixes)
        {
            var text = expression.GetExpression(protocol, prefixes);
            return text.StartsWith('(') && text.EndsWith(')') ? text[1..^1] : text;
        }
    }
}

/// <summary>A filter expression on a to-one navigation property.</summary>
/// <typeparam name="TEntity">The entity type that owns the navigation property.</typeparam>
/// <typeparam name="TLinkedEntity">The type of the related entity.</typeparam>
/// <remarks>Port of <c>OneToOneLink</c>.</remarks>
public interface IOneToOneLink<TEntity, TLinkedEntity>
{
    /// <summary>Creates a filter on the related entity, for example <c>Address/City eq 'Walldorf'</c>.</summary>
    /// <param name="filterExpression">The filter on the related entity.</param>
    /// <returns>The filter on the owning entity.</returns>
    ExpressionFluentHelper<TEntity> Filter(ExpressionFluentHelper<TLinkedEntity> filterExpression);
}
