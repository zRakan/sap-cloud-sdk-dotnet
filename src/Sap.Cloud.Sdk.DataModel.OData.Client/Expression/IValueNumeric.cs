using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>Numeric</c>.</summary>
public interface IValueNumeric : IOperandSingle, IFilterableNumeric, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueNumeric Null { get; } = new ValueNumericImpl((_, _) => "null");

    /// <summary>Creates a numeric literal.</summary>
    /// <typeparam name="TNumber">The numeric type (Java <c>Number</c>).</typeparam>
    /// <param name="v">The value.</param>
    /// <returns>The operand.</returns>
    static IValueNumeric Literal<TNumber>(TNumber v)
        where TNumber : System.Numerics.INumber<TNumber> => ValueNumericFactory.LiteralBoxed(v);

    /// <summary>A filter expression that evaluates to a <c>Numeric</c>.</summary>
    /// <param name="delegate">The underlying expression.</param>
    public sealed class Expression(IFilterExpression @delegate) : IFilterExpression, IValueNumeric
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
