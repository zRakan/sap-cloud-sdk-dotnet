using System.Text;
using Sap.Cloud.Sdk.DataModel.OData.Client.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Client.Tests.Support;
using Sap.Cloud.Sdk.Result;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

/// <summary>Response parsing: V2 and V4 envelopes, primitives, counts, links, paging, errors.</summary>
public class ResultParsingTests
{
    private static readonly IODataProtocol V2 = ODataProtocol.V2;
    private static readonly IODataProtocol V4 = ODataProtocol.V4;

    public sealed class Ticket
    {
        [ElementName("versionIdentifier")]
        public string? VersionIdentifier { get; set; }

        [ElementName("Ticket")]
        public string? TicketId { get; set; }

        [ElementName("TicketName")]
        public string? TicketName { get; set; }

        [ElementName("TicketIsBlocked")]
        public bool TicketIsBlocked { get; set; }
    }

    public enum ColorValue
    {
        Yellow,
    }

    public sealed class ReferenceObject
    {
        [ElementName("BooleanValue")]
        public bool BooleanValue { get; set; }

        [ElementName("BinaryValue")]
        public byte[]? BinaryValue { get; set; }

        [ElementName("StringValue")]
        public string? StringValue { get; set; }

        [ElementName("IntegerValue")]
        public int IntegerValue { get; set; }

        [ElementName("Int64Value")]
        public long Int64Value { get; set; }

        [ElementName("SingleValue")]
        public float SingleValue { get; set; }

        [ElementName("DecimalValue")]
        public double DecimalValue { get; set; }

        [ElementName("DoubleValue")]
        public double DoubleValue { get; set; }

        [ElementName("GuidValue")]
        public Guid GuidValue { get; set; }

        [ElementName("DurationValue")]
        public TimeSpan DurationValue { get; set; }

        [ElementName("TimeOfDayValue")]
        public TimeOnly TimeOfDayValue { get; set; }

        [ElementName("DateValue")]
        public DateOnly DateValue { get; set; }

        [ElementName("DateTimeOffsetValue")]
        public DateTimeOffset DateTimeOffsetValue { get; set; }

        [ElementName("Tags")]
        public List<string>? Tags { get; set; }

        [ElementName("ColorValueValue")]
        public ColorValue ColorValueValue { get; set; }
    }

    private static string TestData(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));

    private static async Task<ODataRequestResultGeneric> ExecuteRead(string body, IODataProtocol protocol, string path = "Entity", string contentType = "application/json")
    {
        var handler = new FakeHttpHandler(_ => Responses.Text(body, contentType));
        return await new ODataRequestRead("/service/", path, null, protocol).ExecuteAsync(handler.CreateClient());
    }

    [Fact]
    public async Task ByKey_V2_ReadsEntityFromD()
    {
        var handler = new FakeHttpHandler(_ => Responses.Json(TestData("Ticket_Single_V2.json")));
        var key = new ODataEntityKey(V2).AddKeyProperty("Ticket", "123");
        var result = await new ODataRequestReadByKey("/v2/", "ticket", key, string.Empty, V2).ExecuteAsync(handler.CreateClient());
        var ticket = await result.AsSingleAsync<Ticket>();
        Assert.Equal("1000001", ticket.TicketId);
        Assert.False(ticket.TicketIsBlocked);
        Assert.Equal("TESTSUPPLIER01", ticket.TicketName);
    }

    [Fact]
    public async Task ByKey_V4_ReadsEntityFromRoot()
    {
        var result = await ExecuteRead(TestData("Ticket_Single_V4.json"), V4);
        var ticket = await result.AsSingleAsync<Ticket>();
        Assert.Equal("1000001", ticket.TicketId);
        Assert.False(ticket.TicketIsBlocked);
    }

    [Fact]
    public async Task GetAll_V2_ReadsDResults()
    {
        var result = await ExecuteRead(TestData("Ticket_Collection_V2.json"), V2);
        Assert.Equal(2, (await result.AsListAsync<Ticket>()).Count);
    }

    [Fact]
    public async Task GetAll_V4_ReadsValue()
    {
        var result = await ExecuteRead(TestData("Ticket_Collection_V4.json"), V4);
        Assert.Equal(2, (await result.AsListAsync<Ticket>()).Count);
    }

    [Fact]
    public async Task Primitive_V2_ReadsFirstPropertyOfD()
    {
        var result = await ExecuteRead("{\"d\" : {\"func\" : \"someString\"}}", V2);
        Assert.Equal("someString", await result.AsSingleAsync<string>());
    }

    [Fact]
    public async Task Primitive_V4_ReadsValue()
    {
        var result = await ExecuteRead("{\"value\" : \"someString\"}", V4);
        Assert.Equal("someString", await result.AsSingleAsync<string>());
        Assert.Equal(["someString"], await (await ExecuteRead("{\"value\" : [\"someString\"]}", V4)).AsListAsync<string>());
    }

    [Fact]
    public async Task Primitive_ConvertsToRequestedType()
    {
        Assert.Equal(42, await (await ExecuteRead("{\"value\": 42}", V4)).AsSingleAsync<int>());
        Assert.Equal(42L, await (await ExecuteRead("{\"value\": \"42\"}", V4)).AsSingleAsync<long>());
        Assert.True(await (await ExecuteRead("{\"value\": true}", V4)).AsSingleAsync<bool>());
        Assert.Equal(1.5m, await (await ExecuteRead("{\"value\": 1.5}", V4)).AsSingleAsync<decimal>());
        Assert.Equal([1, 2, 3], await (await ExecuteRead("{\"value\": [1,2,3]}", V4)).AsListAsync<int>());
        var exception = await Assert.ThrowsAsync<ODataDeserializationException>(
            async () => await (await ExecuteRead("{\"value\": 42}", V4)).AsSingleAsync<DateTime>().ConfigureAwait(true));
        _ = exception;
    }

    [Fact]
    public async Task Primitive_FromPlainText()
    {
        var count = await ExecuteRead("1691", V4, "Entity/$count", "text/plain; charset=utf-8");
        Assert.Equal(1691L, await count.AsSingleAsync<long>());
        Assert.Equal("hello", await (await ExecuteRead("hello", V4, "Entity", "text/plain")).AsSingleAsync<string>());
    }

    [Fact]
    public async Task ResultExtractor_SelectsNestedElement()
    {
        var result = await ExecuteRead(TestData("Ticket_Function_V2.json"), V2, "func");
        var partner = await result.AsSingleAsync<Ticket>(e => e.GetProperty("Partner"));
        Assert.Equal("TESTSUPPLIER01", partner.TicketName);
    }

    [Fact]
    public async Task WrongEnvelope_IsDeserializationException()
    {
        var result = await ExecuteRead("{\"x\": 1}", V4);
        await Assert.ThrowsAsync<ODataDeserializationException>(() => result.AsListAsync<Ticket>());
        var v2 = await ExecuteRead("{\"value\": []}", V2);
        await Assert.ThrowsAsync<ODataDeserializationException>(() => v2.AsListAsync<Ticket>());
    }

    [Fact]
    public async Task InvalidJson_IsDeserializationException()
    {
        var result = await ExecuteRead("{not json", V4);
        var exception = await Assert.ThrowsAsync<ODataDeserializationException>(() => result.AsSingleAsync<Ticket>());
        Assert.Equal("{not json", exception.HttpBody);
        Assert.Equal(200, exception.HttpCode);
    }

    [Fact]
    public async Task NoPayload_IsDeserializationException()
    {
        var handler = new FakeHttpHandler(Responses.NoContent());
        var result = await new ODataRequestAction("/service/", "Act", null, V4).ExecuteAsync(handler.CreateClient());
        Assert.False(result.HasPayload);
        var exception = await Assert.ThrowsAsync<ODataDeserializationException>(() => result.AsMapAsync());
        Assert.Contains("did not contain any payload", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlineCount_V2AndV4()
    {
        var v2 = await ExecuteRead(TestData("odata-v2-response-with-inline-count.json"), V2);
        Assert.Equal(2, await v2.GetInlineCountAsync());
        var map = await v2.AsMapAsync();
        Assert.True(map.ContainsKey("results") && map.ContainsKey("__count"));

        var v4 = await ExecuteRead(TestData("odata-v4-response-with-inline-count.json"), V4);
        Assert.Equal(2, await v4.GetInlineCountAsync());
        var map4 = await v4.AsMapAsync();
        Assert.True(map4.ContainsKey("value") && map4.ContainsKey("@odata.count"));
    }

    [Fact]
    public async Task InlineCount_Missing_IsDeserializationException()
    {
        await Assert.ThrowsAsync<ODataDeserializationException>(
            async () => await (await ExecuteRead(TestData("odata-v2-response-without-inline-count.json"), V2)).GetInlineCountAsync().ConfigureAwait(true));
        await Assert.ThrowsAsync<ODataDeserializationException>(
            async () => await (await ExecuteRead(TestData("odata-v4-response-without-inline-count.json"), V4)).GetInlineCountAsync().ConfigureAwait(true));
    }

    [Fact]
    public async Task NextLink_V2()
    {
        const string payload = "{\"d\":{\"results\":[],\"__next\":\"/v1/foo/bar/endpoint?$skiptoken=s3cReT-t0k3n&foo=bar\"}}";
        var result = await ExecuteRead(payload, V2);
        var nextLink = await result.GetNextLinkAsync();
        Assert.Equal("/v1/foo/bar/endpoint?$skiptoken=s3cReT-t0k3n&foo=bar", nextLink);
        Assert.Equal("s3cReT-t0k3n", ODataUriFactory.ExtractSkipToken(nextLink));
    }

    [Fact]
    public async Task NextLink_V4_IgnoresV2Envelope()
    {
        const string payload = "{\"d\":{\"results\":[],\"__next\":\"/v1/foo/bar/endpoint?$skiptoken=s3cReT-t0k3n&foo=bar\"}}";
        Assert.Null(await (await ExecuteRead(payload, V4)).GetNextLinkAsync());
        Assert.Null(await (await ExecuteRead("{\"d\": {}}", V2)).GetNextLinkAsync());
        Assert.Equal(
            "/v1/foo/bar/endpoint?$skiptoken=&foo=bar",
            await (await ExecuteRead("{\"d\": {\"__next\": \"/v1/foo/bar/endpoint?$skiptoken=&foo=bar\"}}", V2)).GetNextLinkAsync());
        Assert.Equal("/x?$skiptoken=1", await (await ExecuteRead("{\"@nextLink\": \"/x?$skiptoken=1\"}", V4)).GetNextLinkAsync());
    }

    [Fact]
    public async Task DeltaLink_V4Only()
    {
        const string payload = "{\"@odata.context\": \"$metadata#FooBar\",\"value\": [],\"@odata.deltaLink\": \"/v1/foo/bar/endpoint?$deltatoken=s3cReT-t0k3n&foo=bar\"}";
        var v4 = await ExecuteRead(payload, V4);
        var deltaLink = await v4.GetDeltaLinkAsync();
        Assert.Equal("/v1/foo/bar/endpoint?$deltatoken=s3cReT-t0k3n&foo=bar", deltaLink);
        Assert.Equal("s3cReT-t0k3n", ODataUriFactory.ExtractDeltaToken(deltaLink));
        Assert.Null(await (await ExecuteRead(payload, V2)).GetDeltaLinkAsync());
        Assert.Null(await (await ExecuteRead("{}", V4)).GetDeltaLinkAsync());
    }

    private sealed class FixedQueryMerger(string query) : IUriQueryMerger
    {
        public Uri MergeRequestUri(Uri requestUri) => new("?" + query, UriKind.Relative);
    }

    [Theory]
    [InlineData("foo=bar", "/v1/foo/bar/endpoint?$skiptoken=s3cReT-t0k3n")]
    [InlineData("foo=SAP", "/v1/foo/bar/endpoint?$skiptoken=s3cReT-t0k3n&foo=bar")]
    public async Task NextLink_RemovesQueryParametersAddedByTheClient(string clientQuery, string expected)
    {
        const string payload = "{\"d\":{\"results\":[],\"__next\":\"/v1/foo/bar/endpoint?$skiptoken=s3cReT-t0k3n&foo=bar\"}}";
        var handler = new FakeHttpHandler(Responses.Json(payload));
        var request = new ODataRequestRead("/v1/foo/bar/", "endpoint", "blub=42", V2);
        request.UriQueryMerger = new FixedQueryMerger("high=five&" + clientQuery);
        var result = await request.ExecuteAsync(handler.CreateClient());
        Assert.Equal(expected, await result.GetNextLinkAsync());
    }

    private static string Page(int index, bool hasNext) =>
        "{\"@odata.count\":6,\"value\":[{\"CustomerID\":\"A" + index + "\"},{\"CustomerID\":\"B" + index + "\"}]"
        + (hasNext ? ",\"@odata.nextLink\":\"Customers?$count=true&$select=CustomerID&$skiptoken='A" + index + "'\"" : string.Empty) + "}";

    [Fact]
    public async Task Pagination_IteratesAllPagesAndKeepsHeaders()
    {
        var pages = new Queue<string>([Page(1, true), Page(2, true), Page(3, false)]);
        var handler = new FakeHttpHandler(_ => Responses.Json(pages.Dequeue()));
        var request = new ODataRequestRead("V4/Northwind/Northwind.svc", "Customers", "$count=true", V4);
        request.AddHeader("foo", "bar");

        var first = await request.ExecuteAsync(handler.CreateClient());
        Assert.Equal(6, await first.GetInlineCountAsync());
        Assert.Equal(2, (await first.AsListOfMapsAsync()).Count);

        var items = 0;
        var pageCount = 0;
        await foreach (var page in first.IteratePagesAsync<Dictionary<string, object?>>())
        {
            pageCount++;
            items += page.Count;
        }

        Assert.Equal(3, pageCount);
        Assert.Equal(6, items);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(["bar"], r.GetHeader("foo")));
        Assert.Equal("/V4/Northwind/Northwind.svc/Customers?$count=true&$select=CustomerID&$skiptoken='A1'", handler.Requests[1].PathAndQuery);
        Assert.Equal("/V4/Northwind/Northwind.svc/Customers?$count=true&$select=CustomerID&$skiptoken='A2'", handler.Requests[2].PathAndQuery);
    }

    [Fact]
    public async Task Pagination_ServerErrorOnNextPage_IsRequestException()
    {
        var pages = new Queue<HttpResponseMessage>([Responses.Json(Page(1, true)), Responses.Text("Something went wrong!", "text/plain", 500)]);
        var handler = new FakeHttpHandler(_ => pages.Dequeue());
        var first = await new ODataRequestRead("V4/Service", "Customers", "$count=true", V4).ExecuteAsync(handler.CreateClient());
        var exception = await Assert.ThrowsAsync<ODataRequestException>(async () =>
        {
            await foreach (var unused in first.IteratePagesAsync<Dictionary<string, object?>>())
            {
                _ = unused;
            }
        });
        Assert.IsType<ODataResponseException>(exception.InnerException);
    }

    [Fact]
    public async Task Pagination_ConnectionErrorOnNextPage_IsRequestException()
    {
        var calls = 0;
        var handler = new FakeHttpHandler(_ => calls++ == 0 ? Responses.Json(Page(1, true)) : throw new HttpRequestException("down"));
        var first = await new ODataRequestRead("V4/Service", "Customers", "$count=true", V4).ExecuteAsync(handler.CreateClient());
        var exception = await Assert.ThrowsAsync<ODataRequestException>(async () =>
        {
            await foreach (var unused in first.IteratePagesAsync<Dictionary<string, object?>>())
            {
                _ = unused;
            }
        });
        Assert.IsType<ODataConnectionException>(exception.InnerException);
    }

    [Fact]
    public async Task TryGetNextPage_WithoutNextLink_Fails()
    {
        var result = await ExecuteRead(Page(1, false), V4);
        var next = await result.TryGetNextPageAsync();
        Assert.True(next.IsFailure);
        Assert.IsType<ODataRequestException>(next.Exception);
    }

    [Fact]
    public async Task TryGetNextPage_OnlyApplicableForReads()
    {
        var handler = new FakeHttpHandler(_ => Responses.Json("{}", 201));
        var result = await new ODataRequestCreate("/service/", "Entity", "{}", V4).ExecuteAsync(handler.CreateClient());
        var next = await result.TryGetNextPageAsync();
        Assert.IsType<InvalidOperationException>(next.Exception);
    }

    [Fact]
    public async Task StreamElements_YieldsEntities()
    {
        var result = await ExecuteRead(Page(1, false), V4);
        var ids = new List<string>();
        await foreach (var element in result.StreamElementsAsync())
        {
            ids.Add(element.GetAsObject().GetElement("CustomerID")!.AsString());
        }
        Assert.Equal(["A1", "B1"], ids);
    }

    [Fact]
    public async Task GetResultElement_ExposesTheEntity()
    {
        var result = await ExecuteRead(TestData("Ticket_Single_V2.json"), V2);
        var element = await result.GetResultElementAsync();
        Assert.Equal("1000001", element.GetAsObject().GetElement("Ticket")!.AsString());
    }

    [Fact]
    public async Task VersionIdentifierFromHeader_IsTakenFromETag()
    {
        var handler = new FakeHttpHandler(_ => Responses.Json("{}").WithHeader("ETag", "W/\"abc\""));
        var result = await new ODataRequestRead("/service/", "Entity", null, V4).ExecuteAsync(handler.CreateClient());
        Assert.Equal("W/\"abc\"", result.VersionIdentifierFromHeader);
        Assert.Equal(["W/\"abc\""], result.GetHeaderValues("etag"));
        Assert.Contains("ETag", result.HeaderNames);
        Assert.Null((await ExecuteRead("{}", V4)).VersionIdentifierFromHeader);
    }

    private const string ReferencePayload = """
        {
          "BooleanValue": false,
          "BinaryValue": "T0RhdGE",
          "IntegerValue": -128,
          "DoubleValue": 3.1415926535897931,
          "SingleValue": "NaN",
          "DecimalValue": 12.3999999999999999,
          "StringValue": "Say \"Hello\",\nthen go",
          "DateValue": "2012-12-03",
          "DateTimeOffsetValue": "2012-12-03T07:16:23Z",
          "DurationValue": "P12DT23H59M59.999999999S",
          "TimeOfDayValue": "07:59:59.999",
          "GuidValue": "01234567-89ab-cdef-0123-456789abcdef",
          "Int64Value": 0,
          "ColorValueValue": "Yellow"
        }
        """;

    [Fact]
    public async Task DataTypeParsing_ByReferenceObject()
    {
        var result = await ExecuteRead(ReferencePayload, V4);
        var actual = await result.AsSingleAsync<ReferenceObject>();
        Assert.False(actual.BooleanValue);
        Assert.Equal([79, 68, 97, 116, 97], actual.BinaryValue);
        Assert.Equal("Say \"Hello\",\nthen go", actual.StringValue);
        Assert.Equal(-128, actual.IntegerValue);
        Assert.Equal(0L, actual.Int64Value);
        Assert.True(float.IsNaN(actual.SingleValue));
        Assert.Equal(12.4d, actual.DecimalValue);
        Assert.Equal(3.1415926535897931, actual.DoubleValue);
        Assert.Equal(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), actual.GuidValue);
        Assert.Equal(new TimeSpan(12, 23, 59, 59).Ticks + 9999999, actual.DurationValue.Ticks);
        Assert.Equal(new TimeOnly(7, 59, 59, 999), actual.TimeOfDayValue);
        Assert.Equal(new DateOnly(2012, 12, 3), actual.DateValue);
        Assert.Equal(new DateTimeOffset(2012, 12, 3, 7, 16, 23, TimeSpan.Zero), actual.DateTimeOffsetValue);
        Assert.Null(actual.Tags);
        Assert.Equal(ColorValue.Yellow, actual.ColorValueValue);
    }

    [Fact]
    public async Task DataTypeParsing_ByNumberDeserializationStrategy()
    {
        var asDouble = await (await ExecuteRead(ReferencePayload, V4)).AsMapAsync();
        Assert.Equal(12.4d, asDouble["DecimalValue"]);

        var result = await ExecuteRead(ReferencePayload, V4);
        var asDecimal = await result.WithNumberDeserializationStrategy(NumberDeserializationStrategy.AsDecimal).AsMapAsync();
        Assert.Equal(12.3999999999999999m, asDecimal["DecimalValue"]);
    }

    [Fact]
    public async Task AsMap_ReadsNestedValuesAsPlainObjects()
    {
        var map = await (await ExecuteRead("{\"a\": {\"b\": [1, \"x\", null, true]}}", V4)).AsMapAsync();
        var a = Assert.IsType<Dictionary<string, object?>>(map["a"]);
        Assert.Equal([1d, "x", null, true], Assert.IsType<List<object?>>(a["b"]));
    }

    [Fact]
    public void Converters_RoundTripJavaFormats()
    {
        var options = ODataJsonOptions.Get();
        Assert.Equal("\"PT1H30M\"", System.Text.Json.JsonSerializer.Serialize(TimeSpan.FromMinutes(90), options));
        Assert.Equal("\"13:37\"", System.Text.Json.JsonSerializer.Serialize(new TimeOnly(13, 37), options));
        Assert.Equal("\"2012-12-03T07:16Z\"", System.Text.Json.JsonSerializer.Serialize(new DateTimeOffset(2012, 12, 3, 7, 16, 0, TimeSpan.Zero), options));
        Assert.Equal("\"T0RhdGE=\"", System.Text.Json.JsonSerializer.Serialize(Encoding.ASCII.GetBytes("OData"), options));
        Assert.Equal("\"1.50\"", System.Text.Json.JsonSerializer.Serialize(1.50m, options.WithBigDecimalConverter()));
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<byte[]>("\"***\"", options));
    }

    [Fact]
    public async Task ResponseException_ExposesStatusHeadersAndBody()
    {
        var handler = new FakeHttpHandler(_ => Responses.Text("oops", "text/plain", 502).WithHeader("X-Trace", "t1"));
        var request = new ODataRequestRead("/service/", "Entity", null, V4);
        var exception = await Assert.ThrowsAsync<ODataResponseException>(() => request.ExecuteAsync(handler.CreateClient()));
        Assert.Equal(502, exception.HttpCode);
        Assert.Equal("oops", exception.HttpBody);
        Assert.Contains(exception.HttpHeaders, h => h.Key == "X-Trace" && h.Value == "t1");
        Assert.Same(request, exception.Request);
        Assert.Contains("(502)", exception.Message, StringComparison.Ordinal);
    }
}

internal static class JsonOptionsExtensions
{
    public static System.Text.Json.JsonSerializerOptions WithBigDecimalConverter(this System.Text.Json.JsonSerializerOptions options)
    {
        var copy = new System.Text.Json.JsonSerializerOptions(options);
        copy.Converters.Add(new BigDecimalJsonConverter());
        return copy;
    }
}
