using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A reference to a field (property) of an entity.</summary>
public interface IFieldReference : IOperandSingle
{
    /// <summary>The name or path of the field.</summary>
    string FieldName { get; }

    /// <inheritdoc />
    string IOperand.GetExpression(IODataProtocol protocol, Prefixes prefixes)
    {
        ArgumentNullException.ThrowIfNull(prefixes);
        var result = FieldName;
        foreach (var (prefix, predicate) in prefixes)
        {
            if (predicate(this))
            {
                return prefix + "/" + result;
            }
        }
        return result;
    }
}

/// <summary>A field reference whose type is not known; it can be compared and converted to typed operands.</summary>
public interface IFieldUntyped : IFieldReference, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>Treats the field as a string.</summary>
    /// <returns>The typed operand.</returns>
    IValueString AsString() => Values.AsString(this);

    /// <summary>Treats the field as a number.</summary>
    /// <returns>The typed operand.</returns>
    IValueNumeric AsNumber() => Values.AsNumeric(this);

    /// <summary>Treats the field as a boolean.</summary>
    /// <returns>The typed operand.</returns>
    IValueBoolean AsBoolean() => Values.AsBoolean(this);

    /// <summary>Treats the field as binary data.</summary>
    /// <returns>The typed operand.</returns>
    IValueBinary AsBinary() => Values.AsBinary(this);

    /// <summary>Treats the field as a duration.</summary>
    /// <returns>The typed operand.</returns>
    IValueDuration AsDuration() => Values.AsDuration(this);

    /// <summary>Treats the field as a time of day.</summary>
    /// <returns>The typed operand.</returns>
    IValueTimeOfDay AsTimeOfDay() => Values.AsTimeOfDay(this);

    /// <summary>Treats the field as a date-time-offset.</summary>
    /// <returns>The typed operand.</returns>
    IValueDateTimeOffset AsDateTimeOffset() => Values.AsDateTimeOffset(this);

    /// <summary>Treats the field as a date.</summary>
    /// <returns>The typed operand.</returns>
    IValueDate AsDate() => Values.AsDate(this);

    /// <summary>Treats the field as a collection.</summary>
    /// <returns>The typed operand.</returns>
    IValueArray AsArray() => Values.AsArray(this);
}

/// <summary>Factory for <see cref="IFieldUntyped"/>.</summary>
/// <remarks>Port of the static methods of <c>FieldReference</c>.</remarks>
public static class FieldReference
{
    /// <summary>Creates a reference to a field.</summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The reference.</returns>
    public static IFieldUntyped Of(string fieldName)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        return new FieldUntyped(fieldName);
    }

    /// <summary>Creates a reference to a nested field.</summary>
    /// <param name="fieldNames">The names of the path segments; they are joined with <c>/</c>.</param>
    /// <returns>The reference.</returns>
    public static IFieldUntyped OfPath(params string[] fieldNames)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);
        return new FieldUntyped(string.Join('/', fieldNames));
    }

    private sealed class FieldUntyped(string fieldName) : IFieldUntyped
    {
        public string FieldName => fieldName;
    }
}
