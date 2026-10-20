using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// The default <see cref="IDestinationHttpClientFactory"/>. Every client is
/// <c>new HttpClient(DestinationRequestHandler → CsrfTokenHandler → SocketsHttpHandler)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>DefaultApacheHttpClient5Factory</c> merged with <c>DefaultApacheHttpClient5Cache</c>. Only the primary
/// <see cref="SocketsHttpHandler"/> (connection pool, cookies, TLS and proxy configuration) is expensive and is cached.
/// The two delegating handlers are cheap and created per call, which is how Java's <c>withDestination</c> hands the
/// current (equal but not identical) destination instance, with its fresh header providers, to a cached client.
/// </para>
/// <para>
/// Cache key (as in Java): without destination tenant and principal, each only if available. With a destination: the
/// current tenant (if available) and the destination itself (by value equality of
/// <see cref="DefaultHttpDestination"/>). If the destination requires user token exchange or uses principal propagation
/// the tenant and the principal are required and part of the key. Destinations from the destination service are
/// tenant-specific, so two tenants never share a handler (and hence never a cookie jar or client certificate).
/// A destination type without value equality gets one cache entry per instance. Entries expire
/// <see cref="DestinationHttpClientFactoryOptions.CacheDuration"/> after their last use and are registered with
/// <see cref="CacheManager"/>.
/// </para>
/// <para>
/// Disposal: an evicted handler may still be used by clients that obtained it before. It is therefore not disposed on
/// eviction but after a grace period of the request timeout plus one minute, by which time every request that started
/// before the eviction has completed or timed out. Connections of an evicted handler that is never used again are
/// closed by <see cref="DestinationHttpClientFactoryOptions.PooledConnectionLifetime"/> in any case.
/// </para>
/// <para>Clients are meant to be short-lived: obtain one per use from <see cref="HttpClientAccessor"/>.</para>
/// </remarks>
public sealed class DefaultDestinationHttpClientFactory : IDestinationHttpClientFactory, IDisposable
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultDestinationHttpClientFactory>();
    private static readonly Lazy<DefaultDestinationHttpClientFactory> SharedInstance = new(() => new DefaultDestinationHttpClientFactory());

    private static volatile DefaultDestinationHttpClientFactory? _shared;

    private readonly DestinationHttpClientFactoryOptions _options;
    private readonly Func<IHttpDestinationProperties?, HttpMessageHandler> _primaryHandlerFactory;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="DefaultDestinationHttpClientFactory"/> class with default options.</summary>
    public DefaultDestinationHttpClientFactory()
        : this(new DestinationHttpClientFactoryOptions())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DefaultDestinationHttpClientFactory"/> class.</summary>
    /// <param name="options">The options.</param>
    public DefaultDestinationHttpClientFactory(DestinationHttpClientFactoryOptions options)
        : this(options, null)
    {
    }

    internal DefaultDestinationHttpClientFactory(
        DestinationHttpClientFactoryOptions options,
        Func<IHttpDestinationProperties?, HttpMessageHandler>? primaryHandlerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _primaryHandlerFactory = primaryHandlerFactory
            ?? (destination => PrimaryHandlerBuilder.Build(destination, options, CfPlatformCertificateProvider.Default));
        DisposalGracePeriod = options.Timeout + TimeSpan.FromMinutes(1);
        CacheManager.Register(_cache);
    }

    /// <summary>The factory used by <see cref="HttpClientAccessor"/> by default and by the dependency injection extension.</summary>
    internal static DefaultDestinationHttpClientFactory Shared
    {
        get => _shared ?? SharedInstance.Value;
        set => _shared = value;
    }

    internal TimeSpan DisposalGracePeriod { get; set; }

    internal int CachedHandlerCount => _cache.Count;

    internal TimeSpan Timeout => _options.Timeout;

    /// <inheritdoc/>
    public HttpClient CreateHttpClient(IHttpDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        // Validates the destination before a connection pool is created.
        var destinationHandler = new DestinationRequestHandler(destination);
        return CreateClient(destination, destinationHandler);
    }

    /// <inheritdoc/>
    public HttpClient CreateHttpClient() => CreateClient(null, null);

    /// <summary>Gets the cached primary handler for a destination, creating it if necessary.</summary>
    /// <exception cref="HttpClientInstantiationException">The handler cannot be created.</exception>
    internal HttpMessageHandler GetPrimaryHandler(IHttpDestinationProperties? destination)
    {
        CacheKey key;
        try
        {
            key = GetCacheKey(destination);
        }
        catch (Exception e) when (e is Tenant.Exceptions.TenantAccessException or PrincipalAccessException or InvalidOperationException)
        {
            throw new HttpClientInstantiationException("Failed to create cache key for HttpClient", e);
        }
        if (_options.CacheDuration <= TimeSpan.Zero)
        {
            return CreatePrimaryHandler(destination);
        }
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out HttpMessageHandler? cached) && cached is not null)
            {
                return cached;
            }
            Log.LogDebug("HttpClient handler with given cache key is not yet in the cache.");
            var handler = CreatePrimaryHandler(destination);
            var entry = new MemoryCacheEntryOptions { SlidingExpiration = _options.CacheDuration };
            entry.RegisterPostEvictionCallback(OnEvicted);
            _cache.Set(key, handler, entry);
            return handler;
        }
    }

    /// <summary>Removes all cached handlers and unregisters the cache.</summary>
    public void Dispose()
    {
        CacheManager.Unregister(_cache);
        _cache.Clear();
        _cache.Dispose();
    }

    private HttpClient CreateClient(IHttpDestinationProperties? destination, DestinationRequestHandler? destinationHandler)
    {
        var ownsHandler = _options.CacheDuration <= TimeSpan.Zero;
        var primary = GetPrimaryHandler(destination);
        HttpMessageHandler chain = primary;
        if (_options.EnableCsrfTokenHandling)
        {
            chain = new CsrfTokenHandler { InnerHandler = chain };
        }
        if (destinationHandler is not null)
        {
            destinationHandler.InnerHandler = chain;
            chain = destinationHandler;
        }
        var client = new HttpClient(chain, disposeHandler: ownsHandler) { Timeout = _options.Timeout };
        if (destination is not null && destination.Uri.IsAbsoluteUri)
        {
            client.BaseAddress = destination.Uri;
        }
        return client;
    }

    private HttpMessageHandler CreatePrimaryHandler(IHttpDestinationProperties? destination)
    {
        try
        {
            return _primaryHandlerFactory(destination);
        }
        catch (Exception e) when (e is not (OperationCanceledException or HttpClientInstantiationException or DestinationAccessException))
        {
            throw new HttpClientInstantiationException("Failed to create HTTP client connection handler.", e);
        }
    }

    private void OnEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is not HttpMessageHandler handler)
        {
            return;
        }
        var delay = DisposalGracePeriod;
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay).ConfigureAwait(false);
            handler.Dispose();
        });
    }

    private static CacheKey GetCacheKey(IHttpDestinationProperties? destination)
    {
        if (destination is null)
        {
            return CacheKey.OfTenantAndPrincipalOptionalIsolation();
        }
        if (!RequiresPrincipalIsolation(destination))
        {
            return CacheKey.OfTenantOptionalIsolation().Append(destination);
        }
        var tenant = TenantAccessor.TryGetCurrentTenant();
        var principal = PrincipalAccessor.TryGetCurrentPrincipal();
        if (principal.IsFailure)
        {
            throw new InvalidOperationException(
                "The destination requires a principal, but none was found in the current context.",
                principal.Exception);
        }
        if (tenant.IsFailure)
        {
            throw new InvalidOperationException(
                "Tenant and Principal accessors are returning inconsistent results: A principal is defined, but no tenant is defined in the current context.",
                tenant.Exception);
        }
        return CacheKey.Of(tenant.GetValueOrThrow(), principal.GetValueOrThrow()).Append(destination);
    }

    private static bool RequiresPrincipalIsolation(IHttpDestinationProperties destination) =>
        DestinationUtility.RequiresUserTokenExchange(destination)
        || destination.AuthenticationType == AuthenticationType.PrincipalPropagation;
}
