namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Factory methods for collection filter expressions.</summary>
public static class FilterExpressionArray
{
    /// <summary>Creates hassubset().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression HasSubset(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("hassubset", operand1, operand2));

    /// <summary>Creates hassubsequence().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression HasSubSequence(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("hassubsequence", operand1, operand2));

    /// <summary>Creates concat().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueArray.Expression Concat(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("concat", operand1, operand2));

    /// <summary>Creates contains().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Contains(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("contains", operand1, operand2));

    /// <summary>Creates endswith().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression EndsWith(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("endswith", operand1, operand2));

    /// <summary>Creates startswith().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression StartsWith(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("startswith", operand1, operand2));

    /// <summary>Creates indexof().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression IndexOf(IOperandMultiple operand1, IOperandMultiple operand2) =>
        new(Expressions.CreateFunctionPrefix("indexof", operand1, operand2));

    /// <summary>Creates length().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Length(IOperandMultiple operand) =>
        new(Expressions.CreateFunctionPrefix("length", operand));

    /// <summary>Creates substring() with start index.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueArray.Expression Substring(IOperandMultiple operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionPrefix("substring", operand1, operand2));

    /// <summary>Creates substring() with start index and length.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <param name="operand3">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueArray.Expression Substring(IOperandMultiple operand1, IValueNumeric operand2, IValueNumeric operand3) =>
        new(Expressions.CreateFunctionPrefix("substring", operand1, operand2, operand3));

    /// <summary>Creates all() with a lambda condition.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <param name="lambdaFieldPredicate">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression All(IOperandMultiple operand1, IValueBoolean operand2, Func<IFieldReference, bool> lambdaFieldPredicate) =>
        new(Expressions.CreateFunctionLambda("all", operand1, operand2, lambdaFieldPredicate));

    /// <summary>Creates any() with a lambda condition.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <param name="lambdaFieldPredicate">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Any(IOperandMultiple operand1, IValueBoolean operand2, Func<IFieldReference, bool> lambdaFieldPredicate) =>
        new(Expressions.CreateFunctionLambda("any", operand1, operand2, lambdaFieldPredicate));

    /// <summary>Creates any() without condition.</summary>
    /// <param name="operand1">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Any(IOperandMultiple operand1) =>
        new(Expressions.CreateFunctionLambda("any", operand1));
}
