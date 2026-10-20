using System.Text.Json;
using Sap.Cloud.Sdk.Result;
using Sap.Cloud.Sdk.TypeConverter;
using Sap.Cloud.Sdk.TypeConverter.Exceptions;

namespace Sap.Cloud.Sdk.DataModel.FluentResult.Tests;

public class ResultElementTests
{
    private static readonly JsonSerializerOptions Options = ElementNameJsonTypeInfoResolver.CreateOptions();

    private sealed record MyYear(int Year);

    private sealed record MyYearWithLongValue(long Year);

    private sealed class MyOptional(bool? option)
    {
        public bool? Option { get; } = option;

        public override bool Equals(object? obj) => obj is MyOptional o && o.Option == Option;

        public override int GetHashCode() => Option.GetHashCode();
    }

    private sealed class MyObject(object value)
    {
        public object Value { get; } = value;
    }

    private sealed class Person
    {
        [ElementName("first_name")]
        public string? FirstName { get; set; }

        public int Age { get; set; }
    }

    private sealed class PrivateConstructed
    {
        private PrivateConstructed(int year)
        {
            Year = year;
        }

        public int Year { get; }
    }

    private static IResultElement Parse(string json) =>
        new JsonResultElementFactory(Options).Create(JsonDocument.Parse(json).RootElement)!;

    private static List<T> CollectFromJson<T>(string attributeName, params string[] jsonValues)
    {
        var json = "[" + string.Join(",", jsonValues.Select(v => $"{{\"{attributeName}\":{v}}}")) + "]";
        var resultElement = Parse(json);
        Assert.True(resultElement.IsResultCollection);
        return new DefaultCollectedResult(attributeName, resultElement.GetAsCollection()).AsList<T>().ToList();
    }

    [Fact]
    public void Factory_CreatesElementsByKind()
    {
        Assert.True(Parse("\"x\"").IsResultPrimitive);
        Assert.True(Parse("12").IsResultPrimitive);
        Assert.True(Parse("true").IsResultPrimitive);
        Assert.True(Parse("{}").IsResultObject);
        Assert.True(Parse("[]").IsResultCollection);
        Assert.Null(new JsonResultElementFactory(Options).Create(null));
        Assert.Throws<ArgumentException>(() => Parse("null"));
    }

    [Fact]
    public void Primitive_ConvertsToNumericAndStringTypes()
    {
        var number = Parse("42").GetAsPrimitive();
        Assert.Equal(42, number.AsInteger());
        Assert.Equal(42L, number.AsLong());
        Assert.Equal((short)42, number.AsShort());
        Assert.Equal((byte)42, number.AsByte());
        Assert.Equal(42d, number.AsDouble());
        Assert.Equal(42m, number.AsBigDecimal());
        Assert.Equal(new System.Numerics.BigInteger(42), number.AsBigInteger());
        Assert.Equal("42", number.AsString());
        Assert.Equal('4', number.AsCharacter());
        Assert.Equal("true", Parse("true").AsString());
        Assert.True(Parse("true").AsBoolean());
        Assert.True(Parse("\"true\"").AsBoolean());
        Assert.Equal(7, Parse("\"7\"").AsInteger());
    }

    [Fact]
    public void Primitive_ConversionFailureIsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => Parse("\"abc\"").AsInteger());
        Assert.Throws<NotSupportedException>(() => Parse("300").AsByte());
        Assert.Throws<NotSupportedException>(() => Parse("1").GetAsObject());
        Assert.Throws<NotSupportedException>(() => Parse("1").GetAsCollection());
    }

    [Fact]
    public void Object_GetReturnsChildrenAndNullForMissing()
    {
        var obj = Parse("{\"a\":1,\"b\":{\"c\":\"x\"},\"d\":[1,2]}").GetAsObject();
        Assert.Equal(1, obj.GetElement("a")!.AsInteger());
        Assert.Equal("x", obj.GetElement("b")!.GetAsObject().GetElement("c")!.AsString());
        Assert.True(obj.GetElement("d")!.IsResultCollection);
        Assert.Null(obj.GetElement("missing"));
        Assert.Throws<NotSupportedException>(() => obj.AsInteger());
        Assert.Throws<NotSupportedException>(() => obj.GetAsPrimitive());
        Assert.Throws<NotSupportedException>(() => obj.GetAsCollection());
    }

    [Fact]
    public void Object_GetOfJsonNullIsNotSupported()
    {
        var obj = Parse("{\"a\":null}").GetAsObject();
        Assert.Throws<NotSupportedException>(() => obj.GetElement("a"));
    }

    [Fact]
    public void Object_AsTypeHonorsElementName()
    {
        var person = Parse("{\"first_name\":\"Ada\",\"Age\":36}").GetAsObject().AsType<Person>();
        Assert.Equal("Ada", person.FirstName);
        Assert.Equal(36, person.Age);
    }

    [Fact]
    public void Object_AsTypeWithOnlyAnnotatedIgnoresOtherMembers()
    {
        var factory = new JsonResultElementFactory(ElementNameJsonTypeInfoResolver.CreateOptions(onlyAnnotated: true));
        var obj = factory.Create(JsonDocument.Parse("{\"first_name\":\"Ada\",\"Age\":36}").RootElement)!.GetAsObject();
        var person = obj.AsType<Person>();
        Assert.Equal("Ada", person.FirstName);
        Assert.Equal(0, person.Age);
    }

    [Fact]
    public void Object_AsTypeFailureIsNotSupported()
    {
        var obj = Parse("{\"Age\":\"not a number\"}").GetAsObject();
        Assert.Throws<NotSupportedException>(() => obj.AsType<Person>());
    }

    [Fact]
    public void Collection_AsPrimitiveListsAndSets()
    {
        var collection = Parse("[1,2,2,3]").GetAsCollection();
        Assert.Equal([1, 2, 2, 3], collection.AsIntegerList());
        Assert.Equal(3, collection.AsIntegerSet().Count);
        Assert.Equal(["1", "2", "2", "3"], collection.AsStringList());
        Assert.Equal([1L, 2L, 2L, 3L], collection.AsLongList());
        Assert.Equal([true, false], Parse("[true,false]").GetAsCollection().AsBooleanList());
    }

    [Fact]
    public void Collection_AsListOfObjects()
    {
        var list = Parse("[{\"Age\":1},{\"Age\":2}]").GetAsCollection().AsList<Person>();
        Assert.Equal([1, 2], list.Select(p => p.Age));
    }

    [Fact]
    public void Collection_Enumerates()
    {
        var collection = Parse("[1,\"a\",{}]").GetAsCollection();
        Assert.Equal(3, collection.Count());
        Assert.Equal([true, true, false], collection.Select(e => e.IsResultPrimitive).Take(2).Append(collection.Last().IsResultPrimitive));
        Assert.Throws<NotSupportedException>(() => collection.AsString());
        Assert.Throws<NotSupportedException>(() => collection.GetAsObject());
        Assert.Throws<NotSupportedException>(() => collection.GetAsPrimitive());
    }

    [Fact]
    public void Collected_CollectsNamedChildrenOfEachObject()
    {
        var collection = Parse("[{\"n\":1,\"x\":\"a\"},{\"n\":2,\"x\":\"b\"}]").GetAsCollection();
        Assert.Equal([1, 2], collection.Collect("n").AsIntegerList());
        Assert.Equal(["a", "b"], collection.Collect("x").AsStringList());
        Assert.Throws<NotSupportedException>(() => collection.Collect("missing").AsStringList());
    }

    [Fact]
    public void PrimitiveBased_FromInteger()
    {
        var element = Parse("1996");
        Assert.Equal(new MyYear(1996), new PrimitiveBasedObjectExtractor<MyYear>().Extract(element));
    }

    [Fact]
    public void PrimitiveBased_FromLong()
    {
        var element = Parse("10");
        Assert.Equal(new MyYearWithLongValue(10L), new PrimitiveBasedObjectExtractor<MyYearWithLongValue>().Extract(element));
    }

    [Fact]
    public void PrimitiveBased_FromPrivateConstructor()
    {
        var value = new PrimitiveBasedObjectExtractor<PrivateConstructed>().Extract(Parse("1996"));
        Assert.Equal(1996, value.Year);
    }

    [Fact]
    public void PrimitiveBased_FromString()
    {
        Assert.Equal(new Uri("http://localhost/x"), new PrimitiveBasedObjectExtractor<Uri>().Extract(Parse("\"http://localhost/x\"")));
    }

    [Fact]
    public void PrimitiveBased_FromBooleanList()
    {
        var list = CollectFromJson<MyOptional>("value", "true", "false", "false", "true");
        Assert.Equal(4, list.Count);
        Assert.Equal([true, false, false, true], list.Select(o => o.Option));
    }

    [Fact]
    public void PrimitiveBased_FromLongList()
    {
        var list = CollectFromJson<Random>("value", "10", "100", "1000", "10000");
        Assert.Equal(4, list.Count);
    }

    [Fact]
    public void PrimitiveBased_FromNonPrimitiveIsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => CollectFromJson<MyObject>("value", "null", "null"));
        Assert.Throws<NotSupportedException>(() => new PrimitiveBasedObjectExtractor<MyYear>().Extract(Parse("{}")));
    }

    [Fact]
    public void PrimitiveBased_WithoutMatchingConstructorIsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => new PrimitiveBasedObjectExtractor<Person>().Extract(Parse("1")));
    }

    [Fact]
    public void ConvertedObject_Behaves()
    {
        Assert.Equal("test", ConvertedObject<string>.Of("test").Get());
        Assert.Throws<ObjectNotConvertibleException>(() => ConvertedObject<string>.OfNotConvertible().Get());
        Assert.Equal("test", ConvertedObject<string>.Of("test").OrNull());
        Assert.Null(ConvertedObject<string>.OfNotConvertible().OrNull());
        Assert.Equal("test", ConvertedObject<string>.Of("test").OrElse("something"));
        Assert.Equal("something", ConvertedObject<string>.OfNotConvertible().OrElse("something"));
        Assert.Equal(123, ConvertedObject<int?>.Of(123).Get());
        Assert.Null(ConvertedObject<string>.Of(null).Get());
        Assert.Null(ConvertedObject<string>.OfNull().Get());
        Assert.True(ConvertedObject<string>.OfNull().IsConvertible);
        Assert.True(ConvertedObject<string>.OfNotConvertible().IsNotConvertible);
    }

    private sealed class IntConverter : AbstractTypeConverter<int, string>
    {
        public override ConvertedObject<string> ToDomainNonNull(int obj) => ConvertedObject<string>.Of(obj.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public override ConvertedObject<int> FromDomainNonNull(string domainObject) => ConvertedObject<int>.Of(int.Parse(domainObject, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AbstractTypeConverter_HandlesNullAndFailures()
    {
        var converter = new IntConverter();
        Assert.Equal("5", converter.ToDomain(5).Get());
        Assert.Equal(7, converter.FromDomain("7").Get());
        Assert.True(converter.FromDomain("abc").IsNotConvertible);
        Assert.True(converter.FromDomain(null).IsConvertible);
        Assert.Equal(typeof(int), converter.Type);
        Assert.Equal(typeof(string), converter.DomainType);
    }

    private sealed class Holder
    {
        public int Value { get; set; }
    }

    [Fact]
    public void TypeConverterJsonConverter_ReadsThroughConverter()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TypeConverterJsonConverter<int>(new IntConverter()));
        Assert.Equal(12, JsonSerializer.Deserialize<Holder>("{\"Value\":\"12\"}", options)!.Value);
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new Holder(), options));
    }
}
