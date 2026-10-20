using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>String</c>.</summary>
public interface IValueString : IOperandSingle, IFilterableString, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueString Null { get; } = new ValueStringImpl((_, _) => "null");

    /// <summary>Creates a string literal; single quotes are escaped by doubling them.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The operand.</returns>
    static IValueString Literal(string v)
    {
        ArgumentNullException.ThrowIfNull(v);
        var escapedValue = v.Replace("'", "''", StringComparison.Ordinal);
        return new ValueStringImpl((_, _) => "'" + escapedValue + "'");
    }

    /// <summary>A filter expression that evaluates to a <c>String</c>.</summary>
    /// <param name="delegate">The underlying expression.</param>
    public sealed class Expression(IFilterExpression @delegate) : IFilterExpression, IValueString
    {
        /// <inheritdoc />
        public string FilterOperator => @delegate.FilterOperator;

        /// <inheritdoc />
        public IReadOnlyList<IOperand> Operands => @delegate.Operands;

        /// <summary>Renders the expression without lambda variable prefixes.</summary>
        /// <param name="protocol">The OData protocol.</param>
        /// <returns>The expression string.</returns>
        public string GetExpression(IODataProtocol protocol) => GetExpression(protocol, Expressions.NoPrefixes);

        /// <inheritdoc />
        public string GetExpression(IODataProtocol protocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>> prefixes) =>
            @delegate.GetExpression(protocol, prefixes);
    }
}
