using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>Navigation from entities to related entities with <c>ForEntity</c> and <c>NavigateTo</c>. Ports <c>NestedEntityTest</c>.</summary>
public class NavigableEntityTests
{
    private static readonly ITrippinService Service = new DefaultTrippinService().WithServicePath("/TripPinServiceRW");

    private static Person Russell => Person.Builder().UserName("russellwhyte").Build();

    private static FakeHttpHandler Handler(Func<RecordedRequest, HttpResponseMessage> responder) =>
        new(request => request.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", "foo")
            : responder(request));

    [Fact]
    public void Get_SingleNestedFriend()
    {
        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.BestFriend).Get().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/BestFriend", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Get_NestedNestedNestedFriend()
    {
        var request = Service.ForEntity(Russell)
            .NavigateTo(Person.Fields.BestFriend)
            .NavigateTo(Person.Fields.BestFriend)
            .NavigateTo(Person.Fields.BestFriend)
            .Get()
            .ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/BestFriend/BestFriend/BestFriend", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Delete_SingleNestedFriend()
    {
        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.BestFriend).Delete().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/BestFriend", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Update_SingleNestedFriend()
    {
        var friend = new Person { LastName = "Jobs" };

        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.BestFriend).Update(friend).ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/BestFriend", request.GetRelativeUri().OriginalString);
        Assert.Equal("{\"LastName\":\"Jobs\",\"@odata.type\":\"#Trippin.Person\"}", request.SerializedEntity);
    }

    [Fact]
    public void Create_NestedTrip()
    {
        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.Trips).Create(new Trip()).ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void GetAll_NestedTrips()
    {
        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.Trips).GetAll().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Delete_BestFriendMatchingAnyVersion()
    {
        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.BestFriend).Delete().MatchAnyVersionIdentifier().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/BestFriend", request.GetRelativeUri().OriginalString);
        Assert.Equal("*", request.VersionIdentifier);
    }

    [Fact]
    public void Update_BestFriendMatchingAnyVersion()
    {
        var oldPerson = Person.Builder().UserName("oldMan").Build();
        var newPerson = Person.Builder().UserName("youngMan").Build();

        var request = Service.ForEntity(oldPerson).NavigateTo(Person.Fields.BestFriend).Update(newPerson).MatchAnyVersionIdentifier().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('oldMan')/BestFriend", request.GetRelativeUri().OriginalString);
        Assert.Equal("*", request.VersionIdentifier);
    }

    [Fact]
    public void Count_NestedTrip()
    {
        var request = Service.ForEntity(Russell).NavigateTo(Person.Fields.Trips).Count().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips/$count", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Count_ToRequestIsStable()
    {
        var builder = Service.ForEntity(Russell).NavigateTo(Person.Fields.Trips).Count();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips/$count", builder.ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips/$count", builder.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void ForEntity_CanBeCalledRepeatedlyOnTheSameCollection()
    {
        var trips = Service.ForEntity(Russell).NavigateTo(Person.Fields.Trips);

        var first = trips.ForEntity(Trip.Builder().TripId(1).Build()).Get().ToRequest();
        var second = trips.ForEntity(Trip.Builder().TripId(2).Build()).Get().ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips(1)", first.GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips(2)", second.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void GetAll_NestedPlanItems()
    {
        var tripByKey = Trip.Builder().TripId(1003).Build();

        var request = Service.ForEntity(Russell)
            .NavigateTo(Person.Fields.Trips)
            .ForEntity(tripByKey)
            .NavigateTo(Trip.Fields.PlanItems)
            .GetAll()
            .ToRequest();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips(1003)/PlanItems", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public async Task Create_NestedPlanItem_ExecutesWithCustomHeaderAndQueryParameter()
    {
        var tripByKey = Trip.Builder().TripId(1003).Build();
        var builder = Service.ForEntity(Russell)
            .NavigateTo(Person.Fields.Trips)
            .ForEntity(tripByKey)
            .NavigateTo(Trip.Fields.PlanItems)
            .Create(new PlanItem())
            .WithHeader("header", "value")
            .WithQueryParameter("query", "parameter");

        var request = builder.ToRequest();
        Assert.Equal(
            "/TripPinServiceRW/People('russellwhyte')/Trips(1003)/PlanItems?query=parameter",
            request.GetRelativeUri().OriginalString);

        var handler = Handler(_ => Responses.Json("{}"));
        var response = await builder.ExecuteAsync(handler.CreateClient());

        Assert.Equal(200, response.ResponseStatusCode);
        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("/TripPinServiceRW/People('russellwhyte')/Trips(1003)/PlanItems?query=parameter", post.PathAndQuery);
        Assert.Equal("value", post.GetFirstHeader("header"));
        Assert.Equal("/TripPinServiceRW", Assert.Single(handler.Requests, r => r.Method == HttpMethod.Head).PathAndQuery);
    }

    [Fact]
    public async Task Delete_EntityWithETag()
    {
        var person = Russell;
        person.VersionIdentifier = "foobar";
        var handler = Handler(_ => Responses.Json("{}"));
        var delete = Service.ForEntity(person).Delete();

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')", delete.ToRequest().GetRelativeUri().OriginalString);
        await delete.ExecuteAsync(handler.CreateClient());

        var sent = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.Equal("foobar", sent.GetFirstHeader("If-Match"));
    }

    [Fact]
    public async Task Update_EntityWithETag()
    {
        var person = Russell;
        person.VersionIdentifier = "foobar";
        var handler = Handler(_ => Responses.Json("{}"));
        var update = Service.ForEntity(person).Update(person);

        Assert.Equal("/TripPinServiceRW/People('russellwhyte')", update.ToRequest().GetRelativeUri().OriginalString);
        await update.ExecuteAsync(handler.CreateClient());

        var sent = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Equal("foobar", sent.GetFirstHeader("If-Match"));
    }

    [Fact]
    public void ForEntity_RequiresAnEntitySet()
    {
        var withoutEntitySet = new EntityWithoutEntitySet();
        var withEntitySet = new EntityWithEntitySet();

        Assert.Throws<InvalidOperationException>(() => Service.ForEntity(withoutEntitySet));
        Assert.NotNull(Service.ForEntity(withEntitySet));
        Assert.NotNull(Service.ForEntity(withEntitySet).NavigateTo(EntityWithEntitySet.NavigationProperty).ForEntity(withoutEntitySet));
    }

    [Fact]
    public void GetAndCountAndCreate_OnEntityCollections()
    {
        Assert.Equal("/TripPinServiceRW/People", Service.GetAllPeople().ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/People/$count", Service.CountPeople().ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/Airlines('AA')", Service.GetAirlinesByKey("AA").ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/Airports('KSFO')", Service.GetAirportsByKey("KSFO").ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/Airlines", Service.CreateAirlines(new Airline()).ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/Airlines('AA')", Service.DeleteAirlines(new Airline { AirlineCode = "AA" }).ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal("/TripPinServiceRW/Airports('K')", Service.UpdateAirports(new Airport { IcaoCode = "K" }).ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void ServicePath_CanBeChanged()
    {
        var service = new DefaultTrippinService();

        Assert.Equal(ITrippinService.DefaultServicePath, service.ServicePath);
        Assert.Equal("/other", ((DefaultTrippinService)service.WithServicePath("/other")).ServicePath);
    }
}
