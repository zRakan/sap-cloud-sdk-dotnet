using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>
/// Serialization and deserialization of entities, complex values and enums. Ports <c>PropertySerializationTest</c>,
/// <c>FieldSerializationTest</c>, <c>PropertyCustomSerializationTest</c>, <c>BigDecimalSerialisationTest</c>,
/// <c>SerializerAdapterTest</c> and <c>VdmEntityTest</c>.
/// </summary>
public class SerializationTests
{
    // https://docs.oasis-open.org/odata/odata-json-format/v4.01/csprd06/odata-json-format-v4.01-csprd06.html#sec_PrimitiveValue
    private const string ReferencePayload =
        "{\"@odata.type\":\"#TestEntity\"," +
        "\"ByteValue\":255,\"SByteValue\":-128,\"Int16Value\":1,\"Int32Value\":-1234,\"Int64Value\":1234567890," +
        "\"SingleValue\":1234.5677,\"DoubleValue\":1234.5678,\"DecimalValue\":110,\"BooleanValue\":false," +
        "\"StringValue\":\"test\",\"BinaryValue\":\"AQID\",\"GuidValue\":\"00000000-1111-2222-3333-444444444444\"," +
        "\"TimeOfDayValue\":\"12:00:00\",\"DateValue\":\"1999-03-14\",\"DateTimeOffsetValue\":\"1999-03-14T00:00:00Z\"," +
        "\"GeographyPoint\":{\"type\":\"Point\",\"coordinates\":[142.1,64.1]}}";

    [Fact]
    public void Serialize_EnumAndNavigationLists()
    {
        var person = Person.Builder().Gender(PersonGender.Female).FirstName("Eve").Build();

        var json = VdmJson.Serialize(person);

        Asserts.JsonEqual(
            "{\"FirstName\":\"Eve\",\"Gender\":\"Female\",\"@odata.type\":\"#Trippin.Person\",\"Friends\":[],\"Trips\":[]}",
            json);
    }

    [Fact]
    public void Serialize_PutsTypeFirstThenPropertiesInDeclarationOrder()
    {
        var person = Person.Builder().LastName("Bar").FirstName("Foo").Build();
        person.VersionIdentifier = "W/\"1\"";
        person.SetCustomField("custom", "value");

        var json = VdmJson.Serialize(person);

        Assert.Equal(
            "{\"@odata.type\":\"#Trippin.Person\",\"@odata.etag\":\"W/\\u00221\\u0022\",\"FirstName\":\"Foo\",\"LastName\":\"Bar\",\"Friends\":[],\"Trips\":[],\"custom\":\"value\"}".Replace("\\u0022", "\\\"", StringComparison.Ordinal),
            json);
    }

    [Fact]
    public void Serialize_ComplexCollection()
    {
        var city = new City { Name = "Potsdam", Region = "Nedlitz", CountryRegion = "Brandenburg" };
        var location = new Location { Address = "Konrad-Zuse-Ring 10, 14469", City = city };
        var person = Person.Builder().AddressInfo([location]).Build();

        Asserts.JsonEqual(
            "{\"AddressInfo\":[{\"Address\":\"Konrad-Zuse-Ring 10, 14469\"," +
            "\"City\":{\"Name\":\"Potsdam\",\"CountryRegion\":\"Brandenburg\",\"Region\":\"Nedlitz\",\"@odata.type\":\"#Trippin.City\"}," +
            "\"@odata.type\":\"#Trippin.Location\"}],\"@odata.type\":\"#Trippin.Person\",\"Friends\":[],\"Trips\":[]}",
            VdmJson.Serialize(person));
    }

    [Fact]
    public void Serialize_PrimitiveCollection()
    {
        var person = Person.Builder().Emails(["eve@sap.com"]).Build();

        Asserts.JsonEqual(
            "{\"Emails\":[\"eve@sap.com\"],\"@odata.type\":\"#Trippin.Person\",\"Friends\":[],\"Trips\":[]}",
            VdmJson.Serialize(person));
    }

    [Fact]
    public void Serialize_WithNullsWritesNullProperties()
    {
        var json = VdmJson.Serialize(TestEntity.WithId("a"), includeNulls: true);

        Assert.Equal("{\"@odata.type\":\"#TestEntity\",\"id\":\"a\"}", json);
        Assert.Equal("{\"@odata.type\":\"#TestEntity\",\"id\":null}", VdmJson.Serialize(new TestEntity(), includeNulls: true));
        Assert.Equal("{\"@odata.type\":\"#TestEntity\"}", VdmJson.Serialize(new TestEntity()));
    }

    [Fact]
    public void RoundTrip_DateTimeOffsetsKeepTheirOffset()
    {
        var date1 = new DateTimeOffset(2020, 2, 20, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2020, 2, 20, 3, 0, 0, TimeSpan.FromHours(1));
        var trip = Trip.Builder().Name("Trip1").StartsAt(date1).EndsAt(date2).Build();

        var json = VdmJson.Serialize(trip);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("2020-02-20T00:00:00Z", document.RootElement.GetProperty("StartsAt").GetString());
        Assert.Equal("2020-02-20T03:00:00+01:00", document.RootElement.GetProperty("EndsAt").GetString());
        var read = VdmJson.Deserialize<Trip>(json)!;
        Assert.Equal(trip, read);
        Assert.Equal(date1, read.StartsAt);
        Assert.Equal(date2, read.EndsAt);
        Assert.Equal(TimeSpan.FromHours(1), read.EndsAt!.Value.Offset);
    }

    [Fact]
    public void RoundTrip_AllEdmPrimitives()
    {
        var read = VdmJson.Deserialize<ReferenceObject>(ReferencePayload)!;

        Assert.Equal((byte)255, read.ByteValue);
        Assert.Equal((sbyte)-128, read.SByteValue);
        Assert.Equal(-1234, read.Int32Value);
        Assert.Equal(1234567890L, read.Int64Value);
        Assert.Equal(1234.5677f, read.SingleValue);
        Assert.Equal(1234.5678d, read.DoubleValue);
        Assert.Equal(110m, read.DecimalValue);
        Assert.False(read.BooleanValue);
        Assert.Equal("test", read.StringValue);
        Assert.Equal(new byte[] { 1, 2, 3 }, read.BinaryValue);
        Assert.Equal(Guid.Parse("00000000-1111-2222-3333-444444444444"), read.GuidValue);
        Assert.Equal(new TimeOnly(12, 0, 0), read.TimeOfDayValue);
        Assert.Equal(new DateOnly(1999, 3, 14), read.DateValue);
        Assert.Equal(new DateTimeOffset(1999, 3, 14, 0, 0, 0, TimeSpan.Zero), read.DateTimeOffsetValue);
        Assert.Equal(ReferencePayload, VdmJson.Serialize(read));
    }

    [Fact]
    public async Task Create_SerializesLikeTheReferencePayload()
    {
        var result = await ResultOf(ReferencePayload);
        var entity = await result.AsSingleAsync<ReferenceObject>();

        var serialized = new CreateRequestBuilder<ReferenceObject>("/", entity, "EntityCollection").ToRequest().SerializedEntity;

        Assert.Equal(ReferencePayload, serialized);
    }

    [Theory]
    [InlineData("-__v")]
    [InlineData("+__v")]
    [InlineData("+//v")]
    public async Task Binary_AcceptsBothBase64Alphabets_AndWritesTheStandardAlphabet(string encoded)
    {
        var payload = ReferencePayload.Replace("\"BinaryValue\":\"AQID\"", $"\"BinaryValue\":\"{encoded}\"", StringComparison.Ordinal);
        var result = await ResultOf(payload);

        var entity = await result.AsSingleAsync<ReferenceObject>();

        Assert.Equal(Convert.FromBase64String("+//v"), entity.BinaryValue);
        var serialized = new CreateRequestBuilder<ReferenceObject>("/", entity, "EntityCollection").ToRequest().SerializedEntity;
        Assert.Contains("\"BinaryValue\":\"+//v\"", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deserialize_UnknownPropertiesBecomeCustomFields()
    {
        var result = await ResultOf(ReferencePayload);

        var entity = await result.AsSingleAsync<ReferenceObject>();

        Assert.Equal(["GeographyPoint"], entity.GetCustomFieldNames());
        Assert.IsAssignableFrom<IDictionary<string, object?>>(entity.GetCustomField<object>("GeographyPoint"));
    }

    [Fact]
    public void CustomConverters_AreUsedPerProperty()
    {
        var user = new UserEntity
        {
            DateOfBirth = new DateTimeOffset(1960, 5, 31, 18, 0, 0, TimeSpan.FromHours(1)),
            LocaleTime = new DateTimeOffset(2020, 2, 20, 0, 0, 0, TimeSpan.Zero),
            RegistrationDate = new DateTime(2005, 12, 24, 0, 0, 0, DateTimeKind.Unspecified),
        };

        var json = VdmJson.Serialize(user);

        Asserts.JsonEqual(
            "{\"@odata.type\":\"#Example.User\",\"DateOfBirth\":\"1960-05-31T18:00:00+01:00\",\"LocaleTime\":\"2020-02-20T00:00:00+00:00\",\"RegistrationDate\":\"2005-12-24T00:00:00+00:00\"}",
            json);
        var read = VdmJson.Deserialize<UserEntity>(json)!;
        Assert.Equal(user.DateOfBirth, read.DateOfBirth);
        Assert.Equal(user.LocaleTime, read.LocaleTime);
        Assert.Equal(user.RegistrationDate, read.RegistrationDate);
    }

    [Fact]
    public void Decimal_IsNotWrittenInExponentNotation()
    {
        var entity = new DecimalEntity { BigDecimalField = 0.000000003m };

        var json = VdmJson.Serialize(entity);

        Assert.DoesNotContain("3E-9", json, StringComparison.Ordinal);
        Assert.Contains("0.000000003", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_NullableAndNonNullableOptions()
    {
        var city = new City { Name = "Potsdam", CountryRegion = "Brandenburg", Region = "Deutschland" };
        var trip = Trip.Builder().Budget(100.0f).EndsAt(DateTimeOffset.UtcNow).ShareId(Guid.NewGuid()).Name("a").Build();
        var person = Person.Builder()
            .LastName("Bar")
            .Emails(["foo@bar.com"])
            .FavoriteFeature(Feature.Feature1)
            .Trips(trip)
            .AddressInfo([new Location { Address = "Sesamstr.1", City = city }])
            .Build();

        var withoutNulls = VdmJson.Deserialize<Person>(VdmJson.Serialize(person))!;
        var withNulls = VdmJson.Deserialize<Person>(VdmJson.Serialize(person, includeNulls: true))!;

        Assert.Equal(person, withoutNulls);
        Assert.Equal(withoutNulls, withNulls);
    }

    [Fact]
    public void Enum_UnknownMemberIsReadAsNull()
    {
        var person = VdmJson.Deserialize<Person>("{\"Gender\":\"Alien\",\"Features\":[\"Feature2\",\"Nope\"]}")!;

        Assert.Null(person.Gender);
        Assert.Equal(2, person.Features!.Count);
        Assert.Equal(Feature.Feature2, person.Features[0]);
        Assert.Null(person.Features[1]);
    }

    [Fact]
    public void Enum_ProvidesNameValueAndLookup()
    {
        Assert.Equal("Female", PersonGender.Female.Name);
        Assert.Equal(1L, PersonGender.Female.Value);
        Assert.Same(PersonGender.Unknown, VdmEnum.GetConstant<PersonGender>("Unknown"));
        Assert.Null(VdmEnum.GetConstant<PersonGender>("nope"));
        Assert.Equal("Female", PersonGender.Female.ToString());
        Assert.Equal(3, PersonGender.Values.Count);
    }

    [Fact]
    public void Deserialize_NestedStructuresAndNavigationProperties()
    {
        const string payload =
            "{\"UserName\":\"russ\",\"Age\":30,\"Gender\":\"Male\",\"Emails\":[\"a@b.c\",\"d@e.f\"]," +
            "\"HomeAddress\":{\"Address\":\"Home\",\"City\":{\"Name\":\"Berlin\"}}," +
            "\"AddressInfo\":[{\"Address\":\"A1\"},{\"Address\":\"A2\",\"City\":null}]," +
            "\"Friends\":[{\"UserName\":\"f1\"},{\"UserName\":\"f2\"}],\"BestFriend\":{\"UserName\":\"bf\"}," +
            "\"Trips\":[{\"TripId\":7,\"Name\":\"T\",\"ShareId\":\"11111111-1111-1111-1111-111111111111\",\"Budget\":12.5,\"Tags\":[\"x\"],\"PlanItems\":[{\"PlanItemId\":1,\"Duration\":\"PT3H\"}]}]," +
            "\"@odata.type\":\"#Trippin.Person\"}";

        var person = VdmJson.Deserialize<Person>(payload)!;

        Assert.Equal("russ", person.UserName);
        Assert.Equal(30L, person.Age);
        Assert.Same(PersonGender.Male, person.Gender);
        Assert.Equal(["a@b.c", "d@e.f"], person.Emails);
        Assert.Equal("Berlin", person.HomeAddress!.City!.Name);
        Assert.Equal(2, person.AddressInfo!.Count);
        Assert.Null(person.AddressInfo[1].City);
        Assert.Equal(["f1", "f2"], person.GetFriendsIfPresent()!.Select(f => f.UserName));
        Assert.Equal("bf", person.GetBestFriendIfPresent()!.UserName);
        var trip = Assert.Single(person.GetTripsIfPresent()!);
        Assert.Equal(7, trip.TripId);
        Assert.Equal(12.5f, trip.Budget);
        Assert.Equal(["x"], trip.Tags);
        Assert.Equal(TimeSpan.FromHours(3), Assert.Single(trip.GetPlanItemsIfPresent()!).Duration);
        Assert.Empty(person.GetCustomFieldNames());
    }

    [Fact]
    public void Deserialize_ResetsChangeTracking()
    {
        var person = VdmJson.Deserialize<Person>("{\"UserName\":\"u\",\"FirstName\":\"f\",\"unknown\":42,\"HomeAddress\":{\"Address\":\"x\",\"extra\":true}}")!;

        Assert.Empty(person.GetChangedFields());
        Assert.Empty(person.HomeAddress!.GetChangedFields());
        Assert.Equal(["unknown"], person.GetCustomFieldNames());
        Assert.Equal(42d, person.GetCustomField<double>("unknown"));
    }

    [Fact]
    public void Deserialize_ReadsETagAnnotations()
    {
        var person = VdmJson.Deserialize<Person>("{\"@odata.etag\":\"W/\\\"1\\\"\",\"@odata.type\":\"#Trippin.Person\",\"@type\":\"x\",\"UserName\":\"u\"}")!;

        Assert.Equal("W/\"1\"", person.VersionIdentifier);
        Assert.Empty(person.GetCustomFieldNames());
        Assert.Equal("W/\"2\"", VdmJson.Deserialize<Person>("{\"@etag\":\"W/\\\"2\\\"\"}")!.VersionIdentifier);
    }

    [Fact]
    public void Deserialize_RejectsNonObjects()
    {
        Assert.Throws<JsonException>(() => VdmJson.Deserialize<Person>("[1]"));
        Assert.Null(VdmJson.Deserialize<Person>("null"));
    }

    [Fact]
    public void Copy_IsIndependentAndWithoutChanges()
    {
        var person = new Person { UserName = "a" };
        person.SetCustomField("x", "y");
        person.VersionIdentifier = "etag";

        var copy = VdmJson.Copy(person);

        Assert.NotSame(person, copy);
        Assert.Equal("a", copy.UserName);
        Assert.Equal("y", copy.GetCustomField<string>("x"));
        Assert.Equal("etag", copy.VersionIdentifier);
        Assert.Empty(copy.GetChangedFields());
        Assert.NotEmpty(person.GetChangedFields());
    }

    [Fact]
    public void EqualsAndHashCode_CompareDeclaredProperties()
    {
        Assert.Equal(new TestEntity(), new TestEntity());
        Assert.Equal(TestEntity.WithId("a"), new TestEntity { Id = "a" });
        Assert.Equal(TestEntity.WithId("a").GetHashCode(), new TestEntity { Id = "a" }.GetHashCode());
        Assert.NotEqual(TestEntity.WithId("a"), TestEntity.WithId("b"));
        Assert.NotEqual<object>(TestEntity.WithId("a"), new NamedEntity { Name = "a" });
        var withEtag = new TestEntity { VersionIdentifier = "1" };
        Assert.NotEqual(new TestEntity(), withEtag);
        Assert.Contains("id=\"a\"", TestEntity.WithId("a").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CustomFields_CanBeSetReadAndChecked()
    {
        var entity = new TestEntity();
        Assert.Null(entity.VersionIdentifier);

        entity.SetCustomField("foo", "bar");
        var customField = OData.Client.Expression.FieldReference.Of("fizz");
        entity.SetCustomField(customField, "buzz");

        Assert.Equal("bar", entity.GetCustomFields()["foo"]);
        Assert.Equal("buzz", entity.GetCustomFields()["fizz"]);
        Assert.Equal("bar", entity.GetCustomField<string>("foo"));
        Assert.Equal("buzz", entity.GetCustomField<string>(customField));
        Assert.True(entity.HasCustomField("fizz"));
        Assert.True(entity.HasCustomField(customField));
        Assert.False(entity.HasCustomField("nope"));
        Assert.Equal(["foo", "fizz"], entity.GetCustomFieldNames());
        Assert.Throws<KeyNotFoundException>(() => entity.GetCustomField<string>("nope"));
    }

    [Fact]
    public void ChangedFields_TrackNonCustomFields()
    {
        var entity = TestEntity.WithId("old");
        Assert.Empty(entity.GetChangedFields());

        entity.Id = "old";
        Assert.Empty(entity.GetChangedFields());

        entity.Id = "new";
        Assert.Equal(["id"], entity.GetChangedFields().Keys);
        Assert.Equal("new", entity.GetChangedFields()["id"]);

        entity.Id = "old";
        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void ChangedFields_TrackCustomFields()
    {
        var entity = TestEntity.WithId("id");
        Assert.Empty(entity.GetChangedFields());

        entity.SetCustomField("foo", "bar");
        Assert.Equal(["foo"], entity.GetChangedFields().Keys);

        entity.ResetChangedFields();
        entity.SetCustomField("foo", "bar");
        Assert.Empty(entity.GetChangedFields());

        entity.SetCustomField("foo", "baz");
        Assert.Equal(["foo"], entity.GetChangedFields().Keys);
    }

    [Fact]
    public void ChangedFields_ReportNestedChanges()
    {
        var city = new City();
        var location = Location.Builder().Address("a").City(city).Build();
        var person = Person.Builder().AddressInfo([location]).HomeAddress(location).Build();
        Assert.Empty(person.GetChangedFields());

        city.Name = "Berlin";

        Assert.Equal(["AddressInfo", "HomeAddress"], person.GetChangedFields().Keys);
    }

    [Fact]
    public void ChangedFields_SetCollectionToEqualCollectionIsNoChange()
    {
        var person = Person.Builder().Emails(["a"]).Build();

        person.Emails = ["a"];
        Assert.Empty(person.GetChangedFields());

        person.Emails = ["a", "b"];
        Assert.Equal(["Emails"], person.GetChangedFields().Keys);
    }

    private static async Task<OData.Client.Request.ODataRequestResultGeneric> ResultOf(string payload)
    {
        var handler = new FakeHttpHandler(Responses.Json(payload));
        return await new OData.Client.Request.ODataRequestRead("/", "EntityCollection", null, OData.Client.ODataProtocol.V4)
            .ExecuteAsync(handler.CreateClient());
    }
}
