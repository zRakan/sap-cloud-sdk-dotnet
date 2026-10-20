namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Factory methods for arithmetic filter expressions.</summary>
public static class FilterExpressionArithmetic
{
    /// <summary>Creates an addition.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Add(IValueNumeric operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("add", operand1, operand2));

    /// <summary>Adds a duration to a date-time-offset.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDateTimeOffset.Expression Add(IValueDateTimeOffset operand1, IValueDuration operand2) =>
        new(Expressions.CreateFunctionInfix("add", operand1, operand2));

    /// <summary>Adds two durations.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDuration.Expression Add(IValueDuration operand1, IValueDuration operand2) =>
        new(Expressions.CreateFunctionInfix("add", operand1, operand2));

    /// <summary>Adds a duration to a date.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDate.Expression Add(IValueDate operand1, IValueDuration operand2) =>
        new(Expressions.CreateFunctionInfix("add", operand1, operand2));

    /// <summary>Creates a subtraction.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Subtract(IValueNumeric operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("sub", operand1, operand2));

    /// <summary>Subtracts a duration from a date-time-offset.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDateTimeOffset.Expression Subtract(IValueDateTimeOffset operand1, IValueDuration operand2) =>
        new(Expressions.CreateFunctionInfix("sub", operand1, operand2));

    /// <summary>Subtracts two durations.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDuration.Expression Subtract(IValueDuration operand1, IValueDuration operand2) =>
        new(Expressions.CreateFunctionInfix("sub", operand1, operand2));

    /// <summary>Subtracts a duration from a date.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDate.Expression Subtract(IValueDate operand1, IValueDuration operand2) =>
        new(Expressions.CreateFunctionInfix("sub", operand1, operand2));

    /// <summary>Subtracts two dates.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDuration.Expression Subtract(IValueDate operand1, IValueDate operand2) =>
        new(Expressions.CreateFunctionInfix("sub", operand1, operand2));

    /// <summary>Negates a number.</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Negate(IValueNumeric operand) =>
        new(Expressions.CreateFunctionPrefix("-", operand));

    /// <summary>Negates a duration.</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDuration.Expression Negate(IValueDuration operand) =>
        new(Expressions.CreateFunctionPrefix("-", operand));

    /// <summary>Creates a multiplication.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Multiply(IValueNumeric operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("mul", operand1, operand2));

    /// <summary>Multiplies a duration.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDuration.Expression Multiply(IValueDuration operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("mul", operand1, operand2));

    /// <summary>Creates a division.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Divide(IValueNumeric operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("divby", operand1, operand2));

    /// <summary>Creates an integer division.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression DivideEuclidean(IValueNumeric operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("div", operand1, operand2));

    /// <summary>Divides a duration.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDuration.Expression Divide(IValueDuration operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("div", operand1, operand2));

    /// <summary>Creates a modulo operation.</summary>
    /// <param name="operand1">The operand.</param>
    /// <param name="operand2">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Modulo(IValueNumeric operand1, IValueNumeric operand2) =>
        new(Expressions.CreateFunctionInfix("mod", operand1, operand2));

    /// <summary>Creates ceiling().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Ceiling(IValueNumeric operand) =>
        new(Expressions.CreateFunctionPrefix("ceiling", operand));

    /// <summary>Creates floor().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Floor(IValueNumeric operand) =>
        new(Expressions.CreateFunctionPrefix("floor", operand));

    /// <summary>Creates round().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Round(IValueNumeric operand) =>
        new(Expressions.CreateFunctionPrefix("round", operand));
}
