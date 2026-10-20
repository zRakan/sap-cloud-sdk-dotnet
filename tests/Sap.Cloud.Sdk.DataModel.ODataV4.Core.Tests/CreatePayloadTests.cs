using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>Payload of create requests: all fields, custom fields and deep inserts through navigation properties.</summary>
public class CreatePayloadTests
{
    private static readonly DefaultTrippinService Service = new();

    [Fact]
    public void Create_SendsAllNonNullFieldsAndCustomFields()
    {
        var person = new Person { UserName = "u", FirstName = "f", Gender = PersonGender.Male, Age = 7, Emails = ["a@b.c"] };
        person.SetCustomField("Custom", 42);
        person.SetCustomField("Nested", new Dictionary<string, object?> { ["x"] = "y" });

        var request = Service.CreatePeople(person).ToRequest();

        Asserts.JsonEqual(
            "{\"@odata.type\":\"#Trippin.Person\",\"UserName\":\"u\",\"FirstName\":\"f\",\"Gender\":\"Male\",\"Age\":7,\"Emails\":[\"a@b.c\"],\"Custom\":42,\"Nested\":{\"x\":\"y\"}}",
            request.SerializedEntity);
        Assert.Equal(ITrippinService.DefaultServicePath + "/People", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Create_DeepInsertSendsNavigationProperties()
    {
        var trip = Trip.Builder()
            .Name("Trip")
            .TripId(1)
            .StartsAt(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .PlanItems(PlanItem.Builder().PlanItemId(3).Duration(TimeSpan.FromHours(2)).Build())
            .Build();
        var person = Person.Builder()
            .UserName("u")
            .BestFriend(Person.Builder().UserName("friend").Build())
            .Trips(trip)
            .Build();

        var request = Service.CreatePeople(person).ToRequest();

        Asserts.JsonEqual(
            "{\"@odata.type\":\"#Trippin.Person\",\"UserName\":\"u\"," +
            "\"Friends\":[],\"BestFriend\":{\"@odata.type\":\"#Trippin.Person\",\"UserName\":\"friend\",\"Friends\":[],\"Trips\":[]}," +
            "\"Trips\":[{\"@odata.type\":\"#Trippin.Trip\",\"TripId\":1,\"Name\":\"Trip\",\"StartsAt\":\"2020-01-01T00:00:00Z\"," +
            "\"PlanItems\":[{\"@odata.type\":\"#Trippin.PlanItem\",\"PlanItemId\":3,\"Duration\":\"PT2H\"}]}]}",
            request.SerializedEntity);
    }

    [Fact]
    public void Create_SerializationFailureIsReportedAsODataSerializationException()
    {
        var person = new Person();
        person.SetCustomField("bad", new BadValue());

        var exception = Assert.Throws<OData.Client.Exceptions.ODataSerializationException>(() => Service.CreatePeople(person).ToRequest());

        Assert.Same(person, exception.NonSerializableObject);
    }

    [Fact]
    public void Update_ReplacingWithPutSendsAllFieldsAndNavigationProperties()
    {
        var person = Person.Builder().UserName("u").FirstName("f").Build();

        var request = Service.UpdatePeople(person).ReplacingEntity().ToRequest();

        Assert.Equal(OData.Client.Request.UpdateStrategy.ReplaceWithPut, request.UpdateStrategy);
        Asserts.JsonEqual(
            "{\"@odata.type\":\"#Trippin.Person\",\"UserName\":\"u\",\"FirstName\":\"f\",\"Friends\":[],\"Trips\":[]}",
            request.SerializedEntity);
    }

    [Fact]
    public void Update_PatchSendsCustomFieldChanges()
    {
        var person = VdmJson.Deserialize<Person>("{\"UserName\":\"u\",\"known\":1,\"other\":2}")!;
        person.SetCustomField("known", 5);

        var request = Service.UpdatePeople(person).ToRequest();

        Assert.Equal("{\"known\":5,\"@odata.type\":\"#Trippin.Person\"}", request.SerializedEntity);
    }

    [Fact]
    public void Update_PatchOfNavigationPropertyOnlyIfIncluded()
    {
        var person = Person.Builder().UserName("u").BestFriend(Person.Builder().UserName("bf").Build()).Build();
        person.FirstName = "x";

        var withoutNavigation = Service.UpdatePeople(person).ToRequest();
        var withNavigation = Service.UpdatePeople(person).IncludingFields(Person.Fields.BestFriend).ToRequest();

        Assert.Equal("{\"FirstName\":\"x\",\"@odata.type\":\"#Trippin.Person\"}", withoutNavigation.SerializedEntity);
        Asserts.JsonEqual(
            "{\"FirstName\":\"x\",\"BestFriend\":{\"@id\":\"People('bf')\"},\"@odata.type\":\"#Trippin.Person\"}",
            withNavigation.SerializedEntity);
    }

    [Fact]
    public void Action_ParametersCanBeEnumsEntitiesAndLists()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["gender"] = PersonGender.Female,
            ["person"] = new Person { UserName = "u" },
            ["emails"] = new List<string> { "a", "b" },
            ["people"] = new List<Person> { new() { UserName = "x" } },
            ["nothing"] = null,
        };

        var request = new SingleValueActionRequestBuilder<VdmVoid>("/s", "Do", parameters).ToRequest();

        // Like Java's serializeNulls Gson, action parameters carry the null properties of nested entities.
        Asserts.JsonLenient(
            "{\"gender\":\"Female\",\"person\":{\"@odata.type\":\"#Trippin.Person\",\"UserName\":\"u\"},\"emails\":[\"a\",\"b\"]," +
            "\"people\":[{\"@odata.type\":\"#Trippin.Person\",\"UserName\":\"x\"}],\"nothing\":null}",
            request.ActionParameters);
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(ThrowingConverter))]
    private sealed class BadValue
    {
    }

    private sealed class ThrowingConverter : System.Text.Json.Serialization.JsonConverter<BadValue>
    {
        public override BadValue Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(System.Text.Json.Utf8JsonWriter writer, BadValue value, System.Text.Json.JsonSerializerOptions options) =>
            throw new InvalidOperationException("cannot be serialized");
    }
}
