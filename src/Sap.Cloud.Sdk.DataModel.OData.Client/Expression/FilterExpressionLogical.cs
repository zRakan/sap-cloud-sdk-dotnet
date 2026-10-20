namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Factory methods for logical filter expressions.</summary>
public static class FilterExpressionLogical
{
    /// <summary>Creates a equality comparison.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression EqualTo(IOperand operand1, IOperand operand2) =>
        new(Expressions.CreateFunctionInfix("eq", operand1, operand2));

    /// <summary>Creates a inequality comparison.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression NotEqualTo(IOperand operand1, IOperand operand2) =>
        new(Expressions.CreateFunctionInfix("ne", operand1, operand2));

    /// <summary>Creates a greater-than comparison.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression GreaterThan(IOperand operand1, IOperand operand2) =>
        new(Expressions.CreateFunctionInfix("gt", operand1, operand2));

    /// <summary>Creates a greater-or-equal comparison.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression GreaterThanEquals(IOperand operand1, IOperand operand2) =>
        new(Expressions.CreateFunctionInfix("ge", operand1, operand2));

    /// <summary>Creates a less-than comparison.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression LessThan(IOperand operand1, IOperand operand2) =>
        new(Expressions.CreateFunctionInfix("lt", operand1, operand2));

    /// <summary>Creates a less-or-equal comparison.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression LessThanEquals(IOperand operand1, IOperand operand2) =>
        new(Expressions.CreateFunctionInfix("le", operand1, operand2));

    /// <summary>Creates a logical AND.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression And(IValueBoolean operand1, IValueBoolean operand2) =>
        new(Expressions.CreateFunctionInfix("and", operand1, operand2));

    /// <summary>Creates a logical OR.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Or(IValueBoolean operand1, IValueBoolean operand2) =>
        new(Expressions.CreateFunctionInfix("or", operand1, operand2));

    /// <summary>Creates a logical NOT.</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Not(IValueBoolean operand) =>
        new(Expressions.CreateOperatorPrefix("not", operand));

    /// <summary>Creates an enum flag check.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Has(IOperandSingle operand1, IValueEnumMember operand2) =>
        new(Expressions.CreateFunctionInfix("has", operand1, operand2));
    /// <summary>Creates a membership test against a list of operands.</summary>
    /// <param name="operand1">The tested operand.</param>
    /// <param name="operands2">The candidates.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression In(IOperand operand1, params IOperand[] operands2)
    {
        ArgumentNullException.ThrowIfNull(operands2);
        var operand2 = new MultipleOperand(
            (protocol, _) => "(" + string.Join(",", operands2.Select(o => o.GetExpression(protocol))) + ")");
        return In(operand1, operand2);
    }

    /// <summary>Creates a membership test against a collection operand.</summary>
    /// <param name="operand1">The tested operand.</param>
    /// <param name="operand2">The collection.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression In(IOperand operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionInfix("in", operand1, operand2));

    private sealed class MultipleOperand(Func<IODataProtocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>>, string> expression)
        : IOperandMultiple
    {
        public string GetExpression(IODataProtocol protocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>> prefixes) =>
            expression(protocol, prefixes);
    }
}
