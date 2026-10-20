using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Decides which tenant (<see cref="OnBehalfOf"/>) and which OAuth flow (token forwarding) the requests to the
/// destination service use, for a given retrieval strategy, token exchange strategy and current context.
/// </summary>
/// <remarks>
/// <para>
/// Decision table of <see cref="ResolveSingleRequestStrategy"/> (the same for <c>CurrentTenant</c> and <c>OnlySubscriber</c>):
/// </para>
/// <list type="table">
/// <listheader><term>Token exchange strategy</term><description>Request (refresh token not set)</description></listheader>
/// <item><term>LookupOnly</term><description>no token, technical user (provider for <c>AlwaysProvider</c>, else current tenant)</description></item>
/// <item><term>ExchangeOnly</term><description>no token, named user of the current tenant (the retrieval strategy is ignored)</description></item>
/// <item><term>ForwardUserToken</term><description>the current JWT as user token if there is one, otherwise no token; technical user as for LookupOnly</description></item>
/// <item><term>LookupThenExchange</term><description>two steps, see <see cref="PrepareSupplier(DestinationServiceRetrievalStrategy, DestinationServiceTokenExchangeStrategy, string?, string?, IReadOnlyList{Header})"/></description></item>
/// </list>
/// <para>A refresh token always wins and is sent on behalf of the technical user.</para>
/// </remarks>
internal class DestinationRetrievalStrategyResolver(
    Func<string> providerTenantIdSupplier,
    Func<DestinationRetrievalStrategy, CancellationToken, Task<DestinationServiceV1Response>> destinationRetriever,
    Func<OnBehalfOf, CancellationToken, Task<List<IDestinationProperties>>> allDestinationRetriever)
{
    internal const string JwtAttrExt = "ext_attr";
    internal const string JwtAttrEnhancer = "enhancer";
    internal const string JwtAttrXsuaa = "XSUAA";

    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationRetrievalStrategyResolver>();

    public static DestinationRetrievalStrategyResolver ForSingleDestination(
        Func<string> providerTenantIdSupplier,
        Func<DestinationRetrievalStrategy, CancellationToken, Task<DestinationServiceV1Response>> destinationRetriever) =>
        new(providerTenantIdSupplier, destinationRetriever, (_, _) => Task.FromResult<List<IDestinationProperties>>([]));

    public static DestinationRetrievalStrategyResolver ForAllDestinations(
        Func<string> providerTenantIdSupplier,
        Func<OnBehalfOf, CancellationToken, Task<List<IDestinationProperties>>> allDestinationRetriever) =>
        new(
            providerTenantIdSupplier,
            (_, _) => Task.FromResult<DestinationServiceV1Response>(null!),
            allDestinationRetriever);

    public static DestinationRetrievalStrategy ResolveSingleRequestStrategy(
        DestinationServiceRetrievalStrategy retrievalStrategy,
        DestinationServiceTokenExchangeStrategy tokenExchangeStrategy,
        string? refreshToken)
    {
        var behalfTechnicalUser = retrievalStrategy switch
        {
            DestinationServiceRetrievalStrategy.AlwaysProvider => OnBehalfOf.TechnicalUserProvider,
            DestinationServiceRetrievalStrategy.CurrentTenant or DestinationServiceRetrievalStrategy.OnlySubscriber =>
                OnBehalfOf.TechnicalUserCurrentTenant,
            _ => throw new ArgumentOutOfRangeException(nameof(retrievalStrategy)),
        };

        if (refreshToken is not null)
        {
            return DestinationRetrievalStrategy.WithRefreshToken(behalfTechnicalUser, refreshToken);
        }

        var maybeToken = AuthTokenAccessor.TryGetCurrentToken().Map(t => t.Jwt.EncodedToken);

        return tokenExchangeStrategy switch
        {
            DestinationServiceTokenExchangeStrategy.ForwardUserToken => maybeToken.IsFailure
                ? DestinationRetrievalStrategy.WithoutToken(behalfTechnicalUser)
                : DestinationRetrievalStrategy.WithUserToken(behalfTechnicalUser, maybeToken.GetValueOrThrow()),
            DestinationServiceTokenExchangeStrategy.LookupOnly =>
                DestinationRetrievalStrategy.WithoutToken(behalfTechnicalUser),
            DestinationServiceTokenExchangeStrategy.ExchangeOnly =>
                DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant),
            // this method must never be called for LookupThenExchange
            _ => throw new InvalidOperationException(
                $"Unexpected token exchange strategy {tokenExchangeStrategy.GetIdentifier()} when building a request towards the destination service."),
        };
    }

    public DestinationRetrieval PrepareSupplier(DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var retrievalStrategy =
            DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options) ?? DestinationServiceRetrievalStrategy.CurrentTenant;
        var tokenExchangeStrategy =
            DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(options) ?? GetDefaultTokenExchangeStrategy();
        var refreshToken = DestinationServiceOptionsAugmenter.GetRefreshToken(options);
        if (refreshToken is not null)
        {
            Log.LogDebug("Refresh token given, applying refresh token flow.");
        }
        var fragmentName = DestinationServiceOptionsAugmenter.GetFragmentName(options);
        var additionalHeaders = DestinationServiceOptionsAugmenter.GetAdditionalHeaders(options);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            if (fragmentName is not null)
            {
                Log.LogDebug("Found fragment name '{FragmentName}'.", fragmentName);
            }
            foreach (var header in additionalHeaders)
            {
                Log.LogDebug("Found additional header: {Header}.", header);
            }
        }

        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Loading destination from the destination service with retrieval strategy {Retrieval} and token exchange strategy {Exchange}.",
                retrievalStrategy.GetIdentifier(),
                tokenExchangeStrategy.GetIdentifier());
        }
        return PrepareSupplier(retrievalStrategy, tokenExchangeStrategy, refreshToken, fragmentName, additionalHeaders);
    }

    /// <summary>
    /// Prepares the requests to the destination service. For <see cref="DestinationServiceTokenExchangeStrategy.LookupThenExchange"/>
    /// the first request is a lookup without user token; if the returned configuration needs a user token exchange, a second
    /// request on behalf of the named user follows. Custom headers are not sent for LookupThenExchange (as in Java).
    /// </summary>
    /// <exception cref="DestinationAccessException">The combination of strategies cannot be satisfied in the current tenant.</exception>
    public DestinationRetrieval PrepareSupplier(
        DestinationServiceRetrievalStrategy retrievalStrategy,
        DestinationServiceTokenExchangeStrategy tokenExchangeStrategy,
        string? refreshToken,
        string? fragmentName,
        IReadOnlyList<Header> additionalHeaders)
    {
        ArgumentNullException.ThrowIfNull(additionalHeaders);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Preparing request(s) towards the destination service based on the strategies {Retrieval} and {Exchange}",
                retrievalStrategy.GetIdentifier(),
                tokenExchangeStrategy.GetIdentifier());
        }
        WarnOrThrowOnUnsupportedCombinations(retrievalStrategy, tokenExchangeStrategy);

        if (tokenExchangeStrategy == DestinationServiceTokenExchangeStrategy.LookupThenExchange)
        {
            var lookup = ResolveSingleRequestStrategy(
                retrievalStrategy,
                DestinationServiceTokenExchangeStrategy.LookupOnly,
                refreshToken);
            if (fragmentName is not null)
            {
                lookup.WithFragmentName(fragmentName);
            }
            return new DestinationRetrieval(
                async cancellationToken =>
                {
                    var result = await destinationRetriever(lookup, cancellationToken).ConfigureAwait(false);
                    if (!DoesDestinationConfigurationRequireUserTokenExchange(result))
                    {
                        return result;
                    }
                    if (retrievalStrategy == DestinationServiceRetrievalStrategy.AlwaysProvider && !CurrentTenantIsProvider())
                    {
                        throw new DestinationAccessException(
                            "Can't perform token exchange, the current token is not issued for the provider tenant.");
                    }
                    return await destinationRetriever(
                        DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant),
                        cancellationToken).ConfigureAwait(false);
                },
                lookup.Behalf);
        }

        var strategy = ResolveSingleRequestStrategy(retrievalStrategy, tokenExchangeStrategy, refreshToken);
        if (fragmentName is not null)
        {
            strategy.WithFragmentName(fragmentName);
        }
        if (additionalHeaders.Count > 0)
        {
            strategy.WithAdditionalHeaders(additionalHeaders);
        }
        return new DestinationRetrieval(cancellationToken => destinationRetriever(strategy, cancellationToken), strategy.Behalf);
    }

    public Func<CancellationToken, Task<List<IDestinationProperties>>> PrepareSupplierAllDestinations(DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var tokenExchangeStrategy =
            DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(options) ?? DestinationServiceTokenExchangeStrategy.LookupOnly;
        if (tokenExchangeStrategy != DestinationServiceTokenExchangeStrategy.LookupOnly)
        {
            Log.LogWarning(
                "The provided token exchange strategy {Strategy} is not applicable while retrieving all destinations, hence switching to {Switched}",
                tokenExchangeStrategy.GetIdentifier(),
                DestinationServiceTokenExchangeStrategy.LookupOnly.GetIdentifier());
        }
        var retrievalStrategy =
            DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options) ?? DestinationServiceRetrievalStrategy.CurrentTenant;
        return PrepareSupplierAllDestinations(retrievalStrategy);
    }

    /// <exception cref="DestinationAccessException">The strategy cannot be satisfied in the current tenant.</exception>
    public Func<CancellationToken, Task<List<IDestinationProperties>>> PrepareSupplierAllDestinations(
        DestinationServiceRetrievalStrategy strategy)
    {
        WarnOrThrowOnUnsupportedCombinations(strategy, null);
        return strategy switch
        {
            DestinationServiceRetrievalStrategy.AlwaysProvider =>
                cancellationToken => allDestinationRetriever(OnBehalfOf.TechnicalUserProvider, cancellationToken),
            DestinationServiceRetrievalStrategy.OnlySubscriber or DestinationServiceRetrievalStrategy.CurrentTenant =>
                cancellationToken => allDestinationRetriever(OnBehalfOf.TechnicalUserCurrentTenant, cancellationToken),
            _ => throw new ArgumentException($"The provided destination retrieval strategy {strategy} is not valid.", nameof(strategy)),
        };
    }

    internal virtual bool DoesDestinationConfigurationRequireUserTokenExchange(DestinationServiceV1Response response)
    {
        var configuration = new Dictionary<string, string>(response.DestinationConfiguration, StringComparer.OrdinalIgnoreCase);
        configuration.TryGetValue(DestinationProperty.AuthType.KeyName, out var authenticationValue);
        AuthenticationType authType;
        try
        {
            authType = AuthenticationTypeExtensions.OfIdentifier(authenticationValue!);
        }
        catch (ArgumentException)
        {
            // also covers the missing (null) value
            Log.LogWarning(
                "No configuration value set for 'Authentication' in destination {Name}.",
                configuration.GetValueOrDefault(DestinationProperty.Name.KeyName));
            return false;
        }
        configuration.TryGetValue(DestinationProperty.SystemUser.KeyName, out var systemUser);
        return DestinationUtility.RequiresUserTokenExchange(authType, systemUser);
    }

    /// <summary>
    /// The default token exchange strategy if the user has not specified one: <c>ForwardUserToken</c> if the current token
    /// originates from XSUAA (its payload contains <c>{"ext_attr":{"enhancer":"XSUAA"}}</c>), <c>LookupThenExchange</c>
    /// if there is no token or the token does not originate from XSUAA.
    /// </summary>
    private static DestinationServiceTokenExchangeStrategy GetDefaultTokenExchangeStrategy()
    {
        var token = AuthTokenAccessor.TryGetCurrentToken();
        var isXsuaa = token.IsSuccess && TryGetEnhancer(token.GetValueOrThrow()) is { } enhancer
            && string.Equals(JwtAttrXsuaa, enhancer, StringComparison.OrdinalIgnoreCase);
        if (isXsuaa)
        {
            return DestinationServiceTokenExchangeStrategy.ForwardUserToken;
        }
        // consider scenario where "forward user token" will not work out-of-the-box, e.g. for IAS identity service
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Falling back to {Strategy}. Current user token may not originate from XSUAA.",
                DestinationServiceTokenExchangeStrategy.LookupThenExchange.GetIdentifier());
        }
        return DestinationServiceTokenExchangeStrategy.LookupThenExchange;
    }

    private static string? TryGetEnhancer(AuthToken token)
    {
        if (!token.Jwt.TryGetPayloadValue<JsonElement>(JwtAttrExt, out var extAttr) || extAttr.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return extAttr.TryGetProperty(JwtAttrEnhancer, out var enhancer) ? enhancer.ToString() : null;
    }

    private void WarnOrThrowOnUnsupportedCombinations(
        DestinationServiceRetrievalStrategy retrievalStrategy,
        DestinationServiceTokenExchangeStrategy? tokenExchangeStrategy)
    {
        if (retrievalStrategy == DestinationServiceRetrievalStrategy.OnlySubscriber && CurrentTenantIsProvider())
        {
            throw new DestinationAccessException(
                "The current tenant is the provider tenant, which should not be the case with the option "
                + DestinationServiceRetrievalStrategy.OnlySubscriber.GetIdentifier()
                + ". Cannot retrieve destination.");
        }
        if (retrievalStrategy == DestinationServiceRetrievalStrategy.AlwaysProvider && !CurrentTenantIsProvider())
        {
            if (tokenExchangeStrategy == DestinationServiceTokenExchangeStrategy.ExchangeOnly)
            {
                throw new DestinationAccessException(
                    "The current tenant is not the provider tenant, which should not be the case with the options "
                    + DestinationServiceRetrievalStrategy.AlwaysProvider.GetIdentifier()
                    + " and "
                    + DestinationServiceTokenExchangeStrategy.ExchangeOnly.GetIdentifier()
                    + ". Cannot retrieve destination.");
            }
            if (tokenExchangeStrategy != DestinationServiceTokenExchangeStrategy.LookupOnly)
            {
                Log.LogWarning(
                    "The current tenant is not the provider tenant. Only destinations which don't require a user token will be supported. "
                    + "Use retrieval strategy {Strategy} to avoid this warning.",
                    DestinationServiceTokenExchangeStrategy.LookupOnly.GetIdentifier());
            }
        }
    }

    private bool CurrentTenantIsProvider()
    {
        var currentTenantId = TenantAccessor.TryGetCurrentTenant().Map(t => t.TenantId).GetValueOrDefault();
        var providerTenantId = providerTenantIdSupplier();
        return string.Equals(currentTenantId, providerTenantId, StringComparison.Ordinal);
    }
}
