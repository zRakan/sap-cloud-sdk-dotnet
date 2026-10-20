using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

public class DestinationRequestHandlerTests : HttpStateReset
{
    private static (HttpClient Client, RecordingHandler Recorder) CreateClient(DefaultHttpDestination destination)
    {
        var recorder = new RecordingHandler();
        var handler = new DestinationRequestHandler(destination) { InnerHandler = recorder };
        return (new HttpClient(handler) { BaseAddress = destination.Uri }, recorder);
    }

    [Theory]
    [InlineData("https://host", "api/v1", "https://host/api/v1")]
    [InlineData("https://host/", "/api/v1", "https://host/api/v1")]
    [InlineData("https://host/base", "api", "https://host/base/api")]
    [InlineData("https://host/base/", "api", "https://host/base/api")]
    [InlineData("https://host/base", "/api", "https://host/base/api")]
    [InlineData("https://host/base", "/base/api", "https://host/base/api")]
    [InlineData("https://host/base", "api?x=1", "https://host/base/api?x=1")]
    [InlineData("https://host/base?a=1", "api?x=1", "https://host/base/api?a=1&x=1")]
    [InlineData("https://host/base", "a%20b", "https://host/base/a%20b")]
    public async Task RelativeRequestUris_AreAppendedToTheDestinationPath(string destinationUri, string requestUri, string expected)
    {
        var (client, recorder) = CreateClient(DefaultHttpDestination.CreateBuilder(destinationUri).Build());

        await client.GetAsync(new Uri(requestUri, UriKind.Relative));

        Assert.Equal(expected, recorder.Requests.Single().Uri.OriginalString);
    }

    [Fact]
    public async Task EmptyRequestUri_TargetsTheDestinationUri()
    {
        var (client, recorder) = CreateClient(DefaultHttpDestination.CreateBuilder("https://host/service/").Build());

        await client.GetAsync((Uri?)null);

        Assert.Equal("https://host/service/", recorder.Requests.Single().Uri.OriginalString);
    }

    [Fact]
    public async Task DestinationQueryProperties_AreAppended_AndEscaped()
    {
        var destination = DefaultHttpDestination.CreateBuilder("http://foo.com/service/?sap-client=200")
            .Property("URL.queries.sap-client", "100")
            .Property("URL.queries.q", "a b&c")
            .Build();
        var (client, recorder) = CreateClient(destination);

        await client.GetAsync(new Uri("Entity?$top=1", UriKind.Relative));

        var query = recorder.Requests.Single().Uri.Query;
        Assert.Contains("sap-client=200", query, StringComparison.Ordinal);
        Assert.Contains("sap-client=100", query, StringComparison.Ordinal);
        Assert.Contains("$top=1", query, StringComparison.Ordinal);
        Assert.Contains("q=a%20b%26c", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AbsoluteRequestUriOfAnotherHost_IsRejected()
    {
        var (client, recorder) = CreateClient(DefaultHttpDestination.CreateBuilder("https://host/base").Build());

        await Assert.ThrowsAsync<DestinationPathsNotMergeableException>(() => client.GetAsync("https://evil.example/steal"));

        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task AbsoluteRequestUriOfTheDestinationHost_IsAppendedLikeARelativeOne()
    {
        var (client, recorder) = CreateClient(DefaultHttpDestination.CreateBuilder("https://host/base").Build());

        // HttpClient resolves "/other" against the base address to https://host/other before the handler runs.
        await client.GetAsync("/other");

        Assert.Equal("https://host/base/other", recorder.Requests.Single().Uri.OriginalString);
    }

    [Fact]
    public void OnPremiseDestinationWithoutProxy_IsRejected()
    {
        var destination = new StubHttpDestination(new Uri("http://virtual:80"), ProxyType.OnPremise, null);

        Assert.Throws<DestinationAccessException>(() => new DestinationRequestHandler(destination));
    }

    // ---- header rules -------------------------------------------------------------------------------------------

    [Fact]
    public async Task DestinationHeaders_AreAddedToTheRequest()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host")
            .Header("X-Custom", "from-destination")
            .BasicCredentials("user", "pw")
            .AuthenticationType(AuthenticationType.BasicAuthentication)
            .Build();
        var (client, recorder) = CreateClient(destination);

        await client.GetAsync("x");

        var request = recorder.Requests.Single();
        Assert.Equal("from-destination", request.Header("X-Custom"));
        Assert.StartsWith("Basic ", request.Header("Authorization"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultiValuedHeaderOfTheRequest_IsNotReplaced_ButExtended()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host").Header("X-Custom", "destination").Build();
        var (client, recorder) = CreateClient(destination);
        using var request = new HttpRequestMessage(HttpMethod.Get, "x");
        request.Headers.TryAddWithoutValidation("X-Custom", "caller");

        await client.SendAsync(request);

        Assert.Equal(["caller", "destination"], recorder.Requests.Single().Headers["X-Custom"]);
    }

    [Fact]
    public async Task SingleValuedHeaderOfTheRequest_WinsOverTheDestinationHeader()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host").Header("Authorization", "Bearer destination").Build();
        var (client, recorder) = CreateClient(destination);
        using var request = new HttpRequestMessage(HttpMethod.Get, "x");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer caller");

        await client.SendAsync(request);

        Assert.Equal(["Bearer caller"], recorder.Requests.Single().Headers["Authorization"]);
    }

    [Fact]
    public async Task SingleValuedHeader_IsAdded_IfTheRequestHasNone()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host").Header("Authorization", "Bearer destination").Build();
        var (client, recorder) = CreateClient(destination);

        await client.GetAsync("x");

        Assert.Equal(["Bearer destination"], recorder.Requests.Single().Headers["Authorization"]);
    }

    [Fact]
    public async Task ContentHeader_GoesToTheContent_OrIsSkippedWithoutContent()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host").Header("Content-Type", "application/json").Build();
        var (client, recorder) = CreateClient(destination);

        using var withContent = new HttpRequestMessage(HttpMethod.Post, "x") { Content = new ByteArrayContent([1]) };
        await client.SendAsync(withContent);
        await client.GetAsync("y");
        await client.PostAsync("z", new StringContent("{}")); // sets its own Content-Type: the caller wins

        Assert.Equal("application/json", recorder.Requests[0].Header("Content-Type"));
        Assert.Null(recorder.Requests[1].Header("Content-Type"));
        Assert.StartsWith("text/plain", recorder.Requests[2].Header("Content-Type"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullHeaderValue_IsSentAsEmptyValue()
    {
        var provider = new AsyncHeaderProvider(_ => [new Header("X-Empty", null)]);
        var destination = DefaultHttpDestination.CreateBuilder("https://host").HeaderProviders(provider).Build();
        var (client, recorder) = CreateClient(destination);

        await client.GetAsync("x");

        Assert.Equal("", recorder.Requests.Single().Header("X-Empty"));
    }

    [Fact]
    public async Task AsyncHeaderProvider_IsAwaitedPerRequest_WithTheFinalUri()
    {
        var counter = 0;
        var provider = new AsyncHeaderProvider(_ => [new Header("X-Count", (++counter).ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        var destination = DefaultHttpDestination.CreateBuilder("https://host/base")
            .Property("URL.queries.sap-client", "100")
            .HeaderProviders(provider)
            .Build();
        var (client, recorder) = CreateClient(destination);

        await client.GetAsync("a");
        await client.GetAsync("b");

        Assert.Equal(["https://host/base/a?sap-client=100", "https://host/base/b?sap-client=100"], provider.Requested.Select(u => u.OriginalString));
        Assert.Equal("1", recorder.Requests[0].Header("X-Count"));
        Assert.Equal("2", recorder.Requests[1].Header("X-Count"));
    }

    [Fact]
    public async Task FailingHeaderProvider_FailsTheRequest_WithoutSendingIt()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://host").HeaderProviders(new ThrowingProvider()).Build();
        var (client, recorder) = CreateClient(destination);

        await Assert.ThrowsAsync<DestinationAccessException>(() => client.GetAsync("x"));

        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task CancellationToken_IsPassedToTheHeaderProvider()
    {
        var provider = new AsyncHeaderProvider(_ => []);
        var destination = DefaultHttpDestination.CreateBuilder("https://host").HeaderProviders(provider).Build();
        var (client, _) = CreateClient(destination);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("x", cts.Token));
    }

    [Fact]
    public async Task DestinationIsResolvedPerRequest_WhenAResolverIsUsed()
    {
        var recorder = new RecordingHandler();
        var current = DefaultHttpDestination.CreateBuilder("https://one.example/a").Build();
        var handler = new DestinationRequestHandler(_ => ValueTask.FromResult<IHttpDestinationProperties>(current), new Uri("https://placeholder.invalid/"))
        {
            InnerHandler = recorder,
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://placeholder.invalid/") };

        await client.GetAsync("x");
        current = DefaultHttpDestination.CreateBuilder("https://two.example/b").Build();
        await client.GetAsync("x");

        Assert.Equal(["https://one.example/a/x", "https://two.example/b/x"], recorder.Requests.Select(r => r.Uri.OriginalString));
    }

    private sealed class ThrowingProvider : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) =>
            throw new InvalidOperationException("boom");
    }
}
