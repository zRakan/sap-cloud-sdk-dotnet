namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Factory methods for string filter expressions.</summary>
public static class FilterExpressionString
{
    /// <summary>Creates matchesPattern().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression MatchesPattern(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("matchesPattern", operand1, operand2));

    /// <summary>Creates tolower().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueString.Expression ToLower(IValueString operand) =>
        new(Expressions.CreateFunctionPrefix("tolower", operand));

    /// <summary>Creates toupper().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueString.Expression ToUpper(IValueString operand) =>
        new(Expressions.CreateFunctionPrefix("toupper", operand));

    /// <summary>Creates trim().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueString.Expression Trim(IValueString operand) =>
        new(Expressions.CreateFunctionPrefix("trim", operand));

    /// <summary>Creates concat().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueString.Expression Concat(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("concat", operand1, operand2));

    /// <summary>Creates contains().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression Contains(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("contains", operand1, operand2));

    /// <summary>Creates substringof().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression SubstringOf(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("substringof", operand1, operand2));

    /// <summary>Creates endswith().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression EndsWith(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("endswith", operand1, operand2));

    /// <summary>Creates startswith().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean.Expression StartsWith(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("startswith", operand1, operand2));

    /// <summary>Creates indexof().</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression IndexOf(IValueString operand1, IValueString operand2) =>
        new(Expressions.CreateFunctionPrefix("indexof", operand1, operand2));

    /// <summary>Creates length().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Length(IValueString operand) =>
        new(Expressions.CreateFunctionPrefix("length", operand));

    /// <summary>Creates substring() with start index.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueString.Expression Substring(IValueString operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionPrefix("substring", operand1, operand2));

    /// <summary>Creates substring() with start index and length.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <param name="operand3">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueString.Expression Substring(IValueString operand1, IValueNumeric operand2, IValueNumeric operand3) =>
        new(Expressions.CreateFunctionPrefix("substring", operand1, operand2, operand3));
}
