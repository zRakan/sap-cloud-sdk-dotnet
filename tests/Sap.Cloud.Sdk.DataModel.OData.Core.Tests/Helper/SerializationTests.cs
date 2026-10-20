using System.Text;
using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Deserialization of OData V2 payloads and serialization of entities (Java: ODataSerializationTest, VdmSerializationTest, adapter tests).</summary>
public class SerializationTests
{
    private const string SamplePayload = """
        {
          "d" : {
              "__metadata": {
                "uri": "https://services.odata.org/OData/OData.svc/Categories(42)",
                "etag": "W/\"some-version-id\"",
                "type": "DataServiceProviderDemo.Category"
              },
              "SByteValue": -127,
              "Int16Value": 1337,
              "IntegerValue": 42,
              "Int64Value": 123456789000,
              "DecimalValue": "123456.789",
              "DoubleValue": 42.1,
              "BooleanValue": true,
              "StringValue": "Food",
              "LocalDateTimeValue": "/Date(694224000000)/",
              "OffsetDateTimeValue": "/Date(694224000000-0240)/",
              "LocalTimeValue": "PT13H20M",
              "GuidValue": "123e4567-e89b-12d3-a456-426614174000",
              "to_Parent": {
                "__deferred": {
                  "uri": "https://services.odata.org/OData/OData.svc/Categories(42)/to_Parent"
                }
              },
              "to_Children": {
                "__deferred": {
                  "uri": "https://services.odata.org/OData/OData.svc/Categories(42)/to_Children"
                }
              },
              "UnmappedStringValue": "foo",
              "UnmappedArrayValue": ["fizz","buzz"],
              "UnmappedComplexValue": {"bar":"fizzbuzz"}
          }
        }
        """;

    private static ODataRequestResultGeneric ResultOf(string body, int status = 201, string contentType = "application/json")
    {
        var request = new ODataRequestRead("/", "Collection", null, ODataProtocol.V2);
        var response = new ODataHttpResponse(status, null, [new("Content-Type", contentType)], Encoding.UTF8.GetBytes(body));
        return new ODataRequestResultGeneric(request, response);
    }

    private static T Read<T>(string body) => ODataResponseReader.ReadSingleVdm<T>(ResultOf(body), null);

    [Fact]
    public void BrokenResponse_ThrowsDeserializationException() =>
        Assert.Throws<ODataDeserializationException>(() => Read<SerializationTestEntity>("{\"d\":{\"broken\"}}"));

    [Fact]
    public void ResponseWithoutEnvelope_ThrowsDeserializationException() =>
        Assert.Throws<ODataDeserializationException>(() => Read<SerializationTestEntity>("{\"x\":{}}"));

    [Fact]
    public void ResponseWithoutPayload_ThrowsDeserializationException() =>
        Assert.Throws<ODataDeserializationException>(() => ODataResponseReader.ReadSingleVdm<SerializationTestEntity>(
            new ODataRequestResultGeneric(new ODataRequestRead("/", "C", null, ODataProtocol.V2), new ODataHttpResponse(204, null, [], null)),
            null));

    [Fact]
    public void ResponseWithoutValues_YieldsEmptyEntity()
    {
        var entity = Read<SerializationTestEntity>("{\"d\":{}}");

        Assert.Null(entity.VersionIdentifier);
        Assert.Null(entity.IntegerValue);
        Assert.Null(entity.DecimalValue);
        Assert.Null(entity.DoubleValue);
        Assert.Null(entity.BooleanValue);
        Assert.Null(entity.LocalTimeValue);
        Assert.Null(entity.StringValue);
        Assert.Null(entity.GuidValue);
        Assert.Null(entity.LocalDateTimeValue);
        Assert.Null(entity.OffsetDateTimeValue);
        Assert.Empty(entity.CustomFields);
    }

    [Fact]
    public void CreateResponse_IsDeserializedWithAllEdmTypes()
    {
        var entity = Read<SerializationTestEntity>(SamplePayload);

        Assert.Equal("W/\"some-version-id\"", entity.VersionIdentifier);
        Assert.Equal((sbyte)-127, entity.SByteValue);
        Assert.Equal((short)1337, entity.Int16Value);
        Assert.Equal(42, entity.IntegerValue);
        Assert.Equal(123456789000L, entity.Int64Value);
        Assert.Equal(123456.789m, entity.DecimalValue);
        Assert.Equal(42.1, entity.DoubleValue);
        Assert.True(entity.BooleanValue);
        Assert.Equal(new TimeOnly(13, 20), entity.LocalTimeValue);
        Assert.Equal("Food", entity.StringValue);
        Assert.Equal(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), entity.GuidValue);
        Assert.Equal(new DateTime(1992, 1, 1), entity.LocalDateTimeValue);
        var offsetDateTime = entity.OffsetDateTimeValue!.Value;
        Assert.Equal(new DateTimeOffset(1992, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4)), offsetDateTime);
        Assert.Equal(TimeSpan.FromHours(-4), offsetDateTime.Offset);
    }

    [Fact]
    public void CreateResponse_DeferredNavigationPropertiesAreNotLoaded_AndUnmappedValuesBecomeCustomFields()
    {
        var entity = Read<SerializationTestEntity>(SamplePayload);

        Assert.Null(entity.GetToParentIfPresent());
        Assert.Null(entity.GetToChildrenIfPresent());
        Assert.Equal(["UnmappedStringValue", "UnmappedArrayValue", "UnmappedComplexValue"], entity.CustomFieldNames);
        Assert.Equal("foo", entity.CustomFields["UnmappedStringValue"]);
        Assert.Equal(new List<object?> { "fizz", "buzz" }, Assert.IsType<List<object?>>(entity.CustomFields["UnmappedArrayValue"]));
        Assert.Equal("fizzbuzz", Assert.IsType<Dictionary<string, object?>>(entity.CustomFields["UnmappedComplexValue"])["bar"]);
    }

    [Fact]
    public void DeserializedEntity_HasNoChangedFields() => Assert.Empty(Read<SerializationTestEntity>(SamplePayload).GetChangedFields());

    [Fact]
    public void SerializationOfDateAndTimeAttributes()
    {
        var entity = new SerializationTestEntity
        {
            IntegerValue = 1,
            LocalDateTimeValue = new DateTime(1992, 1, 1),
            LocalTimeValue = new TimeOnly(13, 20),
            OffsetDateTimeValue = new DateTimeOffset(1992, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4)),
        };

        var json = JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get());

        Assert.Equal(
            "{\"IntegerValue\":1,\"OffsetDateTimeValue\":\"/Date(694224000000-0240)/\",\"LocalTimeValue\":\"PT13H20M0S\",\"LocalDateTimeValue\":\"/Date(694224000000)/\"}",
            json);
    }

    [Fact]
    public void NumbersAreSerializedAsStrings_ExceptSmallIntegers()
    {
        var entity = new SerializationTestEntity
        {
            SByteValue = -5,
            Int16Value = 1337,
            IntegerValue = 42,
            Int64Value = 123456789000L,
            DecimalValue = 12.50m,
            DoubleValue = 1.0,
            SingleValue = 2.5f,
            BooleanValue = false,
        };

        var json = JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get());

        JsonAssert.Equal(
            """
            {"IntegerValue":42,"DecimalValue":"12.50","DoubleValue":"1.0","BooleanValue":false,"SByteValue":-5,"Int16Value":1337,"Int64Value":"123456789000","SingleValue":"2.5"}
            """,
            json);
    }

    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(42.1, "42.1")]
    [InlineData(100.0, "100.0")]
    [InlineData(0.001, "0.001")]
    [InlineData(1e7, "1.0E7")]
    [InlineData(1.5e-5, "1.5E-5")]
    [InlineData(-3.25, "-3.25")]
    [InlineData(3.1415926535897E93, "3.1415926535897E93")]
    public void DoublesAreFormattedLikeJava(double value, string expected) =>
        Assert.Equal(expected, ODataWireFormats.FormatDouble(value));

    [Fact]
    public void BinaryValuesUseBase64()
    {
        var entity = new SerializationTestEntity { BinaryValue = [1, 2, 3] };

        Assert.Contains("\"BinaryValue\":\"AQID\"", JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get()), StringComparison.Ordinal);
        Assert.Equal([1, 2, 3], Read<SerializationTestEntity>("{\"d\":{\"BinaryValue\":\"AQID\"}}").BinaryValue);
        Assert.Null(Read<SerializationTestEntity>("{\"d\":{\"BinaryValue\":\"not base 64!\"}}").BinaryValue);
    }

    [Fact]
    public void LenientReading_UsesNullOrFalseForMismatchingTokens()
    {
        var entity = Read<SerializationTestEntity>(
            """{"d":{"BooleanValue":"yes","LocalDateTimeValue":1234,"Int64Value":"x","StringValue":12,"GuidValue":null}}""");

        Assert.False(entity.BooleanValue);
        Assert.Null(entity.LocalDateTimeValue);
        Assert.Null(entity.Int64Value);
        Assert.Equal("12", entity.StringValue);
        Assert.Null(entity.GuidValue);
    }

    [Fact]
    public void ExpandedNavigationProperties_AreDeserialized()
    {
        var entity = Read<TestVdmEntity>(
            """
            {"d":{"IntegerValue":1,
              "to_Parent":{"__metadata":{"uri":"x","etag":"W/\"p\""},"IntegerValue":0,"StringValue":"parent"},
              "to_Children":{"results":[{"IntegerValue":2},{"IntegerValue":3,"to_Parent":{"__deferred":{"uri":"y"}}}]}}}
            """);

        var parent = entity.GetToParentIfPresent()!;
        Assert.Equal("parent", parent.StringValue);
        Assert.Equal("W/\"p\"", parent.VersionIdentifier);
        var children = entity.GetToChildrenIfPresent()!;
        Assert.Equal([2, 3], children.Select(c => c.IntegerValue));
        Assert.Null(children[1].GetToParentIfPresent());
        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void ExpandedNavigationProperty_AsPlainArray_AndAsNull()
    {
        var entity = Read<TestVdmEntity>("""{"d":{"to_Children":[{"IntegerValue":2}],"to_Parent":null}}""");

        Assert.Equal(2, Assert.Single(entity.GetToChildrenIfPresent()!).IntegerValue);
        Assert.Null(entity.GetToParentIfPresent());
    }

    [Fact]
    public void ComplexProperties_AreDeserialized()
    {
        var entity = Read<TestVdmEntity>(
            """{"d":{"ComplexValue":{"__metadata":{"type":"SERVICE.SomeComplex"},"SomeValue":"Some","OtherValue":"Another","ComplexValue":{"SomeValue":"inner"}}}}""");

        Assert.Equal("Some", entity.ComplexValue!.SomeValue);
        Assert.Equal("Another", entity.ComplexValue.OtherValue);
        Assert.Equal("inner", entity.ComplexValue.ComplexValue!.SomeValue);
    }

    [Fact]
    public void ResultSets_AreReadFromResultsOrFromPlainArrays()
    {
        var fromResults = ODataResponseReader.ReadListVdm<TestVdmEntity>(ResultOf("""{"d":{"results":[{"IntegerValue":1},{"IntegerValue":2}]}}"""), null);
        var fromArray = ODataResponseReader.ReadListVdm<TestVdmEntity>(ResultOf("""{"d":[{"IntegerValue":1}]}"""), null);
        var fromFunction = ODataResponseReader.ReadListVdm<TestVdmEntity>(ResultOf("""{"d":{"Fn":{"results":[{"IntegerValue":7}]}}}"""), "Fn");

        Assert.Equal(2, fromResults.Count);
        Assert.Single(fromArray);
        Assert.Equal(7, Assert.Single(fromFunction).IntegerValue);
        Assert.Throws<ODataDeserializationException>(() => ODataResponseReader.ReadListVdm<TestVdmEntity>(ResultOf("""{"d":{"x":1}}"""), null));
    }

    [Fact]
    public void SampleProduct_RoundTripsThroughJson()
    {
        var product = Product.Builder()
            .Id(10401010)
            .Name("Cloud SDK")
            .Vendor(Vendor.Builder().Name("SAP").Build())
            .Shelf(Shelf.Builder().Id(500).FloorPlanId(200).Build())
            .Build();

        var json = JsonSerializer.Serialize(product, ODataVdmJsonOptions.Get(serializeNulls: true));

        JsonAssert.Equal(
            """
            {
              "versionIdentifier":null,
              "Id":10401010,
              "Name":"Cloud SDK",
              "ShelfId":null,
              "VendorId":null,
              "Price":null,
              "Image":null,
              "Vendor":{"versionIdentifier":null,"Id":null,"Name":"SAP","AddressId":null,"Address":null},
              "Shelf":[{"versionIdentifier":null,"Id":500,"FloorPlanId":200,"FloorPlan":null,"Products":[]}]
            }
            """,
            json);

        var deserialized = JsonSerializer.Deserialize<Product>(json, ODataVdmJsonOptions.Get())!;
        Assert.Equal("Cloud SDK", deserialized.Name);
        Assert.Equal(10401010, deserialized.Id);
        Assert.Equal("SAP", deserialized.GetVendorIfPresent()!.Name);
        var shelf = Assert.Single(deserialized.GetShelfIfPresent()!);
        Assert.Equal(500, shelf.Id);
        Assert.Equal(200, shelf.FloorPlanId);
        Assert.Equal(product, deserialized);
    }

    [Fact]
    public void WriterSkipsNullsUnlessAsked()
    {
        var entity = new TestVdmEntity { IntegerValue = 1 };

        Assert.Equal("{\"IntegerValue\":1}", JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get()));
        Assert.Contains("\"StringValue\":null", JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get(serializeNulls: true)), StringComparison.Ordinal);
    }

    [Fact]
    public void CustomFields_AreWrittenAfterTheModelProperties()
    {
        var entity = new TestVdmEntity { IntegerValue = 1 };
        entity.SetCustomField("Zzz", "last");
        entity.SetCustomField("Aaa", new DateTimeOffset(2022, 2, 2, 4, 20, 0, TimeSpan.Zero));
        entity.SetCustomField("Num", 7L);

        Assert.Equal(
            "{\"IntegerValue\":1,\"Zzz\":\"last\",\"Aaa\":\"/Date(1643775600000)/\",\"Num\":\"7\"}",
            JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get()));
    }

    [Fact]
    public void ExtendedEntityClass_KeepsParentAndAddsOwnProperties()
    {
        var entity = JsonSerializer.Deserialize<ExtendedAddress>(
            """{"Id":1,"City":"Walldorf","Extension":"x","versionIdentifier":"W/\"1\""}""",
            ODataVdmJsonOptions.Get())!;

        Assert.Equal("Walldorf", entity.City);
        Assert.Equal("x", entity.Extension);
        Assert.Equal("W/\"1\"", entity.VersionIdentifier);
        Assert.Equal(
            """{"versionIdentifier":"W/\"1\"","Id":1,"City":"Walldorf","Extension":"x"}""",
            JsonSerializer.Serialize(entity, ODataVdmJsonOptions.Get()));
    }

    /// <summary>A user-written subclass of a generated entity with an additional property.</summary>
    private sealed class ExtendedAddress : Address
    {
        private static readonly VdmTypeMetadata ExtendedMetadata = VdmTypeMetadata.Extending<ExtendedAddress>(EntityMetadata)
            .Property("Extension", static e => e._extension, static (e, v) => e._extension = v)
            .Build();

        private string? _extension;

        public string? Extension
        {
            get => _extension;
            set
            {
                RememberChangedField("Extension", _extension);
                _extension = value;
            }
        }

        protected override VdmTypeMetadata TypeMetadata => ExtendedMetadata;
    }
}
