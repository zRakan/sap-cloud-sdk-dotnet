namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class AsyncHeaderProviderTests
{
    private sealed class AsyncOnlyProvider(string name, string value) : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) =>
            throw new InvalidOperationException("sync path must not be used");

        public async ValueTask<IReadOnlyList<Header>> GetHeadersAsync(DestinationRequestContext requestContext, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return [new Header(name, value)];
        }
    }

    [Fact]
    public async Task GetHeadersAsync_UsesAsyncProviders_AndKeepsAuthorizationPrecedence()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://example.com")
            .BasicCredentials("user", "pass")
            .HeaderProviders(new AsyncOnlyProvider("Authorization", "Bearer from-provider"))
            .Build();

        var headers = await destination.GetHeadersAsync(new Uri("https://example.com/x"));

        var authorization = Assert.Single(headers, h => h.Name == "Authorization");
        Assert.Equal("Bearer from-provider", authorization.Value);
    }

    [Fact]
    public async Task GetHeadersAsync_WrapsProviderFailures()
    {
        var destination = DefaultHttpDestination.CreateBuilder("https://example.com")
            .HeaderProviders(new FailingProvider())
            .Build();

        await Assert.ThrowsAsync<Exceptions.DestinationAccessException>(
            async () => await destination.GetHeadersAsync(new Uri("https://example.com")));
    }

    [Fact]
    public async Task DefaultInterfaceImplementation_FallsBackToSync()
    {
        IDestinationHeaderProvider provider = new SyncProvider();
        var context = new DestinationRequestContext(DefaultHttpDestination.CreateBuilder("https://e.com").Build(), new Uri("https://e.com"));
        Assert.Equal("v", Assert.Single(await provider.GetHeadersAsync(context)).Value);
    }

    private sealed class FailingProvider : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) => throw new InvalidOperationException();

        public ValueTask<IReadOnlyList<Header>> GetHeadersAsync(DestinationRequestContext requestContext, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<IReadOnlyList<Header>>(new InvalidOperationException("boom"));
    }

    private sealed class SyncProvider : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) => [new Header("X", "v")];
    }
}
