using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>
/// Executing the read and modifying builders against a fake service: deserialization of results, ETags, modification
/// responses, pagination and errors. Ports <c>ETagTest</c>, <c>RequestBuilderEtagParsingTest</c>,
/// <c>ModificationResponseTest</c>, <c>HttpResponseEvaluationTest</c> and <c>CountEntityUnitTest</c>.
/// </summary>
public class ReadAndModifyTests
{
    private const string ServiceUrl = "/some/service";
    private const string GetByKeyUrl = ServiceUrl + "/People('russellwhyte')";
    private const string EtagRussell = "W/\"123\"";
    private const string EtagRussellUpdated = "W/\"456\"";
    private const string EtagScott = "W/\"999\"";

    private static readonly ITrippinService Service = new DefaultTrippinService().WithServicePath(ServiceUrl);

    private static string TestData(string directory, string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", directory, file));

    private static FakeHttpHandler Handler(Func<RecordedRequest, HttpResponseMessage> responder) =>
        new(request => request.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", "awesome-csrf-token")
            : responder(request));

    // ------------------------------------------------------------------ ETag

    [Fact]
    public async Task GetAll_ParsesETagsOfThePayload()
    {
        var handler = Handler(_ => Responses.Json(TestData("ETagTest", "GetAllResponseBody.json")));

        var result = await Service.GetAllPeople().ExecuteAsync(handler.CreateClient());

        Assert.Equal(EtagRussell, result[0].VersionIdentifier);
        Assert.Equal(EtagScott, result[1].VersionIdentifier);
        Assert.Equal("russellwhyte", result[0].UserName);
        Assert.Equal(["@context"], result[0].GetCustomFieldNames());
        Assert.Empty(result[0].GetChangedFields());
    }

    [Fact]
    public async Task Create_ParsesETagHeaderOfTheResponse()
    {
        var handler = Handler(_ => Responses.Json(TestData("ETagTest", "GetSingleResponseBody.json")).WithHeader("ETag", EtagRussell));

        var result = await Service.CreatePeople(Person.Builder().FirstName("Russel").Build()).ExecuteAsync(handler.CreateClient());

        Assert.Equal(EtagRussell, result.ModifiedEntity.VersionIdentifier);
        Assert.Equal("Russell", result.ModifiedEntity.FirstName);
        Assert.Equal("Russel", result.RequestEntity.FirstName);
    }

    [Fact]
    public async Task Update_ParsesETagHeaderOfTheResponse()
    {
        var handler = Handler(_ => Responses.Json(TestData("ETagTest", "GetSingleResponseBody.json")).WithHeader("ETag", EtagRussell));

        var result = await Service.UpdatePeople(Person.Builder().UserName("russellwhyte").Build()).ExecuteAsync(handler.CreateClient());

        Assert.Equal(EtagRussell, result.ModifiedEntity.VersionIdentifier);
    }

    [Fact]
    public async Task GetByKey_ParsesETagHeaderOfTheResponse()
    {
        var handler = Handler(_ => Responses.Json(TestData("ETagTest", "GetSingleResponseBody.json")).WithHeader("ETag", EtagRussell));

        var russell = await Service.GetPeopleByKey("russellwhyte").ExecuteAsync(handler.CreateClient());

        Assert.Equal(EtagRussell, russell.VersionIdentifier);
        Assert.Equal(GetByKeyUrl, Assert.Single(handler.Requests).PathAndQuery);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Update_SendsIfMatchAccordingToTheStrategy(bool replace, bool disabled)
    {
        var handler = Handler(_ => Responses.NoContent().WithHeader("ETag", EtagRussellUpdated));
        var russell = new Person { UserName = "russellwhyte", VersionIdentifier = EtagRussell };
        var builder = Service.UpdatePeople(russell);
        builder = replace ? builder.ReplacingEntity() : builder.ModifyingEntity();
        if (disabled)
        {
            builder.DisableVersionIdentifier();
        }

        var result = await builder.ExecuteAsync(handler.CreateClient());

        var sent = Assert.Single(handler.Requests, r => r.Method != HttpMethod.Head);
        Assert.Equal(replace ? HttpMethod.Put : HttpMethod.Patch, sent.Method);
        Assert.Equal(disabled ? null : EtagRussell, sent.GetFirstHeader("If-Match"));
        Assert.Equal(EtagRussell, russell.VersionIdentifier);
        Assert.Equal(EtagRussellUpdated, result.UpdatedVersionIdentifier);
        Assert.Equal(EtagRussellUpdated, result.ModifiedEntity.VersionIdentifier);
        Assert.Null(result.ResponseEntity);
        Assert.Equal(204, result.ResponseStatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_MatchesAnyETagIfChosen(bool replace)
    {
        var handler = Handler(_ => Responses.NoContent().WithHeader("ETag", EtagRussellUpdated));
        var russell = new Person { UserName = "russellwhyte", VersionIdentifier = EtagRussell };
        var builder = Service.UpdatePeople(russell);

        await (replace ? builder.ReplacingEntity() : builder.ModifyingEntity()).MatchAnyVersionIdentifier().ExecuteAsync(handler.CreateClient());

        Assert.Equal("*", Assert.Single(handler.Requests, r => r.Method != HttpMethod.Head).GetFirstHeader("If-Match"));
    }

    [Fact]
    public async Task Delete_SendsTheETagByDefault()
    {
        var handler = Handler(_ => Responses.NoContent());
        var russell = new Person { UserName = "russellwhyte", VersionIdentifier = EtagRussell };

        await Service.DeletePeople(russell).ExecuteAsync(handler.CreateClient());

        var sent = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.Equal(GetByKeyUrl, sent.PathAndQuery);
        Assert.Equal(EtagRussell, sent.GetFirstHeader("If-Match"));
    }

    [Fact]
    public async Task Delete_LacksTheETagIfDisabled()
    {
        var handler = Handler(_ => Responses.NoContent());
        var russell = new Person { UserName = "russellwhyte", VersionIdentifier = EtagRussell };

        await Service.DeletePeople(russell).DisableVersionIdentifier().ExecuteAsync(handler.CreateClient());

        Assert.False(Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete).HasHeader("If-Match"));
    }

    [Fact]
    public async Task Delete_MatchesAnyETagIfChosen()
    {
        var handler = Handler(_ => Responses.NoContent());
        var russell = new Person { UserName = "russellwhyte", VersionIdentifier = EtagRussell };

        await Service.DeletePeople(russell).MatchAnyVersionIdentifier().ExecuteAsync(handler.CreateClient());

        Assert.Equal("*", Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete).GetFirstHeader("If-Match"));
    }

    [Fact]
    public async Task GetByKey_WithoutETag()
    {
        var handler = Handler(_ => Responses.Json(TestData("ETagTest", "GetSingleResponseBody.json")));

        var person = await Service.GetPeopleByKey("russellwhyte").ExecuteAsync(handler.CreateClient());

        Assert.Null(person.VersionIdentifier);
    }

    [Fact]
    public async Task NoIfMatchIsSentWithoutETag()
    {
        var handler = Handler(_ => Responses.NoContent());
        var person = new Person { UserName = "russellwhyte" };

        await Service.UpdatePeople(person).ExecuteAsync(handler.CreateClient());
        await Service.DeletePeople(person).ExecuteAsync(handler.CreateClient());

        Assert.All(handler.Requests.Where(r => r.Method != HttpMethod.Head), r => Assert.False(r.HasHeader("If-Match")));
    }

    [Fact]
    public async Task CustomIfMatchHeaderOverwritesTheETagOfTheEntity()
    {
        var handler = Handler(_ => Responses.NoContent());
        var person = new Person { UserName = "russellwhyte", VersionIdentifier = EtagRussell };

        await Service.DeletePeople(person).WithHeader("If-Match", EtagRussellUpdated).ExecuteAsync(handler.CreateClient());
        await Service.UpdatePeople(person).ModifyingEntity().WithHeader("If-Match", EtagRussellUpdated).ExecuteAsync(handler.CreateClient());

        var sent = handler.Requests.Where(r => r.Method != HttpMethod.Head).ToList();
        Assert.Equal(2, sent.Count);
        Assert.All(sent, r => Assert.Equal([EtagRussellUpdated], r.GetHeader("If-Match")));
    }

    [Fact]
    public async Task ETag_HeaderWinsOverPayloadAndPayloadIsUsedWithoutHeader()
    {
        var withBoth = Handler(_ => Responses.Json("{\"@etag\":\"bar\"}").WithHeader("Etag", "foo"));
        var payloadOnly = Handler(_ => Responses.Json("{\"@etag\":\"bar\"}"));
        var headerOnly = Handler(_ => Responses.Json("{}").WithHeader("Etag", "foo"));
        var neither = Handler(_ => Responses.Json("{}"));
        var key = new Dictionary<string, object?> { ["key"] = "val" };

        Assert.Equal("foo", (await new GetByKeyRequestBuilder<BareEntity>("/service", key, "/EntityCollection").ExecuteAsync(withBoth.CreateClient())).VersionIdentifier);
        Assert.Equal("bar", (await new GetByKeyRequestBuilder<BareEntity>("/service", key, "/EntityCollection").ExecuteAsync(payloadOnly.CreateClient())).VersionIdentifier);
        Assert.Equal("foo", (await new GetByKeyRequestBuilder<BareEntity>("/service", key, "/EntityCollection").ExecuteAsync(headerOnly.CreateClient())).VersionIdentifier);
        Assert.Null((await new GetByKeyRequestBuilder<BareEntity>("/service", key, "/EntityCollection").ExecuteAsync(neither.CreateClient())).VersionIdentifier);
    }

    [Fact]
    public async Task ETag_ParsedFromPayloadOfGetAll()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[{\"@etag\":\"bar\"}]}"));

        var entities = await new GetAllRequestBuilder<BareEntity>("/service", "/EntityCollection").ExecuteAsync(handler.CreateClient());

        Assert.Equal("bar", Assert.Single(entities).VersionIdentifier);
    }

    [Theory]
    [InlineData("{\"FUNCTION_NAME\":{}}", null, null)]
    [InlineData("{\"FUNCTION_NAME\":{}}", "foo", "foo")]
    [InlineData("{\"@etag\":\"bar\"}", null, "bar")]
    [InlineData("{\"FUNCTION_NAME\":{\"@etag\":\"bar\"}}", "foo", "foo")]
    public async Task ETag_FunctionAndActionResults(string payload, string? header, string? expected)
    {
        FakeHttpHandler Create()
        {
            return Handler(_ =>
            {
                var response = Responses.Json(payload);
                return header is null ? response : response.WithHeader("Etag", header);
            });
        }

        var function = await new SingleValueFunctionRequestBuilder<BareEntity>("/service", "FUNCTION_NAME").ExecuteAsync(Create().CreateClient());
        var action = await new SingleValueActionRequestBuilder<BareEntity>("/service", "FUNCTION_NAME").ExecuteAsync(Create().CreateClient());

        Assert.Equal(expected, function.VersionIdentifier);
        Assert.True(action.HasResponseResult);
        Assert.Equal(expected, action.ResponseResult!.VersionIdentifier);
    }

    [Theory]
    [InlineData("{}", null, null, null)]
    [InlineData("{}", "foo", "foo", "foo")]
    [InlineData("{\"@etag\":\"bar\"}", null, "bar", "bar")]
    [InlineData("{\"@etag\":\"bar\"}", "foo", "foo", "foo")]
    public async Task ETag_UpdateResponse(string payload, string? header, string? expectedUpdated, string? expectedResponseEntity)
    {
        var handler = Handler(_ =>
        {
            var response = Responses.Json(payload);
            return header is null ? response : response.WithHeader("Etag", header);
        });
        var requestEntity = new BareEntity();

        var result = await new UpdateRequestBuilder<BareEntity>("/service", requestEntity, "/EntityCollection").ExecuteAsync(handler.CreateClient());

        Assert.Same(requestEntity, result.RequestEntity);
        Assert.Equal(expectedUpdated, result.UpdatedVersionIdentifier);
        Assert.Equal(expectedUpdated, result.ModifiedEntity.VersionIdentifier);
        Assert.NotNull(result.ResponseEntity);
        Assert.Equal(expectedResponseEntity, result.ResponseEntity!.VersionIdentifier);
        Assert.Null(requestEntity.VersionIdentifier);
    }

    // ------------------------------------------------------------------ ModificationResponse

    [Fact]
    public async Task ModificationResponse_ParsesEntityAndHeaders()
    {
        var handler = Handler(_ =>
        {
            var response = Responses.Json("{\"foo\":\"bar\"}");
            response.Headers.TryAddWithoutValidation("fizz", "buzz");
            response.Headers.TryAddWithoutValidation("fizz", "fuzz, bizz=1");
            return response;
        });
        var input = new FooObject();

        var modification = await new UpdateRequestBuilder<FooObject>("/service", input, "TestObject").ExecuteAsync(handler.CreateClient());

        Assert.Equal(200, modification.ResponseStatusCode);
        Assert.Same(input, modification.RequestEntity);
        Assert.NotSame(input, modification.ResponseEntity);
        Assert.Equal(new FooObject { Name = "bar" }, modification.ResponseEntity);
        Assert.Equal("bar", modification.ModifiedEntity.Name);
        Assert.NotSame(modification.ResponseEntity, modification.ModifiedEntity);
        Assert.Equal(["buzz", "fuzz, bizz=1"], modification.ResponseHeaders["fizz"]);
    }

    [Fact]
    public async Task ModificationResponse_EmptyResponseKeepsACopyOfTheRequestEntity()
    {
        var handler = Handler(_ => Responses.NoContent());
        var input = new FooObject { Name = "x" };

        var modification = await new UpdateRequestBuilder<FooObject>("/service", input, "TestObject").ExecuteAsync(handler.CreateClient());

        Assert.Equal(204, modification.ResponseStatusCode);
        Assert.Same(input, modification.RequestEntity);
        Assert.Null(modification.ResponseEntity);
        Assert.NotSame(input, modification.ModifiedEntity);
        Assert.Equal(input, modification.ModifiedEntity);
        Assert.Empty(modification.ModifiedEntity.GetChangedFields());
        Assert.Null(modification.UpdatedVersionIdentifier);
    }

    // ------------------------------------------------------------------ Reading

    [Fact]
    public async Task GetByKey_DeserializesExpandedResults()
    {
        var handler = Handler(_ => Responses.Json(
            "{\"@odata.context\":\"c\",\"UserName\":\"russ\",\"Trips\":[{\"TripId\":1,\"PlanItems\":[{\"PlanItemId\":5}]}],\"BestFriend\":{\"UserName\":\"bf\"}}"));

        var person = await Service.GetPeopleByKey("russ")
            .Select(Person.Fields.Trips.Select(Trip.Fields.PlanItems), Person.Fields.BestFriend)
            .ExecuteAsync(handler.CreateClient());

        Assert.Equal("russ", person.UserName);
        Assert.Equal(["@odata.context"], person.GetCustomFieldNames());
        Assert.Equal(5, person.GetTripsIfPresent()![0].GetPlanItemsIfPresent()![0].PlanItemId);
        Assert.Equal("bf", person.GetBestFriendIfPresent()!.UserName);
        Assert.Equal(ServiceUrl + "/People('russ')?$expand=Trips($expand=PlanItems),BestFriend", Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GetAll_FollowsNextLinksAcrossPages()
    {
        var handler = Handler(request => request.PathAndQuery.Contains("skiptoken", StringComparison.Ordinal)
            ? Responses.Json("{\"value\":[{\"UserName\":\"c\"}]}")
            : Responses.Json("{\"value\":[{\"UserName\":\"a\"},{\"UserName\":\"b\"}],\"@odata.nextLink\":\"http://localhost:8080/some/service/People?$skiptoken=2\"}"));

        var people = await Service.GetAllPeople().WithPreferredPageSize(2).ExecuteAsync(handler.CreateClient());

        Assert.Equal(["a", "b", "c"], people.Select(p => p.UserName));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(ServiceUrl + "/People?$skiptoken=2", handler.Requests[1].PathAndQuery);
        Assert.Equal(["odata.maxpagesize=2"], handler.Requests[1].GetHeader("Prefer"));
    }

    [Fact]
    public async Task GetAll_IteratesPagesLazily()
    {
        var handler = Handler(request => request.PathAndQuery.Contains("skiptoken", StringComparison.Ordinal)
            ? Responses.Json("{\"value\":[{\"UserName\":\"c\"}]}")
            : Responses.Json("{\"value\":[{\"UserName\":\"a\"},{\"UserName\":\"b\"}],\"@odata.nextLink\":\"http://localhost:8080/some/service/People?$skiptoken=2\"}"));

        var pages = await Service.GetAllPeople().IteratingPages().ExecuteAsync(handler.CreateClient());

        Assert.Single(handler.Requests);
        var sizes = new List<int>();
        await foreach (var page in pages)
        {
            sizes.Add(page.Count);
        }

        Assert.Equal([2, 1], sizes);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAll_IteratesEntities()
    {
        var handler = Handler(request => request.PathAndQuery.Contains("skiptoken", StringComparison.Ordinal)
            ? Responses.Json("{\"value\":[{\"UserName\":\"c\"}]}")
            : Responses.Json("{\"value\":[{\"UserName\":\"a\"},{\"UserName\":\"b\"}],\"@odata.nextLink\":\"http://localhost:8080/some/service/People?$skiptoken=2\"}"));

        var names = new List<string?>();
        await foreach (var person in await Service.GetAllPeople().IteratingEntities().ExecuteAsync(handler.CreateClient()))
        {
            names.Add(person.UserName);
        }

        Assert.Equal(["a", "b", "c"], names);
    }

    [Fact]
    public async Task GetAll_EmptyResult()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[]}"));

        Assert.Empty(await new GetAllRequestBuilder<BareEntity>("/path", "TestEntitySet").ExecuteAsync(handler.CreateClient()));
    }

    [Fact]
    public async Task Count_ParsesPlainTextAndJson()
    {
        var plain = Handler(_ => Responses.Text("42"));
        var json = Handler(_ => Responses.Json("{\"value\":42}"));

        Assert.Equal(42L, await Service.CountPeople().Search("Foo").Filter(Person.Fields.FirstName.Contains("Bar")).ExecuteAsync(plain.CreateClient()));
        Assert.Equal(42L, await Service.CountPeople().ExecuteAsync(json.CreateClient()));

        var sent = Assert.Single(plain.Requests);
        Assert.StartsWith(ServiceUrl + "/People/$count?", sent.PathAndQuery, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("a")]
    public async Task Count_FailsForInvalidPlainText(string body)
    {
        var handler = Handler(_ => Responses.Text(body));

        await Assert.ThrowsAsync<ODataDeserializationException>(() => Service.CountPeople().ExecuteAsync(handler.CreateClient()));
    }

    [Fact]
    public async Task Count_FailsForUnexpectedJson()
    {
        var handler = Handler(_ => Responses.Json("{\"foo\":\"bar\"}"));

        await Assert.ThrowsAsync<ODataDeserializationException>(() => Service.CountPeople().ExecuteAsync(handler.CreateClient()));
    }

    [Fact]
    public async Task ServiceErrorsAreMappedToExceptions()
    {
        var handler = Handler(_ => Responses.Json("{\"error\":{\"code\":\"ZCU/100\",\"message\":{\"lang\":\"en\",\"value\":\"Boom\"}}}", 400));

        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => Service.GetPeopleByKey("x").ExecuteAsync(handler.CreateClient()));

        Assert.Equal(400, exception.HttpCode);
        Assert.Equal("ZCU/100", exception.ODataError.ODataCode);
    }

    [Fact]
    public async Task ErrorsOfModifyingRequestsAreMappedToExceptions()
    {
        var handler = Handler(_ => Responses.Json("{\"error\":{\"code\":\"E1\",\"message\":\"Nope\"}}", 412));

        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(
            () => Service.DeletePeople(new Person { UserName = "x" }).ExecuteAsync(handler.CreateClient()));

        Assert.Equal(412, exception.HttpCode);
    }

    // ------------------------------------------------------------------ Destination and HTTP client provider

    [Fact]
    public async Task ExecuteWithDestination_UsesTheRegisteredProvider()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[{\"UserName\":\"a\"}]}"));
        var destination = DefaultHttpDestination.CreateBuilder("http://localhost:8080").Build();
        IHttpDestinationProperties? seen = null;
        var previous = ODataHttpClientProvider.Provider;
        ODataHttpClientProvider.Provider = d =>
        {
            seen = d;
            return handler.CreateClient();
        };
        try
        {
            var people = await Service.GetAllPeople().ExecuteAsync(destination);

            Assert.Same(destination, seen);
            Assert.Equal("a", Assert.Single(people).UserName);
        }
        finally
        {
            ODataHttpClientProvider.Provider = previous;
        }
    }

    [Fact]
    public async Task TryExecuteWithDestination_CapturesFailures()
    {
        var destination = DefaultHttpDestination.CreateBuilder("http://localhost:8080").Build();
        var previous = ODataHttpClientProvider.Provider;
        ODataHttpClientProvider.Provider = _ => throw new InvalidOperationException("no client");
        try
        {
            var result = await ((IRequestBuilderExecutable<IReadOnlyList<Person>>)Service.GetAllPeople()).TryExecuteAsync(destination);

            Assert.True(result.IsFailure);
            Assert.IsType<InvalidOperationException>(result.Exception);
        }
        finally
        {
            ODataHttpClientProvider.Provider = previous;
        }
    }

    [Fact]
    public async Task ExecuteWithDestination_FailsWithAHintWithoutRegisteredProvider()
    {
        var destination = DefaultHttpDestination.CreateBuilder("http://localhost:8080").Build();
        var previous = ODataHttpClientProvider.Provider;
        ODataHttpClientProvider.Provider = null!;
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Service.GetAllPeople().ExecuteAsync(destination));

            Assert.Contains("Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            ODataHttpClientProvider.Provider = previous;
        }
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[]}"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Service.GetAllPeople().ExecuteAsync(handler.CreateClient(), cancellation.Token));
    }
}
