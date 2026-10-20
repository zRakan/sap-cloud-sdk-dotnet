using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class DestinationLoaderChainTests
{
    private const string Name1 = "some-destination";
    private const string Name2 = "some-other-destination";

    private readonly IDestination _destination1 = DefaultDestination.CreateBuilder().Name(Name1).Uri("https://one").Build();
    private readonly IDestination _destination2 = DefaultDestination.CreateBuilder().Name(Name2).Uri("https://two").Build();

    [Fact]
    public void FirstSuccessWins_LaterLoadersAreNotAsked()
    {
        var full1 = FakeLoader.Returning(Name1, _destination1);
        var full2 = FakeLoader.Returning(Name2, _destination2);
        var empty = FakeLoader.NotFound();
        var chain = DestinationLoaderChain.CreateBuilder(full1).Append(full2).Append(empty).Build();

        Assert.Same(_destination1, chain.TryGetDestination(Name1).GetValueOrThrow());
        Assert.Equal([Name1], full1.Requests);
        Assert.Empty(full2.Requests);
        Assert.Empty(empty.Requests);
    }

    [Fact]
    public void NotFound_FallsBackToNextLoader()
    {
        var empty = FakeLoader.NotFound();
        var full = FakeLoader.Returning(Name1, _destination1);
        var chain = DestinationLoaderChain.CreateBuilder(empty).Append(empty).Append(full).Build();

        Assert.Same(_destination1, chain.TryGetDestination(Name1).GetValueOrThrow());
        Assert.Equal(2, empty.Requests.Count);
        Assert.Equal([Name1], full.Requests);
    }

    [Fact]
    public void NotFoundEverywhere_AggregatesAllFailures()
    {
        var chain = DestinationLoaderChain.CreateBuilder(FakeLoader.NotFound()).Append(FakeLoader.NotFound()).Build();

        var result = chain.TryGetDestination(Name2);

        var e = Assert.IsType<DestinationNotFoundException>(result.Exception);
        Assert.Equal(Name2, e.DestinationName);
        var aggregate = Assert.IsType<AggregateException>(e.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.All(aggregate.InnerExceptions, inner => Assert.IsType<DestinationNotFoundException>(inner));
    }

    [Fact]
    public void OtherFailure_EndsTheLookupAndIsReturnedAsIs()
    {
        var failure = new DestinationAccessException("wrapped", new ArgumentException("root"));
        var failing = FakeLoader.Failing(failure);
        var full = FakeLoader.Returning(Name1, _destination1);
        var chain = DestinationLoaderChain.CreateBuilder(failing).Append(full).Append(failing).Build();

        var result = chain.TryGetDestination(Name2);

        Assert.Same(failure, result.Exception);
        Assert.Single(failing.Requests);
        Assert.Empty(full.Requests);
    }

    [Fact]
    public void FailureAfterNotFound_IsReturned()
    {
        var failure = new DestinationAccessException("broken");
        var chain = DestinationLoaderChain.CreateBuilder(FakeLoader.NotFound()).Append(FakeLoader.Failing(failure)).Build();

        Assert.Same(failure, chain.TryGetDestination(Name2).Exception);
    }

    [Fact]
    public void NestedNotFoundCause_CountsAsNotFound()
    {
        var wrapped = new InvalidOperationException("wrapper", new DestinationNotFoundException(Name1));
        var chain = DestinationLoaderChain.CreateBuilder(FakeLoader.Failing(wrapped))
            .Append(FakeLoader.Returning(Name1, _destination1))
            .Build();

        Assert.Same(_destination1, chain.TryGetDestination(Name1).GetValueOrThrow());
    }

    [Fact]
    public void EmptyLoaderList_Throws() =>
        Assert.Throws<ArgumentException>(() => new DestinationLoaderChain([]));

    [Fact]
    public async Task Async_UsesAsyncMethodsOfLoadersAndFallsBack()
    {
        var asyncLoader = new AsyncOnlyLoader(_destination1);
        var chain = DestinationLoaderChain.CreateBuilder(FakeLoader.NotFound()).Append(asyncLoader).Build();

        var result = await chain.TryGetDestinationAsync(Name1, CancellationToken.None);

        Assert.Same(_destination1, result.GetValueOrThrow());
        Assert.Equal(1, asyncLoader.AsyncCalls);
    }

    [Fact]
    public async Task Async_NotFoundEverywhere_IsNotFound()
    {
        var chain = DestinationLoaderChain.CreateBuilder(FakeLoader.NotFound()).Build();

        var result = await chain.TryGetDestinationAsync(Name1, CancellationToken.None);

        Assert.IsType<DestinationNotFoundException>(result.Exception);
    }

    [Fact]
    public async Task DefaultAsyncImplementation_CallsTheSyncMethod()
    {
        IDestinationLoader loader = FakeLoader.Returning(Name1, _destination1);

        var result = await loader.TryGetDestinationAsync(Name1, DestinationOptions.CreateBuilder().Build(), CancellationToken.None);

        Assert.Same(_destination1, result.GetValueOrThrow());
    }

    [Fact]
    public void DefaultDestinationLoader_ReturnsRegisteredDestinations()
    {
        var loader = new DefaultDestinationLoader().RegisterDestination(_destination1);

        Assert.Same(_destination1, loader.TryGetDestination(Name1).GetValueOrThrow());
        Assert.IsType<DestinationNotFoundException>(loader.TryGetDestination(Name2).Exception);
    }

    [Fact]
    public void DefaultDestinationLoader_RequiresAName()
    {
        var loader = new DefaultDestinationLoader();

        Assert.Throws<ArgumentException>(() => loader.RegisterDestination(DefaultDestination.CreateBuilder().Uri("https://x").Build()));
        Assert.Throws<ArgumentException>(() => loader.RegisterDestination(DefaultDestination.CreateBuilder().Name("").Build()));
    }

    [Fact]
    public void DefaultDestinationLoader_ReplacesDestinationsWithTheSameName()
    {
        var replacement = DefaultDestination.CreateBuilder().Name(Name1).Uri("https://replacement").Build();
        var loader = new DefaultDestinationLoader().RegisterDestination(_destination1).RegisterDestination(replacement);

        Assert.Same(replacement, loader.TryGetDestination(Name1).GetValueOrThrow());
    }
}
