using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

/// <summary>Ports of ExpressionsTest, ExpressionsFilterTest, FilterExpressionsWithNullTest and ODataEntityKeyTest.</summary>
public class ExpressionTests
{
    private static readonly IODataProtocol V2 = ODataProtocol.V2;
    private static readonly IODataProtocol V4 = ODataProtocol.V4;

    private static readonly DateOnly LocalDate = new(2001, 1, 1);
    private static readonly TimeOnly LocalTime = new(13, 37, 0);

    // .NET has 100ns resolution: 1 tick = 100ns (Java test: 111ns), 1111111 ticks = 111111100ns (Java test: 111111111ns).
    private static readonly DateTime LocalDateTime = new(2001, 1, 1, 13, 37, 0);
    private static readonly DateTime DateTimeWith1Tick = LocalDateTime.AddTicks(1);
    private static readonly DateTime DateTimeWith7Digits = LocalDateTime.AddTicks(1111111);

    private static readonly IValueDate Date1 = IValueDate.Literal(LocalDate);
    private static readonly IValueDate Date2 = IValueDate.Literal(LocalDate.AddDays(1));
    private static readonly IValueDateTime DateTime1 = IValueDateTime.Literal(LocalDateTime);
    private static readonly IValueDuration Duration1 = IValueDuration.Literal(TimeSpan.FromDays(1));
    private static readonly IValueDuration Duration2 = IValueDuration.Literal(TimeSpan.FromDays(2));
    private static readonly IValueNumeric NumberInt = IValueNumeric.Literal(1);
    private static readonly IValueNumeric Number2 = IValueNumeric.Literal(2);
    private static readonly IValueDateTimeOffset DateTimeOffset1 = IValueDateTimeOffset.Literal(new DateTimeOffset(LocalDateTime, TimeSpan.Zero));
    private static readonly IValueTimeOfDay TimeOfDay = IValueTimeOfDay.Literal(LocalTime);
    private static readonly IValueNumeric NumberFloat = IValueNumeric.Literal(0.9f);
    private static readonly IValueNumeric NumberDecimal = IValueNumeric.Literal(110m);
    private static readonly IValueNumeric NumberDouble = IValueNumeric.Literal(1E+10d);
    private static readonly IValueGuid Guid1 = IValueGuid.Literal(Guid.Parse("b3e130fe-d72c-4a5b-8dcf-463b497f985c"));

    private readonly IValueArray _multiple1 = IValueArray.Literal(new[] { "A", "B" });
    private readonly IValueArray _multiple2 = IValueArray.Literal(new[] { "C", "D" });
    private readonly IValueBoolean _boolean1 = IValueBoolean.Literal(true);
    private readonly IValueBoolean _boolean2 = IValueBoolean.Literal(false);
    private readonly IValueString _string1 = IValueString.Literal("Aaa");
    private readonly IValueString _string2 = IValueString.Literal("Bbb");
    private readonly IValueArray _fieldCollection = FieldReference.Of("Friends").AsArray();
    private readonly IValueArray _fieldCollection2 = FieldReference.Of("Feet").AsArray();
    private readonly IFieldUntyped _fieldUntyped = FieldReference.Of("BestFriend");
    private readonly IFieldReference _fieldComplex = FieldReference.Of("OperatingSystem");
    private readonly IFieldReference _fieldPrimitive = FieldReference.Of("ShowSize");
    private readonly IValueEnumMember _fieldEnum = IValueEnumMember.Literal("EnumType", "EnumValue");
    private readonly IValueEnumMember _fieldEnumAnonymous = IValueEnumMember.Literal("Debian");

    [Fact]
    public void Literals_V2()
    {
        Assert.Equal("1", NumberInt.GetExpression(V2));
        Assert.Equal("0.9f", NumberFloat.GetExpression(V2));
        Assert.Equal("110M", NumberDecimal.GetExpression(V2));
        Assert.Equal("1.0E10d", NumberDouble.GetExpression(V2));
        Assert.Equal("guid'b3e130fe-d72c-4a5b-8dcf-463b497f985c'", Guid1.GetExpression(V2));
        Assert.Equal("time'PT13H37M'", TimeOfDay.GetExpression(V2));
        Assert.Equal("datetime'2001-01-01T13:37:00'", DateTime1.GetExpression(V2));
        Assert.Equal("datetime'2001-01-01T13:37:00.0000001'", IValueDateTime.Literal(DateTimeWith1Tick).GetExpression(V2));
        Assert.Equal("datetime'2001-01-01T13:37:00.1111111'", IValueDateTime.Literal(DateTimeWith7Digits).GetExpression(V2));
        Assert.Equal("datetimeoffset'2001-01-01T13:37:00Z'", DateTimeOffset1.GetExpression(V2));
    }

    [Fact]
    public void Literals_V4()
    {
        Assert.Equal("1", NumberInt.GetExpression(V4));
        Assert.Equal("0.9", NumberFloat.GetExpression(V4));
        Assert.Equal("110", NumberDecimal.GetExpression(V4));
        Assert.Equal("1.0E10", NumberDouble.GetExpression(V4));
        Assert.Equal("b3e130fe-d72c-4a5b-8dcf-463b497f985c", Guid1.GetExpression(V4));
        Assert.Equal("EnumType'EnumValue'", _fieldEnum.GetExpression(V4));
        Assert.Equal("duration'PT24H'", Duration1.GetExpression(V4));
        Assert.Equal("13:37:00", TimeOfDay.GetExpression(V4));
        Assert.Equal("2001-01-01", Date1.GetExpression(V4));
        Assert.Equal("2001-01-01T13:37:00Z", DateTimeOffset1.GetExpression(V4));
        Assert.Equal("2001-01-01T13:37:00.0000001Z", IValueDateTimeOffset.Literal(new DateTimeOffset(DateTimeWith1Tick, TimeSpan.Zero)).GetExpression(V4));
        Assert.Equal("2001-01-01T13:37:00.1111111Z", IValueDateTimeOffset.Literal(new DateTimeOffset(DateTimeWith7Digits, TimeSpan.Zero)).GetExpression(V4));
        Assert.Equal("2001-01-01T13:37:00Z", DateTime1.GetExpression(V4));
        Assert.Equal("2001-01-01T13:37:00+01:00", IValueDateTimeOffset.Literal(new DateTimeOffset(LocalDateTime, TimeSpan.FromHours(1))).GetExpression(V4));
    }

    [Fact]
    public void Arithmetic()
    {
        Assert.Equal("(1 add 2)", NumberInt.Add(Number2).GetExpression(V4));
        Assert.Equal("(2001-01-01 add duration'PT24H')", Date1.Add(Duration1).GetExpression(V4));
        Assert.Equal("(2001-01-01 add duration'PT24H')", Date1.Add(TimeSpan.FromHours(24)).GetExpression(V4));
        Assert.Equal("(2001-01-01T13:37:00Z add duration'PT24H')", DateTimeOffset1.Add(Duration1).GetExpression(V4));
        Assert.Equal("(2001-01-01T13:37:00Z add duration'PT24H')", DateTimeOffset1.Add(TimeSpan.FromHours(24)).GetExpression(V4));
        Assert.Equal("(duration'PT24H' add duration'PT48H')", Duration1.Add(Duration2).GetExpression(V4));
        Assert.Equal("(duration'PT24H' add duration'PT48H')", Duration1.Add(TimeSpan.FromHours(48)).GetExpression(V4));
        Assert.Equal("ceiling(0.9)", NumberFloat.Ceil().GetExpression(V4));
        Assert.Equal("(1 divby 2)", NumberInt.Divide(Number2).GetExpression(V4));
        Assert.Equal("(duration'PT24H' div 1)", Duration1.Divide(NumberInt).GetExpression(V4));
        Assert.Equal("(duration'PT24H' div 1)", Duration1.Divide(1).GetExpression(V4));
        Assert.Equal("floor(0.9)", NumberFloat.Floor().GetExpression(V4));
        Assert.Equal("(1 mod 2)", NumberInt.Modulo(Number2).GetExpression(V4));
        Assert.Equal("(1 mul 2)", NumberInt.Multiply(Number2).GetExpression(V4));
        Assert.Equal("(duration'PT24H' mul 1)", Duration1.Multiply(NumberInt).GetExpression(V4));
        Assert.Equal("(duration'PT24H' mul 1)", Duration1.Multiply(1).GetExpression(V4));
        Assert.Equal("-(1)", NumberInt.Negate().GetExpression(V4));
        Assert.Equal("-(duration'PT24H')", Duration1.Negate().GetExpression(V4));
        Assert.Equal("round(0.9)", NumberFloat.Round().GetExpression(V4));
        Assert.Equal("(1 sub 2)", NumberInt.Subtract(Number2).GetExpression(V4));
        Assert.Equal("(2001-01-01 sub 2001-01-02)", Date1.Difference(Date2).GetExpression(V4));
        Assert.Equal("(2001-01-01T13:37:00Z sub duration'PT24H')", DateTimeOffset1.Subtract(Duration1).GetExpression(V4));
        Assert.Equal("(2001-01-01T13:37:00Z sub duration'PT24H')", DateTimeOffset1.Subtract(TimeSpan.FromHours(24)).GetExpression(V4));
        Assert.Equal("(duration'PT24H' sub duration'PT48H')", Duration1.Subtract(Duration2).GetExpression(V4));
        Assert.Equal("(duration'PT24H' sub duration'PT48H')", Duration1.Subtract(TimeSpan.FromHours(48)).GetExpression(V4));
        Assert.Equal("(2001-01-01 sub duration'PT48H')", Date1.Subtract(Duration2).GetExpression(V4));
        Assert.Equal("(2001-01-01 sub duration'PT48H')", Date1.Subtract(TimeSpan.FromHours(48)).GetExpression(V4));
        Assert.Equal("(1 div 2)", FilterExpressionArithmetic.DivideEuclidean(NumberInt, Number2).GetExpression(V4));
    }

    [Fact]
    public void Collection()
    {
        Assert.Equal("concat(['A','B'],['C','D'])", _multiple1.Concat(_multiple2).GetExpression(V4));
        Assert.Equal("concat(['A','B'],['C','D'])", _multiple1.Concat(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("contains(['A','B'],['C','D'])", _multiple1.Contains(_multiple2).GetExpression(V4));
        Assert.Equal("contains(['A','B'],['C','D'])", _multiple1.Contains(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("endswith(['A','B'],['C','D'])", _multiple1.EndsWith(_multiple2).GetExpression(V4));
        Assert.Equal("endswith(['A','B'],['C','D'])", _multiple1.EndsWith(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("hassubsequence(['A','B'],['C','D'])", _multiple1.HasSubSequence(_multiple2).GetExpression(V4));
        Assert.Equal("hassubsequence(['A','B'],['C','D'])", _multiple1.HasSubSequence(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("hassubset(['A','B'],['C','D'])", _multiple1.HasSubset(_multiple2).GetExpression(V4));
        Assert.Equal("hassubset(['A','B'],['C','D'])", _multiple1.HasSubset(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("indexof(['A','B'],['C','D'])", _multiple1.IndexOf(_multiple2).GetExpression(V4));
        Assert.Equal("indexof(['A','B'],['C','D'])", _multiple1.IndexOf(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("startswith(['A','B'],['C','D'])", _multiple1.StartsWith(_multiple2).GetExpression(V4));
        Assert.Equal("startswith(['A','B'],['C','D'])", _multiple1.StartsWith(new[] { "C", "D" }).GetExpression(V4));
        Assert.Equal("length(['A','B'])", _multiple1.Length().GetExpression(V4));
        Assert.Equal("substring(['A','B'],1)", _multiple1.Substring(1).GetExpression(V4));
        Assert.Equal("substring(['A','B'],1,2)", _multiple1.Substring(1, 2).GetExpression(V4));

        IValueBoolean c = FilterExpressionLogical.EqualTo(_fieldPrimitive, NumberInt);
        Assert.Equal("Friends/all(a:(a/ShowSize eq 1))", _fieldCollection.All(c).GetExpression(V4));
        Assert.Equal("Friends/any(a:(a/ShowSize eq 1))", _fieldCollection.Any(c).GetExpression(V4));
        Assert.Equal("Friends/any(a:a/Feet/any(b:(b/ShowSize eq 1)))", _fieldCollection.Any(_fieldCollection2.Any(c)).GetExpression(V4));
        Assert.Equal("Friends/any()", _fieldCollection.Any().GetExpression(V4));
    }

    [Fact]
    public void Logical()
    {
        Assert.Equal("(true in (true,false))", _boolean1.In(_boolean1, _boolean2).GetExpression(V4));
        Assert.Equal("(1 in (13.37,42,'foo'))", NumberInt.In(13.37, 42, "foo").GetExpression(V4));
        Assert.Equal("(1 in (1,2,3))", NumberInt.In(new List<int> { 1, 2, 3 }).GetExpression(V4));
        Assert.Equal("(1 in Friends)", NumberInt.In(_fieldCollection).GetExpression(V4));
        Assert.Equal("(BestFriend in Friends)", _fieldUntyped.In(_fieldCollection).GetExpression(V4));
        Assert.Equal("(true and false)", _boolean1.And(_boolean2).GetExpression(V4));
        Assert.Equal("(['A','B'] eq ['C','D'])", _multiple1.EqualTo(_multiple2).GetExpression(V4));
        Assert.Equal(
            "(b3e130fe-d72c-4a5b-8dcf-463b497f985c eq b3e130fe-d72c-4a5b-8dcf-463b497f985c)",
            Guid1.EqualTo(Guid1).GetExpression(V4));
        Assert.Equal("(1 gt 2)", NumberInt.GreaterThan(Number2).GetExpression(V4));
        Assert.Equal("(1 lt 2)", NumberInt.LessThan(Number2).GetExpression(V4));
        Assert.Equal("(1 ge 2)", NumberInt.GreaterThanEqual(Number2).GetExpression(V4));
        Assert.Equal("(1 le 2)", NumberInt.LessThanEqual(Number2).GetExpression(V4));
        Assert.Equal("(not true)", _boolean1.Not().GetExpression(V4));
        Assert.Equal("(['A','B'] ne 0.9)", _multiple1.NotEqualTo(NumberFloat).GetExpression(V4));
        Assert.Equal("(true or false)", _boolean1.Or(_boolean2).GetExpression(V4));
        Assert.Equal("('Aaa' eq null)", _string1.EqualToNull().GetExpression(V4));
        Assert.Equal("(not ('Aaa' eq null))", _string1.EqualToNull().Not().GetExpression(V4));
        Assert.Equal("('Aaa' ne null)", _string1.NotEqualToNull().GetExpression(V4));
        Assert.Equal("(OperatingSystem has 'Debian')", FilterExpressionLogical.Has(_fieldComplex, _fieldEnumAnonymous).GetExpression(V4));
    }

    [Fact]
    public void StringFunctions()
    {
        Assert.Equal("concat('Aaa','Bbb')", _string1.Concat(_string2).GetExpression(V4));
        Assert.Equal("contains('Aaa','Bbb')", _string1.Contains(_string2).GetExpression(V4));
        Assert.Equal("substringof('Aaa','Bbb')", _string1.SubstringOf(_string2).GetExpression(V2));
        Assert.Equal("endswith('Aaa','Bbb')", _string1.EndsWith(_string2).GetExpression(V4));
        Assert.Equal("indexof('Aaa','Bbb')", _string1.IndexOf(_string2).GetExpression(V4));
        Assert.Equal("startswith('Aaa','Bbb')", _string1.StartsWith(_string2).GetExpression(V4));
        Assert.Equal("length('Aaa')", _string1.Length().GetExpression(V4));
        Assert.Equal("matchesPattern('Aaa','Bbb')", _string1.Matches(_string2).GetExpression(V4));
        Assert.Equal("substring('Aaa',1,2)", _string1.Substring(1, 2).GetExpression(V4));
        Assert.Equal("substring('Aaa',1)", _string1.Substring(1).GetExpression(V4));
        Assert.Equal("tolower('Aaa')", _string1.ToLower().GetExpression(V4));
        Assert.Equal("toupper('Aaa')", _string1.ToUpper().GetExpression(V4));
        Assert.Equal("trim('Aaa')", _string1.Trim().GetExpression(V4));
    }

    [Fact]
    public void StringLiteral_EscapesQuotes()
    {
        Assert.Equal("'it''s'", IValueString.Literal("it's").GetExpression(V4));
        Assert.Equal("'it''s'", IValueString.Literal("it's").GetExpression(V2));
    }

    [Fact]
    public void Temporal()
    {
        Assert.Equal("date(2001-01-01T13:37:00Z)", DateTimeOffset1.Date().GetExpression(V4));
        Assert.Equal("day(2001-01-01)", Date1.DateDay().GetExpression(V4));
        Assert.Equal("day(2001-01-01T13:37:00Z)", DateTimeOffset1.DateDay().GetExpression(V4));
        Assert.Equal("fractionalseconds(2001-01-01T13:37:00Z)", DateTimeOffset1.TimeFractionalSeconds().GetExpression(V4));
        Assert.Equal("fractionalseconds(13:37:00)", TimeOfDay.TimeFractionalSeconds().GetExpression(V4));
        Assert.Equal("hour(2001-01-01T13:37:00Z)", DateTimeOffset1.TimeHour().GetExpression(V4));
        Assert.Equal("minute(2001-01-01T13:37:00Z)", DateTimeOffset1.TimeMinute().GetExpression(V4));
        Assert.Equal("minute(13:37:00)", TimeOfDay.TimeMinute().GetExpression(V4));
        Assert.Equal("month(2001-01-01)", Date1.DateMonth().GetExpression(V4));
        Assert.Equal("month(2001-01-01T13:37:00Z)", DateTimeOffset1.DateMonth().GetExpression(V4));
        Assert.Equal("hour(13:37:00)", TimeOfDay.TimeHour().GetExpression(V4));
        Assert.Equal("second(2001-01-01T13:37:00Z)", DateTimeOffset1.TimeSecond().GetExpression(V4));
        Assert.Equal("second(13:37:00)", TimeOfDay.TimeSecond().GetExpression(V4));
        Assert.Equal("time(2001-01-01T13:37:00Z)", DateTimeOffset1.Time().GetExpression(V4));
        Assert.Equal("totaloffsetminutes(2001-01-01T13:37:00Z)", DateTimeOffset1.OffsetMinutes().GetExpression(V4));
        Assert.Equal("totaloffsetseconds(duration'PT24H')", Duration1.OffsetSeconds().GetExpression(V4));
        Assert.Equal("year(2001-01-01)", Date1.DateYear().GetExpression(V4));
        Assert.Equal("year(2001-01-01T13:37:00Z)", DateTimeOffset1.DateYear().GetExpression(V4));
    }

    [Fact]
    public void GlobalFunctions()
    {
        Assert.Equal("maxdatetime()", FilterExpressionTemporal.MaxDateTime().GetExpression(V4));
        Assert.Equal("mindatetime()", FilterExpressionTemporal.MinDateTime().GetExpression(V4));
        Assert.Equal("now()", FilterExpressionTemporal.Now().GetExpression(V4));
    }

    [Fact]
    public void CreateOperand_MapsPrimitives()
    {
        Assert.IsAssignableFrom<IValueString>(Expressions.CreateOperand("A"));
        Assert.IsAssignableFrom<IValueBoolean>(Expressions.CreateOperand(true));
        Assert.IsAssignableFrom<IValueGuid>(Expressions.CreateOperand(Guid.NewGuid()));
        Assert.IsAssignableFrom<IValueNumeric>(Expressions.CreateOperand(42));
        Assert.IsAssignableFrom<IValueNumeric>(Expressions.CreateOperand(42.0));
        Assert.IsAssignableFrom<IValueDuration>(Expressions.CreateOperand(TimeSpan.FromMinutes(1)));
        Assert.IsAssignableFrom<IValueDateTimeOffset>(Expressions.CreateOperand(DateTimeOffset.Now));
        Assert.IsAssignableFrom<IValueDateTime>(Expressions.CreateOperand(DateTime.Now));
        Assert.IsAssignableFrom<IValueDate>(Expressions.CreateOperand(DateOnly.FromDateTime(DateTime.Now)));
        Assert.IsAssignableFrom<IValueTimeOfDay>(Expressions.CreateOperand(TimeOnly.FromDateTime(DateTime.Now)));
        Assert.IsAssignableFrom<IValueBinary>(Expressions.CreateOperand(new byte[] { 1 }));
        Assert.IsAssignableFrom<IOperandSingle>(Expressions.CreateOperand(null));
        Assert.Same(NumberFloat, Expressions.CreateOperand(NumberFloat));
        Assert.Throws<ArgumentException>(() => Expressions.CreateOperand(this));
        Assert.Equal(FieldReference.OfPath("Friends/NestedFriends").FieldName, FieldReference.OfPath("Friends", "NestedFriends").FieldName);
    }

    [Fact]
    public void Operands_AreExposed()
    {
        var f2 = FilterExpressionTemporal.Now();
        Assert.Empty(f2.Operands);
        Assert.Equal("now", f2.FilterOperator);
        var f3 = FilterExpressionTemporal.Date(DateTimeOffset1);
        Assert.Equal([DateTimeOffset1], f3.Operands);
        var f4 = FilterExpressionString.Substring(_string1, NumberInt);
        Assert.Equal<IOperand>([_string1, NumberInt], f4.Operands);
        var f5 = FilterExpressionArray.Substring(_fieldCollection, NumberInt, Number2);
        Assert.Equal<IOperand>([_fieldCollection, NumberInt, Number2], f5.Operands);
        var f6 = FilterExpressionLogical.And(_boolean1, _boolean2);
        Assert.Equal<IOperand>([_boolean1, _boolean2], f6.Operands);
    }

    [Fact]
    public void UntypedExpressions()
    {
        var field1 = FieldReference.Of("FirstName");
        var field2 = FieldReference.Of("Age");
        var expression = field1.EqualTo("Foobar").And(field2.GreaterThan(42)).GetExpression(V4);
        Assert.Equal("((FirstName eq 'Foobar') and (Age gt 42))", expression);
        var read = new ODataRequestRead("/service/path", "EntityName", "$filter=" + expression, V4);
        Assert.Equal("$filter=((FirstName eq 'Foobar') and (Age gt 42))", read.GetRequestQuery());
    }

    [Fact]
    public void TypedExpressions()
    {
        var field1 = FieldReference.Of("FirstName");
        var field2 = FieldReference.Of("Age");
        var field3 = FieldReference.Of("IsRetired");
        Assert.IsAssignableFrom<IValueBoolean>(field1.AsBoolean());
        Assert.IsAssignableFrom<IValueBinary>(field1.AsBinary());
        Assert.IsAssignableFrom<IValueString>(field1.AsString());
        Assert.IsAssignableFrom<IValueNumeric>(field1.AsNumber());
        Assert.IsAssignableFrom<IValueArray>(field1.AsArray());
        Assert.IsAssignableFrom<IValueDate>(field1.AsDate());
        Assert.IsAssignableFrom<IValueDateTimeOffset>(field1.AsDateTimeOffset());
        Assert.IsAssignableFrom<IValueDuration>(field1.AsDuration());
        Assert.IsAssignableFrom<IValueTimeOfDay>(field1.AsTimeOfDay());

        var condition1 = field1.AsString().Substring(1, 5).EqualTo("ooba");
        var condition2 = field2.AsNumber().Modulo(10).EqualTo(0);
        var condition3 = field3.AsBoolean().Or(false);
        Assert.Equal("((substring(FirstName,1,5) eq 'ooba') and ((Age mod 10) eq 0))", condition1.And(condition2).GetExpression(V4));
        Assert.Equal("((substring(FirstName,1,5) eq 'ooba') or (IsRetired or false))", condition1.Or(condition3).GetExpression(V4));
    }

    [Fact]
    public void NumberAndBinaryAndBooleanAndStringExpressions()
    {
        var field = FieldReference.Of("Field");
        Assert.Equal("(Field divby 3)", field.AsNumber().Divide(3).GetExpression(V4));
        Assert.Equal("(Field mul Field)", field.AsNumber().Multiply(field.AsNumber()).GetExpression(V4));
        Assert.Equal("(Field add 3)", field.AsNumber().Add(3).GetExpression(V4));
        Assert.Equal("(Field sub Field)", field.AsNumber().Subtract(field.AsNumber()).GetExpression(V4));
        Assert.Equal("(Field mod 3)", field.AsNumber().Modulo(3).GetExpression(V4));
        Assert.Equal("(Field eq binary'AQID')", field.AsBinary().EqualTo(new byte[] { 1, 2, 3 }).GetExpression(V4));
        Assert.Equal("(Field and true)", field.AsBoolean().And(true).GetExpression(V4));
        Assert.Equal("(Field or Field)", field.AsBoolean().Or(field.AsBoolean()).GetExpression(V4));
        Assert.Equal("(not Field)", field.AsBoolean().Not().GetExpression(V4));
        Assert.Equal("concat(Field,'abc')", field.AsString().Concat("abc").GetExpression(V4));
        Assert.Equal("startswith(Field,Field)", field.AsString().StartsWith(field.AsString()).GetExpression(V4));
        Assert.Equal("endswith(Field,'abc')", field.AsString().EndsWith("abc").GetExpression(V4));
        Assert.Equal("contains(Field,'abc')", field.AsString().Contains("abc").GetExpression(V4));
        Assert.Equal("substringof(Field,'abc')", field.AsString().SubstringOf("abc").GetExpression(V2));
        Assert.Equal("indexof(Field,'abc')", field.AsString().IndexOf("abc").GetExpression(V4));
        Assert.Equal("substring(Field,1,3)", field.AsString().Substring(1, 3).GetExpression(V4));
        Assert.Equal("matchesPattern(Field,'abc')", field.AsString().Matches("abc").GetExpression(V4));
    }

    [Fact]
    public void LogicalExpressionsOnUntypedFields()
    {
        var field = FieldReference.Of("Field");
        Assert.Equal("(Field eq 'abc')", field.EqualTo("abc").GetExpression(V4));
        Assert.Equal("(Field ne 123)", field.NotEqualTo(123).GetExpression(V4));
        Assert.Equal("(Field eq Field)", field.EqualTo(field).GetExpression(V4));
        Assert.Equal("(Field gt Field)", field.GreaterThan(field).GetExpression(V4));
        Assert.Equal("(Field ge 123)", field.GreaterThanEqual(123).GetExpression(V4));
        Assert.Equal("(Field lt 'abc')", field.LessThan("abc").GetExpression(V4));
        Assert.Equal("(Field le Field)", field.LessThanEqual(field).GetExpression(V4));
        Assert.Equal("(Field in ('abc','foo'))", field.In("abc", "foo").GetExpression(V4));
        Assert.Equal("(Field in (1,2,3))", field.In(new List<int> { 1, 2, 3 }).GetExpression(V4));
        Assert.Equal("(Field in Field)", field.In(field.AsArray()).GetExpression(V4));
    }

    [Fact]
    public void ComplexLogicalExpressions()
    {
        var field = FieldReference.Of("Field");
        var cond1 = field.EqualTo(1);
        var cond2 = field.EqualTo(2);
        var cond1Exp = cond1.GetExpression(V2);
        var cond2Exp = cond2.GetExpression(V2);
        Assert.Equal($"(not {cond1Exp})", cond1.Not().GetExpression(V2));
        Assert.Equal($"(not {cond1Exp})", FilterExpressionLogical.Not(cond1).GetExpression(V2));
        Assert.Equal($"((not {cond1Exp}) and {cond2Exp})", cond1.Not().And(cond2).GetExpression(V2));
        Assert.Equal($"((not {cond1Exp}) or {cond2Exp})", cond1.Not().Or(cond2).GetExpression(V2));
        Assert.Equal($"(not ({cond1Exp} or {cond2Exp}))", cond1.Or(cond2).Not().GetExpression(V2));
    }

    [Fact]
    public void NullOperands_InComplexExpression()
    {
        var field1 = FieldReference.Of("FirstName");
        var field2 = FieldReference.Of("Age");
        var expression = field1.EqualTo(IValueString.Null).And(field2.GreaterThan(IValueNumeric.Null)).Or(IValueBoolean.Null).GetExpression(V4);
        Assert.Equal("(((FirstName eq null) and (Age gt null)) or null)", expression);
        var read = new ODataRequestRead("/service/path", "EntityName", "$filter=" + expression, V4);
        Assert.Equal("$filter=(((FirstName eq null) and (Age gt null)) or null)", read.GetRequestQuery());
        Assert.Equal("((FirstName eq null) and null)", field1.EqualTo(IValueString.Null).And(IValueBoolean.Null).GetExpression(V4));
        Assert.Equal("(contains(FirstName,null) or null)", field1.AsString().Contains(IValueString.Null).Or(IValueBoolean.Null).GetExpression(V4));
    }

    [Fact]
    public void NullOperands_InLogicalStringCollectionAndArithmeticExpressions()
    {
        var field1 = FieldReference.Of("FirstName");
        var field2 = FieldReference.Of("Age");
        Assert.Equal("(null ne 123)", IValueNumeric.Null.NotEqualTo(123).GetExpression(V4));
        Assert.Equal("(null ge 'abc')", IValueString.Null.GreaterThanEqual("abc").GetExpression(V4));
        Assert.Equal("(null lt null)", IValueNumeric.Null.LessThan(IValueNumeric.Null).GetExpression(V4));
        Assert.Equal("substringof(FirstName,null)", field1.AsString().SubstringOf(IValueString.Null).GetExpression(V2));
        Assert.Equal("concat(FirstName,null)", field1.AsString().Concat(IValueString.Null).GetExpression(V4));
        Assert.Equal("tolower(null)", IValueString.Null.ToLower().GetExpression(V4));
        Assert.Equal("substring(null,1)", IValueString.Null.Substring(1).GetExpression(V4));
        Assert.Equal("matchesPattern(FirstName,null)", field1.AsString().Matches(IValueString.Null).GetExpression(V4));
        Assert.Equal("concat(null,null)", IValueArray.Null.Concat(IValueArray.Null).GetExpression(V4));
        Assert.Equal("contains(['A','B'],null)", IValueArray.Literal(new[] { "A", "B" }).Contains(IValueArray.Null).GetExpression(V4));
        Assert.Equal("substring(null,1)", IValueArray.Null.Substring(1).GetExpression(V4));
        Assert.Equal("(null divby 3)", IValueNumeric.Null.Divide(3).GetExpression(V4));
        Assert.Equal("(null mul null)", IValueNumeric.Null.Multiply(IValueNumeric.Null).GetExpression(V4));
        Assert.Equal("(Age add null)", field2.AsNumber().Add(IValueNumeric.Null).GetExpression(V4));
        Assert.Equal("(Age mod null)", field2.AsNumber().Modulo(IValueNumeric.Null).GetExpression(V4));
    }

    [Fact]
    public void EntityKey_Encoding()
    {
        var keyV2 = new ODataEntityKey(V2).AddKeyProperty("key", "/? #&value%$");
        var keyV4 = new ODataEntityKey(V4).AddKeyProperty("key", "/? #&value%$");
        Assert.Equal("('%2F%3F%20%23&value%25$')", keyV2.ToEncodedString());
        Assert.Equal("('%2F%3F%20%23&value%25$')", keyV4.ToEncodedString());
    }

    [Fact]
    public void EntityKey_EncodingForComplexKeys()
    {
        foreach (var protocol in new[] { V2, V4 })
        {
            var key = new ODataEntityKey(protocol).AddKeyProperty("key1#&%$", "/? #&value%$").AddKeyProperty("key2", "value");
            Assert.Equal("(key1#&%$='%2F%3F%20%23&value%25$',key2='value')", key.ToEncodedString());
        }
    }

    [Fact]
    public void EntityKey_NoEncodingForSafeChars()
    {
        var key = new ODataEntityKey(V4).AddKeyProperty("key1", "-._~!$'(").AddKeyProperty("key2", ")*,;&=@:");
        Assert.Equal("(key1='-._~!$''(',key2=')*,;&=@:')", key.ToEncodedString());
    }

    [Fact]
    public void EntityKey_QuoteEscaping()
    {
        Assert.Equal("('valu''e')", new ODataEntityKey(V2).AddKeyProperty("key", "valu'e").ToEncodedString());
        Assert.Equal("('valu''e')", new ODataEntityKey(V4).AddKeyProperty("key", "valu'e").ToEncodedString());
    }

    [Fact]
    public void EntityKey_EmptyAndNull()
    {
        var empty = new ODataEntityKey(V4);
        Assert.Equal("()", empty.ToString());
        Assert.Equal("()", empty.ToEncodedString());
        Assert.Equal("(null)", new ODataEntityKey(V2).AddKeyProperty("key", null).ToEncodedString());
        Assert.Equal("(null)", new ODataEntityKey(V4).AddKeyProperty("key", null).ToEncodedString());
    }

    [Fact]
    public void EntityKey_DuplicatePropertyIsRejected()
    {
        var key = new ODataEntityKey(V4).AddKeyProperty("a", 1);
        Assert.Throws<ArgumentException>(() => key.AddKeyProperty("a", 2));
        Assert.Equal(["a"], key.FieldNames);
    }

    [Fact]
    public void EntityKey_DataTypeSerialisationV4()
    {
        var key = new ODataEntityKey(V4)
            .AddKeyProperty("stringParameter", "test")
            .AddKeyProperty("booleanParameter", true)
            .AddKeyProperty("integerParameter", 9000)
            .AddKeyProperty("decimalParameter", 3.14)
            .AddKeyProperty("durationParameter", TimeSpan.FromHours(8))
            .AddKeyProperty("dateTimeParameter", new DateTime(2019, 12, 25, 8, 0, 0));
        Assert.Equal(
            "(stringParameter='test',booleanParameter=true,integerParameter=9000,decimalParameter=3.14,durationParameter=duration'PT8H',dateTimeParameter=2019-12-25T08:00:00Z)",
            key.ToString());
    }
}
