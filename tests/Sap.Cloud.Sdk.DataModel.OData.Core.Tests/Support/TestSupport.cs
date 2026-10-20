using System.Text.Json.Nodes;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;

/// <summary>Compares JSON documents regardless of whitespace and property order.</summary>
internal static class JsonAssert
{
    public static void Equal(string expected, string? actual)
    {
        Assert.NotNull(actual);
        var expectedNode = JsonNode.Parse(expected);
        var actualNode = JsonNode.Parse(actual);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"JSON differs.{Environment.NewLine}Expected: {expectedNode?.ToJsonString()}{Environment.NewLine}Actual:   {actualNode?.ToJsonString()}");
    }
}

/// <summary>An OData V2 "server" for tests: answers CSRF token requests and everything else with a configurable response.</summary>
internal sealed class FakeODataServer
{
    public const string CsrfToken = "awesome-token";

    public FakeODataServer(Func<RecordedRequest, HttpResponseMessage> responder, bool issueCsrfToken = true)
    {
        Handler = new FakeHttpHandler(request =>
            request.Method == HttpMethod.Head
                ? (issueCsrfToken ? Responses.Empty().WithHeader("x-csrf-token", CsrfToken) : Responses.Empty())
                : responder(request));
    }

    public FakeODataServer(HttpResponseMessage response, bool issueCsrfToken = true)
        : this(_ => response, issueCsrfToken)
    {
    }

    public FakeHttpHandler Handler { get; }

    public HttpClient Client => Handler.CreateClient();

    public IReadOnlyList<RecordedRequest> Requests => Handler.Requests;

    /// <summary>The requests that are not CSRF token fetches.</summary>
    public IReadOnlyList<RecordedRequest> DataRequests => Handler.Requests.Where(r => r.Method != HttpMethod.Head).ToList();

    public RecordedRequest SingleDataRequest => Assert.Single(DataRequests);
}

internal static class RequestExtensions
{
    /// <summary>The decoded path and query as sent, for readable assertions.</summary>
    public static string Url(this ODataRequestGeneric request) => request.GetRelativeUri().OriginalString;

    public static string Query(this ODataRequestGeneric request) => request.GetRequestQuery();
}

internal static class V2
{
    public static IODataProtocol Protocol => ODataProtocol.V2;
}
