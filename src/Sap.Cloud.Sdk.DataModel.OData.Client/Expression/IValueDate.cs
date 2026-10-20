using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>Date</c>.</summary>
public interface IValueDate : IOperandSingle, IFilterableDate, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueDate Null { get; } = new ValueDateImpl((_, _) => "null");

    /// <summary>Creates a date literal (Java <c>LocalDate</c>).</summary>
    /// <param name="v">The value.</param>
    /// <returns>The operand.</returns>
    static IValueDate Literal(DateOnly v) => new ValueDateImpl((_, _) => JavaFormat.ToIsoLocalDate(v));

    /// <summary>A filter expression that evaluates to a <c>Date</c>.</summary>
    /// <param name="delegate">The underlying expression.</param>
    public sealed class Expression(IFilterExpression @delegate) : IFilterExpression, IValueDate
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
