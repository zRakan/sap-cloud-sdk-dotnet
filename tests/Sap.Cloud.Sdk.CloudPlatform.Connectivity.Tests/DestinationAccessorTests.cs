using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class DestinationAccessorTests : ConnectivityStateReset
{
    private readonly IDestination _destination = DefaultDestination.CreateBuilder().Name("dest").Uri("https://example.org").Build();

    [Fact]
    public void GetDestination_UsesTheConfiguredLoader()
    {
        DestinationAccessor.Loader = FakeLoader.Returning("dest", _destination);

        Assert.Same(_destination, DestinationAccessor.GetDestination("dest"));
        Assert.True(DestinationAccessor.TryGetDestination("dest").IsSuccess);
    }

    [Fact]
    public void GetDestination_NotFound_ThrowsNotFound()
    {
        DestinationAccessor.Loader = FakeLoader.NotFound();

        var e = Assert.Throws<DestinationNotFoundException>(() => DestinationAccessor.GetDestination("missing"));
        Assert.Equal("missing", e.DestinationName);
    }

    [Fact]
    public void GetDestination_AccessFailure_IsRethrownAsIs()
    {
        var failure = new DestinationAccessException("broken");
        DestinationAccessor.Loader = FakeLoader.Failing(failure);

        Assert.Same(failure, Assert.Throws<DestinationAccessException>(() => DestinationAccessor.GetDestination("x")));
    }

    [Fact]
    public void GetDestination_OtherFailure_IsWrapped()
    {
        var failure = new InvalidOperationException("other");
        DestinationAccessor.Loader = FakeLoader.Failing(failure);

        var e = Assert.Throws<DestinationAccessException>(() => DestinationAccessor.GetDestination("x"));
        Assert.Same(failure, e.InnerException);
        Assert.Contains("'x'", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDestinationAsync_UsesAsyncLoaderMethod()
    {
        var loader = new AsyncOnlyLoader(_destination);
        DestinationAccessor.Loader = loader;

        var destination = await DestinationAccessor.GetDestinationAsync("dest", CancellationToken.None);

        Assert.Same(_destination, destination);
        Assert.Equal(1, loader.AsyncCalls);
    }

    [Fact]
    public async Task GetDestinationAsync_NotFound_Throws()
    {
        DestinationAccessor.Loader = FakeLoader.NotFound();

        await Assert.ThrowsAsync<DestinationNotFoundException>(() => DestinationAccessor.GetDestinationAsync("nope", CancellationToken.None));
    }

    [Fact]
    public async Task TryGetDestinationAsync_ReturnsFailureInsteadOfThrowing()
    {
        DestinationAccessor.Loader = FakeLoader.NotFound();

        var result = await DestinationAccessor.TryGetDestinationAsync("nope", CancellationToken.None);

        Assert.IsType<DestinationNotFoundException>(result.Exception);
    }

    [Fact]
    public void DefaultLoader_ReadsTheDestinationsEnvironmentVariable()
    {
        var previous = Environment.GetEnvironmentVariable("destinations");
        Environment.SetEnvironmentVariable("destinations", """[{"name":"fromEnv","URL":"https://env.example"}]""");
        try
        {
            DestinationAccessor.Loader = null!;

            Assert.Equal(new Uri("https://env.example"), DestinationAccessor.GetDestination("fromEnv").AsHttp().Uri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("destinations", previous);
        }
    }

    [Fact]
    public void DefaultLoader_AsksRegisteredSecondaryLoadersAfterTheEnvironmentVariable()
    {
        var secondary = new DefaultDestinationLoader().RegisterDestination(_destination);
        DestinationAccessorTestHooks.RegisterDefaultLoader(secondary);
        DestinationAccessor.Loader = null!;

        Assert.Same(_destination, DestinationAccessor.GetDestination("dest"));
        var chain = Assert.IsType<DestinationLoaderChain>(DestinationAccessor.Loader);
        Assert.Equal(2, chain.Loaders.Count);
        Assert.IsType<EnvVarDestinationLoader>(chain.Loaders[0]);
    }

    [Fact]
    public void RegisteringADefaultLoader_AfterTheDefaultWasCreated_TakesEffect()
    {
        _ = DestinationAccessor.Loader;
        DestinationAccessorTestHooks.RegisterDefaultLoader(new DefaultDestinationLoader().RegisterDestination(_destination));

        Assert.Same(_destination, DestinationAccessor.GetDestination("dest"));
    }

    [Fact]
    public void AppendAndPrepend_ExtendTheCurrentChain()
    {
        var first = FakeLoader.Returning("a", DefaultDestination.CreateBuilder().Name("a").Build());
        DestinationAccessor.Loader = first;
        var appended = FakeLoader.Returning("b", _destination);
        var prepended = FakeLoader.Returning("c", _destination);

        DestinationAccessor.AppendDestinationLoader(appended);
        DestinationAccessor.PrependDestinationLoader(prepended);

        var chain = Assert.IsType<DestinationLoaderChain>(DestinationAccessor.Loader);
        Assert.Equal(new IDestinationLoader[] { prepended, first, appended }, chain.Loaders);
    }

    [Fact]
    public void Append_SameLoaderAsCurrent_IsIgnored()
    {
        var loader = FakeLoader.NotFound();
        DestinationAccessor.Loader = loader;

        DestinationAccessor.AppendDestinationLoader(loader);

        Assert.Same(loader, DestinationAccessor.Loader);
    }

    [Fact]
    public void AppendToAutoCreatedChain_SurvivesLaterRegistration()
    {
        var appended = new DefaultDestinationLoader().RegisterDestination(_destination);
        DestinationAccessor.AppendDestinationLoader(appended);
        DestinationAccessorTestHooks.RegisterDefaultLoader(FakeLoader.NotFound());

        Assert.Same(_destination, DestinationAccessor.GetDestination("dest"));
    }

    [Fact]
    public void DestinationOptions_ArePassedToTheLoader()
    {
        DestinationOptions? seen = null;
        DestinationAccessor.Loader = new OptionsCapturingLoader(o => seen = o, _destination);
        var options = DestinationOptions.CreateBuilder().Parameter("k", "v").Build();

        DestinationAccessor.GetDestination("dest", options);

        Assert.Same(options, seen);
    }

    private sealed class OptionsCapturingLoader(Action<DestinationOptions> capture, IDestination destination) : IDestinationLoader
    {
        public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
        {
            capture(options);
            return Result<IDestination>.Success(destination);
        }
    }
}
