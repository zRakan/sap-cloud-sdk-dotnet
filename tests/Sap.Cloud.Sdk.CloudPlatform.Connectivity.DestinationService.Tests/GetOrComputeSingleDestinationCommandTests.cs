using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>
/// The cache keys, isolation keys and failure modes of the single destination command for every combination of
/// authentication type, token exchange strategy, tenant and principal (port of
/// <c>GetOrComputeSingleDestinationCommandWithoutAllDestinationsTest</c>).
/// </summary>
public sealed class GetOrComputeSingleDestinationCommandTests : DestinationServiceTestBase
{
    private static readonly DefaultTenant Tenant = new("tenant-1");
    private static readonly DefaultPrincipal Principal = new("P1");

    private readonly ExpiringCache<IDestination> _destinationCache = new(null, null, false);
    private readonly IsolationLocks _isolationLocks = new();

    private enum Outcome
    {
        Success,
        CreationFails,
        ExecutionFails,
    }

    private sealed record Expectation(Outcome Outcome, (string? Tenant, string? Principal)? IsolationKey, (string? Tenant, string? Principal)? CacheKey);

    /// <summary>The decision table, row by row.</summary>
    private static Expectation Expected(
        bool requiresExchange,
        DestinationServiceTokenExchangeStrategy strategy,
        bool hasTenant,
        bool hasPrincipal)
    {
        var tenant = hasTenant ? Tenant.TenantId : null;
        var principal = hasPrincipal ? Principal.PrincipalId : null;
        return (strategy, requiresExchange) switch
        {
            // ExchangeOnly: isolated by tenant and principal, both required
            (DestinationServiceTokenExchangeStrategy.ExchangeOnly, _) => hasTenant && hasPrincipal
                ? new Expectation(Outcome.Success, (tenant, principal), (tenant, principal))
                : new Expectation(Outcome.CreationFails, null, null),

            // no user token exchange needed: shared by all principals of the tenant
            (_, false) => new Expectation(Outcome.Success, (tenant, null), (tenant, null)),

            // LookupOnly cannot produce a usable destination for user propagation
            (DestinationServiceTokenExchangeStrategy.LookupOnly, true) => new Expectation(Outcome.ExecutionFails, (tenant, null), null),

            // ForwardUserToken and LookupThenExchange: cached per principal, which must exist
            (_, true) => hasPrincipal
                ? new Expectation(Outcome.Success, (tenant, null), (tenant, principal))
                : new Expectation(Outcome.ExecutionFails, (tenant, null), null),
        };
    }

    public static TheoryData<AuthenticationType, DestinationServiceTokenExchangeStrategy, bool, bool> AllCombinations()
    {
        TheoryData<AuthenticationType, DestinationServiceTokenExchangeStrategy, bool, bool> data = [];
        foreach (var authType in Enum.GetValues<AuthenticationType>())
        {
            foreach (var strategy in Enum.GetValues<DestinationServiceTokenExchangeStrategy>())
            {
                foreach (var tenant in new[] { false, true })
                {
                    foreach (var principal in new[] { false, true })
                    {
                        data.Add(authType, strategy, tenant, principal);
                    }
                }
            }
        }
        return data;
    }

    [Fact]
    public void TheDecisionTable_CoversEveryCombination() =>
        Assert.Equal(
            Enum.GetValues<AuthenticationType>().Length * Enum.GetValues<DestinationServiceTokenExchangeStrategy>().Length * 4,
            AllCombinations().Count);

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public async Task IsolationAndCaching_FollowTheDecisionTable(
        AuthenticationType authType,
        DestinationServiceTokenExchangeStrategy strategy,
        bool hasTenant,
        bool hasPrincipal)
    {
        var requiresExchange = DestinationUtility.RequiresUserTokenExchange(authType, null);
        var expectation = Expected(requiresExchange, strategy, hasTenant, hasPrincipal);
        CurrentTenant = hasTenant ? Tenant : null;
        CurrentPrincipal = hasPrincipal ? Principal : null;

        var name = Guid.NewGuid().ToString();
        var options = Options(Augmenter().TokenExchangeStrategy(strategy));
        var tokenFails = requiresExchange && (!hasPrincipal || strategy == DestinationServiceTokenExchangeStrategy.LookupOnly);
        var adapter = new FakeAdapter().Respond(
            "/v1/destinations/" + name,
            Responses.Of(
                new Dictionary<string, string>
                {
                    ["Authentication"] = authType.GetIdentifier(),
                    ["Name"] = name,
                    ["Type"] = "HTTP",
                    ["URL"] = "https://example.com",
                },
                tokenFails
                    ? new { error = "This is an error in the destination auth token" }
                    : new { value = "success", http_header = new { key = "destination-auth-token", value = "success" }, expires_in = "43200" }));
        var service = CreateService(adapter);

        var command = GetOrComputeSingleDestinationCommand.PrepareCommand(
            name,
            options,
            _destinationCache,
            _isolationLocks,
            service.LoadAndParseDestinationAsync,
            null);

        if (expectation.Outcome == Outcome.CreationFails)
        {
            Assert.IsType<DestinationAccessException>(command.Exception);
            Assert.Empty(_isolationLocks.Keys);
            return;
        }
        var result = await command.GetValueOrThrow().ExecuteAsync(default);

        Assert.Equal(expectation.Outcome == Outcome.Success, result.IsSuccess);
        var expectedIsolation = CacheKey.FromIds(expectation.IsolationKey!.Value.Tenant, expectation.IsolationKey.Value.Principal).Append(name, options);
        Assert.Equal([expectedIsolation], _isolationLocks.Keys);
        Assert.Equal(
            expectation.CacheKey is { } key ? [CacheKey.FromIds(key.Tenant, key.Principal).Append(name, options)] : [],
            _destinationCache.Keys);
        if (result.IsSuccess)
        {
            // sanity check: the destination service was asked once, or twice for an exchange after a lookup
            var lookupAndExchange = strategy == DestinationServiceTokenExchangeStrategy.LookupThenExchange && requiresExchange;
            Assert.Equal(lookupAndExchange ? 2 : 1, adapter.Calls.Count);
        }
    }
}

/// <summary>Port of <c>GetOrComputeDestinationCommandTest</c> and the tests of the command's change detection.</summary>
public sealed class GetOrComputeDestinationCommandTests : DestinationServiceTestBase
{
    private const string DestinationName = "SomeDestinationName";
    private static readonly DefaultTenant T1 = new("tenant-1");
    private static readonly DefaultPrincipal P1 = new("principal-1");

    private readonly ExpiringCache<IDestination> _destinationCache = new(null, null, false);
    private readonly ExpiringCache<IReadOnlyList<IDestinationProperties>> _allCache = new(null, null, false);
    private readonly IsolationLocks _isolationLocks = new();

    private static DefaultHttpDestination HttpDestination(string uri = "https://foo", AuthenticationType? authType = null)
    {
        var builder = DefaultHttpDestination.CreateBuilder(uri).Name(DestinationName);
        if (authType is { } type)
        {
            builder.Property(DestinationProperty.AuthType, type);
        }
        return builder.Build();
    }

    private static Func<string, DestinationOptions, CancellationToken, Task<IDestination>> Returning(IDestination destination) =>
        (_, _, _) => Task.FromResult(destination);

    private static DestinationOptions OptionsWith(DestinationServiceTokenExchangeStrategy strategy) =>
        Options(Augmenter().TokenExchangeStrategy(strategy));

    private GetOrComputeSingleDestinationCommand Prepare(
        DestinationOptions options,
        Func<string, DestinationOptions, CancellationToken, Task<IDestination>> retriever,
        GetOrComputeAllDestinationsCommand? getAll = null) =>
        GetOrComputeSingleDestinationCommand
            .PrepareCommand(DestinationName, options, _destinationCache, _isolationLocks, retriever, getAll)
            .GetValueOrThrow();

    private GetOrComputeAllDestinationsCommand AllCommand(Func<IReadOnlyList<IDestinationProperties>> destinations, Action? onCall = null) =>
        GetOrComputeAllDestinationsCommand.PrepareCommand(
            NoOptions,
            _allCache,
            _isolationLocks,
            (_, _) =>
            {
                onCall?.Invoke();
                return Task.FromResult(Result.Success(destinations()));
            });

    [Fact]
    public void DefaultTokenExchangeStrategy_IsLookupThenExchange()
    {
        Assert.Null(DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(NoOptions));

        var sut = Prepare(NoOptions, (_, _, _) => Task.FromResult<IDestination>(null!));

        Assert.Equal(DestinationServiceTokenExchangeStrategy.LookupThenExchange, sut.ExchangeStrategy);
    }

    [Theory]
    [InlineData(DestinationServiceTokenExchangeStrategy.LookupOnly, false, false)]
    [InlineData(DestinationServiceTokenExchangeStrategy.LookupThenExchange, false, true)]
    [InlineData(DestinationServiceTokenExchangeStrategy.ForwardUserToken, false, true)]
    [InlineData(DestinationServiceTokenExchangeStrategy.ExchangeOnly, true, false)]
    public void PrepareCommand_DeterminesTheCacheKeysByTheExchangeStrategy(
        DestinationServiceTokenExchangeStrategy strategy,
        bool keyHasPrincipal,
        bool hasAdditionalKey)
    {
        CurrentTenant = T1;
        CurrentPrincipal = P1;
        var options = OptionsWith(strategy);

        var sut = Prepare(options, (_, _, _) => throw new InvalidOperationException("The function is not expected to be executed in this test."));

        var expected = CacheKey.FromIds("tenant-1", keyHasPrincipal ? "principal-1" : null).Append(DestinationName, options);
        Assert.Equal(expected, sut.CacheKey);
        Assert.Equal(
            hasAdditionalKey ? CacheKey.FromIds("tenant-1", "principal-1").Append(DestinationName, options) : null,
            sut.AdditionalKeyWithTenantAndPrincipal);
        Assert.NotNull(_isolationLocks.GetIfPresent(expected));
    }

    [Theory]
    [InlineData(AuthenticationType.OAuth2JwtBearer)]
    [InlineData(AuthenticationType.OAuth2ClientCredentials)]
    public async Task LookupOnly_CachesForTheTenant_EvenForUserPropagationDestinations(AuthenticationType authType)
    {
        var destination = HttpDestination(authType: authType);
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.LookupOnly);
        var expectedKey = CacheKey.OfTenantOptionalIsolation().Append(DestinationName, options);

        var fetched = await Prepare(options, Returning(destination)).ExecuteAsync(default);

        Assert.NotNull(_destinationCache.GetIfPresent(expectedKey));
        Assert.NotNull(_isolationLocks.GetIfPresent(expectedKey));
        Assert.Same(destination, fetched.GetValueOrThrow());
    }

    [Fact]
    public async Task LookupThenExchange_CachesUserPropagationDestinationsPerPrincipal()
    {
        CurrentTenant = T1;
        CurrentPrincipal = P1;
        var destination = HttpDestination(authType: AuthenticationType.OAuth2JwtBearer);
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.LookupThenExchange);
        var sut = Prepare(options, Returning(destination));

        var fetched = await sut.ExecuteAsync(default);

        Assert.NotNull(sut.AdditionalKeyWithTenantAndPrincipal);
        // the destination cache key contains both tenant and principal
        Assert.NotNull(_destinationCache.GetIfPresent(CacheKey.FromIds("tenant-1", "principal-1").Append(DestinationName, options)));
        // the isolation cache key contains only the tenant
        Assert.NotNull(_isolationLocks.GetIfPresent(CacheKey.FromIds("tenant-1", null).Append(DestinationName, options)));
        Assert.Same(destination, fetched.GetValueOrThrow());
    }

    [Fact]
    public async Task ForwardUserToken_ForUserPropagationDestination_CachesPerPrincipal()
    {
        CurrentTenant = T1;
        CurrentPrincipal = P1;
        var destination = HttpDestination(authType: AuthenticationType.OAuth2JwtBearer);
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.ForwardUserToken);
        var principalKey = CacheKey.FromIds("tenant-1", "principal-1").Append(DestinationName, options);
        var sut = Prepare(options, Returning(destination));

        var fetched = await sut.ExecuteAsync(default);

        Assert.Equal(principalKey, sut.AdditionalKeyWithTenantAndPrincipal);
        Assert.Same(destination, _destinationCache.GetIfPresent(principalKey));
        Assert.Null(_destinationCache.GetIfPresent(CacheKey.FromIds("tenant-1", null).Append(DestinationName, options)));
        Assert.NotNull(_isolationLocks.GetIfPresent(CacheKey.FromIds("tenant-1", null).Append(DestinationName, options)));
        Assert.Same(destination, fetched.GetValueOrThrow());
    }

    [Fact]
    public async Task ForwardUserToken_WhenTheRetrievalFails_NothingIsCached()
    {
        CurrentTenant = T1;
        CurrentPrincipal = P1;
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.ForwardUserToken);
        var sut = Prepare(options, (_, _, _) => throw new DestinationAccessException("error-message-because-principal-is-missing"));

        var fetched = await sut.ExecuteAsync(default);

        Assert.Equal(0, _destinationCache.Count);
        Assert.NotNull(_isolationLocks.GetIfPresent(CacheKey.FromIds("tenant-1", null).Append(DestinationName, options)));
        var failure = Assert.IsType<DestinationAccessException>(fetched.Exception);
        Assert.Contains("principal-is-missing", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForwardUserToken_ForUserPropagationDestinationWithoutPrincipal_IsAnUnexpectedState()
    {
        CurrentTenant = T1;
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.ForwardUserToken);
        var sut = Prepare(options, Returning(HttpDestination(authType: AuthenticationType.OAuth2JwtBearer)));

        var result = await sut.ExecuteAsync(default);

        var failure = Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Contains("No principal is available in the current ThreadContext", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, _destinationCache.Count);
    }

    [Fact]
    public async Task ForwardUserToken_ForClientCredentialsDestination_CachesForTheTenant()
    {
        CurrentTenant = T1;
        CurrentPrincipal = P1;
        var destination = HttpDestination(authType: AuthenticationType.OAuth2ClientCredentials);
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.ForwardUserToken);
        var tenantKey = CacheKey.FromIds("tenant-1", null).Append(DestinationName, options);
        var principalKey = CacheKey.FromIds("tenant-1", "principal-1").Append(DestinationName, options);
        var sut = Prepare(options, Returning(destination));

        var fetched = await sut.ExecuteAsync(default);

        Assert.Equal(principalKey, sut.AdditionalKeyWithTenantAndPrincipal);
        Assert.Same(destination, _destinationCache.GetIfPresent(tenantKey));
        Assert.Null(_destinationCache.GetIfPresent(principalKey));
        Assert.NotNull(_isolationLocks.GetIfPresent(tenantKey));
        Assert.Same(destination, fetched.GetValueOrThrow());
    }

    [Fact]
    public async Task ChangeDetection_RefetchesWhenTheDestinationServiceReportsAChange()
    {
        // the destination in the cache has a valid auth token, but the list of all destinations has a new one
        var token = new DestinationServiceV1Response.DestinationAuthToken { ExpiryTimestamp = DateTimeOffset.UtcNow.AddDays(1) };
        var cached = DefaultHttpDestination.CreateBuilder("https://bar").Name(DestinationName)
            .Property(DestinationProperty.AuthTokens, (IReadOnlyList<object?>)[token])
            .Build();
        var cacheKey = CacheKey.OfTenantOptionalIsolation().Append(DestinationName, NoOptions);
        _destinationCache.Put(cacheKey, cached);
        var newDestination = HttpDestination("https://foo");
        var allCalls = 0;
        var retrieved = 0;

        var result = await Prepare(
            NoOptions,
            (_, _, _) =>
            {
                retrieved++;
                return Task.FromResult<IDestination>(newDestination);
            },
            AllCommand(() => [newDestination], () => allCalls++)).ExecuteAsync(default);

        Assert.NotSame(cached, result.GetValueOrThrow());
        Assert.Same(result.GetValueOrThrow(), _destinationCache.GetIfPresent(cacheKey));
        Assert.Equal(1, retrieved);
        Assert.Equal(1, allCalls); // the list of all destinations is cached itself
    }

    [Fact]
    public async Task ChangeDetection_KeepsTheCachedDestination_WhenNothingChanged()
    {
        var cached = DefaultHttpDestination.CreateBuilder("https://foo").Name(DestinationName)
            .Property(DestinationProperty.PropertiesForChangeDetection, (IReadOnlyCollection<string>)["Name", "URL"])
            .Build();
        var unchanged = DefaultDestination.CreateBuilder().Name(DestinationName).Uri("https://foo").Build();
        _destinationCache.Put(CacheKey.OfTenantOptionalIsolation().Append(DestinationName, NoOptions), cached);

        var result = await Prepare(
            NoOptions,
            (_, _, _) => throw new InvalidOperationException("must not be retrieved"),
            AllCommand(() => [unchanged])).ExecuteAsync(default);

        Assert.Same(cached, result.GetValueOrThrow());
    }

    [Fact]
    public async Task ChangeDetection_KeepsTheCachedDestination_WhenTheListOfAllDestinationsCannotBeLoaded()
    {
        var cached = HttpDestination();
        _destinationCache.Put(CacheKey.OfTenantOptionalIsolation().Append(DestinationName, NoOptions), cached);
        var getAll = GetOrComputeAllDestinationsCommand.PrepareCommand(
            NoOptions,
            _allCache,
            _isolationLocks,
            (_, _) => Task.FromResult(Result.Failure<IReadOnlyList<IDestinationProperties>>(new DestinationAccessException("down"))));

        var result = await Prepare(NoOptions, (_, _, _) => throw new InvalidOperationException("must not be retrieved"), getAll).ExecuteAsync(default);

        Assert.Same(cached, result.GetValueOrThrow());
    }

    [Fact]
    public async Task ChangeDetection_RefetchesWhenTheDestinationDisappeared()
    {
        var cached = HttpDestination();
        var replacement = HttpDestination("https://replacement");
        _destinationCache.Put(CacheKey.OfTenantOptionalIsolation().Append(DestinationName, NoOptions), cached);

        var result = await Prepare(NoOptions, Returning(replacement), AllCommand(() => [])).ExecuteAsync(default);

        Assert.Same(replacement, result.GetValueOrThrow());
    }

    [Fact]
    public async Task FirstDestinationAccess_DoesNotTriggerAGetAllRequest()
    {
        // the getAll call only happens on the second access of a destination: the first access is always a cache miss
        var newDestination = HttpDestination();
        var allCalls = 0;

        var result = await Prepare(NoOptions, Returning(newDestination), AllCommand(() => [], () => allCalls++)).ExecuteAsync(default);

        Assert.Same(newDestination, result.GetValueOrThrow());
        Assert.Equal(0, allCalls);
    }

    [Fact]
    public async Task ExpiredCertificate_IsRefetched()
    {
        var expired = new DestinationServiceV1Response.DestinationCertificate { Name = "c.p12", ExpiryTimestamp = DateTimeOffset.UtcNow.AddSeconds(5) };
        var cached = DefaultHttpDestination.CreateBuilder("https://foo").Name(DestinationName)
            .Property(DestinationProperty.Certificates, (IReadOnlyList<object?>)[expired])
            .Build();
        var fresh = HttpDestination();
        var options = OptionsWith(DestinationServiceTokenExchangeStrategy.LookupOnly);
        _destinationCache.Put(CacheKey.OfTenantOptionalIsolation().Append(DestinationName, options), cached);

        var result = await Prepare(options, Returning(fresh)).ExecuteAsync(default);

        Assert.Same(fresh, result.GetValueOrThrow());
    }

    [Fact]
    public void DestinationIsEqualTo_IgnoresPropertiesNotFromTheDestinationService()
    {
        var destination = Prepared().Property(DestinationProperty.PropertiesForChangeDetection, Relevant()).Build();
        var withToken = Prepared().Property("authToken", "some auth token")
            .Property(DestinationProperty.PropertiesForChangeDetection, Relevant()).Build();

        Assert.True(GetOrComputeSingleDestinationCommand.DestinationIsEqualTo(destination, withToken));
    }

    [Fact]
    public void DestinationIsEqualTo_DetectsChangedProperties()
    {
        var destination = Prepared().Property(DestinationProperty.PropertiesForChangeDetection, Relevant()).Build();
        var changed = Prepared().Property(DestinationProperty.Uri, "https://new-url")
            .Property(DestinationProperty.PropertiesForChangeDetection, Relevant()).Build();

        Assert.False(GetOrComputeSingleDestinationCommand.DestinationIsEqualTo(destination, changed));
    }

    [Fact]
    public void DestinationIsEqualTo_DetectsAddedProperties()
    {
        var destination = Prepared().Property(DestinationProperty.PropertiesForChangeDetection, Relevant()).Build();
        var changed = Prepared().Property("new-property", "foo")
            .Property(DestinationProperty.PropertiesForChangeDetection, (IReadOnlyCollection<string>)[.. Relevant(), "new-property"]).Build();

        Assert.False(GetOrComputeSingleDestinationCommand.DestinationIsEqualTo(destination, changed));
    }

    [Fact]
    public void DestinationIsEqualTo_DetectsDeletedProperties()
    {
        var destination = Prepared().Property("to-be-deleted-property", "foo")
            .Property(DestinationProperty.PropertiesForChangeDetection, (IReadOnlyCollection<string>)[.. Relevant(), "to-be-deleted-property"]).Build();
        var changed = Prepared().Property(DestinationProperty.PropertiesForChangeDetection, Relevant()).Build();

        Assert.False(GetOrComputeSingleDestinationCommand.DestinationIsEqualTo(destination, changed));
    }

    private static DefaultHttpDestination.Builder Prepared() => DefaultHttpDestination.CreateBuilder("https://foo").Name("foo");

    private static IReadOnlyCollection<string> Relevant() =>
        [DestinationProperty.Name.KeyName, DestinationProperty.Type.KeyName, DestinationProperty.Uri.KeyName];
}

/// <summary>Port of <c>GetOrComputeAllDestinationsCommandTest</c>.</summary>
public sealed class GetOrComputeAllDestinationsCommandTests : DestinationServiceTestBase
{
    private static readonly DefaultTenant T1 = new("tenant-1");
    private static readonly DefaultTenant T2 = new("tenant-2");

    private readonly ExpiringCache<IReadOnlyList<IDestinationProperties>> _cache = new(null, null, false);
    private readonly IsolationLocks _isolationLocks = new();

    private GetOrComputeAllDestinationsCommand Command(Func<CancellationToken, Task<Result<IReadOnlyList<IDestinationProperties>>>> supplier) =>
        GetOrComputeAllDestinationsCommand.PrepareCommand(NoOptions, _cache, _isolationLocks, (_, ct) => supplier(ct));

    [Fact]
    public async Task Command_IsIdempotent()
    {
        var destination = DefaultDestination.CreateBuilder().Name("SomeDestinationName").Build();
        var calls = 0;
        Func<CancellationToken, Task<Result<IReadOnlyList<IDestinationProperties>>>> supplier = _ =>
        {
            calls++;
            return Task.FromResult(Result.Success<IReadOnlyList<IDestinationProperties>>([destination]));
        };

        // running multiple times within the cache duration should only yield one execution
        await Command(supplier).ExecuteAsync(default);
        await Command(supplier).ExecuteAsync(default);
        var command = Command(supplier);
        await command.ExecuteAsync(default);
        await command.ExecuteAsync(default);
        var result = await command.ExecuteAsync(default);

        Assert.Equal([destination], result.GetValueOrThrow());
        Assert.Equal(1, _cache.Count);
        Assert.Equal(
            [destination],
            _cache.GetIfPresent(CacheKey.OfNoIsolation().Append(DestinationServiceOptionsAugmenter.RetrievalStrategyCacheComponent(NoOptions))));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failures_AreNotCached()
    {
        var calls = 0;
        var command = Command(_ =>
        {
            calls++;
            return Task.FromResult(Result.Failure<IReadOnlyList<IDestinationProperties>>(new DestinationAccessException("down")));
        });

        await command.ExecuteAsync(default);
        await command.ExecuteAsync(default);

        Assert.Equal(2, calls);
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task IsolationAndAtomicity_PerTenant()
    {
        var t1Key = CacheKey.FromIds("tenant-1", null).Append(DestinationServiceOptionsAugmenter.RetrievalStrategyCacheComponent(NoOptions));
        var t2Key = CacheKey.FromIds("tenant-2", null).Append(DestinationServiceOptionsAugmenter.RetrievalStrategyCacheComponent(NoOptions));
        var releaseT1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var t1Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var t1Calls = 0;
        var t2Calls = 0;

        Task<Result<IReadOnlyList<IDestinationProperties>>> ForT1() => TenantAccessor.ExecuteWithTenantAsync(
            T1,
            () => Command(async _ =>
            {
                Interlocked.Increment(ref t1Calls);
                t1Started.TrySetResult();
                await releaseT1.Task;
                return Result.Success<IReadOnlyList<IDestinationProperties>>([]);
            }).ExecuteAsync(default));
        Task<Result<IReadOnlyList<IDestinationProperties>>> ForT2() => TenantAccessor.ExecuteWithTenantAsync(
            T2,
            () => Command(_ =>
            {
                Interlocked.Increment(ref t2Calls);
                return Task.FromResult(Result.Success<IReadOnlyList<IDestinationProperties>>([]));
            }).ExecuteAsync(default));

        var first = ForT1();
        await t1Started.Task;
        // another tenant is not blocked ...
        var second = await ForT2().WaitAsync(TimeSpan.FromSeconds(30));
        // ... but the same tenant waits for the first computation
        var third = ForT1();
        await Task.Delay(200);
        Assert.False(third.IsCompleted);
        releaseT1.SetResult();
        await Task.WhenAll(first, third).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(second.IsSuccess);
        Assert.Equal(1, t1Calls);
        Assert.Equal(1, t2Calls);
        Assert.Equal(2, _cache.Count);
        Assert.NotNull(_cache.GetIfPresent(t1Key));
        Assert.NotNull(_cache.GetIfPresent(t2Key));
    }
}
