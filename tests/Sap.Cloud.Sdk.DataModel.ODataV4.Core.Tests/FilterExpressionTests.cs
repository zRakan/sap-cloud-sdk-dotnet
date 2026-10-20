using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>Filter expressions that are available on the typed property descriptors. Ports <c>FilterableOperandsTest</c>.</summary>
public class FilterExpressionTests
{
    private static string V4(IOperand operand) => operand.GetExpression(ODataProtocol.V4);

    private static readonly SimpleProperty.String<TestEntity> Name = SimpleProperty.String<TestEntity>.Of("Name");
    private static readonly SimpleProperty.NumericInteger<TestEntity> Age = SimpleProperty.NumericInteger<TestEntity>.Of("Age");
    private static readonly SimpleProperty.NumericDecimal<TestEntity> Size = SimpleProperty.NumericDecimal<TestEntity>.Of("Size");
    private static readonly SimpleProperty.Boolean<TestEntity> Alive = SimpleProperty.Boolean<TestEntity>.Of("Alive");
    private static readonly SimpleProperty.Duration<TestEntity> ResponseTime = SimpleProperty.Duration<TestEntity>.Of("ResponseTime");
    private static readonly SimpleProperty.DateTime<TestEntity> DateTimeField = SimpleProperty.DateTime<TestEntity>.Of("Registration");
    private static readonly SimpleProperty.Date<TestEntity> DateField = SimpleProperty.Date<TestEntity>.Of("Registration");
    private static readonly SimpleProperty.Time<TestEntity> TimeField = SimpleProperty.Time<TestEntity>.Of("Registration");
    private static readonly SimpleProperty.Collection<TestEntity, int> Telephone = SimpleProperty.Collection<TestEntity, int>.Of("TelephoneNumber");
    private static readonly SimpleProperty.Collection<TestEntity, int> AreaCode = SimpleProperty.Collection<TestEntity, int>.Of("AreaCode");
    private static readonly NavigationProperty.Collection<TestEntity, TestEntity> Friends = NavigationProperty.Collection<TestEntity, TestEntity>.Of("Friends");

    [Fact]
    public void BooleanOperators()
    {
        Assert.Equal("(((not ((Alive and true) and Alive)) or true) or Alive)", V4(Alive.And(true).And(Alive).Not().Or(true).Or(Alive)));
    }

    [Fact]
    public void Collection()
    {
        Assert.Equal("Friends/all(a:(a/Alive eq true))", V4(Friends.All(Alive.EqualTo(true))));
        Assert.Equal("Friends/any(a:(a/Alive eq true))", V4(Friends.Any(Alive.EqualTo(true))));
        Assert.Equal("concat(concat(AreaCode,TelephoneNumber),[4,2])", V4(AreaCode.Concat(Telephone).Concat(new[] { 4, 2 })));
        Assert.Equal("substring(substring(AreaCode,1),2,3)", V4(AreaCode.Substring(1).Substring(2, 3)));
        Assert.Equal("contains(TelephoneNumber,AreaCode)", V4(Telephone.Contains(AreaCode)));
        Assert.Equal("contains(TelephoneNumber,[0,3,0])", V4(Telephone.Contains(new[] { 0, 3, 0 })));
        Assert.Equal("endswith(TelephoneNumber,[4,2])", V4(Telephone.EndsWith(new[] { 4, 2 })));
        Assert.Equal("endswith(TelephoneNumber,TelephoneNumber)", V4(Telephone.EndsWith(Telephone)));
        Assert.Equal("startswith(TelephoneNumber,[0,3,0])", V4(Telephone.StartsWith(new[] { 0, 3, 0 })));
        Assert.Equal("startswith(TelephoneNumber,TelephoneNumber)", V4(Telephone.StartsWith(Telephone)));
        Assert.Equal("hassubsequence(TelephoneNumber,AreaCode)", V4(Telephone.HasSubSequence(AreaCode)));
        Assert.Equal("hassubsequence(TelephoneNumber,[0,3,0])", V4(Telephone.HasSubSequence(new[] { 0, 3, 0 })));
        Assert.Equal("hassubset(TelephoneNumber,AreaCode)", V4(Telephone.HasSubset(AreaCode)));
        Assert.Equal("hassubset(TelephoneNumber,[0,3,0])", V4(Telephone.HasSubset(new[] { 0, 3, 0 })));
        Assert.Equal("indexof(TelephoneNumber,AreaCode)", V4(Telephone.IndexOf(AreaCode)));
        Assert.Equal("indexof(TelephoneNumber,[0,3,0])", V4(Telephone.IndexOf(new[] { 0, 3, 0 })));
        Assert.Equal("length(TelephoneNumber)", V4(Telephone.Length()));
    }

    [Fact]
    public void ComplexValues()
    {
        var operatingSystem = ComplexProperty.Single<TestEntity, CityCountry>.Of("OperatingSystem");
        var animal = SimpleProperty.Enum<TestEntity, PersonGender>.Of("Animal", "OData.Test.AnimalType");

        Assert.Equal("(OperatingSystem has 'Debian')", V4(operatingSystem.Has("Debian")));
        Assert.Equal("(OperatingSystem has Animal)", V4(operatingSystem.Has(animal)));
        Assert.Equal("(OperatingSystem eq null)", V4(operatingSystem.EqualToNull()));
        Assert.Equal("(OperatingSystem ne null)", V4(operatingSystem.NotEqualToNull()));
    }

    [Fact]
    public void Time()
    {
        Assert.Equal("fractionalseconds(Registration)", V4(TimeField.TimeFractionalSeconds()));
        Assert.Equal("second(Registration)", V4(TimeField.TimeSecond()));
        Assert.Equal("minute(Registration)", V4(TimeField.TimeMinute()));
        Assert.Equal("hour(Registration)", V4(TimeField.TimeHour()));
    }

    [Fact]
    public void Date()
    {
        Assert.Equal(
            "(((Registration add duration'PT24H') sub duration'PT48H') sub 2001-01-01)",
            V4(DateField.Add(TimeSpan.FromDays(1)).Subtract(TimeSpan.FromDays(2)).Difference(IValueDate.Literal(new DateOnly(2001, 1, 1)))));
        Assert.Equal(
            "(((Registration add ResponseTime) sub ResponseTime) sub Registration)",
            V4(DateField.Add(ResponseTime).Subtract(ResponseTime).Difference(DateField)));
        Assert.Equal("day(Registration)", V4(DateField.DateDay()));
        Assert.Equal("month(Registration)", V4(DateField.DateMonth()));
        Assert.Equal("year(Registration)", V4(DateField.DateYear()));
    }

    [Fact]
    public void DateTime()
    {
        Assert.Equal(
            "((Registration add duration'PT24H') sub duration'PT48H')",
            V4(DateTimeField.Add(TimeSpan.FromDays(1)).Subtract(TimeSpan.FromDays(2))));
        Assert.Equal("((Registration add ResponseTime) sub ResponseTime)", V4(DateTimeField.Add(ResponseTime).Subtract(ResponseTime)));
        Assert.Equal("date(Registration)", V4(DateTimeField.Date()));
        Assert.Equal("time(Registration)", V4(DateTimeField.Time()));
        Assert.Equal("fractionalseconds(Registration)", V4(DateTimeField.TimeFractionalSeconds()));
        Assert.Equal("hour(Registration)", V4(DateTimeField.TimeHour()));
        Assert.Equal("minute(Registration)", V4(DateTimeField.TimeMinute()));
        Assert.Equal("second(Registration)", V4(DateTimeField.TimeSecond()));
        Assert.Equal("day(Registration)", V4(DateTimeField.DateDay()));
        Assert.Equal("month(Registration)", V4(DateTimeField.DateMonth()));
        Assert.Equal("year(Registration)", V4(DateTimeField.DateYear()));
        Assert.Equal("totaloffsetminutes(Registration)", V4(DateTimeField.OffsetMinutes()));
    }

    [Fact]
    public void DateTimeLiteralsKeepTheirOffset()
    {
        var filter = DateTimeField.GreaterThan(new DateTimeOffset(2020, 2, 20, 3, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal("(Registration gt 2020-02-20T03:00:00+01:00)", V4(filter));
    }

    [Fact]
    public void Duration()
    {
        var number = SimpleProperty.NumericDecimal<TestEntity>.Of("Size");

        Assert.Equal("-(((ResponseTime add ResponseTime) sub ResponseTime))", V4(ResponseTime.Add(ResponseTime).Subtract(ResponseTime).Negate()));
        Assert.Equal("((ResponseTime add duration'PT24H') sub duration'PT48H')", V4(ResponseTime.Add(TimeSpan.FromDays(1)).Subtract(TimeSpan.FromDays(2))));
        Assert.Equal("((ResponseTime div 2) mul 3)", V4(ResponseTime.Divide(2).Multiply(3)));
        Assert.Equal("((ResponseTime div Size) mul Size)", V4(ResponseTime.Divide(number).Multiply(number)));
        Assert.Equal("totaloffsetseconds(ResponseTime)", V4(ResponseTime.OffsetSeconds()));
    }

    [Fact]
    public void Numbers()
    {
        Assert.Equal("-(((Age add Age) sub Age))", V4(Age.Add(Age).Subtract(Age).Negate()));
        Assert.Equal("-(((Size add Size) sub Size))", V4(Size.Add(Size).Subtract(Size).Negate()));
        Assert.Equal("((Age add 1) sub 2)", V4(Age.Add(1).Subtract(2)));
        Assert.Equal("((Age mul Age) divby Age)", V4(Age.Multiply(Age).Divide(Age)));
        Assert.Equal("((Size mul Size) divby Size)", V4(Size.Multiply(Size).Divide(Size)));
        Assert.Equal("((Age mul 1) divby 2)", V4(Age.Multiply(1).Divide(2)));
        Assert.Equal("((Age mod Age) mod 3)", V4(Age.Modulo(Age).Modulo(3)));
        Assert.Equal("((Size mod Size) mod 3.0)", V4(Size.Modulo(Size).Modulo(3.0)));
        Assert.Equal("(Size in (42,13.37,9000.0))", V4(Size.In(42, 13.37, 9000f)));
        Assert.Equal("ceiling(Size)", V4(Size.Ceil()));
        Assert.Equal("floor(Size)", V4(Size.Floor()));
        Assert.Equal("round(Size)", V4(Size.Round()));
        Assert.Equal(
            "((((Age eq Age) and (Age eq 42)) and (Age ne Age)) and (Age ne 42))",
            V4(Age.EqualTo(Age).And(Age.EqualTo(42)).And(Age.NotEqualTo(Age)).And(Age.NotEqualTo(42))));
        Assert.Equal(
            "((((Age gt Age) and (Age gt 42)) and (Age ge Age)) and (Age ge 42))",
            V4(Age.GreaterThan(Age).And(Age.GreaterThan(42)).And(Age.GreaterThanEqual(Age)).And(Age.GreaterThanEqual(42))));
        Assert.Equal(
            "((((Age lt Age) and (Age lt 42)) and (Age le Age)) and (Age le 42))",
            V4(Age.LessThan(Age).And(Age.LessThan(42)).And(Age.LessThanEqual(Age)).And(Age.LessThanEqual(42))));
    }

    [Fact]
    public void GuidLiteral()
    {
        var id = SimpleProperty.Guid<TestEntity>.Of("Id");

        Assert.Equal(
            "(Id eq b3e130fe-d72c-4a5b-8dcf-463b497f985c)",
            V4(id.EqualTo(System.Guid.Parse("b3e130fe-d72c-4a5b-8dcf-463b497f985c"))));
    }

    [Fact]
    public void Strings()
    {
        Assert.Equal("(Name eq null)", V4(Name.EqualTo((string?)null)));
        Assert.Equal("(Name eq null)", V4(Name.EqualToNull()));
        Assert.Equal("(not (Name eq null))", V4(Name.EqualToNull().Not()));
        Assert.Equal("(Name ne null)", V4(Name.NotEqualToNull()));
        Assert.Equal("indexof(Name,Name)", V4(Name.IndexOf(Name)));
        Assert.Equal("indexof(Name,'Foo')", V4(Name.IndexOf("Foo")));
        Assert.Equal("length(Name)", V4(Name.Length()));
        Assert.Equal("matchesPattern(Name,'Foo')", V4(Name.Matches("Foo")));
        Assert.Equal("tolower(toupper(Name))", V4(Name.ToUpper().ToLower()));
        Assert.Equal("trim(concat(concat(Name,Name),'Foo'))", V4(Name.Concat(Name).Concat("Foo").Trim()));
        Assert.Equal("substring(substring(Name,1),2,3)", V4(Name.Substring(1).Substring(2, 3)));
        Assert.Equal("(startswith(Name,Name) and startswith(Name,'Foo'))", V4(Name.StartsWith(Name).And(Name.StartsWith("Foo"))));
        Assert.Equal("(endswith(Name,Name) and endswith(Name,'Foo'))", V4(Name.EndsWith(Name).And(Name.EndsWith("Foo"))));
        Assert.Equal("(contains(Name,'Foo') and contains(Name,Name))", V4(Name.Contains("Foo").And(Name.Contains(Name))));
        Assert.Equal("(Name eq 'Qu''te')", V4(Name.EqualTo("Qu'te")));
    }

    [Fact]
    public void EnumMembers()
    {
        var language = SimpleProperty.Enum<TestEntity, PersonGender>.Of("ProgrammingLanguage", "SoftwareEngineer.Programming.Language");

        Assert.Equal("(ProgrammingLanguage eq null)", V4(language.EqualToNull()));
        Assert.Equal("(ProgrammingLanguage ne null)", V4(language.NotEqualToNull()));
        Assert.Equal("(ProgrammingLanguage eq null)", V4(language.EqualTo((PersonGender?)null)));
        Assert.Equal("(ProgrammingLanguage ne null)", V4(language.NotEqualTo((PersonGender?)null)));
        Assert.Equal("(ProgrammingLanguage eq SoftwareEngineer.Programming.Language'Male')", V4(language.EqualTo(PersonGender.Male)));
        Assert.Equal("(ProgrammingLanguage ne SoftwareEngineer.Programming.Language'Male')", V4(language.NotEqualTo(PersonGender.Male)));
        Assert.Equal("(ProgrammingLanguage eq ProgrammingLanguage)", V4(language.EqualTo(language)));
        Assert.Equal("(ProgrammingLanguage ne ProgrammingLanguage)", V4(language.NotEqualTo(language)));
    }

    [Fact]
    public void Properties_KnowTheirEntityAndName()
    {
        Assert.Equal(typeof(TestEntity), Name.EntityType);
        Assert.Equal("Name", Name.FieldName);
        Assert.Equal(typeof(int), Telephone.ItemType);
        Assert.Equal(typeof(TestEntity), Friends.ItemType);
        Assert.Equal("Name", Name.ToString());
    }
}
