using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Client.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

/// <summary>Request execution against a fake HTTP handler: URLs, headers, bodies, CSRF handling and ETags.</summary>
public class HttpExecutionTests
{
    private const string ServicePath = "/service/";
    private static readonly IODataProtocol V2 = ODataProtocol.V2;
    private static readonly IODataProtocol V4 = ODataProtocol.V4;

    private static HttpResponseMessage CsrfAwareResponder(RecordedRequest request, string token = "foobar") =>
        request.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", token)
            : request.Method == HttpMethod.Post && request.Body is not null
                ? Responses.Json("{}", 201)
                : Responses.NoContent();

    [Fact]
    public async Task Read_AppendsRequestPathToBaseAddressPath()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        var request = new ODataRequestRead(ServicePath, "Entity", "$select=select1&$expand=expand1,expand2($select=nestedSelect;$top=10)&$top=1", V4);
        request.AddQueryParameter("query-param1", "qp1");
        request.AddHeader("header-key1", "hk1");
        request.AddHeader("header-key1", "hk2");

        var result = await request.ExecuteAsync(handler.CreateClient("http://localhost:8080/destination/path"));

        Assert.Equal(200, result.HttpResponse.StatusCode);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal(
            "/destination/path/service/Entity?$select=select1&$expand=expand1,expand2($select=nestedSelect;$top=10)&$top=1&query-param1=qp1",
            sent.PathAndQuery);
        Assert.Equal(["application/json"], sent.GetHeader("Accept"));
        Assert.Equal(["hk1", "hk2"], sent.GetHeader("header-key1"));
        Assert.False(sent.HasHeader("x-csrf-token"));
    }

    [Fact]
    public async Task Read_WithoutBaseAddress_FailsWithConnectionException()
    {
        using var client = new HttpClient(new FakeHttpHandler(Responses.Json("{}")));
        var request = new ODataRequestRead(ServicePath, "Entity", null, V4);
        var exception = await Assert.ThrowsAsync<ODataConnectionException>(() => request.ExecuteAsync(client));
        Assert.Same(request, exception.Request);
        Assert.NotNull(exception.HttpRequest);
    }

    [Fact]
    public async Task ExecuteThroughInterface_ReturnsResult()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        IODataRequestExecutable executable = new ODataRequestRead(ServicePath, "Entity", null, V4);
        var result = await executable.ExecuteAsync(handler.CreateClient());
        Assert.IsType<ODataRequestResultGeneric>(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task ReadByKey_SendsKeyInPath()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        var key = new ODataEntityKey(V4)
            .AddKeyProperty("stringKey", "stringValue")
            .AddKeyProperty("booleanKey", true)
            .AddKeyProperty("numberKey", 9000)
            .AddKeyProperty("durationKey", TimeSpan.FromHours(8))
            .AddKeyProperty("dateTimeKey", new DateTime(2019, 12, 25, 8, 0, 0));
        await new ODataRequestReadByKey(ServicePath, "Entity", key, string.Empty, V4).ExecuteAsync(handler.CreateClient());
        Assert.Equal(
            "/service/Entity(stringKey='stringValue',booleanKey=true,numberKey=9000,durationKey=duration'PT8H',dateTimeKey=2019-12-25T08:00:00Z)",
            Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task Create_FetchesCsrfTokenAndPostsJson()
    {
        var handler = new FakeHttpHandler(r => CsrfAwareResponder(r));
        var request = new ODataRequestCreate(ServicePath, "Entity", "{\"foo\": \"bar\"}", V4);

        var result = await request.ExecuteAsync(handler.CreateClient());

        Assert.Equal(201, result.HttpResponse.StatusCode);
        Assert.Equal(2, handler.Requests.Count);
        var head = handler.Requests[0];
        Assert.Equal(HttpMethod.Head, head.Method);
        Assert.Equal("/service/", head.PathAndQuery);
        Assert.Equal(["fetch"], head.GetHeader("x-csrf-token"));
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/service/Entity", post.PathAndQuery);
        Assert.Equal(["foobar"], post.GetHeader("x-csrf-token"));
        Assert.Equal(["application/json"], post.GetHeader("Content-Type"));
        Assert.Equal("{\"foo\": \"bar\"}", post.Body);
    }

    [Fact]
    public async Task Action_FetchesCsrfTokenOnce()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head ? CsrfAwareResponder(r) : Responses.NoContent());
        var request = new ODataRequestAction(ServicePath, "TestAction", null, V4);
        await request.ExecuteAsync(handler.CreateClient());
        Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Head));
        var post = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Equal("{}", post.Body);
        Assert.Equal("/service/TestAction", post.PathAndQuery);
        Assert.Equal(["application/json"], post.GetHeader("Content-Type"));
    }

    [Fact]
    public async Task Action_WithCsrfDisabled_DoesNotFetchToken()
    {
        var handler = new FakeHttpHandler(Responses.NoContent());
        var request = new ODataRequestAction(ServicePath, ODataResourcePath.Of("TestAction"), null, V4);
        request.UseCsrfTokenRetrieval(false);
        await request.ExecuteAsync(handler.CreateClient());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.False(sent.HasHeader("x-csrf-token"));
    }

    [Fact]
    public async Task Create_WithCsrfHeaderAlreadyPresent_SkipsRetrieval()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}", 201));
        var request = new ODataRequestCreate(ServicePath, "Entity", "{}", V4);
        request.SetHeader("X-CSRF-Token", "mine");
        await request.ExecuteAsync(handler.CreateClient());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(["mine"], sent.GetHeader("x-csrf-token"));
    }

    [Fact]
    public async Task Read_WithCsrfEnabled_FetchesToken()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head ? CsrfAwareResponder(r) : Responses.Json("{}"));
        var request = new ODataRequestRead(ServicePath, "Entity", null, V4);
        request.UseCsrfTokenRetrieval(true);
        await request.ExecuteAsync(handler.CreateClient());
        Assert.Equal([HttpMethod.Head, HttpMethod.Get], handler.Requests.Select(r => r.Method));
        Assert.Equal(["foobar"], handler.Requests[1].GetHeader("x-csrf-token"));
    }

    [Fact]
    public async Task CsrfRetrievalFailure_DoesNotPreventTheRequest()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head ? Responses.Empty(500) : Responses.Json("{}", 201));
        var result = await new ODataRequestCreate(ServicePath, "Entity", "{}", V4).ExecuteAsync(handler.CreateClient());
        Assert.Equal(201, result.HttpResponse.StatusCode);
        Assert.False(handler.Requests[1].HasHeader("x-csrf-token"));
    }

    [Fact]
    public async Task CsrfRetrievalFailure_IsAttachedToTheRequestFailure()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head ? Responses.NoContent() : Responses.Empty(403));
        var exception = await Assert.ThrowsAsync<ODataResponseException>(
            () => new ODataRequestCreate(ServicePath, "Entity", "{}", V4).ExecuteAsync(handler.CreateClient()));
        Assert.Equal(403, exception.HttpCode);
        Assert.IsType<CsrfTokenRetrievalException>(exception.Data["CsrfTokenRetrievalFailure"]);
    }

    [Fact]
    public async Task CsrfToken_IsNotLoggedOrPrinted()
    {
        var handler = new FakeHttpHandler(r => CsrfAwareResponder(r, "secret\u0001-token"));
        await new ODataRequestCreate(ServicePath, "Entity", "{}", V4).ExecuteAsync(handler.CreateClient());
        // control characters are removed from the token, as in Java
        Assert.Equal(["secret-token"], handler.Requests[1].GetHeader("x-csrf-token"));
        Assert.DoesNotContain("secret", new CsrfToken("secret-token").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_Patch_SendsIfMatch()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head ? CsrfAwareResponder(r) : Responses.NoContent());
        var key = new ODataEntityKey(V4).AddKeyProperty("key", 123);
        var request = new ODataRequestUpdate(ServicePath, "Entity", key, "{\"foo\": \"bar\"}", UpdateStrategy.ModifyWithPatch, "version-identifier", V4);

        await request.ExecuteAsync(handler.CreateClient());

        var patch = handler.Requests.Single(r => r.Method == HttpMethod.Patch);
        Assert.Equal("/service/Entity(123)", patch.PathAndQuery);
        Assert.Equal(["version-identifier"], patch.GetHeader("If-Match"));
        Assert.Equal(["application/json"], patch.GetHeader("Content-Type"));
        Assert.Equal("{\"foo\": \"bar\"}", patch.Body);
    }

    [Fact]
    public async Task Update_Put_WithoutVersionIdentifier_HasNoIfMatch()
    {
        var handler = new FakeHttpHandler(_ => Responses.NoContent());
        var key = new ODataEntityKey(V2).AddKeyProperty("Id", 0);
        var request = new ODataRequestUpdate(
            "/service",
            ODataResourcePath.Of("Products", key).AddSegment("Description"),
            "{\"Description\": \"Whole grain bread\"}",
            UpdateStrategy.ReplaceWithPut,
            null,
            V2);

        await request.ExecuteAsync(handler.CreateClient());

        var put = handler.Requests.Single(r => r.Method == HttpMethod.Put);
        Assert.Equal("/service/Products(0)/Description", put.PathAndQuery);
        Assert.False(put.HasHeader("If-Match"));
        Assert.Contains("application/json", put.GetFirstHeader("Content-Type"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_SendsIfMatch()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head ? CsrfAwareResponder(r) : Responses.NoContent());
        var key = new ODataEntityKey(V4).AddKeyProperty("key", 123);
        await new ODataRequestDelete(ServicePath, "Entity", key, "version-identifier", V4).ExecuteAsync(handler.CreateClient());
        var delete = handler.Requests.Single(r => r.Method == HttpMethod.Delete);
        Assert.Equal(["version-identifier"], delete.GetHeader("If-Match"));
        Assert.Equal(["foobar"], delete.GetHeader("x-csrf-token"));
    }

    [Theory]
    [InlineData(ETagSubmissionStrategy.SubmitEtagFromEntity, "v1", "v1")]
    [InlineData(ETagSubmissionStrategy.SubmitEtagFromEntity, "", null)]
    [InlineData(ETagSubmissionStrategy.SubmitEtagFromEntity, null, null)]
    [InlineData(ETagSubmissionStrategy.SubmitNoEtag, "v1", null)]
    [InlineData(ETagSubmissionStrategy.SubmitAnyMatchEtag, "v1", "*")]
    [InlineData(ETagSubmissionStrategy.SubmitAnyMatchEtag, null, "*")]
    public void ETagSubmissionStrategy_ProducesHeader(ETagSubmissionStrategy strategy, string? versionIdentifier, string? expected)
    {
        Assert.Equal(expected, strategy.GetHeaderFromVersionIdentifier(versionIdentifier));
    }

    [Fact]
    public async Task Function_V2_SendsParametersInQuery()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        var parameters = new ODataFunctionParameters(V2)
            .AddParameter("stringParameter", "foo/bar")
            .AddParameter("booleanParameter", true)
            .AddParameter("integerParameter", 9000)
            .AddParameter("doubleParameter", 3.14d)
            .AddParameter("nullParameter", null)
            .AddParameter("durationParameter", TimeSpan.FromHours(8))
            .AddParameter("dateTimeParameter", new DateTime(2019, 12, 25, 8, 0, 0));
        await new ODataRequestFunction(ServicePath, "TestFunction", parameters, V2).ExecuteAsync(handler.CreateClient());
        Assert.Equal(
            "/service/TestFunction?stringParameter='foo/bar'&booleanParameter=true&integerParameter=9000&doubleParameter=3.14d&nullParameter=null&durationParameter=duration'PT8H'&dateTimeParameter=datetime'2019-12-25T08:00:00'",
            Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task Function_V4_SendsParametersInPath()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        var parameters = new ODataFunctionParameters(V4)
            .AddParameter("stringParameter", "foo/bar")
            .AddParameter("booleanParameter", true)
            .AddParameter("integerParameter", 9000)
            .AddParameter("doubleParameter", 3.14d)
            .AddParameter("nullParameter", null)
            .AddParameter("durationParameter", TimeSpan.FromHours(8))
            .AddParameter("dateTimeParameter", new DateTime(2019, 12, 25, 8, 0, 0));
        await new ODataRequestFunction(ServicePath, "TestFunction", parameters, V4).ExecuteAsync(handler.CreateClient());
        Assert.Equal(
            "/service/TestFunction(stringParameter='foo%2Fbar',booleanParameter=true,integerParameter=9000,doubleParameter=3.14,nullParameter=null,durationParameter=duration'PT8H',dateTimeParameter=2019-12-25T08:00:00Z)",
            Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task Function_V4_WithoutParameters()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        await new ODataRequestFunction(ServicePath, "TestFunction", ODataFunctionParameters.Empty(V4), V4).ExecuteAsync(handler.CreateClient());
        Assert.Equal("/service/TestFunction()", Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task Connection_Failure_IsReportedAsConnectionException()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("boom"));
        var request = new ODataRequestRead(ServicePath, "Entity", null, V4);
        var exception = await Assert.ThrowsAsync<ODataConnectionException>(() => request.ExecuteAsync(handler.CreateClient()));
        Assert.Equal("Connection could not be established.", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task Cancellation_IsNotWrapped()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ODataRequestRead(ServicePath, "Entity", null, V4).ExecuteAsync(handler.CreateClient(), cts.Token));
    }

    [Fact]
    public async Task Listener_SeesTheHttpRequest()
    {
        var handler = new FakeHttpHandler(Responses.Json("{}"));
        var listener = new RecordingListener();
        var request = new ODataRequestRead(ServicePath, "Entity", null, V4);
        request.AddListener(listener);
        await request.ExecuteAsync(handler.CreateClient());
        Assert.Equal(HttpMethod.Get, Assert.Single(listener.Requests).Method);
    }

    [Fact]
    public async Task WithoutResponseBuffering_StreamsAndMustBeDisposed()
    {
        var handler = new FakeHttpHandler(Responses.Json("{\"d\":{\"results\":[{\"a\":1}]}}"));
        var executable = new ODataRequestRead(ServicePath, "Entity", null, V2).WithoutResponseBuffering();
        using var result = await executable.ExecuteAsync(handler.CreateClient());
        Assert.False(result.HttpResponse.IsBuffered);
        var list = await result.AsListAsync<Dictionary<string, object?>>();
        Assert.Equal(1d, Assert.Single(list)["a"]);
    }

    private sealed class RecordingListener : IODataRequestListener
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public void ListenOnRequest(HttpRequestMessage request) => Requests.Add(request);

        public void ListenOnRequestError(Exception exception)
        {
        }

        public void ListenOnParsingError(Exception exception)
        {
        }
    }
}
