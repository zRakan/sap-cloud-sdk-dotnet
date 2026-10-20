using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>Collection</c>.</summary>
public interface IValueArray : IOperandMultiple, IFilterableArray, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueArray Null { get; } = new ValueArrayImpl((_, _) => "null");

    /// <summary>Creates a collection literal. Each element is converted with <see cref="Expressions.CreateOperand(object?)"/>.</summary>
    /// <param name="v">The elements.</param>
    /// <returns>The operand.</returns>
    static IValueArray Literal(System.Collections.IEnumerable v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return new ValueArrayImpl((protocol, _) =>
            "[" + string.Join(",", v.Cast<object?>().Select(o => Expressions.CreateOperand(o).GetExpression(protocol))) + "]");
    }

    /// <summary>A filter expression that evaluates to a <c>Collection</c>.</summary>
    /// <param name="delegate">The underlying expression.</param>
    public sealed class Expression(IFilterExpression @delegate) : IFilterExpression, IValueArray
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
