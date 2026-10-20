using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>Collects everything that is logged, including exceptions, at all levels.</summary>
internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    private readonly Lock _gate = new();
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private void Add(string entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private sealed class CapturingLogger(CapturingLoggerFactory owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Add(formatter(state, exception) + (exception is null ? string.Empty : " " + exception));
    }
}

/// <summary>Registration as default loader, loader chains, and the rule that secrets never reach logs or <c>ToString</c>.</summary>
public sealed class SecretsAndRegistrationTests : DestinationServiceTestBase
{
    private const string RefreshTokenSecret = "refresh-token-secret-value";
    private const string HeaderSecret = "custom-header-secret-value";

    protected override void Reset()
    {
        base.Reset();
        SdkLogging.LoggerFactory = null!;
    }

    [Fact]
    public void TheDestinationService_IsRegisteredAsADefaultLoader_AfterTheEnvironmentVariableLoader()
    {
        // touching the assembly runs its module initializer
        _ = DestinationService.CreateBuilder();

        var chain = Assert.IsType<DestinationLoaderChain>(DestinationAccessor.Loader);

        Assert.IsType<EnvVarDestinationLoader>(chain.Loaders[0]);
        Assert.Contains(chain.Loaders, loader => loader is DestinationService);
    }

    [Fact]
    public void TheOAuthLoaders_AreRegistered_WhenTheDestinationServiceIsUsed()
    {
        // the destination service reaches its REST API through a destination that the OAuth loaders create from the binding
        _ = DestinationService.CreateBuilder();

        Assert.Contains(ServiceBindingDestinationLoader.DefaultLoaders, loader => loader is OAuth2ServiceBindingDestinationLoader);
    }

    [Fact]
    public void RegisteringTheDefaultLoaderAgain_HasNoEffect()
    {
        DestinationService.EnsureRegisteredAsDefaultLoader();
        DestinationService.EnsureRegisteredAsDefaultLoader();

        var chain = Assert.IsType<DestinationLoaderChain>(DestinationAccessor.Loader);
        Assert.Single(chain.Loaders, loader => loader is DestinationService);
    }

    [Fact]
    public async Task TheChain_FallsBackToTheDestinationService_WhenTheFirstLoaderDoesNotKnowTheDestination()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        CurrentTenant = new DefaultTenant("tenant");
        var adapter = new FakeAdapter().Respond("/v1/destinations/remote", Responses.Http("remote", "https://remote.example.com"));
        DestinationAccessor.Loader = DestinationLoaderChain.CreateBuilder(new EnvVarDestinationLoader()).Append(CreateService(adapter)).Build();

        var destination = (await DestinationAccessor.GetDestinationAsync("remote")).AsHttp();

        Assert.Equal(new Uri("https://remote.example.com"), destination.Uri);
    }

    [Fact]
    public async Task TheChain_ReportsNotFound_WhenNoLoaderKnowsTheDestination()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        var adapter = new FakeAdapter().Fail(
            (_, _) => true,
            new DestinationNotFoundException(null, "Destination could not be found for path /destination-configuration/v1/destinations/missing."));
        DestinationAccessor.Loader = DestinationLoaderChain.CreateBuilder(new EnvVarDestinationLoader()).Append(CreateService(adapter)).Build();

        await Assert.ThrowsAsync<DestinationNotFoundException>(() => DestinationAccessor.GetDestinationAsync("missing"));
    }

    [Fact]
    public async Task TheChain_StopsAtAnAccessFailure()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        var adapter = new FakeAdapter().Fail((_, _) => true, new DestinationAccessException("service down"));
        DestinationAccessor.Loader = DestinationLoaderChain.CreateBuilder(new EnvVarDestinationLoader()).Append(CreateService(adapter)).Build();

        await Assert.ThrowsAsync<DestinationAccessException>(() => DestinationAccessor.GetDestinationAsync("anything"));
    }

    [Fact]
    public void SynchronousLookup_ThroughTheAccessor_BlocksOnTheAsynchronousImplementation()
    {
        DestinationService.Cache.DisablePreLookupCheck();
        var adapter = new FakeAdapter().Respond("/v1/destinations/remote", Responses.Http("remote", "https://remote.example.com"));
        DestinationAccessor.Loader = CreateService(adapter);
        CurrentTenant = new DefaultTenant("tenant");

        var destination = DestinationAccessor.GetDestination("remote");

        Assert.Equal(new Uri("https://remote.example.com"), destination.AsHttp().Uri);
    }

    [Fact]
    public async Task Secrets_AreNeverLogged_AndNeverPartOfToString()
    {
        var logs = new CapturingLoggerFactory();
        SdkLogging.LoggerFactory = logs;
        DestinationService.Cache.DisablePreLookupCheck();
        var userToken = XsuaaToken("zid", "someone");
        CurrentToken = userToken;
        CurrentTenant = new DefaultTenant("tenant");
        CurrentPrincipal = new DefaultPrincipal("principal");
        var adapter = new FakeAdapter()
            .Respond("/v1/destinations/with-token", Responses.BasicWithAuthToken)
            .Respond("/v1/destinations/refresh", Responses.OAuthWithAuthToken)
            .Fail((path, _) => path == "/v1/destinations/broken", new DestinationAccessException("Failed to get destinations: status 500"));
        var service = CreateService(adapter);
        var refresh = Options(Augmenter().RefreshToken(RefreshTokenSecret).CustomHeaders(new Header("X-Secret", HeaderSecret)).FragmentName("frag"));

        var withToken = (await service.TryGetDestinationAsync("with-token", NoOptions)).GetValueOrThrow();
        var withRefreshToken = (await service.TryGetDestinationAsync("refresh", refresh)).GetValueOrThrow();
        var broken = await service.TryGetDestinationAsync("broken", NoOptions);

        List<string> texts =
        [
            .. logs.Entries,
            withToken.ToString()!,
            withRefreshToken.ToString()!,
            withToken.AsHttp().ToString()!,
            refresh.ToString(),
            broken.Exception!.ToString(),
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.NamedUserCurrentTenant, userToken.Jwt.EncodedToken).ToString(),
            DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.NamedUserCurrentTenant, RefreshTokenSecret).ToString(),
            string.Join(' ', adapter.Calls.Select(c => c.Strategy.ToString())),
        ];
        Assert.NotEmpty(logs.Entries);
        foreach (var text in texts)
        {
            Assert.DoesNotContain(userToken.Jwt.EncodedToken, text, StringComparison.Ordinal);
            Assert.DoesNotContain(RefreshTokenSecret, text, StringComparison.Ordinal);
            Assert.DoesNotContain(HeaderSecret, text, StringComparison.Ordinal);
            Assert.DoesNotContain("bearer_token", text, StringComparison.Ordinal);
            Assert.DoesNotContain("dGVzdDpwYgXNzMTIzNDU=", text, StringComparison.Ordinal);
        }
        // the secrets are used, though
        Assert.Contains(adapter.Calls, c => c.Strategy.Token == RefreshTokenSecret);
        Assert.Contains(adapter.Calls, c => c.Strategy.Token == userToken.Jwt.EncodedToken);
    }

    [Fact]
    public void CacheKeys_DoNotRevealTheirComponents()
    {
        var key = CacheKey.FromIds("tenant", "principal").Append("destination", Options(Augmenter().RefreshToken(RefreshTokenSecret)));

        Assert.DoesNotContain(RefreshTokenSecret, key.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RetrievalStrategies_AreEqualByValue()
    {
        Assert.Equal(
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserProvider, "t").WithFragmentName("f"),
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserProvider, "t").WithFragmentName("f"));
        Assert.NotEqual(
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserProvider, "t"),
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserProvider, "u"));
        Assert.NotEqual(
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserProvider, "t"),
            DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.TechnicalUserProvider, "t"));
        Assert.NotEqual(
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant));
    }

    [Fact]
    public void RetrievalStrategies_RejectEmptyTokensAndRepeatedFragments()
    {
        Assert.Throws<ArgumentException>(() => DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserProvider, " "));
        Assert.Throws<ArgumentException>(() => DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.TechnicalUserProvider, ""));
        Assert.Throws<ArgumentException>(() => DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider).WithFragmentName(" "));
        Assert.Throws<InvalidOperationException>(
            () => DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider).WithFragmentName("a").WithFragmentName("b"));
    }
}

/// <summary>The options of the destination service.</summary>
public sealed class DestinationServiceOptionsAugmenterTests : DestinationServiceTestBase
{
    [Fact]
    public void Augmenter_SetsAllParameters()
    {
        var options = Options(
            Augmenter()
                .RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider)
                .TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.ExchangeOnly)
                .RefreshToken("refresh")
                .FragmentName("fragment")
                .CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderInstance)
                .CustomHeaders(new Header("X-A", "1"), new Header("X-B", "2")));

        Assert.Equal(DestinationServiceRetrievalStrategy.AlwaysProvider, DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options));
        Assert.Equal(DestinationServiceTokenExchangeStrategy.ExchangeOnly, DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(options));
        Assert.Equal("refresh", DestinationServiceOptionsAugmenter.GetRefreshToken(options));
        Assert.Equal("fragment", DestinationServiceOptionsAugmenter.GetFragmentName(options));
        Assert.Equal(DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderInstance, DestinationServiceOptionsAugmenter.GetCrossLevelScope(options));
        Assert.Equal([new Header("X-A", "1"), new Header("X-B", "2")], DestinationServiceOptionsAugmenter.GetAdditionalHeaders(options));
    }

    [Fact]
    public void NothingIsSet_MeansNothingIsRead()
    {
        Assert.Null(DestinationServiceOptionsAugmenter.GetRetrievalStrategy(NoOptions));
        Assert.Null(DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(NoOptions));
        Assert.Null(DestinationServiceOptionsAugmenter.GetRefreshToken(NoOptions));
        Assert.Null(DestinationServiceOptionsAugmenter.GetFragmentName(NoOptions));
        Assert.Null(DestinationServiceOptionsAugmenter.GetCrossLevelScope(NoOptions));
        Assert.Empty(DestinationServiceOptionsAugmenter.GetAdditionalHeaders(NoOptions));
    }

    [Fact]
    public void UnknownIdentifiers_AreIgnored()
    {
        var options = DestinationOptions.CreateBuilder()
            .Parameter(DestinationServiceOptionsAugmenter.DestinationRetrievalStrategyKey, "Nonsense")
            .Parameter(DestinationServiceOptionsAugmenter.DestinationTokenExchangeStrategyKey, "Nonsense")
            .Build();

        Assert.Null(DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options));
        Assert.Null(DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(options));
    }

    [Fact]
    public void ValuesOfTheWrongType_AreRejected()
    {
        var options = DestinationOptions.CreateBuilder()
            .Parameter(DestinationServiceOptionsAugmenter.DestinationRetrievalStrategyKey, 42)
            .Parameter(DestinationServiceOptionsAugmenter.DestinationTokenExchangeStrategyKey, 42)
            .Build();

        Assert.Throws<InvalidCastException>(() => DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options));
        Assert.Throws<InvalidCastException>(() => DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(options));
    }

    [Theory]
    [InlineData(DestinationServiceOptionsAugmenter.CrossLevelScope.Subaccount, "@subaccount")]
    [InlineData(DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderSubaccount, "@provider_subaccount")]
    [InlineData(DestinationServiceOptionsAugmenter.CrossLevelScope.Instance, "@instance")]
    [InlineData(DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderInstance, "@provider_instance")]
    public void CrossLevelScopes_HaveTheirSuffix(DestinationServiceOptionsAugmenter.CrossLevelScope scope, string suffix) =>
        Assert.Equal(suffix, scope.GetSuffix());

    [Fact]
    public void Identifiers_AreCaseInsensitive_AndRoundTrip()
    {
        foreach (var strategy in Enum.GetValues<DestinationServiceRetrievalStrategy>())
        {
            Assert.Equal(strategy, DestinationServiceRetrievalStrategyExtensions.OfIdentifier(strategy.GetIdentifier().ToUpperInvariant()));
        }
        foreach (var strategy in Enum.GetValues<DestinationServiceTokenExchangeStrategy>())
        {
            Assert.Equal(strategy, DestinationServiceTokenExchangeStrategyExtensions.OfIdentifier(strategy.GetIdentifier().ToLowerInvariant()));
        }
        Assert.Null(DestinationServiceRetrievalStrategyExtensions.OfIdentifier(null));
        Assert.Equal("AlwaysProvider", DestinationServiceRetrievalStrategy.AlwaysProvider.GetIdentifier());
        Assert.Equal("ForwardUserToken", DestinationServiceTokenExchangeStrategy.ForwardUserToken.GetIdentifier());
    }

    [Fact]
    public void Options_WithTheSameParameters_AreEqual()
    {
        Assert.Equal(
            Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider).CustomHeaders(new Header("X", "1"))),
            Options(Augmenter().CustomHeaders(new Header("X", "1")).RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider)));
        Assert.NotEqual(
            Options(Augmenter().CustomHeaders(new Header("X", "1"))),
            Options(Augmenter().CustomHeaders(new Header("X", "2"))));
    }
}
