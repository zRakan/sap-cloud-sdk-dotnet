using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>The JSON converters of the EDM value types (Java: LocalTimeAdapterTest, ZonedDateTimeAdapterTest, LocalDateTimeAdapterTest, ODataCustomFieldAdapterTest).</summary>
public class AdapterTests
{
    private static readonly JsonSerializerOptions Options = ODataVdmJsonOptions.Get();

    private static T? Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    [Theory]
    [InlineData("\"PT13H20M\"", "13:20:00")]
    [InlineData("\"PT22M\"", "00:22:00")]
    [InlineData("\"PT15H\"", "15:00:00")]
    [InlineData("\"PT54S\"", "00:00:54")]
    [InlineData("\"PT54.123S\"", "00:00:54.1230000")]
    [InlineData("\"P11Y22M33DT13H22M12.345S\"", "13:22:12.3450000")]
    [InlineData("\"P11Y22M33DT\"", "00:00:00")]
    [InlineData("\"PT07H30M00S\"", "07:30:00")]
    public void Time_IsParsed(string json, string expected) =>
        Assert.Equal(TimeOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Read<TimeOnly?>(json));

    [Theory]
    [InlineData("\"PT\"")]
    [InlineData("1234")]
    [InlineData("\"not a time\"")]
    [InlineData("\"PT0.0000005S\"")]
    public void Time_ThatCannotBeConverted_IsNull(string json) => Assert.Null(Read<TimeOnly?>(json));

    [Fact]
    public void Time_IsWritten()
    {
        Assert.Equal("\"PT1H2M3S\"", Write(new TimeOnly(1, 2, 3)));
        Assert.Equal("\"PT14H54M32.123S\"", Write(new TimeOnly(14, 54, 32, 123)));
        Assert.Equal("\"PT0H0M0.5S\"", Write(new TimeOnly(0, 0, 0, 500)));
        Assert.Equal("\"PT0H0M0.005S\"", Write(new TimeOnly(0, 0, 0, 5)));
    }

    [Fact]
    public void DateTime_IsParsedAndWritten()
    {
        Assert.Equal(new DateTime(2022, 4, 11, 9, 32, 55, 192), Read<DateTime?>("\"/Date(1649669575192)/\""));
        Assert.Equal(new DateTime(1969, 12, 31, 23, 59, 59, 999), Read<DateTime?>("\"/Date(-1)/\""));
        Assert.Equal("\"/Date(1649677015123)/\"", Write(new DateTime(2022, 4, 11, 11, 36, 55, 123)));
        Assert.Equal("\"/Date(-1)/\"", Write(new DateTime(1969, 12, 31, 23, 59, 59, 999)));
    }

    [Theory]
    [InlineData("\"Something that is not a date\"")]
    [InlineData("\"/Date(100000000000000000000000)/\"")]
    [InlineData("\"2022-04-11T12:47:14\"")]
    [InlineData("1234")]
    public void DateTime_ThatCannotBeConverted_IsNull(string json)
    {
        Assert.Null(Read<DateTime?>(json));
        Assert.Null(Read<DateTimeOffset?>(json));
    }

    [Fact]
    public void DateTimeOffset_IsParsed()
    {
        Assert.Equal(new DateTimeOffset(2022, 4, 11, 9, 32, 55, 192, TimeSpan.Zero), Read<DateTimeOffset?>("\"/Date(1649669575192)/\""));
        var withOffset = Read<DateTimeOffset?>("\"/Date(1649669575192+0120)/\"")!.Value;
        Assert.Equal(new DateTimeOffset(2022, 4, 11, 9, 32, 55, 192, TimeSpan.FromHours(2)), withOffset);
        Assert.Equal(TimeSpan.FromHours(2), withOffset.Offset);
        Assert.Equal(TimeSpan.FromMinutes(-300), Read<DateTimeOffset?>("\"/Date(346833000-300)/\"")!.Value.Offset);
    }

    [Fact]
    public void DateTimeOffset_WithTooLargeOffset_IsNull() => Assert.Null(Read<DateTimeOffset?>("\"/Date(1649669575192+1441)/\""));

    [Fact]
    public void DateTimeOffset_IsWritten()
    {
        Assert.Equal("\"/Date(1649677015123)/\"", Write(new DateTimeOffset(2022, 4, 11, 11, 36, 55, 123, TimeSpan.Zero)));
        Assert.Equal("\"/Date(1649677015123+0120)/\"", Write(new DateTimeOffset(2022, 4, 11, 11, 36, 55, 123, TimeSpan.FromHours(2))));
        Assert.Equal("\"/Date(694224000000-0240)/\"", Write(new DateTimeOffset(1992, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4))));
    }

    [Fact]
    public void Decimal_IsWrittenInPlainNotation_AndReadFromNumbersAndStrings()
    {
        Assert.Equal("\"9223372036854775808\"", Write(9223372036854775808m));
        Assert.Equal(9223372036854775808m, Read<decimal?>("9.223372036854775808E18"));
        Assert.Equal(123456.789m, Read<decimal?>("\"123456.789\""));
        Assert.Null(Read<decimal?>("\"abc\""));
    }

    [Fact]
    public void Doubles_CanBeReadFromSpecialValues()
    {
        Assert.Equal(double.NaN, Read<double?>("\"NaN\""));
        Assert.Equal(double.PositiveInfinity, Read<double?>("\"INF\""));
        Assert.Equal(3.14f, Read<float?>("3.14"));
        Assert.Equal("\"3.14\"", Write(3.14f));
    }

    private static object? ReadCustom(string json)
    {
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        reader.Read();
        return ODataCustomFieldReader.Read(ref reader);
    }

    [Fact]
    public void CustomFieldReader_ReadsPrimitives()
    {
        Assert.Equal(int.MaxValue, ReadCustom("2147483647"));
        Assert.Equal(long.MaxValue, ReadCustom("9223372036854775807"));
        Assert.Equal(double.MaxValue, ReadCustom("1.7976931348623157E308"));
        Assert.Equal(1.5, ReadCustom("1.5"));
        Assert.Equal(1, ReadCustom("1.0"));
        Assert.Equal(true, ReadCustom("true"));
        Assert.Equal("foo", ReadCustom("\"foo\""));
        Assert.Null(ReadCustom("null"));
    }

    [Fact]
    public void CustomFieldReader_ParsesDateStrings()
    {
        Assert.Equal(DateTimeOffset.Parse("2022-02-02T04:20:00Z", System.Globalization.CultureInfo.InvariantCulture), ReadCustom("\"/Date(1643775600000)/\""));
        Assert.Null(ReadCustom("\"/Date(" + long.MaxValue + "9)/\""));
        Assert.Equal("/Date(2022-02-02T04:20:00Z)/", ReadCustom("\"/Date(2022-02-02T04:20:00Z)/\""));
    }

    [Fact]
    public void CustomFieldReader_ReadsArraysAndObjects()
    {
        Assert.Equal(new List<object?> { "foo", "bar" }, Assert.IsType<List<object?>>(ReadCustom("[\"foo\", \"bar\"]")));
        var map = Assert.IsType<Dictionary<string, object?>>(ReadCustom("{\"foo\": \"bar\", \"baz\": 42}"));
        Assert.Equal("bar", map["foo"]);
        Assert.Equal(42, map["baz"]);
    }

    [Fact]
    public void CustomFieldReader_HandlesMetadataDeferredAndResults()
    {
        Assert.Null(ReadCustom("{\"__deferred\": {\"uri\": \"x\"}}"));
        Assert.Equal(new List<object?> { 1, 2 }, Assert.IsType<List<object?>>(ReadCustom("{\"__metadata\": {}, \"results\": [1, 2]}")));
        var map = Assert.IsType<Dictionary<string, object?>>(ReadCustom("{\"__metadata\": {\"uri\": \"x\"}, \"a\": 1}"));
        Assert.Equal(["a"], map.Keys);
    }
}
