using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>
/// The decision table that determines which tenant and which OAuth flow the requests to the destination service use
/// (port of <c>DestinationRetrievalStrategyResolverTest</c>).
/// </summary>
public sealed class RetrievalStrategyResolverTests : DestinationServiceTestBase
{
    private const string RefreshToken = "refreshToken";
    private static readonly DefaultTenant ProviderTenant = new("provider");
    private static readonly DefaultTenant SubscriberTenant = new("subscriber");

    private readonly List<DestinationRetrievalStrategy> _requests = [];
    private readonly DestinationServiceV1Response _noExchangeNeeded = DestinationServiceV1Response.Parse(
        """{"destinationConfiguration":{"Name":"d","Type":"HTTP","URL":"https://example.com","Authentication":"NoAuthentication"}}""");

    private DestinationRetrievalStrategyResolver CreateResolver(DestinationServiceV1Response? response = null) =>
        new(
            () => ProviderTenant.TenantId,
            (strategy, _) =>
            {
                _requests.Add(strategy);
                return Task.FromResult(response ?? _noExchangeNeeded);
            },
            (_, _) => Task.FromResult<List<IDestinationProperties>>([]));

    private sealed class ExchangeRequiredResolver(
        Func<string> providerTenantIdSupplier,
        Func<DestinationRetrievalStrategy, CancellationToken, Task<DestinationServiceV1Response>> retriever)
        : DestinationRetrievalStrategyResolver(providerTenantIdSupplier, retriever, (_, _) => Task.FromResult<List<IDestinationProperties>>([]))
    {
        internal override bool DoesDestinationConfigurationRequireUserTokenExchange(DestinationServiceV1Response response) => true;
    }

    [Fact]
    public void SimpleBehalfResolutions_CoverEveryRowOfTheDecisionTable()
    {
        var token = XsuaaToken();
        CurrentToken = token;
        var tokenValue = token.Jwt.EncodedToken;

        const OnBehalfOf provider = OnBehalfOf.TechnicalUserProvider;
        const OnBehalfOf current = OnBehalfOf.TechnicalUserCurrentTenant;
        const OnBehalfOf named = OnBehalfOf.NamedUserCurrentTenant;
        var always = DestinationServiceRetrievalStrategy.AlwaysProvider;
        var currentTenant = DestinationServiceRetrievalStrategy.CurrentTenant;
        var onlySubscriber = DestinationServiceRetrievalStrategy.OnlySubscriber;
        var lookupOnly = DestinationServiceTokenExchangeStrategy.LookupOnly;
        var exchangeOnly = DestinationServiceTokenExchangeStrategy.ExchangeOnly;
        var forward = DestinationServiceTokenExchangeStrategy.ForwardUserToken;

        (DestinationServiceRetrievalStrategy, DestinationServiceTokenExchangeStrategy, string?, DestinationRetrievalStrategy)[] cases =
        [
            // LookupOnly
            (currentTenant, lookupOnly, null, DestinationRetrievalStrategy.WithoutToken(current)),
            (onlySubscriber, lookupOnly, null, DestinationRetrievalStrategy.WithoutToken(current)),
            (always, lookupOnly, null, DestinationRetrievalStrategy.WithoutToken(provider)),
            (currentTenant, lookupOnly, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(current, RefreshToken)),
            (onlySubscriber, lookupOnly, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(current, RefreshToken)),
            (always, lookupOnly, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(provider, RefreshToken)),

            // ExchangeOnly
            (currentTenant, exchangeOnly, null, DestinationRetrievalStrategy.WithoutToken(named)),
            (onlySubscriber, exchangeOnly, null, DestinationRetrievalStrategy.WithoutToken(named)),
            (always, exchangeOnly, null, DestinationRetrievalStrategy.WithoutToken(named)),
            (currentTenant, exchangeOnly, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(current, RefreshToken)),
            (onlySubscriber, exchangeOnly, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(current, RefreshToken)),
            (always, exchangeOnly, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(provider, RefreshToken)),

            // ForwardUserToken
            (currentTenant, forward, null, DestinationRetrievalStrategy.WithUserToken(current, tokenValue)),
            (onlySubscriber, forward, null, DestinationRetrievalStrategy.WithUserToken(current, tokenValue)),
            (always, forward, null, DestinationRetrievalStrategy.WithUserToken(provider, tokenValue)),
            (currentTenant, forward, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(current, RefreshToken)),
            (onlySubscriber, forward, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(current, RefreshToken)),
            (always, forward, RefreshToken, DestinationRetrievalStrategy.WithRefreshToken(provider, RefreshToken)),
        ];

        var failures = cases
            .Where(c => !DestinationRetrievalStrategyResolver.ResolveSingleRequestStrategy(c.Item1, c.Item2, c.Item3).Equals(c.Item4))
            .Select(c => $"{c.Item1} + {c.Item2} {(c.Item3 is null ? "without" : "with")} refresh token should resolve to {c.Item4}")
            .ToList();
        Assert.Empty(failures);
        Assert.Equal(18, cases.Length);
    }

    [Fact]
    public void ForwardUserToken_WithoutToken_ResolvesToNoToken()
    {
        CurrentToken = null;

        Assert.Equal(
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant),
            DestinationRetrievalStrategyResolver.ResolveSingleRequestStrategy(
                DestinationServiceRetrievalStrategy.CurrentTenant,
                DestinationServiceTokenExchangeStrategy.ForwardUserToken,
                null));
    }

    [Fact]
    public void LookupThenExchange_CannotBeResolvedToASingleRequest() =>
        Assert.Throws<InvalidOperationException>(() => DestinationRetrievalStrategyResolver.ResolveSingleRequestStrategy(
            DestinationServiceRetrievalStrategy.CurrentTenant,
            DestinationServiceTokenExchangeStrategy.LookupThenExchange,
            null));

    [Fact]
    public void IllegalCombinations_Throw()
    {
        var resolver = CreateResolver();
        (DestinationServiceRetrievalStrategy, DestinationServiceTokenExchangeStrategy, DefaultTenant)[] cases =
        [
            (DestinationServiceRetrievalStrategy.OnlySubscriber, DestinationServiceTokenExchangeStrategy.LookupOnly, ProviderTenant),
            (DestinationServiceRetrievalStrategy.OnlySubscriber, DestinationServiceTokenExchangeStrategy.LookupThenExchange, ProviderTenant),
            (DestinationServiceRetrievalStrategy.OnlySubscriber, DestinationServiceTokenExchangeStrategy.ExchangeOnly, ProviderTenant),
            (DestinationServiceRetrievalStrategy.OnlySubscriber, DestinationServiceTokenExchangeStrategy.ForwardUserToken, ProviderTenant),
            (DestinationServiceRetrievalStrategy.AlwaysProvider, DestinationServiceTokenExchangeStrategy.ExchangeOnly, SubscriberTenant),
        ];

        foreach (var (retrieval, exchange, tenant) in cases)
        {
            TenantAccessor.ExecuteWithTenant(tenant, () =>
                Assert.Throws<DestinationAccessException>(() => resolver.PrepareSupplier(retrieval, exchange, null, null, [])));
        }

        TenantAccessor.ExecuteWithTenant(ProviderTenant, () =>
            Assert.Throws<DestinationAccessException>(
                () => resolver.PrepareSupplierAllDestinations(DestinationServiceRetrievalStrategy.OnlySubscriber)));
    }

    [Fact]
    public async Task AlwaysProvider_WithLookupThenExchange_FailsWhenAnExchangeIsNeededOnASubscriberTenant()
    {
        var resolver = new ExchangeRequiredResolver(
            () => ProviderTenant.TenantId,
            (strategy, _) =>
            {
                _requests.Add(strategy);
                return Task.FromResult(_noExchangeNeeded);
            });

        // preparing is fine; only the exchange is impossible
        var retrieval = resolver.PrepareSupplier(
            DestinationServiceRetrievalStrategy.AlwaysProvider,
            DestinationServiceTokenExchangeStrategy.LookupThenExchange,
            null,
            null,
            []);

        await TenantAccessor.ExecuteWithTenantAsync(SubscriberTenant, async () =>
        {
            await Assert.ThrowsAsync<DestinationAccessException>(() => retrieval.GetAsync(default));
            return 0;
        });
        Assert.Equal([DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider)], _requests);
    }

    [Fact]
    public async Task LookupThenExchange_PerformsTheExchangeOnBehalfOfTheNamedUser_WhenTheDestinationNeedsIt()
    {
        var resolver = new ExchangeRequiredResolver(
            () => ProviderTenant.TenantId,
            (strategy, _) =>
            {
                _requests.Add(strategy);
                return Task.FromResult(_noExchangeNeeded);
            });

        var retrieval = resolver.PrepareSupplier(
            DestinationServiceRetrievalStrategy.CurrentTenant,
            DestinationServiceTokenExchangeStrategy.LookupThenExchange,
            null,
            "fragment",
            []);
        await retrieval.GetAsync(default);

        Assert.Equal(OnBehalfOf.TechnicalUserCurrentTenant, retrieval.OnBehalfOf);
        Assert.Equal(
            [
                DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant).WithFragmentName("fragment"),
                DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant),
            ],
            _requests);
    }

    [Fact]
    public async Task LookupThenExchange_DoesNotExchange_WhenTheDestinationDoesNotNeedIt()
    {
        var retrieval = CreateResolver().PrepareSupplier(
            DestinationServiceRetrievalStrategy.CurrentTenant,
            DestinationServiceTokenExchangeStrategy.LookupThenExchange,
            null,
            null,
            []);

        await retrieval.GetAsync(default);

        Assert.Equal([DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant)], _requests);
    }

    [Fact]
    public async Task DefaultStrategies_ForXsuaaTokens_ForwardTheUserToken()
    {
        var token = XsuaaToken();
        CurrentToken = token;
        var resolver = CreateResolver();

        var retrieval = resolver.PrepareSupplier(NoOptions);
        await retrieval.GetAsync(default);
        resolver.PrepareSupplierAllDestinations(NoOptions);

        Assert.Equal(
            [DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserCurrentTenant, token.Jwt.EncodedToken)],
            _requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultStrategies_ForNonXsuaaTokensOrNoToken_LookUpFirst(bool withNonXsuaaToken)
    {
        CurrentToken = withNonXsuaaToken ? PlainToken() : null;
        var resolver = CreateResolver();

        var retrieval = resolver.PrepareSupplier(NoOptions);
        await retrieval.GetAsync(default);

        Assert.Equal([DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant)], _requests);
    }

    [Fact]
    public async Task RefreshToken_IsForwardedOnBehalfOfTheTechnicalUser()
    {
        var options = Options(Augmenter().RefreshToken(RefreshToken));

        await CreateResolver().PrepareSupplier(options).GetAsync(default);

        Assert.Equal([DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.TechnicalUserCurrentTenant, RefreshToken)], _requests);
    }

    [Fact]
    public async Task FragmentName_AndCustomHeaders_AreAddedToTheRequest()
    {
        var options = Options(Augmenter().FragmentName("my-fragment").CustomHeaders(new Header("X-A", "1"), new Header("X-B", "2")));
        CurrentToken = null;
        var resolver = CreateResolver();

        // custom headers are only applied for strategies other than LookupThenExchange, which is the default without a token
        await resolver.PrepareSupplier(
            DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options) ?? DestinationServiceRetrievalStrategy.CurrentTenant,
            DestinationServiceTokenExchangeStrategy.LookupOnly,
            null,
            DestinationServiceOptionsAugmenter.GetFragmentName(options),
            DestinationServiceOptionsAugmenter.GetAdditionalHeaders(options)).GetAsync(default);

        var request = Assert.Single(_requests);
        Assert.Equal("my-fragment", request.Fragment);
        Assert.Equal([new Header("X-A", "1"), new Header("X-B", "2")], request.AdditionalHeaders);
    }

    [Fact]
    public void AllDestinations_UseTheTenantOfTheStrategy_AndIgnoreTokenExchangeStrategies()
    {
        var retrieved = new List<OnBehalfOf>();
        var resolver = new DestinationRetrievalStrategyResolver(
            () => ProviderTenant.TenantId,
            (_, _) => Task.FromResult(_noExchangeNeeded),
            (behalf, _) =>
            {
                retrieved.Add(behalf);
                return Task.FromResult<List<IDestinationProperties>>([]);
            });

        TenantAccessor.ExecuteWithTenant(ProviderTenant, () =>
        {
            resolver.PrepareSupplierAllDestinations(DestinationServiceRetrievalStrategy.AlwaysProvider)(default).GetAwaiter().GetResult();
            resolver.PrepareSupplierAllDestinations(DestinationServiceRetrievalStrategy.CurrentTenant)(default).GetAwaiter().GetResult();
            // an exchange strategy is not applicable and is replaced by LookupOnly
            resolver.PrepareSupplierAllDestinations(Options(Augmenter().TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy.ExchangeOnly)))(default).GetAwaiter().GetResult();
        });

        Assert.Equal([OnBehalfOf.TechnicalUserProvider, OnBehalfOf.TechnicalUserCurrentTenant, OnBehalfOf.TechnicalUserCurrentTenant], retrieved);
    }

    [Theory]
    [InlineData("OAuth2SAMLBearerAssertion", null, true)]
    [InlineData("OAuth2SAMLBearerAssertion", "admin", false)]
    [InlineData("SAPAssertionSSO", "admin", false)]
    [InlineData("SAPAssertionSSO", null, true)]
    [InlineData("OAuth2JWTBearer", null, true)]
    [InlineData("OAuth2ClientCredentials", null, false)]
    [InlineData("BasicAuthentication", null, false)]
    [InlineData("SAMLAssertion", null, true)]
    [InlineData("NotAnAuthenticationType", null, false)]
    [InlineData(null, null, false)]
    public void RequiresUserTokenExchange_IsDerivedFromTheConfiguration(string? authentication, string? systemUser, bool expected)
    {
        Dictionary<string, string> configuration = new(StringComparer.Ordinal) { ["Name"] = "d" };
        if (authentication is not null)
        {
            configuration["authentication"] = authentication; // property names are case-insensitive
        }
        if (systemUser is not null)
        {
            configuration["SystemUser"] = systemUser;
        }
        var response = new DestinationServiceV1Response { DestinationConfiguration = configuration };

        Assert.Equal(expected, CreateResolver().DoesDestinationConfigurationRequireUserTokenExchange(response));
    }
}
