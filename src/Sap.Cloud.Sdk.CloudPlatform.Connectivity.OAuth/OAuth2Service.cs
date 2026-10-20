using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>How the tenant is communicated to the token endpoint.</summary>
internal enum TenantPropagationStrategy
{
    /// <summary>IAS: tenant subdomain in the token endpoint host, plus <c>app_tid</c>.</summary>
    TenantSubdomain,

    /// <summary>XSUAA: <c>X-zid</c> header.</summary>
    ZidHeader,
}

/// <summary>Retrieves access tokens on behalf of the provider, the current tenant or the current user.</summary>
internal sealed class OAuth2Service
{
    private const string XsuaaTokenPath = "/oauth/token";

    private static readonly ILogger Log = SdkLogging.CreateLogger<OAuth2Service>();

    // Token services (and their token caches and HTTP connections) per tenant and client identity.
    internal static readonly MemoryCache TokenServiceCache = CreateTokenServiceCache();

    private static readonly Lazy<HttpClient> SharedHttpClient = new(() => new HttpClient(CreateHandler(null), disposeHandler: true));

    private readonly IReadOnlyDictionary<string, string> _additionalParameters;
    private readonly TokenCacheParameters _tokenCacheParameters;
    private readonly Uri? _btpTenantApiUri;
    private readonly IasTenantHostResolver _iasTenantHostResolver;
    private readonly Func<OAuth2ClientIdentity, HttpClient> _httpClientFactory;

    private OAuth2Service(Builder builder)
    {
        TokenUri = builder.TokenUri!;
        Identity = builder.Identity!;
        OnBehalfOf = builder.OnBehalfOfValue;
        TenantPropagationStrategy = builder.Strategy;
        _additionalParameters = new Dictionary<string, string>(builder.AdditionalParameters, StringComparer.Ordinal);
        _tokenCacheParameters = builder.CacheParameters;
        _btpTenantApiUri = builder.BtpTenantApiUri;
        _iasTenantHostResolver = builder.IasResolver ?? IasTenantHostResolver.Default;
        _httpClientFactory = builder.HttpClientFactory ?? DefaultHttpClient;
        ResilienceConfiguration = ResilienceConfiguration.Of(TokenUri.Host + "-" + Identity.Id) with
        {
            IsolationMode = ResilienceIsolationMode.TenantOptional,
            TimeLimiter = builder.TimeLimiter,
        };
    }

    public Uri TokenUri { get; }

    public OAuth2ClientIdentity Identity { get; }

    public OnBehalfOf OnBehalfOf { get; }

    public TenantPropagationStrategy TenantPropagationStrategy { get; }

    public ResilienceConfiguration ResilienceConfiguration { get; }

    public IReadOnlyDictionary<string, string> AdditionalParameters => _additionalParameters;

    public static Builder CreateBuilder() => new();

    /// <summary>Retrieves an access token according to <see cref="OnBehalfOf"/>.</summary>
    /// <exception cref="DestinationOAuthTokenException">The response contained no access token.</exception>
    /// <exception cref="TokenRequestFailedException">The token request failed.</exception>
    public async Task<string> RetrieveAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Retrieving access token from '{TokenUri}' on behalf of {OnBehalfOf} with client id '{ClientId}'.", TokenUri, OnBehalfOf, Identity.Id);
        }

        var response = await ResilienceDecorator.ExecuteAsync(
            ct => OnBehalfOf switch
            {
                OnBehalfOf.TechnicalUserProvider => ExecuteClientCredentialsFlowAsync(null, ct),
                OnBehalfOf.TechnicalUserCurrentTenant =>
                    ExecuteClientCredentialsFlowAsync(TenantAccessor.TryGetCurrentTenant().GetValueOrDefault(), ct),
                OnBehalfOf.NamedUserCurrentTenant => ExecuteUserExchangeFlowAsync(ct),
                _ => throw new InvalidOperationException($"Unknown behalf {OnBehalfOf}."),
            },
            ResilienceConfiguration,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.AccessToken
            ?? throw new DestinationOAuthTokenException(null, "OAuth2 token request succeeded but the response did not contain an access token");
    }

    private async Task<OAuth2TokenResponse> ExecuteClientCredentialsFlowAsync(ITenant? tenant, CancellationToken cancellationToken)
    {
        var tenantId = tenant?.TenantId;
        var zidHeader = TenantPropagationStrategy == TenantPropagationStrategy.ZidHeader ? tenantId : null;
        var parameters = ParametersFor(tenantId);
        var subdomain = await GetTenantSubdomainOrNullAsync(tenant, cancellationToken).ConfigureAwait(false);
        var tokenService = GetTokenService(tenantId);
        try
        {
            return await tokenService.RetrieveAccessTokenViaClientCredentialsGrantAsync(
                TokenUri, Identity, zidHeader, subdomain, parameters, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            var message = "Failed to resolve access token.";
            if (e is OAuth2ServiceException { StatusCode: System.Net.HttpStatusCode.Unauthorized } && tenant is not null)
            {
                message += " In case you are accessing a multi-tenant BTP service on behalf of a subscriber tenant, ensure that the service instance"
                    + " is declared as dependency to SaaS Provisioning Service or Subscription Manager (SMS) and subscribed for the current tenant.";
            }
            throw new TokenRequestFailedException(message, e);
        }
    }

    private async Task<OAuth2TokenResponse> ExecuteUserExchangeFlowAsync(CancellationToken cancellationToken)
    {
        var token = AuthTokenAccessor.TryGetCurrentToken();
        if (token.IsFailure)
        {
            throw new CloudPlatformException("Failed to get the current user token.", token.Exception);
        }
        var userToken = token.GetValueOrThrow();
        var tenant = TenantAccessor.TryGetCurrentTenant();

        // Both XSUAA and IAS carry the tenant of a user token in app_tid (XSUAA additionally in zid).
        var tokenTenantId = userToken.GetClaimAsString("app_tid") ?? userToken.GetClaimAsString("zid");
        if (tenant.IsFailure)
        {
            Log.LogWarning(
                "Unexpected state: An Auth Token was found in the current context, but the current tenant is undefined. "
                + "Proceeding with tenant {TenantId} defined in the current token.", tokenTenantId);
        }
        else if (!string.Equals(tenant.GetValueOrThrow().TenantId, tokenTenantId, StringComparison.Ordinal))
        {
            throw new CloudPlatformException(
                "Unexpected state: Auth Token and tenant of the current context have different tenant IDs. "
                + $"AuthTokenAccessor returned a token containing tenant ID {tokenTenantId} while TenantAccessor returned "
                + $"{tenant.GetValueOrThrow().TenantId}. Please ensure the TenantAccessor and AuthTokenAccessor return consistent results.");
        }

        var parameters = ParametersFor(tokenTenantId);
        var tokenService = GetTokenService(tokenTenantId);
        try
        {
            switch (TenantPropagationStrategy)
            {
                case TenantPropagationStrategy.ZidHeader:
                    if (tokenTenantId is null)
                    {
                        throw new DestinationAccessException("The current user token does not contain a tenant ID (zid/app_tid).");
                    }
                    return await tokenService.RetrieveAccessTokenViaJwtBearerGrantWithZoneIdAsync(
                        TokenUri, Identity, userToken.Jwt.EncodedToken, parameters, false, tokenTenantId, cancellationToken).ConfigureAwait(false);
                case TenantPropagationStrategy.TenantSubdomain:
                    var subdomain = await GetTenantSubdomainOrNullAsync(tenant.GetValueOrDefault(), cancellationToken).ConfigureAwait(false);
                    return await tokenService.RetrieveAccessTokenViaJwtBearerGrantAsync(
                        TokenUri, Identity, userToken.Jwt.EncodedToken, subdomain, parameters, false, cancellationToken).ConfigureAwait(false);
                default:
                    throw new DestinationAccessException($"Unhandled TenantPropagation Strategy: {TenantPropagationStrategy}.");
            }
        }
        catch (Exception e) when (e is not OperationCanceledException and not DestinationAccessException)
        {
            throw new TokenRequestFailedException("Failed to resolve access token.", e);
        }
    }

    // Java mutates a map shared by all requests of this service; here every request gets its own copy.
    private Dictionary<string, string> ParametersFor(string? tenantId)
    {
        var parameters = new Dictionary<string, string>(_additionalParameters, StringComparer.Ordinal);
        if (TenantPropagationStrategy == TenantPropagationStrategy.TenantSubdomain && tenantId is not null)
        {
            parameters["app_tid"] = tenantId;
            if (OnBehalfOf == OnBehalfOf.NamedUserCurrentTenant)
            {
                parameters["refresh_expiry"] = "0";
            }
        }
        return parameters;
    }

    private async Task<string?> GetTenantSubdomainOrNullAsync(ITenant? tenant, CancellationToken cancellationToken)
    {
        if (TenantPropagationStrategy != TenantPropagationStrategy.TenantSubdomain || tenant is null)
        {
            return null;
        }
        if (tenant is ITenantWithSubdomain { Subdomain: { } subdomain })
        {
            return subdomain;
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("IAS tenant host is unknown for tenant {TenantId}. Performing IAS host lookup.", tenant.TenantId);
        }
        if (_btpTenantApiUri is null)
        {
            throw new DestinationAccessException(
                "Failed to dynamically resolve IAS tenant host: The BTP API URL is not given. "
                + "Ensure your IAS service binding contains the BTP tenant API URL in the property 'btp-tenant-api'.");
        }
        return await _iasTenantHostResolver.ResolveAsync(_btpTenantApiUri, tenant.TenantId, cancellationToken).ConfigureAwait(false);
    }

    private OAuth2TokenService GetTokenService(string? tenantId)
    {
        var key = CacheKey.FromIds(tenantId, null).Append(Identity, _tokenCacheParameters);
        return TokenServiceCache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            entry.RegisterPostEvictionCallback(static (_, value, _, _) => (value as IDisposable)?.Dispose());
            return new OAuth2TokenService(_httpClientFactory(Identity), _tokenCacheParameters);
        })!;
    }

    private static HttpClient DefaultHttpClient(OAuth2ClientIdentity identity) =>
        identity.IsCertificateBased ? new HttpClient(CreateHandler(identity.Certificate), disposeHandler: true) : SharedHttpClient.Value;

    private static SocketsHttpHandler CreateHandler(X509Certificate2? clientCertificate)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5), UseCookies = false };
        if (clientCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = [clientCertificate];
        }
        return handler;
    }

    private static MemoryCache CreateTokenServiceCache()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        CacheManager.Register(cache);
        return cache;
    }

    /// <summary>Builds an <see cref="OAuth2Service"/>.</summary>
    internal sealed class Builder
    {
        internal Uri? TokenUri { get; private set; }

        internal OAuth2ClientIdentity? Identity { get; private set; }

        internal OnBehalfOf OnBehalfOfValue { get; private set; } = OnBehalfOf.TechnicalUserCurrentTenant;

        internal TenantPropagationStrategy Strategy { get; private set; } = TenantPropagationStrategy.ZidHeader;

        internal Dictionary<string, string> AdditionalParameters { get; } = new(StringComparer.Ordinal);

        internal ResilienceConfiguration.TimeLimiterConfiguration TimeLimiter { get; private set; } = OAuth2Options.DefaultTimeout;

        internal TokenCacheParameters CacheParameters { get; private set; } = TokenCacheParameters.Default;

        internal Uri? BtpTenantApiUri { get; private set; }

        internal IasTenantHostResolver? IasResolver { get; private set; }

        internal Func<OAuth2ClientIdentity, HttpClient>? HttpClientFactory { get; private set; }

        /// <summary>Sets the token endpoint. Without a path, XSUAA's <c>/oauth/token</c> is appended.</summary>
        public Builder WithTokenUri(Uri tokenUri)
        {
            TokenUri = string.IsNullOrEmpty(tokenUri.AbsolutePath) || tokenUri.AbsolutePath == "/"
                ? new UriBuilder(tokenUri) { Path = XsuaaTokenPath }.Uri
                : tokenUri;
            return this;
        }

        public Builder WithTokenUri(string tokenUri) => WithTokenUri(new Uri(tokenUri, UriKind.Absolute));

        public Builder WithIdentity(OAuth2ClientIdentity identity)
        {
            Identity = identity;
            return this;
        }

        public Builder WithOnBehalfOf(OnBehalfOf onBehalfOf)
        {
            OnBehalfOfValue = onBehalfOf;
            return this;
        }

        public Builder WithTenantPropagationStrategy(TenantPropagationStrategy strategy)
        {
            Strategy = strategy;
            return this;
        }

        public Builder WithTenantPropagationStrategyFrom(ServiceIdentifier? serviceIdentifier)
        {
            Strategy = ServiceIdentifier.IdentityAuthentication.Equals(serviceIdentifier)
                ? TenantPropagationStrategy.TenantSubdomain
                : TenantPropagationStrategy.ZidHeader;
            return this;
        }

        public Builder WithAdditionalParameters(IReadOnlyDictionary<string, string> parameters)
        {
            foreach (var (key, value) in parameters)
            {
                AdditionalParameters[key] = value;
            }
            return this;
        }

        public Builder WithTimeLimiter(ResilienceConfiguration.TimeLimiterConfiguration timeLimiter)
        {
            TimeLimiter = timeLimiter;
            return this;
        }

        public Builder WithTokenCacheParameters(TokenCacheParameters parameters)
        {
            CacheParameters = parameters;
            return this;
        }

        public Builder WithBtpTenantApiUri(Uri? uri)
        {
            BtpTenantApiUri = uri;
            return this;
        }

        internal Builder WithIasTenantHostResolver(IasTenantHostResolver resolver)
        {
            IasResolver = resolver;
            return this;
        }

        internal Builder WithHttpClientFactory(Func<OAuth2ClientIdentity, HttpClient> factory)
        {
            HttpClientFactory = factory;
            return this;
        }

        public OAuth2Service Build()
        {
            if (TokenUri is null || Identity is null)
            {
                throw new ShouldNotHappenException("Some required parameters for the OAuth2Service are null.");
            }
            return new OAuth2Service(this);
        }
    }
}
