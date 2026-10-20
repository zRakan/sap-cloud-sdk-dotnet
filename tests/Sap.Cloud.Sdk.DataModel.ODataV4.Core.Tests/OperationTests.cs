using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>
/// Functions, actions and bound operations: URL generation, parameter encoding, request bodies and results. Ports
/// <c>SingleValueFunctionRequestBuilderTest</c>, <c>CollectionValueFunctionRequestBuilderTest</c>,
/// <c>SingleValueActionRequestBuilderTest</c>, <c>CollectionValueActionRequestBuilderTest</c>, <c>BoundFunctionTest</c>,
/// <c>BoundActionTest</c> and the unbound function and action tests.
/// </summary>
public class OperationTests
{
    private const string DefaultServicePath = "/odata/default";
    private const string Function = "TestFunction";
    private const string Action = "TestAction";

    private static readonly Dictionary<string, object?> FunctionParameters = new()
    {
        ["stringParameter"] = "test",
        ["booleanParameter"] = true,
        ["integerParameter"] = 9000,
        ["decimalParameter"] = 3.14,
        ["durationParameter"] = TimeSpan.FromHours(8),
        ["dateTimeParameter"] = new DateTime(2019, 12, 25, 8, 0, 0, DateTimeKind.Unspecified),
    };

    private static string ExpectedParameters() => ODataFunctionParameters.Of(FunctionParameters, ODataProtocol.V4).ToEncodedString();

    private static string TestData(string directory, string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", directory, file));

    private static FakeHttpHandler Handler(Func<RecordedRequest, HttpResponseMessage> responder) =>
        new(request => request.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", "token")
            : responder(request));

    // ------------------------------------------------------------------ unbound functions

    [Fact]
    public void Function_WithoutParameters()
    {
        var single = new SingleValueFunctionRequestBuilder<object>(DefaultServicePath, Function);
        var collection = new CollectionValueFunctionRequestBuilder<object>(DefaultServicePath, Function);

        Assert.Equal($"{DefaultServicePath}/{Function}()", single.ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal($"{DefaultServicePath}/{Function}()", collection.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Function_WithParameters()
    {
        var single = new SingleValueFunctionRequestBuilder<object>(DefaultServicePath, Function, FunctionParameters);
        var collection = new CollectionValueFunctionRequestBuilder<object>(DefaultServicePath, Function, FunctionParameters);

        Assert.Equal(DefaultServicePath + "/" + Function + ExpectedParameters(), single.ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal(DefaultServicePath + "/" + Function + ExpectedParameters(), collection.ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal(
            "(stringParameter='test',booleanParameter=true,integerParameter=9000,decimalParameter=3.14,durationParameter=duration'PT8H',dateTimeParameter=2019-12-25T08:00:00Z)",
            Uri.UnescapeDataString(ExpectedParameters()));
    }

    [Fact]
    public void Function_QuoteInStringParameterIsEscaped()
    {
        var single = new SingleValueFunctionRequestBuilder<object>(DefaultServicePath, Function, new Dictionary<string, object?> { ["stringParameter"] = "t'est" });

        Assert.Equal($"{DefaultServicePath}/{Function}(stringParameter='t''est')", single.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Function_DoubleParametersKeepTheirFraction()
    {
        var service = new DefaultTrippinService();

        Assert.Equal(
            ITrippinService.DefaultServicePath + "/GetNearestAirport(lat=33.0,lon=-118.0)",
            service.GetNearestAirport(33.0, -118.0).ToRequest().GetRelativeUri().OriginalString);
        Assert.Equal(
            ITrippinService.DefaultServicePath + "/GetPersonWithMostFriends()",
            new DefaultTrippinService().GetPersonWithMostFriends().ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public async Task SingleValueFunction_ReturnsPrimitivesStringsAndEntities()
    {
        var floatHandler = Handler(_ => Responses.Json("{\"value\":3.14}"));
        var stringHandler = Handler(_ => Responses.Json("{\"value\":\"Works\"}"));
        var entityHandler = Handler(_ => Responses.Json("{\"Name\":\"Tester\"}"));

        Assert.Equal(3.14f, await new SingleValueFunctionRequestBuilder<float>(DefaultServicePath, Function).ExecuteAsync(floatHandler.CreateClient()));
        Assert.Equal("Works", await new SingleValueFunctionRequestBuilder<string>(DefaultServicePath, Function).ExecuteAsync(stringHandler.CreateClient()));
        var entity = await new SingleValueFunctionRequestBuilder<NamedEntity>(DefaultServicePath, Function).ExecuteAsync(entityHandler.CreateClient());
        Assert.Equal("Tester", entity.Name);
        Assert.Equal(HttpMethod.Get, Assert.Single(entityHandler.Requests).Method);
    }

    [Fact]
    public async Task CollectionValueFunction_ReturnsPrimitivesAndEntities()
    {
        var floatHandler = Handler(_ => Responses.Json("{\"value\":[3.14,9.81]}"));
        var entityHandler = Handler(_ => Responses.Json("{\"value\":[{\"Name\":\"Tester1\"},{\"Name\":\"Tester2\"}]}"));

        Assert.Equal([3.14f, 9.81f], await new CollectionValueFunctionRequestBuilder<float>(DefaultServicePath, Function).ExecuteAsync(floatHandler.CreateClient()));
        var entities = await new CollectionValueFunctionRequestBuilder<NamedEntity>(DefaultServicePath, Function).ExecuteAsync(entityHandler.CreateClient());
        Assert.Equal([new NamedEntity { Name = "Tester1" }, new NamedEntity { Name = "Tester2" }], entities);
    }

    // ------------------------------------------------------------------ unbound actions

    [Fact]
    public async Task SingleValueAction_WithParametersAndNoResponse()
    {
        var handler = Handler(_ => Responses.NoContent());
        var parameters = new Dictionary<string, object?>
        {
            ["stringParameter"] = "test",
            ["booleanParameter"] = true,
            ["integerParameter"] = 9000,
            ["decimalParameter"] = 3.14d,
            ["durationParameter"] = TimeSpan.FromHours(8),
            ["dateTimeOffsetParameter"] = new DateTimeOffset(2020, 3, 12, 5, 2, 30, TimeSpan.Zero),
            ["timeOfDayParameter"] = new TimeOnly(13, 3, 39, 999),
        };
        var sut = new SingleValueActionRequestBuilder<VdmVoid>(DefaultServicePath, Action, parameters);

        Assert.Equal($"{DefaultServicePath}/{Action}", sut.ToRequest().GetRelativeUri().OriginalString);
        var response = await sut.ExecuteAsync(handler.CreateClient());

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(["application/json"], post.GetHeader("Content-Type").Select(v => v.Split(';')[0]));
        Asserts.JsonEqual(TestData("SingleValueActionRequestBuilderTest", "ActionParametersRequestBody.json"), post.Body!);
        var head = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Head);
        Assert.Equal(DefaultServicePath, head.PathAndQuery);
        Assert.Equal("fetch", head.GetFirstHeader("x-csrf-token"));
        Assert.False(response.HasResponseResult);
        Assert.Null(response.ResponseResult);
        Assert.Equal(204, response.ResponseStatusCode);
    }

    [Fact]
    public async Task SingleValueAction_WithNoParametersAndPrimitiveResponse()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":3.14}"));

        var response = await new SingleValueActionRequestBuilder<float>(DefaultServicePath, Action).ExecuteAsync(handler.CreateClient());

        Asserts.JsonEqual("{}", Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post).Body!);
        Assert.True(response.HasResponseResult);
        Assert.Equal(3.14f, response.ResponseResult);
        Assert.Equal(200, response.ResponseStatusCode);
    }

    [Fact]
    public async Task SingleValueAction_WithStringResponse()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":\"Works\"}"));

        var response = await new SingleValueActionRequestBuilder<string>(DefaultServicePath, Action).ExecuteAsync(handler.CreateClient());

        Assert.Equal("Works", response.ResponseResult);
    }

    [Fact]
    public async Task SingleValueAction_WithEntityParameterAndEntityResponse()
    {
        var handler = Handler(_ => Responses.Json("{ \"Name\" : \"Tester\" }"));
        var parameters = new Dictionary<string, object?> { ["entityParameter"] = new NamedEntity { Name = "Tester" } };

        var response = await new SingleValueActionRequestBuilder<NamedEntity>(DefaultServicePath, Action, parameters).ExecuteAsync(handler.CreateClient());

        Asserts.JsonEqual(TestData("SingleValueActionRequestBuilderTest", "ActionEntityRequestBody.json"), Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post).Body!);
        Assert.Equal("Tester", response.ResponseResult!.Name);
    }

    [Fact]
    public async Task SingleValueAction_WithNullEntityParameter()
    {
        var handler = Handler(_ => Responses.Json("{ \"Name\" : \"Tester\" }"));
        var parameters = new Dictionary<string, object?> { ["entityParameter"] = null };

        var response = await new SingleValueActionRequestBuilder<NamedEntity>(DefaultServicePath, Action, parameters).ExecuteAsync(handler.CreateClient());

        Asserts.JsonEqual(TestData("SingleValueActionRequestBuilderTest", "ActionNullEntityRequestBody.json"), Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post).Body!);
        Assert.Equal("Tester", response.ResponseResult!.Name);
    }

    [Fact]
    public async Task SingleValueAction_WithComplexTypeParameterAndResponse()
    {
        var handler = Handler(_ => Responses.Json("{\"City\": \"Stockholm\",\"Country\": \"Sweden\"}"));
        var parameters = new Dictionary<string, object?> { ["complexEntity"] = new CityCountry { City = "Stockholm", Country = "Sweden" } };

        var response = await new SingleValueActionRequestBuilder<CityCountry>(DefaultServicePath, Action, parameters).ExecuteAsync(handler.CreateClient());

        Asserts.JsonEqual(TestData("SingleValueActionRequestBuilderTest", "ActionComplexTypeRequestBody.json"), Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post).Body!);
        Assert.Equal("Stockholm", response.ResponseResult!.City);
        Assert.Equal("Sweden", response.ResponseResult.Country);
    }

    [Fact]
    public async Task SingleValueAction_WithoutCsrfToken()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":3.14}"));

        await new SingleValueActionRequestBuilder<float>(DefaultServicePath, Action).WithoutCsrfToken().ExecuteAsync(handler.CreateClient());

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Head);
    }

    [Fact]
    public async Task CollectionValueAction_ReturnsPrimitivesStringsComplexValuesAndEntities()
    {
        var floats = Handler(_ => Responses.Json("{\"value\" : [ 3.14, 9.81 ]}"));
        var strings = Handler(_ => Responses.Json("{\"value\" : [ \"It\", \"works\",\"as\",\"expected\" ]}"));
        var complex = Handler(_ => Responses.Json("{ \"value\" : [{ \"City\" : \"Stockholm\" ,\"Country\" : \"Sweden\"},{ \"City\" : \"Dubrovnik\",\"Country\" : \"Croatia\" }]}"));
        var entities = Handler(_ => Responses.Json("{ \"value\" : [{ \"Name\" : \"Tester1\" },{ \"Name\" : \"Tester2\" }]}"));

        var floatResponse = await new CollectionValueActionRequestBuilder<float>(DefaultServicePath, Action).ExecuteAsync(floats.CreateClient());
        var stringResponse = await new CollectionValueActionRequestBuilder<string>(DefaultServicePath, Action).ExecuteAsync(strings.CreateClient());
        var complexResponse = await new CollectionValueActionRequestBuilder<CityCountry>(DefaultServicePath, Action).ExecuteAsync(complex.CreateClient());
        var entityResponse = await new CollectionValueActionRequestBuilder<NamedEntity>(DefaultServicePath, Action).ExecuteAsync(entities.CreateClient());

        Assert.Equal([3.14f, 9.81f], floatResponse.ResponseResult);
        Assert.Equal(["It", "works", "as", "expected"], stringResponse.ResponseResult);
        Assert.Equal(2, complexResponse.ResponseResult!.Count);
        Assert.Equal("Dubrovnik", complexResponse.ResponseResult[1].City);
        Assert.Equal(new CityCountry { City = "Stockholm", Country = "Sweden" }, complexResponse.ResponseResult[0]);
        Assert.Equal([new NamedEntity { Name = "Tester1" }, new NamedEntity { Name = "Tester2" }], entityResponse.ResponseResult);
        Asserts.JsonEqual("{}", Assert.Single(floats.Requests, r => r.Method == HttpMethod.Post).Body!);
        Assert.Equal(200, floatResponse.ResponseStatusCode);
    }

    [Fact]
    public async Task CollectionValueAction_WithoutCsrfToken()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[3.14]}"));

        await new CollectionValueActionRequestBuilder<float>(DefaultServicePath, Action).WithoutCsrfToken().ExecuteAsync(handler.CreateClient());

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Head);
    }

    [Fact]
    public void Action_ResourceAndBodyOfServiceImports()
    {
        var request = new DefaultTrippinService().ResetDataSource().ToRequest();

        Assert.Equal(ITrippinService.DefaultServicePath + "/ResetDataSource", request.GetRelativeUri().OriginalString);
        Assert.Equal("{}", request.ActionParameters);
    }

    // ------------------------------------------------------------------ bound functions

    private static readonly DefaultTrippinService Service = (DefaultTrippinService)new DefaultTrippinService().WithServicePath("service-root");

    private static Person Fridolin => Person.Builder().UserName("Fridolin").Build();

    [Fact]
    public void BoundFunction_EntityToPrimitive()
    {
        var function = Service.ForEntity(Fridolin).ApplyFunction(Person.IsHappy());

        Assert.Equal("/service-root/People('Fridolin')/Trippin.IsHappy()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_NavigationToPrimitive()
    {
        var function = Service.ForEntity(Fridolin).NavigateTo(Person.Fields.BestFriend).ApplyFunction(Person.IsHappy());

        Assert.Equal("/service-root/People('Fridolin')/BestFriend/Trippin.IsHappy()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_CollectionToPrimitive()
    {
        var function = Service.ForEntity(Fridolin).NavigateTo(Person.Fields.Friends).ApplyFunction(Person.AreAllFriends());

        Assert.Equal("/service-root/People('Fridolin')/Friends/Trippin.AreAllFriends()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_CollectionToCollection()
    {
        var function = Service.ApplyFunction(Person.MostPopularPersons());

        Assert.Equal("/service-root/People/Trippin.MostPopularPersons()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_CollectionOnRootEntity()
    {
        var function = Service.ApplyFunction(Person.AreAllFriends());

        Assert.Equal("/service-root/People/Trippin.AreAllFriends()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_EntityToEntity()
    {
        var function = Service.ForEntity(Fridolin).ApplyFunction(Person.WorstFriend());

        Assert.Equal("/service-root/People('Fridolin')/Trippin.WorstFriend()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_CompositionOnEntity()
    {
        var request = Service.ForEntity(Fridolin).WithFunction(Person.WorstFriend()).NavigateTo(Person.Fields.Friends).GetAll().Top(5);

        Assert.Equal("/service-root/People('Fridolin')/Trippin.WorstFriend()/Friends?$top=5", request.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_CompositionOnRootEntity()
    {
        var request = Service.WithFunction(Person.MostPopularPerson()).NavigateTo(Person.Fields.Friends).GetAll().Top(5);

        Assert.Equal("/service-root/People/Trippin.MostPopularPerson()/Friends?$top=5", request.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_GetAllAfterFunctionResult()
    {
        var request = Service.WithFunction(Person.MostPopularPersons()).GetAll();

        Assert.Equal("/service-root/People/Trippin.MostPopularPersons()", request.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_SubsequentFunctionInvocation()
    {
        var request = Service.WithFunction(Person.MostPopularPersons()).ApplyFunction(Person.AreAllFriends());

        Assert.Equal(
            "/service-root/People/Trippin.MostPopularPersons()/Trippin.AreAllFriends()",
            request.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_AdditionalParameters()
    {
        var function = Service.ForEntity(Fridolin).ApplyFunction(Person.IsHappy(true));

        Assert.Equal("/service-root/People('Fridolin')/Trippin.IsHappy(really=true)", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_StringParameterIsQuoted()
    {
        var function = Service.ForEntity(Fridolin).ApplyFunction(Person.GetFriendsTrips("a'b"));

        Assert.Equal("/service-root/People('Fridolin')/Trippin.GetFriendsTrips(userName='a''b')", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_OfOtherEntities()
    {
        var trip = Trip.Builder().TripId(1003).Build();

        var function = Service.ForEntity(Fridolin).NavigateTo(Person.Fields.Trips).ForEntity(trip).ApplyFunction(Trip.GetInvolvedPeople());

        Assert.Equal("/service-root/People('Fridolin')/Trips(1003)/Trippin.GetInvolvedPeople()", function.ToRequest().GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundFunction_AllCombinationsCompile()
    {
        var entity = Service.ForEntity(Fridolin);
        SingleValueFunctionRequestBuilder<Location> complex = entity.ApplyFunction(new BoundFunction.SingleToSingleComplex<Person, Location>("Stuff"));
        SingleValueFunctionRequestBuilder<Person> entityResult = entity.ApplyFunction(new BoundFunction.SingleToSingleEntity<Person, Person>("Stuff"));
        SingleValueFunctionRequestBuilder<string> primitive = entity.ApplyFunction(new BoundFunction.SingleToSinglePrimitive<Person, string>("Stuff"));
        CollectionValueFunctionRequestBuilder<string> a = entity.ApplyFunction(new BoundFunction.SingleToCollectionPrimitive<Person, string>("Stuff"));
        CollectionValueFunctionRequestBuilder<Location> b = entity.ApplyFunction(new BoundFunction.SingleToCollectionComplex<Person, Location>("Stuff"));
        CollectionValueFunctionRequestBuilder<Person> c = entity.ApplyFunction(new BoundFunction.SingleToCollectionEntity<Person, Person>("Stuff"));
        SingleValueFunctionRequestBuilder<string> d = Service.ApplyFunction(new BoundFunction.CollectionToSinglePrimitive<Person, string>("Stuff"));
        SingleValueFunctionRequestBuilder<Location> e = Service.ApplyFunction(new BoundFunction.CollectionToSingleComplex<Person, Location>("Stuff"));
        SingleValueFunctionRequestBuilder<Person> f = Service.ApplyFunction(new BoundFunction.CollectionToSingleEntity<Person, Person>("Stuff"));
        CollectionValueFunctionRequestBuilder<string> g = Service.ApplyFunction(new BoundFunction.CollectionToCollectionPrimitive<Person, string>("Stuff"));
        CollectionValueFunctionRequestBuilder<Location> h = Service.ApplyFunction(new BoundFunction.CollectionToCollectionComplex<Person, Location>("Stuff"));
        CollectionValueFunctionRequestBuilder<Person> i = Service.ApplyFunction(new BoundFunction.CollectionToCollectionEntity<Person, Person>("Stuff"));

        Assert.All<object>([complex, entityResult, primitive, a, b, c, d, e, f, g, h, i], Assert.NotNull);
    }

    // ------------------------------------------------------------------ bound actions

    [Fact]
    public void BoundAction_OnEntityWithETagSendsIfMatch()
    {
        var person = Fridolin;
        person.VersionIdentifier = "some-etag";

        var action = Service.ForEntity(person).ApplyAction(Person.MakeHappy());
        var request = action.ToRequest();

        Assert.Equal("/service-root/People('Fridolin')/Trippin.MakeHappy", request.GetRelativeUri().OriginalString);
        Assert.Equal("{}", request.ActionParameters);
        Assert.Equal(["some-etag"], request.GetHeaders()["If-Match"]);
    }

    [Fact]
    public void BoundAction_OnEntityWithoutETagSendsNoIfMatch()
    {
        var request = Service.ForEntity(Fridolin).ApplyAction(Person.MakeHappy()).ToRequest();

        Assert.Equal("/service-root/People('Fridolin')/Trippin.MakeHappy", request.GetRelativeUri().OriginalString);
        Assert.False(request.GetHeaders().ContainsKey("If-Match"));
    }

    [Fact]
    public void BoundAction_WithParameters()
    {
        var request = Service.ForEntity(Fridolin).ApplyAction(Person.MakeUnhappy(true)).ToRequest();

        Assert.Equal("/service-root/People('Fridolin')/Trippin.MakeUnhappy", request.GetRelativeUri().OriginalString);
        Assert.Equal("{\"very\":true}", request.ActionParameters);
    }

    [Fact]
    public void BoundAction_OnCollection()
    {
        var request = Service.ApplyAction(Person.MakeAllHappy()).ToRequest();

        Assert.Equal("/service-root/People/Trippin.MakeAllHappy", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundAction_SingleToCollection()
    {
        var request = Service.ForEntity(Fridolin).ApplyAction(Person.SendMail("foo")).ToRequest();

        Assert.Equal("{\"subject\":\"foo\"}", request.ActionParameters);
        Assert.Equal("/service-root/People('Fridolin')/Trippin.sendMail", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void BoundAction_NullParametersAreSent()
    {
        var request = Service.ForEntity(Fridolin).ApplyAction(Person.SendMail(null)).ToRequest();

        Assert.Equal("{\"subject\":null}", request.ActionParameters);
    }

    [Fact]
    public void BoundAction_OnNestedCollection()
    {
        var request = Service.ForEntity(Fridolin).NavigateTo(Person.Fields.Friends).ApplyAction(Person.MakeAllHappy()).ToRequest();

        Assert.Equal("/service-root/People('Fridolin')/Friends/Trippin.MakeAllHappy", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public async Task BoundAction_ExecutesAndParsesTheResult()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":true}"));

        var response = await Service.ForEntity(Fridolin).ApplyAction(Person.UpdateLastName("Whyte")).ExecuteAsync(handler.CreateClient());

        Assert.True(response.ResponseResult);
        Asserts.JsonEqual("{\"lastName\":\"Whyte\"}", Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post).Body!);
    }

    [Fact]
    public void BoundAction_AllCombinationsCompile()
    {
        var entity = Service.ForEntity(Fridolin);
        SingleValueActionRequestBuilder<Location> a = entity.ApplyAction(new BoundAction.SingleToSingle<Person, Location>("Stuff"));
        CollectionValueActionRequestBuilder<string> b = entity.ApplyAction(new BoundAction.SingleToCollection<Person, string>("Stuff"));
        SingleValueActionRequestBuilder<string> c = Service.ApplyAction(new BoundAction.CollectionToSingle<Person, string>("Stuff"));
        CollectionValueActionRequestBuilder<string> d = Service.ApplyAction(new BoundAction.CollectionToCollection<Person, string>("Stuff"));

        Assert.All<object>([a, b, c, d], Assert.NotNull);
    }

    [Fact]
    public void BoundOperation_KnowsItsTypes()
    {
        var function = Person.GetFriendsTrips("x");

        Assert.Equal("Trippin.GetFriendsTrips", function.QualifiedName);
        Assert.Equal(typeof(Person), function.BindingType);
        Assert.Equal(typeof(Trip), function.ReturnType);
        Assert.Equal("(userName='x')", function.Parameters.ToString());
    }
}
