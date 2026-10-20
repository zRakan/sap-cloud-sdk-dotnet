using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class ServiceBindingDestinationLoaderTests : ConnectivityStateReset
{
    private static readonly IServiceBinding Binding = Bindings.Of("my-service");
    private static readonly ServiceBindingDestinationOptions Options = ServiceBindingDestinationOptions.ForService(Binding).Build();
    private static readonly IHttpDestination Destination = DefaultHttpDestination.CreateBuilder("https://example.org").Build();

    private static Result<IHttpDestination> Ok() => Result<IHttpDestination>.Success(Destination);

    private static Result<IHttpDestination> Fail(Exception e) => Result<IHttpDestination>.Failure(e);

    [Fact]
    public void NoDelegates_FailsWithAccessException()
    {
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([]);

        var result = chain.TryGetDestination(Options);

        var e = Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Contains("no delegate loaders", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoaderReportingNotFound_ResultsInNotFoundWithAggregatedCauses()
    {
        var cause = new DestinationNotFoundException();
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([new FakeBindingLoader(_ => Fail(cause))]);

        var result = chain.TryGetDestination(Options);

        var e = Assert.IsType<DestinationNotFoundException>(result.Exception);
        Assert.Same(cause, Assert.IsType<AggregateException>(e.InnerException).InnerExceptions.Single());
    }

    [Fact]
    public void LoaderReportingAccessException_IsWrappedAndThrownByGetDestination()
    {
        var cause = new DestinationAccessException();
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([new FakeBindingLoader(_ => Fail(cause))]);

        var e = Assert.Throws<DestinationAccessException>(() => chain.GetDestination(Options));
        Assert.Same(cause, e.InnerException);
    }

    [Fact]
    public void LoaderReportingOtherException_IsWrapped()
    {
        var cause = new InvalidOperationException();
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([new FakeBindingLoader(_ => Fail(cause))]);

        var result = chain.TryGetDestination(Options);

        var e = Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Same(cause, e.InnerException);
        Assert.DoesNotContain("binding-secret", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedNotFound_IsSkipped()
    {
        var cause = new InvalidOperationException(new DestinationNotFoundException().Message, new DestinationNotFoundException());
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([new FakeBindingLoader(_ => Fail(cause))]);

        var e = Assert.IsType<DestinationNotFoundException>(chain.TryGetDestination(Options).Exception);
        Assert.Same(cause, Assert.IsType<AggregateException>(e.InnerException).InnerExceptions.Single());
    }

    [Fact]
    public void NestedAccessException_EndsTheChain()
    {
        var cause = new InvalidOperationException(
            "outer",
            new DestinationAccessException("access", new DestinationNotFoundException()));
        var next = new FakeBindingLoader(_ => Ok());
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([new FakeBindingLoader(_ => Fail(cause)), next]);

        var result = chain.TryGetDestination(Options);

        Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Equal(0, next.Calls);
    }

    [Fact]
    public void FirstSuccessWins()
    {
        var first = new FakeBindingLoader(_ => Ok());
        var second = new FakeBindingLoader(_ => Ok());
        var chain = ServiceBindingDestinationLoader.NewLoaderChain([first, second]);

        Assert.Same(Destination, chain.TryGetDestination(Options).GetValueOrThrow());
        Assert.Equal(1, first.Calls);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public void NotFoundThenSuccess_FallsBack()
    {
        var chain = ServiceBindingDestinationLoader.NewLoaderChain(
            [new FakeBindingLoader(_ => Fail(new DestinationNotFoundException())), new FakeBindingLoader(_ => Ok())]);

        Assert.Same(Destination, chain.GetDestination(Options));
    }

    [Fact]
    public async Task Async_FallsBackAndDefaultsToTheSyncMethod()
    {
        var chain = ServiceBindingDestinationLoader.NewLoaderChain(
            [new FakeBindingLoader(_ => Fail(new DestinationNotFoundException())), new FakeBindingLoader(_ => Ok())]);

        var result = await chain.TryGetDestinationAsync(Options, CancellationToken.None);

        Assert.Same(Destination, result.GetValueOrThrow());
    }

    [Fact]
    public void RegisteredDefaultLoaders_AreUsedByTheDefaultChain_EvenIfRegisteredLater()
    {
        var chain = ServiceBindingDestinationLoader.DefaultLoaderChain;
        Assert.IsType<DestinationAccessException>(chain.TryGetDestination(Options).Exception);

        var loader = new FakeBindingLoader(_ => Ok());
        ServiceBindingDestinationLoader.RegisterDefaultLoader(loader);
        ServiceBindingDestinationLoader.RegisterDefaultLoader(loader);

        Assert.Single(ServiceBindingDestinationLoader.DefaultLoaders);
        Assert.Same(Destination, chain.GetDestination(Options));

        Assert.True(ServiceBindingDestinationLoader.UnregisterDefaultLoader(loader));
        Assert.Empty(ServiceBindingDestinationLoader.DefaultLoaders);
    }

    [Fact]
    public void Options_ForServiceIdentifier_FindsTheSingleBinding()
    {
        DefaultServiceBindingAccessor.Instance = new FixedBindingAccessor(Bindings.Of("destination"), Bindings.Of("xsuaa"));

        var options = ServiceBindingDestinationOptions.ForService(ServiceIdentifier.Destination).Build();

        Assert.Equal("destination", options.ServiceBinding.ServiceName);
        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, options.OnBehalfOf);
    }

    [Fact]
    public void Options_ForServiceIdentifier_WithoutOrWithMultipleBindings_Throws()
    {
        DefaultServiceBindingAccessor.Instance = new FixedBindingAccessor(Bindings.Of("xsuaa"), Bindings.Of("xsuaa"));

        Assert.Contains("ambiguous", Assert.Throws<DestinationAccessException>(() => ServiceBindingDestinationOptions.ForService(ServiceIdentifier.Xsuaa)).Message, StringComparison.Ordinal);
        Assert.Contains("Could not find", Assert.Throws<DestinationAccessException>(() => ServiceBindingDestinationOptions.ForService(ServiceIdentifier.Destination)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_FurtherOptions_AreStoredByType()
    {
        var options = ServiceBindingDestinationOptions.ForService(Binding)
            .OnBehalfOf(OnBehalfOf.NamedUserCurrentTenant)
            .WithOption(BtpServiceOptions.WorkflowOptions.RestApi)
            .WithOption(BtpServiceOptions.AuthenticationServiceOptions.WithTargetUri("https://target.example"))
            .Build();

        Assert.Equal(OnBehalfOf.NamedUserCurrentTenant, options.OnBehalfOf);
        Assert.True(options.TryGetOption<BtpServiceOptions.WorkflowOptions, BtpServiceOptions.WorkflowOptions>(out var workflow));
        Assert.Same(BtpServiceOptions.WorkflowOptions.RestApi, workflow);
        Assert.True(options.TryGetOption<BtpServiceOptions.AuthenticationServiceOptions.TargetUri, Uri>(out var target));
        Assert.Equal(new Uri("https://target.example"), target);
        Assert.False(options.TryGetOption<BtpServiceOptions.BusinessRulesOptions, BtpServiceOptions.BusinessRulesOptions>(out _));
    }

    [Fact]
    public void Options_SameOptionTypeTwice_Throws()
    {
        var builder = ServiceBindingDestinationOptions.ForService(Binding).WithOption(BtpServiceOptions.WorkflowOptions.RestApi);

        Assert.Throws<ArgumentException>(() => builder.WithOption(BtpServiceOptions.WorkflowOptions.ODataApi));
    }

    [Fact]
    public void Options_ToString_HidesBindingCredentials() =>
        Assert.DoesNotContain("binding-secret", Options.ToString(), StringComparison.Ordinal);

    [Fact]
    public void ServiceIdentifier_IsDerivedFromTheServiceName()
    {
        Assert.Equal(ServiceIdentifier.Connectivity, Bindings.Of("connectivity").GetServiceIdentifier());
        Assert.Null(new DefaultServiceBinding(null, null, null, [], new Dictionary<string, object?>()).GetServiceIdentifier());
        Assert.Equal("identity", ServiceIdentifier.IdentityAuthentication.ToString());
    }
}
