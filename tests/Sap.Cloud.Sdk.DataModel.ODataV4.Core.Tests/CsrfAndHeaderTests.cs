using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>CSRF token handling and custom headers of all builders. Ports <c>CsrfTokenHandlingTest</c> and <c>HeadersHandlingTest</c>.</summary>
public class CsrfAndHeaderTests
{
    private const string ServicePath = "/remoteService";
    private const string EntityUrl = ServicePath + "/People";
    private const string Token = "awesome-csrf-token";

    private static FakeHttpHandler Handler(Func<RecordedRequest, HttpResponseMessage> responder) =>
        new(request => request.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", Token)
            : responder(request));

    private static void AssertTokenFetched(FakeHttpHandler handler)
    {
        var head = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Head);
        Assert.Equal(ServicePath, head.PathAndQuery);
        Assert.Equal("fetch", head.GetFirstHeader("x-csrf-token"));
    }

    private static void AssertNoTokenFetched(FakeHttpHandler handler, HttpMethod method, string pathAndQuery, int count = 1)
    {
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Head);
        var sent = handler.Requests.Where(r => r.Method == method && r.PathAndQuery == pathAndQuery).ToList();
        Assert.Equal(count, sent.Count);
        Assert.All(sent, r => Assert.False(r.HasHeader("x-csrf-token")));
    }

    private static void AssertSentWithToken(FakeHttpHandler handler, HttpMethod method, string pathAndQuery, int count = 1)
    {
        var sent = handler.Requests.Where(r => r.Method == method && r.PathAndQuery == pathAndQuery).ToList();
        Assert.Equal(count, sent.Count);
        Assert.All(sent, r => Assert.Equal(Token, r.GetFirstHeader("x-csrf-token")));
    }

    [Fact]
    public async Task GetAll_ContainsNoCsrfTokenByDefault()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[]}"));

        await new GetAllRequestBuilder<Person>(ServicePath, "People").ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Get, EntityUrl);
    }

    [Fact]
    public async Task GetByKey_ContainsNoCsrfTokenByDefault()
    {
        var handler = Handler(_ => Responses.Json("{}"));

        await new GetByKeyRequestBuilder<Person>(ServicePath, new Dictionary<string, object?> { ["id"] = 42 }, "People")
            .ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Get, EntityUrl + "(42)");
    }

    [Fact]
    public async Task Count_ContainsNoCsrfTokenByDefault()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":42}"));

        await new CountRequestBuilder<Person>(ServicePath, "People").ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Get, EntityUrl + "/$count");
    }

    [Fact]
    public async Task Functions_ContainNoCsrfTokenByDefault()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[]}"));
        var singleHandler = Handler(_ => Responses.Json("{}"));

        await new SingleValueFunctionRequestBuilder<object>(ServicePath, "function").ExecuteAsync(singleHandler.CreateClient());
        await new CollectionValueFunctionRequestBuilder<object>(ServicePath, "function").ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(singleHandler, HttpMethod.Get, ServicePath + "/function()");
        AssertNoTokenFetched(handler, HttpMethod.Get, ServicePath + "/function()");
    }

    [Fact]
    public async Task GetAll_FetchesCsrfTokenIfEnabled()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":[]}"));

        await new GetAllRequestBuilder<Person>(ServicePath, "People").WithCsrfToken().ExecuteAsync(handler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Get, EntityUrl);
    }

    [Fact]
    public async Task GetByKey_FetchesCsrfTokenIfEnabled()
    {
        var handler = Handler(_ => Responses.Json("{}"));

        await new GetByKeyRequestBuilder<Person>(ServicePath, new Dictionary<string, object?> { ["id"] = 42 }, "People")
            .WithCsrfToken()
            .ExecuteAsync(handler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Get, EntityUrl + "(42)");
    }

    [Fact]
    public async Task Count_FetchesCsrfTokenIfEnabled()
    {
        var handler = Handler(_ => Responses.Json("{\"value\":42}"));

        await new CountRequestBuilder<Person>(ServicePath, "People").WithCsrfToken().ExecuteAsync(handler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Get, EntityUrl + "/$count");
    }

    [Fact]
    public async Task Functions_FetchCsrfTokenIfEnabled()
    {
        var handler = Handler(_ => Responses.Json("{}"));
        await new SingleValueFunctionRequestBuilder<object>(ServicePath, "function").WithCsrfToken().ExecuteAsync(handler.CreateClient());
        var collectionHandler = Handler(_ => Responses.Json("{\"value\":[]}"));
        await new CollectionValueFunctionRequestBuilder<object>(ServicePath, "function").WithCsrfToken().ExecuteAsync(collectionHandler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Get, ServicePath + "/function()");
        AssertTokenFetched(collectionHandler);
        AssertSentWithToken(collectionHandler, HttpMethod.Get, ServicePath + "/function()");
    }

    [Fact]
    public async Task Create_FetchesCsrfToken()
    {
        var handler = Handler(_ => Responses.Json("{\"@odata.context\": \"/sap/ODATA_SRV$metadata#People/$entity\",\"ShoeSize\": 46}"));

        await new CreateRequestBuilder<Person>(ServicePath, new Person(), "People").ExecuteAsync(handler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Post, EntityUrl);
    }

    [Fact]
    public async Task Delete_FetchesCsrfToken()
    {
        var handler = Handler(_ => Responses.Empty());

        await new DeleteRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People").ExecuteAsync(handler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Delete, EntityUrl + "('usr')");
    }

    [Fact]
    public async Task Actions_FetchCsrfToken()
    {
        var handler = Handler(_ => Responses.Json("{}"));
        await new SingleValueActionRequestBuilder<object>(ServicePath, "ActionName", new Dictionary<string, object?>()).ExecuteAsync(handler.CreateClient());
        var collectionHandler = Handler(_ => Responses.Json("{\"value\":[]}"));
        await new CollectionValueActionRequestBuilder<object>(ServicePath, "ActionName", new Dictionary<string, object?>()).ExecuteAsync(collectionHandler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Post, ServicePath + "/ActionName");
        AssertTokenFetched(collectionHandler);
        AssertSentWithToken(collectionHandler, HttpMethod.Post, ServicePath + "/ActionName");
    }

    [Fact]
    public async Task Update_FetchesCsrfToken()
    {
        var handler = Handler(_ => Responses.Empty());

        await new UpdateRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People").ExecuteAsync(handler.CreateClient());

        AssertTokenFetched(handler);
        AssertSentWithToken(handler, HttpMethod.Patch, EntityUrl + "('usr')");
    }

    [Fact]
    public async Task Update_ContainsNoCsrfTokenIfDisabled()
    {
        var handler = Handler(_ => Responses.Empty());

        await new UpdateRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People")
            .WithoutCsrfToken()
            .ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Patch, EntityUrl + "('usr')");
    }

    [Fact]
    public async Task Delete_ContainsNoCsrfTokenIfDisabled()
    {
        var handler = Handler(_ => Responses.Empty());

        await new DeleteRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People")
            .WithoutCsrfToken()
            .ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Delete, EntityUrl + "('usr')");
    }

    [Fact]
    public async Task Actions_ContainNoCsrfTokenIfDisabled()
    {
        var handler = Handler(_ => Responses.Json("{}"));
        await new SingleValueActionRequestBuilder<object>(ServicePath, "ActionName").WithoutCsrfToken().ExecuteAsync(handler.CreateClient());
        var collectionHandler = Handler(_ => Responses.Json("{\"value\":[]}"));
        await new CollectionValueActionRequestBuilder<object>(ServicePath, "ActionName").WithoutCsrfToken().ExecuteAsync(collectionHandler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Post, ServicePath + "/ActionName");
        AssertNoTokenFetched(collectionHandler, HttpMethod.Post, ServicePath + "/ActionName");
    }

    [Fact]
    public async Task Create_ContainsNoCsrfTokenIfDisabled()
    {
        var handler = Handler(_ => Responses.Json("{\"ShoeSize\": 46}"));

        await new CreateRequestBuilder<Person>(ServicePath, new Person(), "People").WithoutCsrfToken().ExecuteAsync(handler.CreateClient());

        AssertNoTokenFetched(handler, HttpMethod.Post, EntityUrl);
    }

    [Fact]
    public async Task Update_SendsHeadersWithNonUniqueKeysToTheTokenRequestAndTheUpdate()
    {
        var handler = Handler(_ => Responses.Empty());
        var builder = new UpdateRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People")
            .WithHeader("Set-Cookie", "foo")
            .WithHeader("Set-Cookie", "bar");

        await builder.ExecuteAsync(handler.CreateClient());

        var head = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Head);
        Assert.Equal(["foo", "bar"], head.GetHeader("Set-Cookie"));
        var patch = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Equal(["foo", "bar"], patch.GetHeader("Set-Cookie"));
    }

    [Fact]
    public async Task Update_SendsHeadersAddedAtOnce()
    {
        var handler = Handler(_ => Responses.Empty());
        var builder = new UpdateRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People")
            .WithHeaders([new("Set-Cookie", "foo")])
            .WithHeaders([new("Set-Cookie", "bar")]);

        await builder.ExecuteAsync(handler.CreateClient());

        var patch = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Equal(["foo", "bar"], patch.GetHeader("Set-Cookie"));
    }

    [Fact]
    public async Task Update_KeepsMultipleResponseHeadersWithTheSameName()
    {
        var handler = Handler(_ =>
        {
            var response = Responses.Empty();
            response.Headers.TryAddWithoutValidation("Set-Cookie", "SimpleKey=SimpleValue");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "KeyWithoutAValue");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "KeyWithCommaValue=Value, which contains a comma");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "MultiValueKey1=Value 1, with comma; MultiValueKey2=Value 2 ; MultiValueKey3;");
            return response;
        });

        var response = await new UpdateRequestBuilder<Person>(ServicePath, new Person { UserName = "usr" }, "People")
            .ExecuteAsync(handler.CreateClient());

        Assert.Equal(
            [
                "SimpleKey=SimpleValue",
                "KeyWithoutAValue",
                "KeyWithCommaValue=Value, which contains a comma",
                "MultiValueKey1=Value 1, with comma; MultiValueKey2=Value 2 ; MultiValueKey3;",
            ],
            response.ResponseHeaders["Set-Cookie"]);
    }

    [Fact]
    public async Task CustomHeadersAndQueryParametersAreSent()
    {
        var handler = new FakeHttpHandler(Responses.Json("{\"value\":[]}"));

        await new GetAllRequestBuilder<Person>(ServicePath, "People")
            .WithHeader("header", "value")
            .WithHeaders(new Dictionary<string, string> { ["other"] = "x" })
            .WithQueryParameter("query", "parameter")
            .ExecuteAsync(handler.CreateClient());

        var request = Assert.Single(handler.Requests);
        Assert.Equal("value", request.GetFirstHeader("header"));
        Assert.Equal("x", request.GetFirstHeader("other"));
        Assert.Equal(EntityUrl + "?query=parameter", request.PathAndQuery);
    }

    [Fact]
    public async Task NullHeaderValuesAreIgnored()
    {
        var handler = new FakeHttpHandler(Responses.Json("{\"value\":[]}"));

        await new GetAllRequestBuilder<Person>(ServicePath, "People").WithHeader("header", null).ExecuteAsync(handler.CreateClient());

        Assert.False(Assert.Single(handler.Requests).HasHeader("header"));
    }

    [Fact]
    public async Task Listeners_AreInvoked()
    {
        var listener = new RecordingListener();
        var handler = new FakeHttpHandler(Responses.Json("{\"value\":[]}"));

        await new GetAllRequestBuilder<Person>(ServicePath, "People").WithListener(listener).ExecuteAsync(handler.CreateClient());

        Assert.Equal(1, listener.Before);
        Assert.Equal(0, listener.After);
    }

    private sealed class RecordingListener : OData.Client.Request.IODataRequestListener
    {
        public int Before { get; private set; }

        public int After { get; private set; }

        public void ListenOnRequest(HttpRequestMessage request) => Before++;

        public void ListenOnRequestError(Exception exception) => After++;

        public void ListenOnParsingError(Exception exception) => After++;
    }
}
