namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Operands that can be compared for (in)equality and membership.</summary>
public interface IFilterableComparisonAbsolute : IOperand
{
    /// <summary>Creates <c>(this eq operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean EqualTo(IOperand operand) => FilterExpressionLogical.EqualTo(this, operand);

    /// <summary>Creates <c>(this eq value)</c>; the value is converted with <see cref="Expressions.CreateOperand(object?)"/>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean EqualTo(object? operand) => EqualTo(Expressions.CreateOperand(operand));

    /// <summary>Creates <c>(this ne operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean NotEqualTo(IOperand operand) => FilterExpressionLogical.NotEqualTo(this, operand);

    /// <summary>Creates <c>(this ne value)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean NotEqualTo(object? operand) => NotEqualTo(Expressions.CreateOperand(operand));

    /// <summary>Creates <c>(this eq null)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueBoolean EqualToNull() => EqualTo(IOperand.Null);

    /// <summary>Creates <c>(this ne null)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueBoolean NotEqualToNull() => NotEqualTo(IOperand.Null);
}

/// <summary>Operands that can be compared for order.</summary>
public interface IFilterableComparisonRelative : IOperand
{
    /// <summary>Creates <c>(this lt operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean LessThan(IOperand operand) => FilterExpressionLogical.LessThan(this, operand);

    /// <summary>Creates <c>(this lt value)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean LessThan(object? operand) => LessThan(Expressions.CreateOperand(operand));

    /// <summary>Creates <c>(this le operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean LessThanEqual(IOperand operand) => FilterExpressionLogical.LessThanEquals(this, operand);

    /// <summary>Creates <c>(this le value)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean LessThanEqual(object? operand) => LessThanEqual(Expressions.CreateOperand(operand));

    /// <summary>Creates <c>(this gt operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean GreaterThan(IOperand operand) => FilterExpressionLogical.GreaterThan(this, operand);

    /// <summary>Creates <c>(this gt value)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean GreaterThan(object? operand) => GreaterThan(Expressions.CreateOperand(operand));

    /// <summary>Creates <c>(this ge operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean GreaterThanEqual(IOperand operand) => FilterExpressionLogical.GreaterThanEquals(this, operand);

    /// <summary>Creates <c>(this ge value)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueBoolean GreaterThanEqual(object? operand) => GreaterThanEqual(Expressions.CreateOperand(operand));
}

/// <summary>Boolean operators.</summary>
public interface IFilterableBoolean : IOperand
{
}

/// <summary>
/// Operators whose names are reserved words in other .NET languages; provided as extension methods rather than interface members.
/// </summary>
public static class FilterableExtensions
{
    /// <summary>Creates <c>(this and operand)</c>.</summary>
    /// <param name="self">The left-hand side.</param>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean And(this IFilterableBoolean self, IValueBoolean operand) =>
        FilterExpressionLogical.And(Values.AsBoolean(self), operand);

    /// <summary>Creates <c>(this and value)</c>.</summary>
    /// <param name="self">The left-hand side.</param>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean And(this IFilterableBoolean self, bool operand) => self.And(IValueBoolean.Literal(operand));

    /// <summary>Creates <c>(this or operand)</c>.</summary>
    /// <param name="self">The left-hand side.</param>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean Or(this IFilterableBoolean self, IValueBoolean operand) =>
        FilterExpressionLogical.Or(Values.AsBoolean(self), operand);

    /// <summary>Creates <c>(this or value)</c>.</summary>
    /// <param name="self">The left-hand side.</param>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean Or(this IFilterableBoolean self, bool operand) => self.Or(IValueBoolean.Literal(operand));

    /// <summary>Creates <c>(not this)</c>.</summary>
    /// <param name="self">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean Not(this IFilterableBoolean self) => FilterExpressionLogical.Not(Values.AsBoolean(self));

    /// <summary>Creates <c>(this in (a,b,…))</c>.</summary>
    /// <param name="self">The tested operand.</param>
    /// <param name="operands">The candidates.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean In(this IFilterableComparisonAbsolute self, params IOperand[] operands) =>
        FilterExpressionLogical.In(self, operands);

    /// <summary>Creates <c>(this in (a,b,…))</c>; the values are converted with <see cref="Expressions.CreateOperand(object?)"/>.</summary>
    /// <param name="self">The tested operand.</param>
    /// <param name="operands">The candidates.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean In(this IFilterableComparisonAbsolute self, params object?[] operands)
    {
        ArgumentNullException.ThrowIfNull(operands);
        return self.In(operands.Select(o => (IOperand)Expressions.CreateOperand(o)).ToArray());
    }

    /// <summary>Creates <c>(this in collection)</c>.</summary>
    /// <param name="self">The tested operand.</param>
    /// <param name="collection">The collection.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean In(this IFilterableComparisonAbsolute self, IFilterableArray collection) =>
        FilterExpressionLogical.In(self, collection);

    /// <summary>Creates <c>(this in (a,b,…))</c> from a list of values.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="self">The tested operand.</param>
    /// <param name="operands">The candidates.</param>
    /// <returns>The expression.</returns>
    public static IValueBoolean In<T>(this IFilterableComparisonAbsolute self, IReadOnlyList<T> operands)
    {
        ArgumentNullException.ThrowIfNull(operands);
        return self.In(operands.Select(o => (IOperand)Expressions.CreateOperand(o)).ToArray());
    }

    /// <summary>Creates <c>date(this)</c>.</summary>
    /// <param name="self">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDate Date(this IFilterableDateTimeOffset self) =>
        FilterExpressionTemporal.Date(Values.AsDateTimeOffset(self));
}
