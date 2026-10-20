namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Factory methods for date and time filter expressions.</summary>
public static class FilterExpressionTemporal
{
    /// <summary>Creates date().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueDate.Expression Date(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("date", operand));

    /// <summary>Creates fractionalseconds().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression FractionalSeconds(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("fractionalseconds", operand));

    /// <summary>Creates fractionalseconds().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression FractionalSeconds(IValueTimeOfDay operand) =>
        new(Expressions.CreateFunctionPrefix("fractionalseconds", operand));

    /// <summary>Creates second().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Second(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("second", operand));

    /// <summary>Creates second().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Second(IValueTimeOfDay operand) =>
        new(Expressions.CreateFunctionPrefix("second", operand));

    /// <summary>Creates minute().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Minute(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("minute", operand));

    /// <summary>Creates minute().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Minute(IValueTimeOfDay operand) =>
        new(Expressions.CreateFunctionPrefix("minute", operand));

    /// <summary>Creates hour().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Hour(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("hour", operand));

    /// <summary>Creates hour().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Hour(IValueTimeOfDay operand) =>
        new(Expressions.CreateFunctionPrefix("hour", operand));

    /// <summary>Creates day().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Day(IValueDate operand) =>
        new(Expressions.CreateFunctionPrefix("day", operand));

    /// <summary>Creates day().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Day(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("day", operand));

    /// <summary>Creates month().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Month(IValueDate operand) =>
        new(Expressions.CreateFunctionPrefix("month", operand));

    /// <summary>Creates month().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Month(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("month", operand));

    /// <summary>Creates year().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Year(IValueDate operand) =>
        new(Expressions.CreateFunctionPrefix("year", operand));

    /// <summary>Creates year().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression Year(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("year", operand));

    /// <summary>Creates time().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueTimeOfDay.Expression Time(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("time", operand));

    /// <summary>Creates now().</summary>
    /// <returns>The expression.</returns>
    public static IValueDateTimeOffset.Expression Now() =>
        new(Expressions.CreateFunctionPrefix("now"));

    /// <summary>Creates maxdatetime().</summary>
    /// <returns>The expression.</returns>
    public static IValueDateTimeOffset.Expression MaxDateTime() =>
        new(Expressions.CreateFunctionPrefix("maxdatetime"));

    /// <summary>Creates mindatetime().</summary>
    /// <returns>The expression.</returns>
    public static IValueDateTimeOffset.Expression MinDateTime() =>
        new(Expressions.CreateFunctionPrefix("mindatetime"));

    /// <summary>Creates totaloffsetseconds().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression TotalOffsetSeconds(IValueDuration operand) =>
        new(Expressions.CreateFunctionPrefix("totaloffsetseconds", operand));

    /// <summary>Creates totaloffsetminutes().</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The expression.</returns>
    public static IValueNumeric.Expression TotalOffsetMinutes(IValueDateTimeOffset operand) =>
        new(Expressions.CreateFunctionPrefix("totaloffsetminutes", operand));
}
