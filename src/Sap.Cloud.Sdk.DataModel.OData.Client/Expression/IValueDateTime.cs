using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>DateTime</c>.</summary>
public interface IValueDateTime : IOperandSingle, IFilterableDateTimeOffset, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueDateTime Null { get; } = new ValueDateTimeImpl((_, _) => "null");

    /// <summary>Creates a date-time literal (Java <c>LocalDateTime</c>).</summary>
    /// <param name="v">The value.</param>
    /// <returns>The operand.</returns>
    static IValueDateTime Literal(DateTime v) => new ValueDateTimeImpl((protocol, _) => protocol.DateTimeSerializer(v));

    /// <summary>A filter expression that evaluates to a <c>DateTime</c>.</summary>
    /// <param name="delegate">The underlying expression.</param>
    public sealed class Expression(IFilterExpression @delegate) : IFilterExpression, IValueDateTime
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
