using System.Text.RegularExpressions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Client.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

/// <summary>Batch request serialization and batch response parsing, including multipart parsing.</summary>
public partial class BatchTests
{
    private const string ServicePath = "/service/";
    private const string EntityCollection = "Entity";
    private static readonly IODataProtocol V4 = ODataProtocol.V4;
    private static readonly ODataEntityKey EntityKey = new ODataEntityKey(V4).AddKeyProperty("key", "the-key#&!%");

    private int _uuidCounter;

    private Guid NextUuid() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)Interlocked.Increment(ref _uuidCounter));

    private static string TestData(string name) =>
        NewLines().Replace(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name)), "\r\n");

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex NewLines();

    private static ODataRequestRead SampleRead() =>
        new(ServicePath, EntityCollection, "$filter=Fieldname%20eq%20'hello'", V4);

    private static ODataRequestReadByKey SampleReadByKey() => new(ServicePath, EntityCollection, EntityKey, null, V4);

    private static ODataRequestCreate SampleCreate()
    {
        var create = new ODataRequestCreate(ServicePath, EntityCollection, "{\"foo\": \"bar\"}", V4);
        create.AddHeader("Set-Cookie", "foo");
        create.AddHeader("Set-Cookie", "bar");
        return create;
    }

    private static ODataRequestUpdate SampleUpdate() =>
        new(ServicePath, EntityCollection, EntityKey, "{\"foo\": \"bar\"}", UpdateStrategy.ModifyWithPatch, "version-identifier", V4);

    private static ODataRequestDelete SampleDelete() => new(ServicePath, EntityCollection, EntityKey, "version-identifier", V4);

    [Fact]
    public void EmptyBatchBody()
    {
        var request = new ODataRequestBatch(ServicePath, V4, NextUuid);
        Assert.Equal(TestData("BatchEmptyRequestBody.txt"), request.GetBatchRequestBody());
    }

    [Fact]
    public void EmptyChangesetBody()
    {
        var request = new ODataRequestBatch(ServicePath, V4, NextUuid).BeginChangeset().EndChangeset();
        Assert.Equal(TestData("BatchEmptyChangesetRequestBody.txt"), request.GetBatchRequestBody());
    }

    [Fact]
    public void ReadOnlyBatchBody()
    {
        var request = new ODataRequestBatch(ServicePath, V4, NextUuid).AddRead(SampleRead());
        Assert.Equal(TestData("BatchReadRequestBody.txt"), request.GetBatchRequestBody());
    }

    [Fact]
    public void CombinedBatchBody()
    {
        var request = new ODataRequestBatch(ServicePath, V4, NextUuid)
            .AddRead(SampleRead())
            .BeginChangeset()
            .AddCreate(SampleCreate())
            .AddUpdate(SampleUpdate())
            .AddDelete(SampleDelete())
            .EndChangeset()
            .AddReadByKey(SampleReadByKey());
        Assert.Equal(TestData("BatchAllRequestBody.txt"), request.GetBatchRequestBody());
    }

    [Fact]
    public async Task Execute_FetchesCsrfTokenAndPostsMultipart()
    {
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", "foobar")
            : Responses.Json("{}"));
        var request = new ODataRequestBatch(ServicePath, V4, NextUuid).AddRead(SampleRead());
        var expectedBody = request.GetBatchRequestBody();

        using var result = await request.ExecuteAsync(handler.CreateClient());

        Assert.Same(request, result.ODataRequest);
        Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
        Assert.Equal("/service/", handler.Requests[0].PathAndQuery);
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/service/$batch", post.PathAndQuery);
        Assert.Equal(expectedBody, post.Body);
        Assert.False(post.HasHeader("Accept"));
        // HttpClient normalizes the media type header to "multipart/mixed; boundary=…"
        var contentType = Assert.Single(post.GetHeader("Content-Type"));
        Assert.Equal("multipart/mixed; boundary=batch_00000000-0000-0000-0000-000000000001", contentType.Replace(";boundary", "; boundary", StringComparison.Ordinal));
        Assert.Equal(["4.0"], post.GetHeader("OData-Version"));
        Assert.Equal(["foobar"], post.GetHeader("x-csrf-token"));
    }

    [Fact]
    public void RequestsToOtherServicePathOrProtocolAreRejected()
    {
        var batch = new ODataRequestBatch("this/", V4, NextUuid);
        batch.AddRead(new ODataRequestRead("this/", "People", "$top=1", V4));
        Assert.Throws<ODataRequestException>(() => batch.AddRead(new ODataRequestRead("other/", "People", "$top=2", V4)));
        Assert.Throws<ODataRequestException>(() => new ODataRequestBatch("this/", V4, NextUuid).AddRead(new ODataRequestRead("this/", "People", "$top=1", ODataProtocol.V2)));
    }

    [Theory]
    [InlineData("service-path/", "/service-path/")]
    [InlineData("/service-path", "/service-path/")]
    [InlineData("/service-path/", "/service-path/")]
    public void ServicePathSlashes_AreNormalized(string servicePath, string expectedEncoded)
    {
        var batch = new ODataRequestBatch(servicePath, V4, NextUuid);
        batch.BeginChangeset().AddCreate(new ODataRequestCreate(servicePath, "entity-path", "{}", V4)).EndChangeset();
        Assert.Equal(expectedEncoded, batch.GetEncodedServicePath(UriEncodingStrategy.Regular));
        Assert.Contains("POST entity-path HTTP/1.1", batch.GetBatchRequestBody(), StringComparison.Ordinal);
    }

    [Fact]
    public void SpecialCharactersInPaths()
    {
        var batch = new ODataRequestBatch("/service-path/", V4, NextUuid);
        batch.BeginChangeset().AddCreate(new ODataRequestCreate("/service-path/", "entity-päth", "{}", V4)).EndChangeset();
        Assert.Contains("POST entity-p%C3%A4th HTTP/1.1", batch.GetBatchRequestBody(), StringComparison.Ordinal);

        var special = new ODataRequestBatch("service-päth;v=001", V4, NextUuid);
        Assert.Equal("/service-p%C3%A4th;v=001/", special.GetEncodedServicePath(UriEncodingStrategy.Regular));
        Assert.Equal("/service-p%C3%A4th%3Bv%3D001/", special.GetEncodedServicePath(UriEncodingStrategy.Batch));

        Assert.Throws<ODataRequestException>(
            () => new ODataRequestBatch("service-path", V4, NextUuid)
                .BeginChangeset()
                .AddCreate(new ODataRequestCreate("service-päth", "entity-path", "{}", V4)));
    }

    [Fact]
    public void DifferentLeadingSlashes_AreAccepted()
    {
        var batch = new ODataRequestBatch("service-path", V4, NextUuid);
        batch.BeginChangeset().AddCreate(new ODataRequestCreate("/service-path", "entity-path", "{}", V4)).EndChangeset();
        Assert.Contains("POST entity-path HTTP/1.1", batch.GetBatchRequestBody(), StringComparison.Ordinal);
    }

    private const string MultipartContentType = "multipart/mixed; boundary=batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef";

    [Fact]
    public async Task Response_MapsChangesetAndErrorItemsToRequests()
    {
        var create1 = new ODataRequestCreate(ServicePath, EntityCollection, "{}", V4);
        var create2 = new ODataRequestCreate(ServicePath, EntityCollection, "{}", V4);
        var read = new ODataRequestRead(ServicePath, EntityCollection, "$top=1", V4);
        var batch = new ODataRequestBatch(ServicePath, V4, NextUuid)
            .BeginChangeset().AddCreate(create1).AddCreate(create2).EndChangeset()
            .AddRead(read);

        // the recorded response has Content-ID 2 and 3 for the changeset and no Content-ID for the error; add one for the error
        var body = TestData("BatchWriteResponseBody.txt").Replace(
            "Content-Transfer-Encoding: binary\r\n\r\nHTTP/1.1 404",
            "Content-Transfer-Encoding: binary\r\nContent-ID: 3\r\n\r\nHTTP/1.1 404",
            StringComparison.Ordinal);
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", "t")
            : Responses.Text(body, MultipartContentType, 200));

        using var result = await batch.ExecuteAsync(handler.CreateClient());

        var responses = result.GetBatchedResponses();
        Assert.Equal(2, responses.Count);
        Assert.Equal(2, responses[0].Count);
        Assert.Single(responses[1]);
        Assert.Equal(201, responses[0][0].StatusCode);
        Assert.Equal("Created", responses[0][0].ReasonPhrase);
        Assert.Equal("application/json; odata.metadata=minimal", responses[0][0].GetFirstHeader("content-type"));

        var first = await result.GetResultAsync(create1);
        Assert.Equal("JohnDoe1", (await first.AsMapAsync())["UserName"]);
        var second = await result.GetResultAsync(create2);
        Assert.Equal("JohnDoe2", (await second.AsMapAsync())["UserName"]);

        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => result.GetResultAsync(read));
        Assert.Same(read, exception.Request);
        Assert.Equal(404, exception.HttpCode);
        Assert.Equal("The request resource is not found.", exception.ODataError.ODataMessage);

        await Assert.ThrowsAsync<ArgumentException>(() => result.GetResultAsync(new ODataRequestRead(ServicePath, "Other", null, V4)));
    }

    [Fact]
    public async Task Response_ReadItems()
    {
        var read1 = new ODataRequestRead(ServicePath, "People", "$top=1", V4);
        var read2 = new ODataRequestRead(ServicePath, "People", "$top=2", V4);
        var batch = new ODataRequestBatch(ServicePath, V4, NextUuid).AddRead(read1).AddRead(read2);
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head
            ? Responses.NoContent()
            : Responses.Text(TestData("BatchReadResponseBody.txt"), MultipartContentType));

        using var result = await batch.ExecuteAsync(handler.CreateClient());

        var one = await (await result.GetResultAsync(read1)).AsListAsync<Dictionary<string, object?>>();
        Assert.Equal("angelhuffman", Assert.Single(one)["UserName"]);
        var two = await (await result.GetResultAsync(read2)).AsListAsync<Dictionary<string, object?>>();
        Assert.Equal(["klauskinski", "DanielBruehl"], two.Select(p => p["UserName"]));
        Assert.Equal("Brühl", two[1]["LastName"]);
    }

    [Fact]
    public async Task Response_TooFewItems_IsResponseException()
    {
        var read1 = new ODataRequestRead(ServicePath, "People", "$top=1", V4);
        var read2 = new ODataRequestRead(ServicePath, "People", "$top=2", V4);
        var read3 = new ODataRequestRead(ServicePath, "People", "$top=3", V4);
        var batch = new ODataRequestBatch(ServicePath, V4, NextUuid).AddRead(read1).AddRead(read2).AddRead(read3);
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head
            ? Responses.NoContent()
            : Responses.Text(TestData("BatchReadResponseBody.txt"), MultipartContentType));
        using var result = await batch.ExecuteAsync(handler.CreateClient());
        var exception = await Assert.ThrowsAsync<ODataResponseException>(() => result.GetResultAsync(read3));
        Assert.Contains("contains only 2 items", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_WithoutBoundary_IsResponseException()
    {
        var read = new ODataRequestRead(ServicePath, "People", null, V4);
        var batch = new ODataRequestBatch(ServicePath, V4, NextUuid).AddRead(read);
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head
            ? Responses.NoContent()
            : Responses.Text("whatever", "multipart/mixed"));
        using var result = await batch.ExecuteAsync(handler.CreateClient());
        var exception = await Assert.ThrowsAsync<ODataResponseException>(() => result.GetResultAsync(read));
        Assert.Contains("Failed to read", exception.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task BatchRequestFailure_IsServiceError()
    {
        var batch = new ODataRequestBatch(ServicePath, V4, NextUuid).AddRead(SampleRead());
        var handler = new FakeHttpHandler(r => r.Method == HttpMethod.Head
            ? Responses.NoContent()
            : Responses.Json("{\"error\":{\"code\":\"x\",\"message\":\"bad batch\"}}", 400));
        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => batch.ExecuteAsync(handler.CreateClient()));
        Assert.Same(batch, exception.Request);
        Assert.Equal("bad batch", exception.ODataError.ODataMessage);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void MultipartParser_SimpleRead(string newLine)
    {
        var responseText = newLine
            + "--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef" + newLine
            + "Content-Type: application/http" + newLine
            + "Content-Transfer-Encoding: binary" + newLine
            + newLine
            + "HTTP/1.1 200 OK" + newLine
            + "Content-Type: application/json; odata.metadata=minimal; odata.streaming=true" + newLine
            + "OData-Version: 4.0" + newLine
            + newLine
            + "{\"foØ\":\"bär\"}" + newLine
            + "--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef--" + newLine;
        var result = MultipartParser.OfText(responseText, "--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef").ToList(e => e);
        var entry = Assert.Single(Assert.Single(result));
        Assert.StartsWith("HTTP/1.1 200 OK", entry.Payload, StringComparison.Ordinal);

        var response = MultipartHttpResponse.OfHttpContent(entry);
        Assert.Equal(
            [
                new KeyValuePair<string, string>("Content-Type", "application/json; odata.metadata=minimal; odata.streaming=true"),
                new KeyValuePair<string, string>("OData-Version", "4.0"),
            ],
            response.Headers);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("OK", response.ReasonPhrase);
        Assert.Equal("{\"foØ\":\"bär\"}\n", response.ReadBufferedText());
        Assert.Null(response.ContentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef--\n")]
    [InlineData("\n--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef--\r\n")]
    public void MultipartParser_EmptyBodies(string text)
    {
        Assert.Empty(MultipartParser.OfText(text, "--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef").ToList(e => e));
    }

    [Fact]
    public void MultipartParser_WrongDelimiterYieldsNothing()
    {
        Assert.Empty(MultipartParser.OfText("--batchresponse_x--\n", "--some-delimiter").ToList(e => e));
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void MultipartParser_WriteResponseWithChangeset(string newLine)
    {
        var text = Regex.Replace(TestData("BatchWriteResponseBody.txt"), "\r\n", newLine);
        var response = new ODataHttpResponse(200, "Ok", [new("Content-Type", MultipartContentType)], System.Text.Encoding.UTF8.GetBytes(text));
        var result = MultipartParser.OfHttpResponse(response).ToList(e => e);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Count);
        Assert.All(result[0], e => Assert.Contains("HTTP/1.1 201 Created", e.Payload, StringComparison.Ordinal));
        Assert.Contains("HTTP/1.1 404 Not Found", Assert.Single(result[1]).Payload, StringComparison.Ordinal);
        Assert.Equal([2, 3], result[0].Select(e => MultipartHttpResponse.OfHttpContent(e).ContentId));
    }

    [Fact]
    public void MultipartParser_RequiresContentAndDelimiter()
    {
        var withoutBody = new ODataHttpResponse(200, "OK", [new("Content-Type", MultipartContentType)], null);
        Assert.Equal("HTTP response does not contain a content.", Assert.Throws<InvalidOperationException>(() => MultipartParser.OfHttpResponse(withoutBody)).Message);
        var withoutDelimiter = new ODataHttpResponse(200, "OK", [new("Content-Type", "multipart/mixed")], []);
        Assert.Equal("No delimiter found in HTTP header.", Assert.Throws<InvalidOperationException>(() => MultipartParser.OfHttpResponse(withoutDelimiter)).Message);
    }

    [Fact]
    public void MultipartHttpResponse_UnparsableStatusLine()
    {
        var response = MultipartHttpResponse.OfHttpContent(new MultipartParser.Entry(string.Empty, "garbage\nX: y\n\nbody\n"));
        Assert.Equal(0, response.StatusCode);
        Assert.Equal("Unknown", response.ReasonPhrase);
        Assert.Equal("body\n", response.ReadBufferedText());
    }
}
