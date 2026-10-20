using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>Behavior of <see cref="DestinationService"/> with a fake destination service REST API (port of <c>DestinationServiceTest</c>).</summary>
public sealed class DestinationServiceTests : DestinationServiceTestBase
{
    private const string DestinationName = "SomeDestinationName";
    private const string ProviderUrl = "https://service.provider.com";
    private const string SubscriberUrl = "https://service.subscriber.com";
    private const string InstancePath = "/v1/instanceDestinations";
    private const string SubaccountPath = "/v1/subaccountDestinations";

    private readonly FakeAdapter _adapter = new() { ProviderTenant = "provider-tenant" };
    private readonly DestinationService _loader;
    private readonly DefaultTenant _providerTenant = new("provider-tenant");
    private readonly DefaultTenant _subscriberTenant = new("subscriber-tenant");
    private readonly DefaultPrincipal _principal1 = new("principal-1");
    private readonly DefaultPrincipal _principal2 = new("principal-2");
    private readonly AuthToken _userToken = XsuaaToken();

    public DestinationServiceTests()
    {
        // Disable PreLookupCheck to simplify test setup
        DestinationService.Cache.DisablePreLookupCheck();

        CurrentTenant = _subscriberTenant;
        CurrentPrincipal = _principal1;
        CurrentToken = _userToken;

        _loader = CreateService(_adapter);
        var path = "/v1/destinations/" + DestinationName;
        _adapter
            .Respond(path, OnBehalfOf.TechnicalUserProvider, Responses.Http(DestinationName, ProviderUrl))
            .Respond(path, OnBehalfOf.TechnicalUserCurrentTenant, Responses.Http(DestinationName, SubscriberUrl));
    }

    private string UserTokenValue => _userToken.Jwt.EncodedToken;

    private DestinationRetrievalStrategy WithUserToken() =>
        DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserCurrentTenant, UserTokenValue);

    private async Task<IDestination> GetAsync(string name, DestinationOptions? options = null, DestinationService? loader = null) =>
        (await (loader ?? _loader).TryGetDestinationAsync(name, options ?? NoOptions)).GetValueOrThrow();

    private async Task<Result<IDestination>> TryGetAsync(string name, DestinationOptions? options = null) =>
        await _loader.TryGetDestinationAsync(name, options ?? NoOptions);

    private static string NameOf(IDestinationProperties properties) =>
        properties.TryGet(DestinationProperty.Name, out var name) ? name : throw new InvalidOperationException("no name");

    private static void AssertKeys(IEnumerable<CacheKey> expected, IEnumerable<CacheKey> actual) =>
        Assert.True(
            expected.ToHashSet().SetEquals(actual),
            $"Expected {expected.Count()} keys, found {actual.Count()}.");

    private static CacheKey AllKey(string? tenantId, DestinationOptions options, string? principalId = null) =>
        CacheKey.FromIds(tenantId, principalId).Append(DestinationServiceOptionsAugmenter.RetrievalStrategyCacheComponent(options));

    private static CacheKey SingleKey(string? tenantId, string name, DestinationOptions options, string? principalId = null) =>
        CacheKey.FromIds(tenantId, principalId).Append(name, options);

    [Fact]
    public void InitialCacheSizeIsZero()
    {
        // the cache exists out of the box and is empty, so that configuring it at startup does not warn
        Assert.True(DestinationService.Cache.IsEnabled);
        Assert.Equal(0, DestinationService.Cache.InstanceSingle().Count);
    }

    [Fact]
    public async Task LoadingHttpDestination()
    {
        var destination = (await GetAsync(DestinationName)).AsHttp();

        Assert.Equal(DestinationName, NameOf(destination));
        Assert.Equal(new Uri(SubscriberUrl), destination.Uri);
        Assert.Equal(ProxyType.Internet, destination.ProxyType);
        Assert.Equal(AuthenticationType.NoAuthentication, destination.AuthenticationType);
        Assert.Null(destination.BasicCredentials);
        Assert.Null(destination.ProxyConfiguration);
        Assert.Null(destination.KeyStore);
        Assert.Null(destination.TrustStore);
        Assert.Null(destination.TlsVersion);
        Assert.True(destination.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal(_subscriberTenant.TenantId, tenantId);
        Assert.False(destination.TryGet(DestinationProperty.AuthTokens, out _));
        Assert.False(destination.TryGet(DestinationProperty.Certificates, out _));
        Assert.Equal(SecurityConfigurationStrategy.FromDestination, destination.SecurityConfigurationStrategy);
        Assert.True(destination.TryGet(DestinationProperty.PropertiesForChangeDetection, out var changeDetection));
        Assert.Equal(
            new[]
            {
                DestinationProperty.Name.KeyName,
                DestinationProperty.Uri.KeyName,
                DestinationProperty.Type.KeyName,
                DestinationProperty.ProxyType.KeyName,
                DestinationProperty.AuthType.KeyName,
                "Description",
            }.Order(),
            changeDetection.Order());
    }

    [Fact]
    public async Task GettingDestinationProperties_InstanceDestinationsTakePrecedence_AndResultIsCached()
    {
        _adapter
            .Respond(InstancePath, OnBehalfOf.TechnicalUserCurrentTenant, Responses.InstanceDestinations)
            .Respond(SubaccountPath, OnBehalfOf.TechnicalUserCurrentTenant, Responses.SubaccountDestinations);

        var destinations = await _loader.GetAllDestinationPropertiesAsync();

        Assert.Equal(["CC8-HTTP-BASIC", "CC8-HTTP-CERT1", "CC8-HTTP-CERT", DestinationName], destinations.Select(NameOf));
        var basic = destinations.Single(d => NameOf(d).Equals("CC8-HTTP-BASIC", StringComparison.OrdinalIgnoreCase));
        // of a subaccount destination and an instance destination of the same name, the instance one was picked
        Assert.Equal("USER1", basic.GetProperty("User"));

        // single destination convenience
        Assert.Same(basic, await _loader.GetDestinationPropertiesAsync("CC8-HTTP-BASIC"));
        await Assert.ThrowsAsync<DestinationNotFoundException>(() => _loader.GetDestinationPropertiesAsync("doesnt exist"));

        // verify all results are cached
        Assert.Equal(1, _adapter.CountCalls(InstancePath));
        Assert.Equal(1, _adapter.CountCalls(SubaccountPath));
    }

    [Fact]
    public void GettingDestinationProperties_SynchronousWrappersReturnTheSameResult()
    {
        _adapter
            .Respond(InstancePath, OnBehalfOf.TechnicalUserCurrentTenant, Responses.InstanceDestinations)
            .Respond(SubaccountPath, OnBehalfOf.TechnicalUserCurrentTenant, Responses.SubaccountDestinations);

        var all = _loader.GetAllDestinationProperties();
        Assert.Equal(4, all.Count);
        Assert.Equal("CC8-HTTP-CERT1", NameOf(_loader.GetDestinationProperties("CC8-HTTP-CERT1")));
        Assert.Equal(4, _loader.GetAllDestinationProperties(DestinationServiceRetrievalStrategy.OnlySubscriber).Count);
        Assert.True(_loader.TryGetDestination(DestinationName, NoOptions).IsSuccess);
    }

    [Theory]
    [InlineData(DestinationServiceRetrievalStrategy.AlwaysProvider, OnBehalfOf.TechnicalUserProvider)]
    [InlineData(DestinationServiceRetrievalStrategy.OnlySubscriber, OnBehalfOf.TechnicalUserCurrentTenant)]
    [InlineData(DestinationServiceRetrievalStrategy.CurrentTenant, OnBehalfOf.TechnicalUserCurrentTenant)]
    public async Task GettingDestinationProperties_UsesTheTenantOfTheStrategy(
        DestinationServiceRetrievalStrategy strategy,
        OnBehalfOf expectedBehalf)
    {
        _adapter
            .Respond(InstancePath, expectedBehalf, Responses.InstanceDestinations)
            .Respond(SubaccountPath, expectedBehalf, Responses.SubaccountDestinations);

        var destinations = await _loader.GetAllDestinationPropertiesAsync(strategy);

        Assert.Equal(["CC8-HTTP-BASIC", "CC8-HTTP-CERT1", "CC8-HTTP-CERT", DestinationName], destinations.Select(NameOf));
        Assert.All(_adapter.Calls, call => Assert.Equal(expectedBehalf, call.Strategy.Behalf));
    }

    [Fact]
    public async Task GettingAllDestinations_ForProvider_ReturnsGenericDestinations()
    {
        _adapter
            .Respond(InstancePath, OnBehalfOf.TechnicalUserProvider, Responses.InstanceDestinations)
            .Respond(SubaccountPath, OnBehalfOf.TechnicalUserProvider, Responses.SubaccountDestinations);
        var options = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider));

        var destinations = (await _loader.TryGetAllDestinationsAsync(options)).GetValueOrThrow();

        Assert.Equal(4, destinations.Count);
        Assert.Equal(
            ["CC8-HTTP-BASIC", "CC8-HTTP-CERT", "CC8-HTTP-CERT1", DestinationName],
            destinations.Select(NameOf).Order());
    }

    [Fact]
    public async Task GettingAllDestinations_OnlySubscriberOnProviderTenant_Fails()
    {
        var options = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.OnlySubscriber));
        CurrentTenant = _providerTenant;

        var result = await _loader.TryGetAllDestinationsAsync(options);

        var failure = Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Contains(
            "The current tenant is the provider tenant, which should not be the case with the option OnlySubscriber. Cannot retrieve destination.",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GettingAllDestinationProperties_OnlySubscriberOnProviderTenant_Throws()
    {
        CurrentTenant = _providerTenant;

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(
            () => _loader.GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy.OnlySubscriber));
        Assert.Contains("which should not be the case with the option OnlySubscriber", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DestinationRetrieval_AlwaysProvider_ReadsProviderDestination()
    {
        var options = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider));

        var destination = (await GetAsync(DestinationName, options)).AsHttp();

        Assert.Equal(new Uri(ProviderUrl), destination.Uri);
        // the destination belongs to the provider, which is expressed by an empty tenant id
        Assert.True(destination.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal(string.Empty, tenantId);
    }

    [Fact]
    public async Task DestinationRetrieval_OnlySubscriber_ReadsSubscriberDestination()
    {
        var options = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.OnlySubscriber));

        Assert.Equal(new Uri(SubscriberUrl), (await GetAsync(DestinationName, options)).AsHttp().Uri);
    }

    [Fact]
    public async Task DestinationRetrieval_OnlySubscriberOnProviderTenant_DoesNotReadProviderDestinations()
    {
        var options = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.OnlySubscriber));
        CurrentTenant = _providerTenant;

        var result = await TryGetAsync(DestinationName, options);

        Assert.IsType<DestinationAccessException>(result.Exception);
    }

    [Fact]
    public async Task DestinationRetrieval_WithoutOptions_ReadsSubscriberDestination() =>
        Assert.Equal(new Uri(SubscriberUrl), (await GetAsync(DestinationName)).AsHttp().Uri);

    [Fact]
    public void DestinationAugmentation_ByText_IsUnderstood()
    {
        var options = DestinationOptions.CreateBuilder()
            .Parameter(DestinationServiceOptionsAugmenter.DestinationRetrievalStrategyKey, "AlwaysProvider")
            .Parameter(DestinationServiceOptionsAugmenter.DestinationTokenExchangeStrategyKey, "exchangeonly")
            .Build();

        Assert.Equal(DestinationServiceRetrievalStrategy.AlwaysProvider, DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options));
        Assert.Equal(DestinationServiceTokenExchangeStrategy.ExchangeOnly, DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(options));
    }

    [Fact]
    public async Task TokenExchange_LookupThenExchange_PerformsTheExchangeWhenTheDestinationNeedsIt()
    {
        _adapter
            .Respond("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.TechnicalUserCurrentTenant, Responses.OAuthWithoutAuthToken)
            .Respond("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.NamedUserCurrentTenant, Responses.OAuthWithAuthToken);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupThenExchange));

        var destination = (await GetAsync("CC8-HTTP-OAUTH", options)).AsHttp();

        Assert.Equal([new Header("Authorization", "Bearer bearer_token")], destination.GetHeaders());
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/CC8-HTTP-OAUTH", DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant)));
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/CC8-HTTP-OAUTH", DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant)));
    }

    [Fact]
    public async Task TokenExchange_LookupOnly_FailsWhenTheDestinationNeedsAnExchange()
    {
        _adapter.Respond("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.TechnicalUserCurrentTenant, Responses.OAuthWithoutAuthToken);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly));

        var result = await TryGetAsync("CC8-HTTP-OAUTH", options);

        Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, Assert.Single(_adapter.Calls).Strategy.Behalf);
    }

    [Fact]
    public async Task TokenExchange_ExchangeOnly_UsesTheNamedUser()
    {
        _adapter.Respond("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.NamedUserCurrentTenant, Responses.OAuthWithAuthToken);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.ExchangeOnly));

        var destination = (await GetAsync("CC8-HTTP-OAUTH", options)).AsHttp();

        Assert.Equal([new Header("Authorization", "Bearer bearer_token")], destination.GetHeaders());
        Assert.Single(_adapter.Calls);
        Assert.Equal(DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant), _adapter.Calls[0].Strategy);
    }

    [Fact]
    public async Task EmailDestination_IsAGenericDestination()
    {
        const string name = "dummy-mail-destination";
        _adapter.Respond(
            "/v1/destinations/" + name,
            $$"""
            {
              "owner": { "SubaccountId": "a89ea924-d9c2-4eab-84fb-3ffcaadf5d24", "InstanceId": null },
              "destinationConfiguration": {
                "Name": "{{name}}",
                "Type": "MAIL",
                "Authentication": "NoAuthentication",
                "ProxyType": "OnPremise",
                "CloudConnectorLocationId": "bla",
                "mail.description": "delete me"
              }
            }
            """);
        var options = Options(
            Augmenter()
                .TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly)
                .RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider));

        var destination = await GetAsync(name, options);

        Assert.IsType<DefaultDestination>(destination);
        Assert.False(destination.IsHttp());
        Assert.Throws<ArgumentException>(() => destination.AsHttp());
        Assert.Equal(name, NameOf(destination));
        Assert.True(destination.TryGet(DestinationProperty.Type, out var type));
        Assert.Equal(DestinationType.Mail, type);
        Assert.True(destination.TryGet(DestinationProperty.AuthType, out var authType));
        Assert.Equal(AuthenticationType.NoAuthentication, authType);
        Assert.True(destination.TryGet(DestinationProperty.ProxyType, out var proxyType));
        Assert.Equal(ProxyType.OnPremise, proxyType);
        Assert.True(destination.TryGet(DestinationProperty.CloudConnectorLocationId, out var locationId));
        Assert.Equal("bla", locationId);
        Assert.Equal("delete me", destination.GetProperty("mail.description"));
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/" + name, OnBehalfOf.TechnicalUserProvider));
    }

    [Fact]
    public async Task Destinations_AreCachedBasedOnOptions()
    {
        var path = "/v1/destinations/" + DestinationName;
        var defaultOptions = NoOptions;
        var secondInstanceOfDefaultOptions = NoOptions;
        var withDefaultRetrievalStrategy = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.CurrentTenant));

        Assert.Equal(defaultOptions, secondInstanceOfDefaultOptions);

        await TryGetAsync(DestinationName);
        Assert.Equal(1, _adapter.CountCalls(path, WithUserToken()));

        await TryGetAsync(DestinationName, defaultOptions);
        Assert.Equal(1, _adapter.CountCalls(path, WithUserToken()));

        await TryGetAsync(DestinationName, withDefaultRetrievalStrategy);
        Assert.Equal(2, _adapter.CountCalls(path, WithUserToken()));

        await TryGetAsync(DestinationName, secondInstanceOfDefaultOptions);
        Assert.Equal(2, _adapter.CountCalls(path, WithUserToken()));
    }

    [Fact]
    public async Task Destinations_WithDifferentCustomParameters_AreNotShared()
    {
        // a parameter that the service does not know is part of the cache key: the documented workaround to bypass the cache
        var path = "/v1/destinations/" + DestinationName;

        await TryGetAsync(DestinationName, DestinationOptions.CreateBuilder().Parameter("timestamp", 1L).Build());
        Assert.Equal(1, _adapter.CountCalls(path, WithUserToken()));

        await TryGetAsync(DestinationName, DestinationOptions.CreateBuilder().Parameter("timestamp", 2L).Build());
        Assert.Equal(2, _adapter.CountCalls(path, WithUserToken()));
    }

    [Fact]
    public async Task CachingHttpDestination_LoadsItOnce()
    {
        for (var i = 0; i < 11; i++)
        {
            await GetAsync(DestinationName);
        }

        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/" + DestinationName, WithUserToken()));
        Assert.Equal(1, DestinationService.Cache.InstanceSingle().Count);
    }

    [Fact]
    public async Task UnknownDestination_LeadsToDestinationNotFoundException()
    {
        _adapter.Fail(
            (path, _) => path == "/v1/destinations/UnknownDestination",
            new DestinationNotFoundException(null, "Destination could not be found for path /destination-configuration/v1/destinations/UnknownDestination."));

        var result = await TryGetAsync("UnknownDestination");

        Assert.IsType<DestinationNotFoundException>(result.Exception);
    }

    [Theory]
    [InlineData("expired token", "CC8-HTTP-OAUTH", nameof(Responses.OAuthWithExpiredAuthToken), 2)]
    [InlineData("valid token", "CC8-HTTP-OAUTH", nameof(Responses.OAuthWithAuthToken), 1)]
    [InlineData("basic auth token has no expires_in", "CC8-HTTP-BASIC", nameof(Responses.BasicWithAuthToken), 1)]
    [InlineData("no auth", "Subscriber-CCT", nameof(Responses.NoAuth), 1)]
    [InlineData("one of two tokens expires soon", "CC8-HTTP-OAUTH", nameof(Responses.OAuthWithMultipleAuthTokens), 2)]
    public async Task Destination_IsFetchedAgainOnlyWhenAnAuthTokenExpires(string reason, string name, string responseName, int expectedFetches)
    {
        _ = reason;
        var response = (string)typeof(Responses).GetField(responseName)!.GetRawConstantValue()!;
        _adapter.Respond("/v1/destinations/" + name, OnBehalfOf.TechnicalUserCurrentTenant, response);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly));

        await TryGetAsync(name, options);
        await TryGetAsync(name, options);

        Assert.Equal(expectedFetches, _adapter.CountCalls("/v1/destinations/" + name, OnBehalfOf.TechnicalUserCurrentTenant));
    }

    [Fact]
    public async Task Destination_IsAlwaysFetchedWhenTheCacheIsDisabled()
    {
        DestinationService.Cache.Disable();
        _adapter.Respond("/v1/destinations/Subscriber-CCT", OnBehalfOf.TechnicalUserCurrentTenant, Responses.NoAuth);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly));

        await TryGetAsync("Subscriber-CCT", options);
        await TryGetAsync("Subscriber-CCT", options);

        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/Subscriber-CCT", OnBehalfOf.TechnicalUserCurrentTenant));
    }

    [Fact]
    public async Task LookupIsPerformedTwice_WhenTheAuthTokenExpiredAndTheDestinationRequiresAUserTokenExchange()
    {
        _adapter
            .Respond("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.TechnicalUserCurrentTenant, Responses.OAuthWithoutAuthToken)
            .Respond("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.NamedUserCurrentTenant, Responses.OAuthWithExpiredAuthToken);
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupThenExchange));

        await TryGetAsync("CC8-HTTP-OAUTH", options);
        await TryGetAsync("CC8-HTTP-OAUTH", options);

        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.TechnicalUserCurrentTenant));
        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/CC8-HTTP-OAUTH", OnBehalfOf.NamedUserCurrentTenant));
    }

    [Fact]
    public async Task ConcurrentFetchOfTheSameDestination_CallsTheDestinationServiceOnce()
    {
        CurrentTenant = _providerTenant;
        const string name = "CC8-HTTP-OAUTH";
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _adapter.Respond(
            "/v1/destinations/" + name,
            async cancellationToken =>
            {
                await release.Task.WaitAsync(cancellationToken);
                return Responses.BasicWithAuthToken;
            },
            OnBehalfOf.TechnicalUserCurrentTenant);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _loader.TryGetDestinationAsync(name, options)))
            .ToList();
        await WaitUntilAsync(() => _adapter.Calls.Count == 1);
        await Task.Delay(200); // give the other callers time to queue up behind the first one
        release.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Exception?.ToString()));
        Assert.Single(results.Select(r => r.GetValueOrThrow()).Distinct());
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/" + name, OnBehalfOf.TechnicalUserCurrentTenant));
        AssertKeys([SingleKey("provider-tenant", name, options)], DestinationService.Cache.InstanceSingle().Keys);
    }

    [Fact]
    public async Task ConcurrentFetchOfTheSameDestination_ForDifferentTenants_DoesNotBlock()
    {
        const string name = "CC8-HTTP-OAUTH";
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupOnly));
        var inFlight = 0;
        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _adapter.Respond(
            "/v1/destinations/" + name,
            async cancellationToken =>
            {
                if (Interlocked.Increment(ref inFlight) == 2)
                {
                    bothArrived.SetResult();
                }
                // would never complete if the second request waited for the first one
                await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                return Responses.BasicWithAuthToken;
            },
            OnBehalfOf.TechnicalUserCurrentTenant);

        var first = TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("TenantA"), () => _loader.TryGetDestinationAsync(name, options));
        var second = TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("TenantB"), () => _loader.TryGetDestinationAsync(name, options));
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Exception?.ToString()));
        // locks per tenant, for the single and the all-destinations command
        AssertKeys(
            [
                SingleKey("TenantA", name, options),
                AllKey("TenantA", options),
                SingleKey("TenantB", name, options),
                AllKey("TenantB", options),
            ],
            DestinationService.Cache.GetIsolationLocks().Keys);
        // one cache entry per tenant
        AssertKeys([SingleKey("TenantA", name, options), SingleKey("TenantB", name, options)], DestinationService.Cache.InstanceSingle().Keys);
        Assert.Equal(0, DestinationService.Cache.InstanceAll().Count);
        Assert.Equal(2, _adapter.Calls.Count);
    }

    [Fact]
    public async Task DestinationsWithoutUserPropagation_AreSharedBetweenPrincipals()
    {
        _adapter.Respond("/v1/destinations/" + DestinationName, WithUserToken(), Responses.BasicWithAuthToken);
        CurrentTenant = _providerTenant;
        CurrentPrincipal = _principal1;

        var first = await GetAsync(DestinationName);
        CurrentPrincipal = _principal2;
        var second = await GetAsync(DestinationName);

        Assert.Same(first, second);
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/" + DestinationName, WithUserToken()));
    }

    [Fact]
    public async Task DestinationsWithUserPropagation_AreIsolatedByPrincipal_WithExchangeOnly()
    {
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.ExchangeOnly));
        _adapter.Respond("/v1/destinations/" + DestinationName, OnBehalfOf.NamedUserCurrentTenant, Responses.OAuthWithAuthToken);

        var first = await GetAsync(DestinationName, options);
        CurrentPrincipal = _principal2;
        var second = await GetAsync(DestinationName, options);

        Assert.NotSame(first, second);
        AssertKeys(
            [
                SingleKey("subscriber-tenant", DestinationName, options, "principal-1"),
                SingleKey("subscriber-tenant", DestinationName, options, "principal-2"),
                AllKey("subscriber-tenant", options),
            ],
            DestinationService.Cache.GetIsolationLocks().Keys);
        AssertKeys(
            [
                SingleKey("subscriber-tenant", DestinationName, options, "principal-1"),
                SingleKey("subscriber-tenant", DestinationName, options, "principal-2"),
            ],
            DestinationService.Cache.InstanceSingle().Keys);
        Assert.Equal(0, DestinationService.Cache.InstanceAll().Count);
        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/" + DestinationName, OnBehalfOf.NamedUserCurrentTenant));
        // there are no 'get-all' requests
        Assert.Equal(2, _adapter.Calls.Count);
    }

    [Fact]
    public async Task DestinationsWithUserPropagation_AreIsolatedByPrincipal_WithLookupThenExchange()
    {
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupThenExchange));
        _adapter
            .Respond("/v1/destinations/" + DestinationName, OnBehalfOf.TechnicalUserCurrentTenant, Responses.OAuthWithoutAuthToken)
            .Respond("/v1/destinations/" + DestinationName, OnBehalfOf.NamedUserCurrentTenant, Responses.OAuthWithAuthToken);

        var first = await GetAsync(DestinationName, options);
        CurrentPrincipal = _principal2;
        var second = await GetAsync(DestinationName, options);

        Assert.NotSame(first, second);
        AssertKeys(
            [SingleKey("subscriber-tenant", DestinationName, options), AllKey("subscriber-tenant", options)],
            DestinationService.Cache.GetIsolationLocks().Keys);
        AssertKeys(
            [
                SingleKey("subscriber-tenant", DestinationName, options, "principal-1"),
                SingleKey("subscriber-tenant", DestinationName, options, "principal-2"),
            ],
            DestinationService.Cache.InstanceSingle().Keys);
        Assert.Equal(0, DestinationService.Cache.InstanceAll().Count);
        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/" + DestinationName, OnBehalfOf.TechnicalUserCurrentTenant));
        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/" + DestinationName, OnBehalfOf.NamedUserCurrentTenant));
        Assert.Equal(4, _adapter.Calls.Count);
    }

    [Fact]
    public async Task SmartCache_ServesAllPrincipalsWithTheSameDestination()
    {
        _adapter
            .Respond(InstancePath, "[]")
            .Respond(SubaccountPath, Responses.SubaccountDestinations)
            .Respond("/v1/destinations/CC8-HTTP-BASIC", Responses.BasicWithAuthToken);
        IDestination? first = null;

        for (var i = 0; i < 10; i++)
        {
            CurrentPrincipal = new DefaultPrincipal("principal-" + i);
            var destination = await GetAsync("CC8-HTTP-BASIC");
            first ??= destination;
            Assert.Same(first, destination);
        }

        Assert.Equal(1, _adapter.CountCalls(InstancePath, OnBehalfOf.TechnicalUserCurrentTenant));
        Assert.Equal(1, _adapter.CountCalls(SubaccountPath, OnBehalfOf.TechnicalUserCurrentTenant));
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/CC8-HTTP-BASIC", WithUserToken()));
    }

    [Fact]
    public async Task ChangeDetection_WithMisconfiguredDestination_OpensTheCircuitBreakerAndKeepsTheCachedDestination()
    {
        _adapter
            .Respond(InstancePath, "[]")
            .Respond(SubaccountPath, Responses.BrokenSubaccountDestinations)
            .Respond("/v1/destinations/CC8-HTTP-BASIC", Responses.BasicWithAuthToken);
        // closed buffer size * failure rate threshold: the number of attempts after which the failure rate is reached
        var circuitBreakerBuffer = (int)(ResilienceConfiguration.CircuitBreakerConfiguration.DefaultClosedBufferSize
            * Math.Round(ResilienceConfiguration.CircuitBreakerConfiguration.DefaultFailureRateThreshold) / 100);

        // the smart cache circuit breaker will try 5 times, then open and not call again
        for (var i = 0; i < 2 * circuitBreakerBuffer; i++)
        {
            Assert.True((await TryGetAsync("CC8-HTTP-BASIC")).IsSuccess);
        }

        Assert.Equal(circuitBreakerBuffer, _adapter.CountCalls(InstancePath, OnBehalfOf.TechnicalUserCurrentTenant));
        Assert.Equal(circuitBreakerBuffer, _adapter.CountCalls(SubaccountPath, OnBehalfOf.TechnicalUserCurrentTenant));
        // the requested destination is unaffected
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/CC8-HTTP-BASIC", WithUserToken()));
        Assert.Equal(2 * circuitBreakerBuffer + 1, _adapter.Calls.Count);
    }

    [Fact]
    public async Task ChangeDetectionDisabled_DoesNotGetAllDestinations()
    {
        DestinationService.Cache.DisableChangeDetection();

        await GetAsync(DestinationName);

        AssertKeys([SingleKey("subscriber-tenant", DestinationName, NoOptions)], DestinationService.Cache.GetIsolationLocks().Keys);
        AssertKeys([SingleKey("subscriber-tenant", DestinationName, NoOptions)], DestinationService.Cache.InstanceSingle().Keys);
        Assert.Equal(0, DestinationService.Cache.InstanceAll().Count);
        Assert.Single(_adapter.Calls);
    }

    [Fact]
    public async Task ConcurrentFetch_ForSameTenantButDifferentPrincipals_IsSerialized()
    {
        const string name = "CC8-HTTP-OAUTH";
        var options = Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.LookupThenExchange));
        _adapter.Respond("/v1/destinations/" + name, OnBehalfOf.TechnicalUserCurrentTenant, Responses.OAuthWithoutAuthToken);
        var concurrent = 0;
        var maxConcurrent = 0;
        _adapter.Respond(
            "/v1/destinations/" + name,
            async cancellationToken =>
            {
                var now = Interlocked.Increment(ref concurrent);
                InterlockedMax(ref maxConcurrent, now);
                await Task.Delay(200, cancellationToken);
                Interlocked.Decrement(ref concurrent);
                return Responses.OAuthWithAuthToken;
            },
            OnBehalfOf.NamedUserCurrentTenant);

        var first = PrincipalAccessor.ExecuteWithPrincipalAsync(_principal1, () => _loader.TryGetDestinationAsync(name, options));
        var second = PrincipalAccessor.ExecuteWithPrincipalAsync(_principal2, () => _loader.TryGetDestinationAsync(name, options));
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Exception?.ToString()));
        Assert.Equal(1, maxConcurrent);
        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/" + name, OnBehalfOf.TechnicalUserCurrentTenant));
        Assert.Equal(2, _adapter.CountCalls("/v1/destinations/" + name, OnBehalfOf.NamedUserCurrentTenant));
        Assert.Equal(4, _adapter.Calls.Count);
        AssertKeys(
            [SingleKey("subscriber-tenant", name, options, "principal-1"), SingleKey("subscriber-tenant", name, options, "principal-2")],
            DestinationService.Cache.InstanceSingle().Keys);
        Assert.Equal(0, DestinationService.Cache.InstanceAll().Count);
    }

    [Fact]
    public async Task InvalidJson_LeadsToDestinationAccessException()
    {
        _adapter.Respond("/v1/destinations/BadDestination", "{ \"destinationConfiguration\" : { \"Name ... } }");

        var result = await TryGetAsync("BadDestination");

        Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Contains(Chain(result.Exception), e => e is System.Text.Json.JsonException);
    }

    [Fact]
    public async Task CircuitBreaker_ReportsTheCauseOfTheFailure_NotTheBreaker()
    {
        DestinationService.Cache.Disable();
        _adapter.Respond("/v1/destinations/BadDestination", "{ \"destinationConfiguration\" : { \"Name ... } }");

        for (var i = 0; i < 10; i++)
        {
            await TryGetAsync("BadDestination");
        }
        var result = await TryGetAsync("BadDestination");

        Assert.DoesNotContain(Chain(result.Exception!), e => e is Polly.CircuitBreaker.BrokenCircuitException);
        Assert.Contains(Chain(result.Exception!), e => e is System.Text.Json.JsonException);
        // the circuit is open: the last attempts have not reached the destination service
        Assert.True(_adapter.Calls.Count < 11);
    }

    [Fact]
    public async Task GettingAllDestinations_HandlesFailure()
    {
        _adapter.Fail((path, _) => path == InstancePath, new DestinationAccessException("Error"));

        await Assert.ThrowsAsync<DestinationAccessException>(() => _loader.GetAllDestinationPropertiesAsync());
    }

    [Fact]
    public async Task AuthTokenFailure_IsNotCached()
    {
        _adapter.Respond("/v1/destinations/CC8-HTTP-OAUTH", WithUserToken(), Responses.OAuthWithoutAuthToken);

        var first = await TryGetAsync("CC8-HTTP-OAUTH");
        await TryGetAsync("CC8-HTTP-OAUTH");
        await TryGetAsync("CC8-HTTP-OAUTH");

        Assert.IsType<DestinationAccessException>(first.Exception);
        Assert.Contains("Failed to read authentication token", first.Exception.Message, StringComparison.Ordinal);
        // the failure is not cached
        Assert.Equal(3, _adapter.CountCalls("/v1/destinations/CC8-HTTP-OAUTH", WithUserToken()));
    }

    [Fact]
    public async Task CrossLevelScope_UsesTheV2ApiAndIsCachedPerScope()
    {
        const string name = "CrossLevelDestination";
        // the /v2 API does NOT respond with the "owner" attribute
        string Response(string level) => $$"""
            { "destinationConfiguration": { "Name": "{{name}}", "Type": "HTTP", "URL": "https://foo.com", "Description": "{{level}} level destination" } }
            """;
        _adapter
            .Respond($"/v2/destinations/{name}@subaccount", Response("subaccount"))
            .Respond($"/v2/destinations/{name}@instance", Response("instance"));
        var subaccount = Options(Augmenter().CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.Subaccount));
        var instance = Options(Augmenter().CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.Instance));

        var destinationSubaccount = await GetAsync(name, subaccount);
        var destinationInstance = await GetAsync(name, instance);

        Assert.Equal(name, NameOf(destinationSubaccount));
        Assert.Equal("subaccount level destination", destinationSubaccount.GetProperty("Description"));
        Assert.Same(destinationSubaccount, await GetAsync(name, subaccount));

        Assert.NotEqual(destinationSubaccount, destinationInstance);
        Assert.Equal("instance level destination", destinationInstance.GetProperty("Description"));
        Assert.Same(destinationInstance, await GetAsync(name, instance));

        Assert.Equal(1, _adapter.CountCalls($"/v2/destinations/{name}@subaccount"));
        Assert.Equal(1, _adapter.CountCalls($"/v2/destinations/{name}@instance"));
    }

    [Fact]
    public async Task FragmentDestinations_AreCacheIsolated()
    {
        DestinationService.Cache.DisableChangeDetection();
        static string Template(string fragmentLine, string host) => $$"""
            {
                "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
                "destinationConfiguration": {
                    "Name": "destination",
                    {{fragmentLine}}
                    "Type": "HTTP",
                    "URL": "https://{{host}}.com/",
                    "Authentication": "NoAuthentication",
                    "ProxyType": "Internet"
                }
            }
            """;
        _adapter
            .Respond((_, s) => s.Fragment == "a-fragment", Template("\"FragmentName\": \"a-fragment\",", "a.fragment"))
            .Respond((_, s) => s.Fragment == "b-fragment", Template("\"FragmentName\": \"b-fragment\",", "b.fragment"))
            .Respond((_, s) => s.Fragment is null, Template(string.Empty, "destination"));
        static DestinationOptions Fragment(string name) => Options(Augmenter().FragmentName(name));

        var a = await GetAsync("destination", Fragment("a-fragment"));
        var b = await GetAsync("destination", Fragment("b-fragment"));
        var plain = await GetAsync("destination");

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, plain);
        Assert.Equal("a-fragment", a.GetProperty("FragmentName"));
        Assert.Equal("b-fragment", b.GetProperty("FragmentName"));
        Assert.Null(plain.GetProperty("FragmentName"));
        Assert.Same(a, await GetAsync("destination", Fragment("a-fragment")));
        Assert.Equal(3, _adapter.Calls.Count);
    }

    [Fact]
    public async Task CustomHeaders_AreForwardedToTheDestinationService()
    {
        var h1 = new Header("X-Custom-Header-1", "value-1");
        var h2 = new Header("X-Custom-Header-2", "value-2");

        await GetAsync(DestinationName, Options(Augmenter().CustomHeaders(h1, h2)));

        var call = Assert.Single(_adapter.Calls);
        Assert.Contains(h1, call.Strategy.AdditionalHeaders);
        Assert.Contains(h2, call.Strategy.AdditionalHeaders);
    }

    [Fact]
    public async Task RefreshToken_IsForwardedAndPartOfTheCacheKey()
    {
        _adapter.Respond("/v1/destinations/" + DestinationName, s => s.TokenForwarding == TokenForwarding.RefreshToken, Responses.Http(DestinationName, "https://refresh.example.com"));

        var first = await GetAsync(DestinationName, Options(Augmenter().RefreshToken("refresh-1")));
        var second = await GetAsync(DestinationName, Options(Augmenter().RefreshToken("refresh-2")));
        var again = await GetAsync(DestinationName, Options(Augmenter().RefreshToken("refresh-1")));

        Assert.NotSame(first, second);
        Assert.Same(first, again);
        Assert.Equal(
            [DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.TechnicalUserCurrentTenant, "refresh-1"), DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.TechnicalUserCurrentTenant, "refresh-2")],
            _adapter.Calls.Where(c => c.Path == "/v1/destinations/" + DestinationName).Select(c => c.Strategy));
    }

    [Fact]
    public async Task TimeLimiter_CancelsSlowRequests()
    {
        var slow = new FakeAdapter();
        slow.Respond(
            (_, _) => true,
            async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "{}";
            });
        var service = new DestinationService(
            slow,
            DestinationService.CreateResilienceConfiguration(
                "singleDestResilience" + Guid.NewGuid(),
                ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromMilliseconds(300)),
                DestinationService.DefaultSingleDestCircuitBreaker),
            DestinationService.CreateResilienceConfiguration(
                "allDestResilience" + Guid.NewGuid(),
                ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromMilliseconds(300)),
                DestinationService.DefaultAllDestCircuitBreaker));

        var result = await service.TryGetDestinationAsync(DestinationName, NoOptions);

        Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Contains(Chain(result.Exception), e => e is Polly.Timeout.TimeoutRejectedException);
        Assert.Single(slow.Calls);
    }

    [Fact]
    public async Task CallerCancellation_IsNotWrapped()
    {
        using var cancellation = new CancellationTokenSource();
        var slow = new FakeAdapter();
        slow.Respond(
            (_, _) => true,
            async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "{}";
            });
        var service = CreateService(slow);

        var pending = service.TryGetDestinationAsync(DestinationName, NoOptions, cancellation.Token);
        await WaitUntilAsync(() => slow.Calls.Count == 1);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public void Builder_UsesTheDefaultTimeLimiterAndCanDisableIt()
    {
        var defaults = DestinationService.CreateBuilder().WithProviderTenant(_providerTenant).Build();
        Assert.True(defaults.SingleDestResilience.TimeLimiter.IsEnabled);
        Assert.True(defaults.AllDestResilience.TimeLimiter.IsEnabled);
        Assert.Equal(DestinationService.DefaultTimeLimiter, defaults.SingleDestResilience.TimeLimiter);
        Assert.Equal(DestinationService.DefaultTimeLimiter, defaults.AllDestResilience.TimeLimiter);
        Assert.Equal(ResilienceIsolationMode.TenantOptional, defaults.SingleDestResilience.IsolationMode);

        var disabled = DestinationService.CreateBuilder()
            .WithTimeLimiterConfiguration(ResilienceConfiguration.TimeLimiterConfiguration.Disabled())
            .WithProviderTenant(_providerTenant)
            .Build();
        Assert.False(disabled.SingleDestResilience.TimeLimiter.IsEnabled);
        Assert.False(disabled.AllDestResilience.TimeLimiter.IsEnabled);

        var custom = DestinationService.CreateBuilder()
            .WithTimeLimiterConfiguration(ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(100)))
            .WithProviderTenant(new DefaultTenant("Foo"))
            .Build();
        Assert.Equal(TimeSpan.FromSeconds(100), custom.SingleDestResilience.TimeLimiter.TimeoutDuration);
        Assert.Equal(TimeSpan.FromSeconds(100), custom.AllDestResilience.TimeLimiter.TimeoutDuration);
    }

    [Fact]
    public async Task TenantAndPrincipal_FlowAcrossAwaitAndThreadPool()
    {
        _adapter.Respond("/v1/destinations/" + DestinationName, WithUserToken(), Responses.Http(DestinationName, "https://flow.example.com"));
        CurrentTenant = null;

        var result = await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("flow-tenant"), async () =>
        {
            await Task.Yield();
            return await Task.Run(() => _loader.TryGetDestinationAsync(DestinationName, NoOptions));
        });

        Assert.True(result.TryGetValue(out var destination));
        Assert.True(destination.TryGet(DestinationProperty.TenantId, out var tenantId));
        Assert.Equal("flow-tenant", tenantId);
    }

    [Fact]
    public async Task PrependedGetAll_FindsTheDestinationBeforeFetchingIt()
    {
        DestinationService.Cache.Reset(); // re-enables the pre-lookup check
        _adapter
            .Respond(InstancePath, Responses.InstanceDestinations)
            .Respond(SubaccountPath, Responses.SubaccountDestinations);

        await GetAsync(DestinationName);

        Assert.Equal(1, _adapter.CountCalls(InstancePath));
        Assert.Equal(1, _adapter.CountCalls(SubaccountPath));
        Assert.Equal(1, _adapter.CountCalls("/v1/destinations/" + DestinationName));
        Assert.Equal(3, _adapter.Calls.Count);
    }

    [Fact]
    public async Task PrependedGetAll_WithMissingDestination_FailsWithoutFetching()
    {
        DestinationService.Cache.Reset();
        _adapter
            .Respond(InstancePath, Responses.InstanceDestinations)
            .Respond(SubaccountPath, Responses.SubaccountDestinations);

        var result = await TryGetAsync("thisDestinationDoesNotExist");

        var failure = Assert.IsType<DestinationNotFoundException>(result.Exception);
        Assert.Contains("was not found among the destinations for CurrentTenant.", failure.Message, StringComparison.Ordinal);
        Assert.Equal(2, _adapter.Calls.Count);
    }

    [Fact]
    public async Task PrependedGetAll_UsesTheRetrievalStrategy()
    {
        DestinationService.Cache.Reset();
        _adapter
            .Respond(InstancePath, Responses.InstanceDestinations)
            .Respond(SubaccountPath, Responses.InstanceDestinations)
            // the destination is provider-only
            .Respond(SubaccountPath, OnBehalfOf.TechnicalUserProvider, Responses.SubaccountDestinations);
        CurrentTenant = _providerTenant;
        var options = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider));

        var destination = (await GetAsync(DestinationName, options)).AsHttp();

        Assert.Equal(new Uri(ProviderUrl), destination.Uri);
    }

    public static TheoryData<string> OptionsThatSkipThePreLookupCheck() => new()
    {
        "header",
        "cross-level",
        "fragment",
    };

    [Theory]
    [MemberData(nameof(OptionsThatSkipThePreLookupCheck))]
    public async Task PrependedGetAll_IsSkipped_ForExperimentalOptions(string kind)
    {
        DestinationService.Cache.Reset();
        var (options, path) = kind switch
        {
            "header" => (Options(Augmenter().CustomHeaders(new Header("X-Custom-Header-1", "value-1"))), "/v1/destinations/" + DestinationName),
            "cross-level" => (Options(Augmenter().CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.Subaccount)), "/v2/destinations/" + DestinationName + "@subaccount"),
            _ => (Options(Augmenter().FragmentName("a-fragment")), "/v1/destinations/" + DestinationName),
        };
        _adapter.Respond(path, Responses.Http(DestinationName, "https://foo.com"));

        await GetAsync(DestinationName, options);

        Assert.Equal(0, _adapter.CountCalls(InstancePath));
        Assert.Equal(0, _adapter.CountCalls(SubaccountPath));
        Assert.Equal(1, _adapter.CountCalls(path));
        Assert.Single(_adapter.Calls);
    }

    [Fact]
    public async Task ValidateDestinationLookup_CoversEveryCase()
    {
        var emptyOptions = NoOptions;
        var experimental = Options(Augmenter().CrossLevelConsumption(DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderSubaccount));
        var current = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.CurrentTenant));
        var provider = Options(Augmenter().RetrievalStrategy(DestinationServiceRetrievalStrategy.AlwaysProvider));
        CurrentTenant = _providerTenant;
        var fake = new FakeAdapter { ProviderTenant = "provider-tenant" }
            .Respond(InstancePath, OnBehalfOf.TechnicalUserCurrentTenant, """[{"Name":"current-dest-1","URL":"http://current-tenant-1"}]""")
            .Respond(SubaccountPath, OnBehalfOf.TechnicalUserCurrentTenant, "[]")
            .Respond(InstancePath, OnBehalfOf.TechnicalUserProvider, """[{"Name":"provider-dest-1","URL":"http://provider-tenant-1"}]""")
            .Respond(SubaccountPath, OnBehalfOf.TechnicalUserProvider, "[]");
        var sut = new DestinationService(fake);

        // valid case: disabled cache
        DestinationService.Cache.Disable();
        Assert.Null(await sut.ValidateDestinationLookupAsync("unknown-dest-1", emptyOptions, default));

        // valid case: disabled pre-lookup check
        DestinationService.Cache.Reset();
        DestinationService.Cache.DisablePreLookupCheck();
        Assert.Null(await sut.ValidateDestinationLookupAsync("unknown-dest-1", emptyOptions, default));

        // valid case: experimental properties
        DestinationService.Cache.Reset();
        Assert.Null(await sut.ValidateDestinationLookupAsync("unknown-dest-1", experimental, default));

        // valid case: current tenant
        DestinationService.Cache.Reset();
        Assert.Null(await sut.ValidateDestinationLookupAsync("current-dest-1", current, default));

        // valid case: provider tenant
        DestinationService.Cache.Reset();
        Assert.Null(await sut.ValidateDestinationLookupAsync("provider-dest-1", provider, default));

        // invalid cases
        DestinationService.Cache.Reset();
        Assert.Equal(
            "Destination unknown-dest-1 was not found among the destinations for AlwaysProvider.",
            (await sut.ValidateDestinationLookupAsync("unknown-dest-1", provider, default))!.Message);
        DestinationService.Cache.Reset();
        Assert.Equal(
            "Destination unknown-dest-1 was not found among the destinations for CurrentTenant.",
            (await sut.ValidateDestinationLookupAsync("unknown-dest-1", current, default))!.Message);
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            yield return e;
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
        }
        while (value > current && Interlocked.CompareExchange(ref target, value, current) != current);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for a condition.");
            await Task.Delay(10);
        }
    }
}
