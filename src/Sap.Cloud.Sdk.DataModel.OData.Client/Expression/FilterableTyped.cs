using System.Numerics;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>Arithmetic and rounding functions on numbers.</summary>
public interface IFilterableNumeric : IOperand
{
    /// <summary>Creates <c>(this add operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Add(IValueNumeric operand) => FilterExpressionArithmetic.Add(Values.AsNumeric(this), operand);

    /// <summary>Creates <c>(this add value)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Add<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Add(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>(this sub operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Subtract(IValueNumeric operand) => FilterExpressionArithmetic.Subtract(Values.AsNumeric(this), operand);

    /// <summary>Creates <c>(this sub value)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Subtract<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Subtract(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>(this mul operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Multiply(IValueNumeric operand) => FilterExpressionArithmetic.Multiply(Values.AsNumeric(this), operand);

    /// <summary>Creates <c>(this mul value)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Multiply<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Multiply(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>(this divby operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Divide(IValueNumeric operand) => FilterExpressionArithmetic.Divide(Values.AsNumeric(this), operand);

    /// <summary>Creates <c>(this divby value)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Divide<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Divide(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>(this mod operand)</c>.</summary>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Modulo(IValueNumeric operand) => FilterExpressionArithmetic.Modulo(Values.AsNumeric(this), operand);

    /// <summary>Creates <c>(this mod value)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The right-hand side.</param>
    /// <returns>The expression.</returns>
    IValueNumeric Modulo<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Modulo(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>ceiling(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric Ceil() => FilterExpressionArithmetic.Ceiling(Values.AsNumeric(this));

    /// <summary>Creates <c>floor(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric Floor() => FilterExpressionArithmetic.Floor(Values.AsNumeric(this));

    /// <summary>Creates <c>round(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric Round() => FilterExpressionArithmetic.Round(Values.AsNumeric(this));

    /// <summary>Creates <c>-(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric Negate() => FilterExpressionArithmetic.Negate(Values.AsNumeric(this));
}

/// <summary>String functions.</summary>
public interface IFilterableString : IOperand
{
    /// <summary>Creates <c>matchesPattern(this,'pattern')</c>.</summary>
    /// <param name="operand">The pattern.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Matches(string operand) => Matches(IValueString.Literal(operand));

    /// <summary>Creates <c>matchesPattern(this,pattern)</c>.</summary>
    /// <param name="operand">The pattern.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Matches(IValueString operand) => FilterExpressionString.MatchesPattern(Values.AsString(this), operand);

    /// <summary>Creates <c>tolower(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueString ToLower() => FilterExpressionString.ToLower(Values.AsString(this));

    /// <summary>Creates <c>toupper(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueString ToUpper() => FilterExpressionString.ToUpper(Values.AsString(this));

    /// <summary>Creates <c>trim(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueString Trim() => FilterExpressionString.Trim(Values.AsString(this));

    /// <summary>Creates <c>length(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric Length() => FilterExpressionString.Length(Values.AsString(this));

    /// <summary>Creates <c>concat(this,'operand')</c>.</summary>
    /// <param name="operand">The string to append.</param>
    /// <returns>The expression.</returns>
    IValueString Concat(string operand) => Concat(IValueString.Literal(operand));

    /// <summary>Creates <c>concat(this,operand)</c>.</summary>
    /// <param name="operand">The string to append.</param>
    /// <returns>The expression.</returns>
    IValueString Concat(IValueString operand) => FilterExpressionString.Concat(Values.AsString(this), operand);

    /// <summary>Creates <c>startswith(this,operand)</c>.</summary>
    /// <param name="operand">The prefix.</param>
    /// <returns>The expression.</returns>
    IValueBoolean StartsWith(IValueString operand) => FilterExpressionString.StartsWith(Values.AsString(this), operand);

    /// <summary>Creates <c>startswith(this,'operand')</c>.</summary>
    /// <param name="operand">The prefix.</param>
    /// <returns>The expression.</returns>
    IValueBoolean StartsWith(string operand) => StartsWith(IValueString.Literal(operand));

    /// <summary>Creates <c>endswith(this,operand)</c>.</summary>
    /// <param name="operand">The suffix.</param>
    /// <returns>The expression.</returns>
    IValueBoolean EndsWith(IValueString operand) => FilterExpressionString.EndsWith(Values.AsString(this), operand);

    /// <summary>Creates <c>endswith(this,'operand')</c>.</summary>
    /// <param name="operand">The suffix.</param>
    /// <returns>The expression.</returns>
    IValueBoolean EndsWith(string operand) => EndsWith(IValueString.Literal(operand));

    /// <summary>Creates <c>contains(this,operand)</c>.</summary>
    /// <param name="operand">The substring.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Contains(IValueString operand) => FilterExpressionString.Contains(Values.AsString(this), operand);

    /// <summary>Creates <c>contains(this,'operand')</c>.</summary>
    /// <param name="operand">The substring.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Contains(string operand) => Contains(IValueString.Literal(operand));

    /// <summary>Creates <c>substringof(this,operand)</c> (OData V2).</summary>
    /// <param name="operand">The string to search in.</param>
    /// <returns>The expression.</returns>
    IValueBoolean SubstringOf(IValueString operand) => FilterExpressionString.SubstringOf(Values.AsString(this), operand);

    /// <summary>Creates <c>substringof(this,'operand')</c> (OData V2).</summary>
    /// <param name="operand">The string to search in.</param>
    /// <returns>The expression.</returns>
    IValueBoolean SubstringOf(string operand) => SubstringOf(IValueString.Literal(operand));

    /// <summary>Creates <c>indexof(this,'operand')</c>.</summary>
    /// <param name="operand">The substring.</param>
    /// <returns>The expression.</returns>
    IValueNumeric IndexOf(string operand) => IndexOf(IValueString.Literal(operand));

    /// <summary>Creates <c>indexof(this,operand)</c>.</summary>
    /// <param name="operand">The substring.</param>
    /// <returns>The expression.</returns>
    IValueNumeric IndexOf(IValueString operand) => FilterExpressionString.IndexOf(Values.AsString(this), operand);

    /// <summary>Creates <c>substring(this,start)</c>.</summary>
    /// <param name="operand">The start index.</param>
    /// <returns>The expression.</returns>
    IValueString Substring(int operand) =>
        FilterExpressionString.Substring(Values.AsString(this), IValueNumeric.Literal(operand));

    /// <summary>Creates <c>substring(this,start,length)</c>.</summary>
    /// <param name="operandIndex">The start index.</param>
    /// <param name="operandLength">The length.</param>
    /// <returns>The expression.</returns>
    IValueString Substring(int operandIndex, int operandLength) =>
        FilterExpressionString.Substring(
            Values.AsString(this),
            IValueNumeric.Literal(operandIndex),
            IValueNumeric.Literal(operandLength));
}

/// <summary>Date functions and arithmetic.</summary>
public interface IFilterableDate : IOperand
{
    /// <summary>Creates <c>day(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric DateDay() => FilterExpressionTemporal.Day(Values.AsDate(this));

    /// <summary>Creates <c>month(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric DateMonth() => FilterExpressionTemporal.Month(Values.AsDate(this));

    /// <summary>Creates <c>year(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric DateYear() => FilterExpressionTemporal.Year(Values.AsDate(this));

    /// <summary>Creates <c>(this add duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDate Add(IValueDuration operand) => FilterExpressionArithmetic.Add(Values.AsDate(this), operand);

    /// <summary>Creates <c>(this add duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDate Add(TimeSpan operand) => Add(IValueDuration.Literal(operand));

    /// <summary>Creates <c>(this sub duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDate Subtract(IValueDuration operand) => FilterExpressionArithmetic.Subtract(Values.AsDate(this), operand);

    /// <summary>Creates <c>(this sub duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDate Subtract(TimeSpan operand) => Subtract(IValueDuration.Literal(operand));

    /// <summary>Creates <c>(this sub date)</c>.</summary>
    /// <param name="operand">The other date.</param>
    /// <returns>The expression.</returns>
    IValueDuration Difference(IValueDate operand) => FilterExpressionArithmetic.Subtract(Values.AsDate(this), operand);
}

/// <summary>Date-time-offset functions and arithmetic.</summary>
public interface IFilterableDateTimeOffset : IOperand
{
    /// <summary>Creates <c>fractionalseconds(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeFractionalSeconds() => FilterExpressionTemporal.FractionalSeconds(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>second(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeSecond() => FilterExpressionTemporal.Second(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>minute(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeMinute() => FilterExpressionTemporal.Minute(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>hour(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeHour() => FilterExpressionTemporal.Hour(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>day(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric DateDay() => FilterExpressionTemporal.Day(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>month(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric DateMonth() => FilterExpressionTemporal.Month(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>year(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric DateYear() => FilterExpressionTemporal.Year(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>time(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueTimeOfDay Time() => FilterExpressionTemporal.Time(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>totaloffsetminutes(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric OffsetMinutes() => FilterExpressionTemporal.TotalOffsetMinutes(Values.AsDateTimeOffset(this));

    /// <summary>Creates <c>(this add duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDateTimeOffset Add(IValueDuration operand) =>
        FilterExpressionArithmetic.Add(Values.AsDateTimeOffset(this), operand);

    /// <summary>Creates <c>(this add duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDateTimeOffset Add(TimeSpan operand) => Add(IValueDuration.Literal(operand));

    /// <summary>Creates <c>(this sub duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDateTimeOffset Subtract(IValueDuration operand) =>
        FilterExpressionArithmetic.Subtract(Values.AsDateTimeOffset(this), operand);

    /// <summary>Creates <c>(this sub duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDateTimeOffset Subtract(TimeSpan operand) => Subtract(IValueDuration.Literal(operand));
}

/// <summary>Duration functions and arithmetic.</summary>
public interface IFilterableDuration : IOperand
{
    /// <summary>Creates <c>totaloffsetseconds(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric OffsetSeconds() => FilterExpressionTemporal.TotalOffsetSeconds(Values.AsDuration(this));

    /// <summary>Creates <c>(this add duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDuration Add(IValueDuration operand) => FilterExpressionArithmetic.Add(Values.AsDuration(this), operand);

    /// <summary>Creates <c>(this add duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDuration Add(TimeSpan operand) => Add(IValueDuration.Literal(operand));

    /// <summary>Creates <c>(this sub duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDuration Subtract(IValueDuration operand) =>
        FilterExpressionArithmetic.Subtract(Values.AsDuration(this), operand);

    /// <summary>Creates <c>(this sub duration)</c>.</summary>
    /// <param name="operand">The duration.</param>
    /// <returns>The expression.</returns>
    IValueDuration Subtract(TimeSpan operand) => Subtract(IValueDuration.Literal(operand));

    /// <summary>Creates <c>(this mul number)</c>.</summary>
    /// <param name="operand">The factor.</param>
    /// <returns>The expression.</returns>
    IValueDuration Multiply(IValueNumeric operand) =>
        FilterExpressionArithmetic.Multiply(Values.AsDuration(this), operand);

    /// <summary>Creates <c>(this mul number)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The factor.</param>
    /// <returns>The expression.</returns>
    IValueDuration Multiply<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Multiply(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>(this div number)</c>.</summary>
    /// <param name="operand">The divisor.</param>
    /// <returns>The expression.</returns>
    IValueDuration Divide(IValueNumeric operand) =>
        FilterExpressionArithmetic.Divide(Values.AsDuration(this), operand);

    /// <summary>Creates <c>(this div number)</c>.</summary>
    /// <typeparam name="TNumber">The numeric type.</typeparam>
    /// <param name="operand">The divisor.</param>
    /// <returns>The expression.</returns>
    IValueDuration Divide<TNumber>(TNumber operand)
        where TNumber : INumber<TNumber> => Divide(IValueNumeric.Literal(operand));

    /// <summary>Creates <c>-(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueDuration Negate() => FilterExpressionArithmetic.Negate(Values.AsDuration(this));
}

/// <summary>Time-of-day functions.</summary>
public interface IFilterableTimeOfDay : IOperand
{
    /// <summary>Creates <c>fractionalseconds(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeFractionalSeconds() => FilterExpressionTemporal.FractionalSeconds(Values.AsTimeOfDay(this));

    /// <summary>Creates <c>second(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeSecond() => FilterExpressionTemporal.Second(Values.AsTimeOfDay(this));

    /// <summary>Creates <c>minute(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeMinute() => FilterExpressionTemporal.Minute(Values.AsTimeOfDay(this));

    /// <summary>Creates <c>hour(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric TimeHour() => FilterExpressionTemporal.Hour(Values.AsTimeOfDay(this));
}

/// <summary>Collection functions.</summary>
public interface IFilterableArray : IOperandMultiple
{
    /// <summary>Creates <c>hassubset(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueBoolean HasSubset(IValueArray operand) => FilterExpressionArray.HasSubset(this, operand);

    /// <summary>Creates <c>hassubset(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean HasSubset(System.Collections.IEnumerable operand) => HasSubset(IValueArray.Literal(operand));

    /// <summary>Creates <c>hassubsequence(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueBoolean HasSubSequence(IValueArray operand) => FilterExpressionArray.HasSubSequence(this, operand);

    /// <summary>Creates <c>hassubsequence(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean HasSubSequence(System.Collections.IEnumerable operand) =>
        HasSubSequence(IValueArray.Literal(operand));

    /// <summary>Creates <c>contains(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Contains(IValueArray operand) => FilterExpressionArray.Contains(this, operand);

    /// <summary>Creates <c>contains(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Contains(System.Collections.IEnumerable operand) => Contains(IValueArray.Literal(operand));

    /// <summary>Creates <c>startswith(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueBoolean StartsWith(IValueArray operand) => FilterExpressionArray.StartsWith(this, operand);

    /// <summary>Creates <c>startswith(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean StartsWith(System.Collections.IEnumerable operand) => StartsWith(IValueArray.Literal(operand));

    /// <summary>Creates <c>endswith(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueBoolean EndsWith(IValueArray operand) => FilterExpressionArray.EndsWith(this, operand);

    /// <summary>Creates <c>endswith(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean EndsWith(System.Collections.IEnumerable operand) => EndsWith(IValueArray.Literal(operand));

    /// <summary>Creates <c>indexof(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueNumeric IndexOf(IValueArray operand) => FilterExpressionArray.IndexOf(this, operand);

    /// <summary>Creates <c>indexof(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueNumeric IndexOf(System.Collections.IEnumerable operand) => IndexOf(IValueArray.Literal(operand));

    /// <summary>Creates <c>concat(this,operand)</c>.</summary>
    /// <param name="operand">The other collection.</param>
    /// <returns>The expression.</returns>
    IValueArray Concat(IValueArray operand) => FilterExpressionArray.Concat(this, operand);

    /// <summary>Creates <c>concat(this,[…])</c>.</summary>
    /// <param name="operand">The elements.</param>
    /// <returns>The expression.</returns>
    IValueArray Concat(System.Collections.IEnumerable operand) => Concat(IValueArray.Literal(operand));

    /// <summary>Creates <c>length(this)</c>.</summary>
    /// <returns>The expression.</returns>
    IValueNumeric Length() => FilterExpressionArray.Length(this);

    /// <summary>Creates <c>substring(this,start)</c>.</summary>
    /// <param name="operand">The start index.</param>
    /// <returns>The expression.</returns>
    IValueArray Substring(int operand) =>
        FilterExpressionArray.Substring(this, IValueNumeric.Literal(operand));

    /// <summary>Creates <c>substring(this,start,length)</c>.</summary>
    /// <param name="operandIndex">The start index.</param>
    /// <param name="operandLength">The length.</param>
    /// <returns>The expression.</returns>
    IValueArray Substring(int operandIndex, int operandLength) =>
        FilterExpressionArray.Substring(this, IValueNumeric.Literal(operandIndex), IValueNumeric.Literal(operandLength));

    /// <summary>Creates <c>this/all(x:operand)</c>.</summary>
    /// <param name="operand">The condition on the elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean All(IValueBoolean operand) => FilterExpressionArray.All(this, operand, _ => true);

    /// <summary>Creates <c>this/any(x:operand)</c>.</summary>
    /// <param name="operand">The condition on the elements.</param>
    /// <returns>The expression.</returns>
    IValueBoolean Any(IValueBoolean operand) => FilterExpressionArray.Any(this, operand, _ => true);

    /// <summary>Creates <c>this/any()</c>.</summary>
    /// <returns>The expression.</returns>
    IValueBoolean Any() => FilterExpressionArray.Any(this);
}
