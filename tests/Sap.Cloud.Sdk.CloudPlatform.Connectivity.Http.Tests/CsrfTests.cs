using System.Net;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

public class CsrfTokenHandlerTests
{
    private const string Token = "test-csrf-token";

    private static HttpResponseMessage Respond(HttpRequestMessage request, string? token = Token)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (token is not null && request.Method == HttpMethod.Head)
        {
            response.Headers.TryAddWithoutValidation("x-csrf-token", token);
        }
        return response;
    }

    private static (HttpClient Client, RecordingHandler Recorder) Create(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var recorder = new RecordingHandler(respond ?? (r => Respond(r)));
        return (new HttpClient(new CsrfTokenHandler { InnerHandler = recorder }), recorder);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task MutatingMethods_FetchAndSendTheToken(string method)
    {
        var (client, recorder) = Create();

        await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "http://host/service/Entity"));

        Assert.Equal(2, recorder.Requests.Count);
        Assert.Equal(HttpMethod.Head, recorder.Requests[0].Method);
        Assert.Equal("http://host/service/", recorder.Requests[0].Uri.ToString());
        Assert.Equal("fetch", recorder.Requests[0].Header("x-csrf-token"));
        Assert.Equal(Token, recorder.Requests[1].Header("x-csrf-token"));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task NonMutatingMethods_DoNotFetchAToken(string method)
    {
        var (client, recorder) = Create();

        await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "http://host/service/Entity"));

        Assert.Single(recorder.Requests);
        Assert.Null(recorder.Requests[0].Header("x-csrf-token"));
    }

    [Fact]
    public async Task ExistingToken_IsKept_AndNothingIsFetched()
    {
        var (client, recorder) = Create();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://host/service/Entity");
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", "existing");

        await client.SendAsync(request);

        Assert.Single(recorder.Requests);
        Assert.Equal("existing", recorder.Requests[0].Header("x-csrf-token"));
    }

    [Fact]
    public async Task MissingTokenInResponse_SendsTheRequestWithoutToken()
    {
        var (client, recorder) = Create(r => Respond(r, token: null));

        var response = await client.PostAsync("http://host/service/Entity", new StringContent("x"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, recorder.Requests.Count);
        Assert.Null(recorder.Requests[1].Header("x-csrf-token"));
    }

    [Fact]
    public async Task FailingHeadRequest_DoesNotFailThePost()
    {
        var (client, recorder) = Create(r => r.Method == HttpMethod.Head ? throw new HttpRequestException("refused") : Respond(r));

        var response = await client.PostAsync("http://host/service/Entity", new StringContent("x"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(recorder.Requests.Last().Header("x-csrf-token"));
    }

    [Fact]
    public async Task NonPrintableCharacters_AreStrippedFromTheToken()
    {
        var (client, recorder) = Create(r => Respond(r, "valid\u0001token\u007f"));

        await client.PostAsync("http://host/service/Entity", new StringContent("x"));

        Assert.Equal("validtoken", recorder.Requests[1].Header("x-csrf-token"));
    }

    [Fact]
    public async Task OnlyTheFirstTokenHeaderIsUsed()
    {
        var (client, recorder) = Create(r =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (r.Method == HttpMethod.Head)
            {
                response.Headers.TryAddWithoutValidation("x-csrf-token", ["first-token", "second-token"]);
            }
            return response;
        });

        await client.PostAsync("http://host/service/Entity", new StringContent("x"));

        Assert.Equal("first-token", recorder.Requests[1].Header("x-csrf-token"));
    }

    [Fact]
    public async Task HeadRequest_CarriesTheHeadersOfTheOriginalRequest()
    {
        var (client, recorder) = Create();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://host/service/Entity(1)");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer abc");

        await client.SendAsync(request);

        Assert.Equal("http://host/service/", recorder.Requests[0].Uri.ToString());
        Assert.Equal("Bearer abc", recorder.Requests[0].Header("Authorization"));
    }

    [Fact]
    public async Task CancellationDuringHeadRequest_IsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        var (client, _) = Create(r =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PostAsync("http://host/service/Entity", new StringContent("x"), cts.Token));
    }

    [Theory]
    [InlineData("http://host/service/Entity", "http://host/service/")]
    [InlineData("http://host/service/", "http://host/service/")]
    [InlineData("http://host/service/$batch", "http://host/service/")]
    [InlineData("http://host/", "http://host/")]
    [InlineData("http://host/a/b/c/Entity", "http://host/a/b/c/")]
    [InlineData("http://host/sap/opu/odata/sap/API_BP/BusinessPartner('123')", "http://host/sap/opu/odata/sap/API_BP/")]
    [InlineData("http://host/service/Entity(42)", "http://host/service/")]
    [InlineData("http://host/service/Entity('key')/Nav", "http://host/service/")]
    [InlineData("http://host/service/Entity('key')/Nav(42)", "http://host/service/")]
    [InlineData("http://host/service/Entity?$filter=x&y=1", "http://host/service/")]
    public void DeriveServiceRootUri_FollowsJavaRules(string requestUri, string expected) =>
        Assert.Equal(expected, CsrfTokenHandler.DeriveServiceRootUri(new Uri(requestUri)).ToString());
}

public class DefaultCsrfTokenRetrieverTests
{
    private static HttpClient Client(RecordingHandler handler) => new(handler) { BaseAddress = new Uri("https://host/base/") };

    [Fact]
    public async Task RetrievesTheToken_WithFetchHeaderAndAdditionalHeaders()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("X-CSRF-Token", "tok\u0002en");
            return response;
        });
        var headers = new Dictionary<string, IReadOnlyCollection<string>> { ["X-Extra"] = ["a", "b"] };

        var token = await new DefaultCsrfTokenRetriever().RetrieveCsrfTokenAsync(Client(handler), "service/", headers);

        Assert.Equal("token", token.Token);
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Head, request.Method);
        Assert.Equal("https://host/base/service/", request.Uri.ToString());
        Assert.Equal("fetch", request.Header("x-csrf-token"));
        Assert.Equal(["a", "b"], request.Headers["X-Extra"]);
    }

    [Fact]
    public async Task MissingHeader_ThrowsCsrfTokenRetrievalException()
    {
        var ex = await Assert.ThrowsAsync<CsrfTokenRetrievalException>(() =>
            new DefaultCsrfTokenRetriever().RetrieveCsrfTokenAsync(Client(new RecordingHandler()), "service/"));

        Assert.Contains("did not respond", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailingRequest_IsWrapped()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("refused"));

        var ex = await Assert.ThrowsAsync<CsrfTokenRetrievalException>(() =>
            new DefaultCsrfTokenRetriever().RetrieveCsrfTokenAsync(Client(handler), "service/"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Cancellation_IsNotWrapped()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DefaultCsrfTokenRetriever().RetrieveCsrfTokenAsync(Client(new RecordingHandler()), "service/", null, cts.Token));
    }

    [Fact]
    public async Task DisabledRetriever_Throws()
    {
        Assert.False(ICsrfTokenRetriever.Disabled.IsEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ICsrfTokenRetriever.Disabled.RetrieveCsrfTokenAsync(Client(new RecordingHandler()), "x"));
    }

    [Fact]
    public void Retrievers_AreEqual_AsInJava() =>
        Assert.Equal(new DefaultCsrfTokenRetriever(), new DefaultCsrfTokenRetriever());

    [Fact]
    public void Token_IsHiddenInToString() =>
        Assert.DoesNotContain("secret", new CsrfToken("secret").ToString(), StringComparison.Ordinal);
}
