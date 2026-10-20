using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Client.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

/// <summary>Port of ODataResponseErrorParsingTest: HTTP error responses become typed exceptions.</summary>
public class ErrorParsingTests
{
    private const string ServicePath = "/service/";

    [Theory]
    [InlineData(403)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task HttpErrorCodes_BecomeResponseExceptionWithEmptyBody(int statusCode)
    {
        foreach (var protocol in new[] { ODataProtocol.V2, ODataProtocol.V4 })
        {
            var handler = new FakeHttpHandler(_ => Responses.Empty(statusCode));
            var exception = await Assert.ThrowsAsync<ODataResponseException>(
                () => new ODataRequestRead(ServicePath, "Entity", string.Empty, protocol).ExecuteAsync(handler.CreateClient()));
            Assert.IsNotType<ODataServiceErrorException>(exception);
            Assert.Equal(statusCode, exception.HttpCode);
            Assert.Equal(string.Empty, exception.HttpBody);
        }
    }

    [Fact]
    public async Task ErrorWithoutEntity_HasNoBody()
    {
        var handler = new FakeHttpHandler(_ => Responses.NoContent());
        var response = new ODataHttpResponse(500, "oh", [], null);
        var exception = new ODataResponseException(null, response, "x", null);
        Assert.Null(exception.HttpBody);
        Assert.Equal(500, exception.HttpCode);
        _ = handler;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsingODataV2Error()
    {
        const string json = """
            {
               "error":{
                  "code":"005056A509B11EE1B9A8FEC11C23378E",
                  "message":{
                     "lang":"en",
                     "value":"System query options '$orderby,$skip,$top,$skiptoken,$inlinecount' are not allowed in the requested URI"
                  },
                  "innererror":{
                     "transactionid":"25669476CF9401E0E005F2FA0752F574",
                     "timestamp":"20200813143348.4420100",
                     "Error_Resolution":{
                        "SAP_Transaction":"For backend administrators: use ADT feed reader",
                        "SAP_Note":"See SAP Note 1797736 for error analysis"
                     },
                     "errordetails": [{
                        "code": "UF1",
                        "message": "$search query option not supported",
                        "target": "t1",
                        "additionalTargets": ["t2","t3"],
                        "severity": "error"
                      }]
                    }
               }
            }
            """;
        var handler = new FakeHttpHandler(_ => Responses.Json(json, 400));
        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(
            () => new ODataRequestRead(ServicePath, "Entity", string.Empty, ODataProtocol.V2).ExecuteAsync(handler.CreateClient()));

        Assert.Equal(400, exception.HttpCode);
        var error = exception.ODataError;
        Assert.Equal("005056A509B11EE1B9A8FEC11C23378E", error.ODataCode);
        Assert.StartsWith("System query options", error.ODataMessage, StringComparison.Ordinal);
        var detail = Assert.Single(error.Details);
        Assert.Equal("UF1", detail.ODataCode);
        Assert.Equal("$search query option not supported", detail.ODataMessage);
        Assert.Equal("t1", detail.Target);
        Assert.Contains("transactionid", error.InnerError.Keys);
        Assert.Contains("timestamp", error.InnerError.Keys);
        Assert.IsType<Dictionary<string, object?>>(error.InnerError["Error_Resolution"]);
        Assert.DoesNotContain("errordetails", error.InnerError.Keys);
        Assert.Contains("responded with an error message", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParsingODataV4Error()
    {
        const string json = """
            {
              "error": {
                "code": "err123",
                "message": "Unsupported functionality",
                "target": "query",
                "details": [
                  {
                    "code": "forty-two",
                    "target": "$search",
                    "message": "$search query option not supported"
                  }
                ],
                "innererror": {
                  "foo": 123,
                  "bar": "ok"
                }
              }
            }
            """;
        var handler = new FakeHttpHandler(_ => Responses.Json(json, 400));
        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(
            () => new ODataRequestRead(ServicePath, "Entity", string.Empty, ODataProtocol.V4).ExecuteAsync(handler.CreateClient()));

        Assert.Equal("err123", exception.ODataError.ODataCode);
        Assert.Equal("Unsupported functionality", exception.ODataError.ODataMessage);
        Assert.Equal("query", exception.ODataError.Target);
        var detail = Assert.Single(exception.ODataError.Details);
        Assert.Equal(new ODataServiceError("forty-two", "$search query option not supported", "$search", [], new Dictionary<string, object?>()), detail);
        Assert.Equal(123.0, exception.ODataError.InnerError["foo"]);
        Assert.Equal("ok", exception.ODataError.InnerError["bar"]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task BrokenODataError_BecomesPlainResponseException(int version)
    {
        const string json = "{\"error\": {\"a\"}}";
        var handler = new FakeHttpHandler(_ => Responses.Json(json, 400));
        var exception = await Assert.ThrowsAsync<ODataResponseException>(
            () => new ODataRequestRead(ServicePath, "Entity", string.Empty, version == 2 ? ODataProtocol.V2 : ODataProtocol.V4).ExecuteAsync(handler.CreateClient()));
        Assert.IsNotType<ODataServiceErrorException>(exception);
        Assert.Equal(400, exception.HttpCode);
        Assert.Equal(json, exception.HttpBody);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task NonJsonError_BecomesPlainResponseException(int version)
    {
        var handler = new FakeHttpHandler(_ => Responses.Text("foo", "text/plain", 400));
        var exception = await Assert.ThrowsAsync<ODataResponseException>(
            () => new ODataRequestRead(ServicePath, "Entity", string.Empty, version == 2 ? ODataProtocol.V2 : ODataProtocol.V4).ExecuteAsync(handler.CreateClient()));
        Assert.IsNotType<ODataServiceErrorException>(exception);
        Assert.Equal("foo", exception.HttpBody);
    }

    [Fact]
    public async Task UnbufferedResultAlsoCarriesTheErrorBody()
    {
        var handler = new FakeHttpHandler(_ => Responses.Json("{\"error\":{\"code\":\"c\",\"message\":\"m\"}}", 404));
        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(
            () => new ODataRequestRead(ServicePath, "Entity", null, ODataProtocol.V4).WithoutResponseBuffering().ExecuteAsync(handler.CreateClient()));
        Assert.Equal("c", exception.ODataError.ODataCode);
    }

    [Fact]
    public void Exceptions_HaveStandardConstructors()
    {
        var inner = new InvalidOperationException("inner");
        Assert.Equal("m", new ODataException("m").Message);
        Assert.Same(inner, new ODataRequestException("m", inner).InnerException);
        Assert.Same(inner, new ODataConnectionException("m", inner).InnerException);
        Assert.Same(inner, new ODataSerializationException("m", inner).InnerException);
        Assert.Same(inner, new ODataResponseException("m", inner).InnerException);
        Assert.Same(inner, new ODataDeserializationException("m", inner).InnerException);
        Assert.Same(inner, new ODataServiceErrorException("m", inner).InnerException);
        Assert.IsAssignableFrom<InvalidOperationException>(new ODataException());
        var serialization = new ODataSerializationException(null, this, "m", null);
        Assert.Same(this, serialization.NonSerializableObject);
    }
}
