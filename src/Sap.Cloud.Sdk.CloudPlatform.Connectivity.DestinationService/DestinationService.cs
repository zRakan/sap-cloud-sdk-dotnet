using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Retrieves destinations from the SAP BTP destination service on Cloud Foundry.
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>DestinationService</c>. The service registers itself as a secondary loader of
/// <see cref="DestinationAccessor"/> (after the environment variable loader). The destination service is reached over
/// HTTP, so <see cref="TryGetDestinationAsync"/> is the primary API; the synchronous <see cref="TryGetDestination"/> blocks
/// the calling thread on it, as the synchronous header providers of the OAuth module do. The same holds for
/// <see cref="GetAllDestinationProperties()"/> and its siblings.
/// </para>
/// <para>
/// Retrieved destinations are cached (see <see cref="Cache"/>) per tenant, and per principal for destinations that depend on
/// the user token. By default, a cached destination is re-validated against the list of all destinations ("change
/// detection"). Concurrent requests for the same destination result in one call to the destination service. Failures are
/// never cached.
/// </para>
/// <para>
/// Calls to the destination service are protected by a time limiter (6 seconds by default) and a circuit breaker, isolated
/// per tenant.
/// </para>
/// </remarks>
public sealed class DestinationService : IDestinationLoader
{
    internal const string PathDefault = "/v1/destinations/";
    internal const string PathV2 = "/v2/destinations/";
    private const string PathServiceInstance = "/v1/instanceDestinations";
    private const string PathSubaccount = "/v1/subaccountDestinations";

    internal static readonly ResilienceConfiguration.TimeLimiterConfiguration DefaultTimeLimiter =
        ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(6));

    internal static readonly ResilienceConfiguration.CircuitBreakerConfiguration DefaultSingleDestCircuitBreaker =
        ResilienceConfiguration.CircuitBreakerConfiguration.Of() with { WaitDuration = TimeSpan.FromSeconds(6) };

    internal static readonly ResilienceConfiguration.CircuitBreakerConfiguration DefaultAllDestCircuitBreaker =
        ResilienceConfiguration.CircuitBreakerConfiguration.Of() with { WaitDuration = TimeSpan.FromMinutes(1) };

    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationService>();
    private static readonly Lazy<DestinationService> DefaultInstance = new(() => new DestinationService());

    private readonly IDestinationServiceAdapter _adapter;

    /// <summary>Creates an instance with all default settings.</summary>
    public DestinationService()
        : this(new DestinationServiceAdapter())
    {
    }

    /// <summary>Creates an instance with a specific adapter and default resilience.</summary>
    internal DestinationService(IDestinationServiceAdapter adapter)
        : this(
            adapter,
            CreateResilienceConfiguration("singleDestResilience", DefaultTimeLimiter, DefaultSingleDestCircuitBreaker),
            CreateResilienceConfiguration("allDestResilience", DefaultTimeLimiter, DefaultAllDestCircuitBreaker))
    {
    }

    internal DestinationService(
        IDestinationServiceAdapter adapter,
        ResilienceConfiguration singleDestResilience,
        ResilienceConfiguration allDestResilience)
    {
        _adapter = adapter;
        SingleDestResilience = singleDestResilience;
        AllDestResilience = allDestResilience;
    }

    internal ResilienceConfiguration SingleDestResilience { get; }

    internal ResilienceConfiguration AllDestResilience { get; }

    /// <summary>
    /// Registers a default instance as secondary loader of <see cref="DestinationAccessor"/> (after the environment variable
    /// loader). This happens automatically when the assembly is loaded; call it explicitly if nothing else of this assembly is
    /// referenced by the application, because the compiler omits assemblies that are not referenced from code. Calling it more
    /// than once has no further effect.
    /// </summary>
    public static void EnsureRegisteredAsDefaultLoader() => DestinationAccessor.RegisterDefaultLoader(DefaultInstance.Value);

    /// <summary>Creates a builder for a customized instance.</summary>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder() => new();

    internal static ResilienceConfiguration CreateResilienceConfiguration(
        string identifier,
        ResilienceConfiguration.TimeLimiterConfiguration timeLimiterConfiguration,
        ResilienceConfiguration.CircuitBreakerConfiguration circuitBreakerConfiguration) =>
        ResilienceConfiguration.Of(typeof(DestinationService).FullName + identifier) with
        {
            IsolationMode = ResilienceIsolationMode.TenantOptional,
            TimeLimiter = timeLimiterConfiguration,
            CircuitBreaker = circuitBreakerConfiguration,
        };

    /// <summary>
    /// Looks up a destination. Blocks the calling thread; prefer <see cref="TryGetDestinationAsync"/>.
    /// </summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Options; see <see cref="DestinationServiceOptionsAugmenter"/>.</param>
    /// <returns>The destination or the reason why it could not be loaded.</returns>
    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options) =>
        TryGetDestinationAsync(destinationName, options).GetAwaiter().GetResult();

    /// <summary>Looks up a destination in the destination service, or in the destination cache.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Options; see <see cref="DestinationServiceOptionsAugmenter"/>.</param>
    /// <param name="cancellationToken">Cancels the operation. A cancellation is thrown, not returned as failure.</param>
    /// <returns>
    /// The destination, or the failure: a <see cref="DestinationNotFoundException"/> if the destination does not exist,
    /// otherwise a <see cref="DestinationAccessException"/>.
    /// </returns>
    public async Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            var validationError = await ValidateDestinationLookupAsync(destinationName, options, cancellationToken).ConfigureAwait(false);
            if (validationError is not null)
            {
                return Result.Failure<IDestination>(validationError);
            }
        }
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return Result.Failure<IDestination>(e);
        }
        return await Cache.GetOrComputeDestinationAsync(this, destinationName, options, LoadAndParseDestinationAsync, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the properties of all destinations from the destination service on behalf of the current tenant.
    /// Blocks the calling thread; prefer <see cref="GetAllDestinationPropertiesAsync(CancellationToken)"/>.
    /// </summary>
    /// <returns>The destination properties.</returns>
    /// <inheritdoc cref="GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy, CancellationToken)" path="/remarks"/>
    public IReadOnlyList<IDestinationProperties> GetAllDestinationProperties() =>
        GetAllDestinationProperties(DestinationServiceRetrievalStrategy.CurrentTenant);

    /// <summary>
    /// Fetches the properties of all destinations from the destination service. Blocks the calling thread; prefer
    /// <see cref="GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy, CancellationToken)"/>.
    /// </summary>
    /// <param name="retrievalStrategy">The strategy for loading destinations in a multi-tenant application.</param>
    /// <returns>The destination properties.</returns>
    /// <inheritdoc cref="GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy, CancellationToken)" path="/remarks"/>
    public IReadOnlyList<IDestinationProperties> GetAllDestinationProperties(DestinationServiceRetrievalStrategy retrievalStrategy) =>
        GetAllDestinationPropertiesAsync(retrievalStrategy).GetAwaiter().GetResult();

    /// <summary>Fetches the properties of all destinations from the destination service on behalf of the current tenant.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination properties.</returns>
    /// <inheritdoc cref="GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy, CancellationToken)" path="/remarks"/>
    public Task<IReadOnlyList<IDestinationProperties>> GetAllDestinationPropertiesAsync(CancellationToken cancellationToken = default) =>
        GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy.CurrentTenant, cancellationToken);

    /// <summary>Fetches the properties of all destinations from the destination service.</summary>
    /// <param name="retrievalStrategy">The strategy for loading destinations in a multi-tenant application.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination properties.</returns>
    /// <remarks>
    /// <strong>Caution: this does not perform any authorization flows for the destinations.</strong> Destinations obtained
    /// this way should only be used for accessing the properties of the destination configuration. For full destination
    /// objects use <see cref="TryGetDestinationAsync"/>. If a destination with the same name exists on service instance and
    /// on subaccount level, the one at service instance level takes precedence. The result is cached like destinations.
    /// </remarks>
    /// <exception cref="DestinationAccessException">The destinations cannot be loaded.</exception>
    public async Task<IReadOnlyList<IDestinationProperties>> GetAllDestinationPropertiesAsync(
        DestinationServiceRetrievalStrategy retrievalStrategy,
        CancellationToken cancellationToken = default)
    {
        var options = DestinationOptions.CreateBuilder()
            .AugmentBuilder(DestinationServiceOptionsAugmenter.Augmenter().RetrievalStrategy(retrievalStrategy))
            .Build();
        var result = await Cache.GetOrComputeAllDestinationsAsync(options, GetAllDestinationsByRetrievalStrategyAsync, cancellationToken)
            .ConfigureAwait(false);
        return [.. result.GetValueOrThrow()];
    }

    /// <summary>
    /// Fetches the properties of one destination on behalf of the current tenant. Blocks the calling thread; prefer
    /// <see cref="GetDestinationPropertiesAsync"/>.
    /// </summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <returns>The destination properties.</returns>
    /// <inheritdoc cref="GetDestinationPropertiesAsync" path="/remarks"/>
    public IDestinationProperties GetDestinationProperties(string destinationName) =>
        GetDestinationPropertiesAsync(destinationName).GetAwaiter().GetResult();

    /// <summary>Fetches the properties of one destination on behalf of the current tenant.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination properties.</returns>
    /// <remarks>
    /// <strong>Caution: this does not perform any authorization flows for the destination.</strong> Destinations obtained
    /// this way should only be used for accessing the properties of the destination configuration. For full destination
    /// objects use <see cref="TryGetDestinationAsync"/>.
    /// </remarks>
    /// <exception cref="DestinationNotFoundException">There is no such destination.</exception>
    public async Task<IDestinationProperties> GetDestinationPropertiesAsync(
        string destinationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        var all = await GetAllDestinationPropertiesAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(d => d.TryGet(DestinationProperty.Name, out var name) && name == destinationName)
            ?? throw new DestinationNotFoundException(destinationName, "Destination " + destinationName + " not found.");
    }

    /// <summary>
    /// Fetches all destinations as generic destinations, without performing authorization flows. Prefer
    /// <see cref="GetAllDestinationPropertiesAsync(DestinationServiceRetrievalStrategy, CancellationToken)"/>.
    /// </summary>
    /// <param name="options">Options; see <see cref="DestinationServiceOptionsAugmenter"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destinations or the failure.</returns>
    /// <remarks>
    /// Port of the deprecated <c>tryGetAllDestinations(DestinationOptions)</c>. Destinations with the same name on service
    /// instance level take precedence over the ones on subaccount level.
    /// </remarks>
    public async Task<Result<IReadOnlyList<IDestination>>> TryGetAllDestinationsAsync(
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await Cache.GetOrComputeAllDestinationsAsync(options, GetAllDestinationsByRetrievalStrategyAsync, cancellationToken)
            .ConfigureAwait(false);
        return result.Map<IReadOnlyList<IDestination>>(list => [.. list.Select(ToDestination)]);
    }

    private static DefaultDestination ToDestination(IDestinationProperties properties)
    {
        if (properties is DefaultDestination destination)
        {
            return destination;
        }
        var builder = DefaultDestination.CreateBuilder();
        foreach (var name in properties.PropertyNames)
        {
            if (properties.GetProperty(name) is { } value)
            {
                builder.Property(name, value);
            }
        }
        return builder.Build();
    }

    internal async Task<Exception?> ValidateDestinationLookupAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken)
    {
        if (!Cache.IsEnabled || !Cache.PreLookupCheckEnabled)
        {
            return null;
        }
        if (Cache.IsUsingExperimentalFeatures(options))
        {
            Log.LogWarning(
                "Using pre-lookup check together with either fragments, cross-level options, or custom headers might lead to unexpected behaviour. Pre-lookup check is skipped.");
            return null;
        }

        var strategy = DestinationServiceOptionsAugmenter.GetRetrievalStrategy(options) ?? DestinationServiceRetrievalStrategy.CurrentTenant;
        var cachedDestinations = await GetAllDestinationPropertiesAsync(strategy, cancellationToken).ConfigureAwait(false);
        if (cachedDestinations.Any(p => p.TryGet(DestinationProperty.Name, out var name) && name == destinationName))
        {
            return null;
        }

        return new DestinationNotFoundException(
            destinationName,
            $"Destination {destinationName} was not found among the destinations for {strategy.GetIdentifier()}.");
    }

    internal async Task<IDestination> LoadAndParseDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken)
    {
        var crossLevelScope = DestinationServiceOptionsAugmenter.GetCrossLevelScope(options);
        var servicePath = crossLevelScope is { } scope
            ? PathV2 + destinationName + scope.GetSuffix()
            : PathDefault + destinationName;

        var resolver = DestinationRetrievalStrategyResolver.ForSingleDestination(
            _adapter.GetProviderTenantId,
            (strategy, token) => ResilientCallAsync(
                innerToken => RetrieveDestinationAsync(strategy, servicePath, innerToken),
                SingleDestResilience,
                token));

        var retrieval = resolver.PrepareSupplier(options);
        var response = await retrieval.GetAsync(cancellationToken).ConfigureAwait(false);
        return DestinationServiceFactory.FromDestinationServiceV1Response(response, retrieval.OnBehalfOf);
    }

    internal async Task<DestinationServiceV1Response> RetrieveDestinationAsync(
        DestinationRetrievalStrategy strategy,
        string servicePath,
        CancellationToken cancellationToken)
    {
        var response = await _adapter.GetConfigurationAsJsonAsync(servicePath, strategy, cancellationToken).ConfigureAwait(false);
        try
        {
            return DestinationServiceV1Response.Parse(response);
        }
        catch (JsonException e)
        {
            Log.LogDebug("Failed to parse destination response payload.");
            throw new DestinationAccessException("Illegal JSON response while fetching a destination from destination service", e);
        }
    }

    private async Task<Result<IReadOnlyList<IDestinationProperties>>> GetAllDestinationsByRetrievalStrategyAsync(
        DestinationOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolver = DestinationRetrievalStrategyResolver.ForAllDestinations(
                _adapter.GetProviderTenantId,
                LoadAndParseAllDestinationsAsync);
            var supplier = resolver.PrepareSupplierAllDestinations(options);
            return Result.Success<IReadOnlyList<IDestinationProperties>>(await supplier(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return Result.Failure<IReadOnlyList<IDestinationProperties>>(e);
        }
    }

    private async Task<List<IDestinationProperties>> LoadAndParseAllDestinationsAsync(
        OnBehalfOf behalf,
        CancellationToken cancellationToken)
    {
        // Priority list:
        // * Service Instance Destinations of same name > Subaccount Destinations of same name
        var serviceInstanceDestinations = await ResilientCallAsync(
            token => GetAndDeserializeDestinationsAsync(PathServiceInstance, behalf, token),
            AllDestResilience,
            cancellationToken).ConfigureAwait(false);
        var subAccountDestinations = await ResilientCallAsync(
            token => GetAndDeserializeDestinationsAsync(PathSubaccount, behalf, token),
            AllDestResilience,
            cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<string, IDestinationProperties>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var destination in serviceInstanceDestinations.Concat(subAccountDestinations))
        {
            destination.TryGet(DestinationProperty.Name, out var name);
            if (result.TryAdd(name!, destination))
            {
                order.Add(name!);
            }
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Loaded {Count} destinations: {Names}", result.Count, string.Join(", ", order));
        }
        return [.. order.Select(name => result[name])];
    }

    private async Task<List<IDestinationProperties>> GetAndDeserializeDestinationsAsync(
        string servicePath,
        OnBehalfOf behalf,
        CancellationToken cancellationToken)
    {
        var json = await _adapter.GetConfigurationAsJsonAsync(
            servicePath,
            DestinationRetrievalStrategy.WithoutToken(behalf),
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The destination service did not respond with a JSON array.");
        }
        List<IDestinationProperties> destinations = [];
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The destination service responded with a destination that is not a JSON object.");
            }
            Dictionary<string, object> properties = [];
            foreach (var property in element.EnumerateObject())
            {
                var text = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };
                if (text is not null)
                {
                    properties[property.Name] = text;
                }
            }
            // It is important that we build the generic default destination here: building HTTP destinations would
            // trigger on-premise proxy header auth flows, which we don't want for only computing the destination properties.
            var destination = DefaultDestination.FromMap(properties).Build();
            if (!destination.TryGet(DestinationProperty.Name, out _))
            {
                throw new DestinationAccessException(
                    "Found a destination without name. A name is required for destinations defined in the destination service.");
            }
            destinations.Add(destination);
        }
        return destinations;
    }

    /// <summary>
    /// Runs an operation with the resilience configuration. Failures are reported as the cause: a
    /// <see cref="DestinationNotFoundException"/> or a <see cref="DestinationAccessException"/>, hiding the resilience exception.
    /// </summary>
    private static async Task<T> ResilientCallAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ResilienceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ResilienceDecorator.ExecuteAsync(operation, configuration, null, cancellationToken).ConfigureAwait(false);
        }
        catch (ResilienceRuntimeException e)
        {
            for (Exception? cause = e; cause is not null; cause = cause.InnerException)
            {
                if (cause is DestinationNotFoundException)
                {
                    throw new DestinationNotFoundException(null, cause.Message, e);
                }
            }
            throw new DestinationAccessException("Failed to get destination.", e);
        }
    }

    /// <summary>Builds <see cref="DestinationService"/> instances.</summary>
    public sealed class Builder
    {
        private ResilienceConfiguration.TimeLimiterConfiguration? _timeLimiterConfiguration;
        private string? _providerTenantId;

        internal Builder()
        {
        }

        /// <summary>Applies the given timeout when retrieving destinations.</summary>
        /// <param name="timeLimiterConfiguration">
        /// The time limiter configuration, for example <c>TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(10))</c> or
        /// <c>TimeLimiterConfiguration.Disabled()</c>.
        /// </param>
        /// <returns>This builder.</returns>
        public Builder WithTimeLimiterConfiguration(ResilienceConfiguration.TimeLimiterConfiguration timeLimiterConfiguration)
        {
            ArgumentNullException.ThrowIfNull(timeLimiterConfiguration);
            _timeLimiterConfiguration = timeLimiterConfiguration;
            return this;
        }

        internal Builder WithProviderTenant(ITenant providerTenant)
        {
            _providerTenantId = providerTenant.TenantId;
            return this;
        }

        /// <summary>Creates the configured instance.</summary>
        /// <returns>The new instance.</returns>
        public DestinationService Build()
        {
            var timeLimiter = _timeLimiterConfiguration ?? DefaultTimeLimiter;
            return new DestinationService(
                new DestinationServiceAdapter(null, null, _providerTenantId),
                CreateResilienceConfiguration("singleDestResilience", timeLimiter, DefaultSingleDestCircuitBreaker),
                CreateResilienceConfiguration("allDestResilience", timeLimiter, DefaultAllDestCircuitBreaker));
        }
    }

    /// <summary>
    /// Configures the caches of the destination service: the cache of single destinations, the cache of the lists of all
    /// destinations (used for change detection and <c>GetAllDestinationProperties</c>), and the locks that serialize
    /// concurrent computations of the same entry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cache keys are isolated by tenant (and principal where the destination depends on the user token), see
    /// <see cref="GetOrComputeSingleDestinationCommand"/>; a destination of one tenant is never returned for another.
    /// </para>
    /// <para>
    /// <strong>Caution:</strong> the configuration methods re-create the caches, so all existing entries are lost. Configure the
    /// cache once at application startup.
    /// </para>
    /// <para>
    /// Differences from Java: the caches are <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/>s; a size limit
    /// rejects new entries when full (instead of evicting eagerly), and <c>EnableChangeDetection</c> is not marked obsolete.
    /// </para>
    /// </remarks>
    public static class Cache
    {
        /// <summary>The default size limit of the cache of single destinations.</summary>
        public const long DefaultSizeLimit = 1000L;

        /// <summary>The default expiration duration of the destination cache.</summary>
        public static readonly TimeSpan DefaultExpirationDuration = TimeSpan.FromMinutes(5);

        /// <summary>The default <see cref="CacheExpirationStrategy"/> of the destination cache.</summary>
        public const CacheExpirationStrategy DefaultExpirationStrategy = CacheExpirationStrategy.WhenCreated;

        private static readonly Lock Gate = new();
        private static readonly string[] ExperimentalFeatureNames =
        [
            DestinationServiceOptionsAugmenter.XFragmentKey,
            DestinationServiceOptionsAugmenter.CrossLevelSettingKey,
            DestinationServiceOptionsAugmenter.CustomHeaderKey,
        ];

        private static long? _sizeLimit = DefaultSizeLimit;
        private static TimeSpan? _expirationDuration = DefaultExpirationDuration;
        private static CacheExpirationStrategy _expirationStrategy = DefaultExpirationStrategy;
        private static volatile ExpiringCache<IDestination>? _destinationsCache;
        private static volatile ExpiringCache<IReadOnlyList<IDestinationProperties>>? _allDestinationsCache;
        private static volatile IsolationLocks? _isolationLocks;
        private static volatile bool _cacheEnabled = true;
        private static volatile bool _changeDetectionEnabled = true;
        private static volatile bool _preLookupCheckEnabled = true;

#pragma warning disable CA1810 // The caches depend on the field initializers above; an explicit constructor keeps the order obvious.
        static Cache()
#pragma warning restore CA1810
        {
            RecreateSingleCache();
            RecreateGetAllCache();
            RecreateIsolationLockCache();
        }

        internal static bool IsEnabled => _cacheEnabled;

        internal static bool IsChangeDetectionEnabled => _changeDetectionEnabled;

        internal static bool PreLookupCheckEnabled => _preLookupCheckEnabled;

        internal static TimeSpan? ExpirationDuration => _expirationDuration;

        /// <summary>
        /// Disables checking whether a destination exists (among all destinations of the tenant) before trying to retrieve
        /// it directly. All available destinations can be found with <c>GetAllDestinationProperties</c>.
        /// </summary>
        public static void DisablePreLookupCheck() => _preLookupCheckEnabled = false;

        internal static ExpiringCache<IDestination> InstanceSingle()
        {
            ThrowIfDisabled();
            return _destinationsCache!;
        }

        internal static ExpiringCache<IReadOnlyList<IDestinationProperties>> InstanceAll()
        {
            ThrowIfDisabled();
            return _allDestinationsCache!;
        }

        internal static IsolationLocks GetIsolationLocks()
        {
            ThrowIfDisabled();
            return _isolationLocks!;
        }

        /// <summary>
        /// Disables the entire destination cache and deletes its content. Destinations are then always fetched from the
        /// destination service, which might be slow.
        /// </summary>
        public static void Disable()
        {
            lock (Gate)
            {
                Log.LogDebug("Disabling the destination cache.");
                _cacheEnabled = false;
                Discard(_destinationsCache);
                Discard(_allDestinationsCache);
                _destinationsCache = null;
                _allDestinationsCache = null;
                if (_isolationLocks is { } locks)
                {
                    CacheManager.Unregister(locks.Memory);
                    locks.Memory.Clear();
                }
                _isolationLocks = null;
            }
        }

        /// <summary>Restores the default cache configuration with empty caches. Intended for tests.</summary>
        internal static void Reset()
        {
            lock (Gate)
            {
                Log.LogWarning("Resetting the destination cache. This should not be done outside of testing.");
                _cacheEnabled = true;
                _changeDetectionEnabled = true;
                _preLookupCheckEnabled = true;

                _sizeLimit = DefaultSizeLimit;
                _expirationDuration = DefaultExpirationDuration;
                _expirationStrategy = DefaultExpirationStrategy;
                RecreateSingleCache();
                RecreateGetAllCache();
                RecreateIsolationLockCache();
            }
        }

        /// <summary>Sets the size limit of the cache of single destinations.</summary>
        /// <param name="size">
        /// The size limit. Zero effectively disables the cache, as nothing is stored; consider <see cref="Disable"/> instead.
        /// To allow an infinite number of entries use <see cref="DisableSizeLimit"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">The cache is disabled.</exception>
        public static void SetSizeLimit(long size)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            lock (Gate)
            {
                ThrowIfDisabled();
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Setting destination cache size limit to {Size}.", size);
                }
                _sizeLimit = size;
                RecreateSingleCache();
            }
        }

        /// <summary>
        /// Disables the size limit of the cache of single destinations. Entries can still be removed with
        /// <see cref="CacheManager.InvalidateAll"/>. An unbounded cache might lead to memory exhaustion.
        /// </summary>
        /// <exception cref="InvalidOperationException">The cache is disabled.</exception>
        public static void DisableSizeLimit()
        {
            lock (Gate)
            {
                ThrowIfDisabled();
                Log.LogDebug("Disabling destination cache size limit. The cache now stores an infinite number of entries.");
                _sizeLimit = null;
                RecreateSingleCache();
            }
        }

        /// <summary>Sets the expiration duration and strategy of the destination cache.</summary>
        /// <param name="duration">
        /// The expiration duration. With change detection enabled, this is the interval in which the cache checks for
        /// changes, and the strategy only applies to the cache of all destinations. A zero duration effectively disables the
        /// cache; consider <see cref="Disable"/> instead. To store entries forever use <see cref="DisableExpiration"/>.
        /// </param>
        /// <param name="strategy">The expiration strategy.</param>
        /// <exception cref="InvalidOperationException">The cache is disabled.</exception>
        public static void SetExpiration(TimeSpan duration, CacheExpirationStrategy strategy)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
            lock (Gate)
            {
                ThrowIfDisabled();
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Setting destination cache expiration to {Duration} {Strategy}.", duration, strategy);
                }
                _expirationDuration = duration;
                _expirationStrategy = strategy;
                RecreateSingleCache();
                RecreateGetAllCache();
            }
        }

        /// <summary>
        /// Disables the automatic entry expiration of the destination cache. Destinations are still fetched again if their
        /// attached token has expired. Changes of the destination configuration (for example in the BTP cockpit) are not
        /// picked up while the destination is cached. Change detection is not supported without expiration and is disabled.
        /// </summary>
        /// <exception cref="InvalidOperationException">The cache is disabled.</exception>
        public static void DisableExpiration()
        {
            lock (Gate)
            {
                ThrowIfDisabled();
                Log.LogDebug("Destination cache expiration has been disabled. The cache will now keep its entries until evicted (potentially forever).");
                _expirationDuration = null;

                if (_changeDetectionEnabled)
                {
                    Log.LogWarning(
                        "Using the 'change detection' mode is not supported when disabling the Destination cache expiration. "
                        + "Therefore, change detection mode will be disabled from now on.");
                    DisableChangeDetection();
                    return;
                }

                RecreateSingleCache();
                RecreateGetAllCache();
            }
        }

        /// <summary>
        /// Enables the "change detection" mode (the default). Destinations are cached for a longer period and the cache regularly
        /// checks for updates in the destination service with one cheap request that is valid for all destinations. A
        /// destination is fetched again if (A) an authentication token is expired, (B) it has been updated in the destination
        /// service, or (C) it references a certificate (certificate changes cannot be detected, so such destinations are
        /// cached only for the change detection interval, see <see cref="SetExpiration"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">The cache is disabled.</exception>
        public static void EnableChangeDetection()
        {
            lock (Gate)
            {
                ThrowIfDisabled();
                if (_changeDetectionEnabled)
                {
                    return;
                }

                _changeDetectionEnabled = true;
                Log.LogDebug("Destination change detection has been enabled.");

                if (_expirationDuration is null)
                {
                    Log.LogWarning(
                        "Using the 'change detection' mode is not supported with disabled Destination cache expiration. "
                        + "Therefore, the default expiration ({Duration} {Strategy}) will be restored.",
                        DefaultExpirationDuration,
                        DefaultExpirationStrategy);
                    _expirationDuration = DefaultExpirationDuration;
                    _expirationStrategy = DefaultExpirationStrategy;
                }
                _allDestinationsCache = Replace(
                    new ExpiringCache<IReadOnlyList<IDestinationProperties>>(null, _expirationDuration, sliding: false),
                    _allDestinationsCache);
                _destinationsCache = Replace(
                    new ExpiringCache<IDestination>(_sizeLimit, TimeSpan.FromDays(1), IsSliding(_expirationStrategy)),
                    _destinationsCache);
            }
        }

        /// <summary>Disables the "change detection" mode. The caches are re-created.</summary>
        /// <exception cref="InvalidOperationException">The cache is disabled.</exception>
        public static void DisableChangeDetection()
        {
            lock (Gate)
            {
                ThrowIfDisabled();
                if (!_changeDetectionEnabled)
                {
                    return;
                }

                _changeDetectionEnabled = false;
                Log.LogDebug("Destination change detection has been disabled.");

                RecreateSingleCache();
                RecreateGetAllCache();
            }
        }

        internal static bool IsUsingExperimentalFeatures(DestinationOptions options) =>
            options.OptionKeys.Any(key => ExperimentalFeatureNames.Any(name => key.StartsWith(name, StringComparison.Ordinal)));

        internal static async Task<Result<IDestination>> GetOrComputeDestinationAsync(
            DestinationService loader,
            string destinationName,
            DestinationOptions options,
            Func<string, DestinationOptions, CancellationToken, Task<IDestination>> destinationDownloader,
            CancellationToken cancellationToken)
        {
            if (!_cacheEnabled)
            {
                try
                {
                    return Result.Success(await destinationDownloader(destinationName, options, cancellationToken).ConfigureAwait(false));
                }
                catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
                {
                    return Result.Failure<IDestination>(e);
                }
            }

            GetOrComputeAllDestinationsCommand? getAllCommand;
            if (_changeDetectionEnabled)
            {
                if (IsUsingExperimentalFeatures(options))
                {
                    Log.LogWarning(
                        "Using change detection together with either fragments, cross-level options, or custom headers is discouraged and might lead to unexpected caching behaviour.");
                }
                getAllCommand = GetOrComputeAllDestinationsCommand.PrepareCommand(
                    options,
                    InstanceAll(),
                    GetIsolationLocks(),
                    loader.GetAllDestinationsByRetrievalStrategyAsync);
            }
            else
            {
                getAllCommand = null;
            }

            var command = GetOrComputeSingleDestinationCommand.PrepareCommand(
                destinationName,
                options,
                InstanceSingle(),
                GetIsolationLocks(),
                destinationDownloader,
                getAllCommand);
            return command.IsFailure
                ? Result.Failure<IDestination>(command.Exception)
                : await command.GetValueOrThrow().ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<Result<IReadOnlyList<IDestinationProperties>>> GetOrComputeAllDestinationsAsync(
            DestinationOptions options,
            Func<DestinationOptions, CancellationToken, Task<Result<IReadOnlyList<IDestinationProperties>>>> destinationDownloader,
            CancellationToken cancellationToken)
        {
            if (!_cacheEnabled)
            {
                return await destinationDownloader(options, cancellationToken).ConfigureAwait(false);
            }

            if (IsUsingExperimentalFeatures(options))
            {
                Log.LogWarning(
                    "Using caching together with either fragments, cross-level options, or custom headers is discouraged and might lead to unexpected caching behaviour.");
            }

            return await GetOrComputeAllDestinationsCommand
                .PrepareCommand(options, InstanceAll(), GetIsolationLocks(), destinationDownloader)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private static bool IsSliding(CacheExpirationStrategy strategy) => strategy switch
        {
            CacheExpirationStrategy.WhenCreated or CacheExpirationStrategy.WhenLastModified => false,
            CacheExpirationStrategy.WhenLastAccessed or CacheExpirationStrategy.WhenLastTouched => true,
            _ => throw new InvalidOperationException($"Unhandled '{nameof(CacheExpirationStrategy)}': {strategy}."),
        };

        private static void RecreateSingleCache()
        {
            if (!_changeDetectionEnabled)
            {
                _destinationsCache = Replace(
                    new ExpiringCache<IDestination>(_sizeLimit, _expirationDuration, IsSliding(_expirationStrategy)),
                    _destinationsCache);
                return;
            }
            if (_expirationDuration is null)
            {
                Log.LogWarning(
                    "Using the 'change detection' mode is not supported with disabled Destination cache expiration. "
                    + "Therefore, the default expiration strategy ({Strategy}) will be restored.",
                    DefaultExpirationStrategy);
                _expirationStrategy = DefaultExpirationStrategy;
            }
            _destinationsCache = Replace(
                new ExpiringCache<IDestination>(_sizeLimit, TimeSpan.FromDays(1), IsSliding(_expirationStrategy)),
                _destinationsCache);
        }

        private static void RecreateGetAllCache()
        {
            if (!_changeDetectionEnabled)
            {
                _allDestinationsCache = Replace(
                    new ExpiringCache<IReadOnlyList<IDestinationProperties>>(null, _expirationDuration, IsSliding(_expirationStrategy)),
                    _allDestinationsCache);
                return;
            }
            if (_expirationDuration is null)
            {
                Log.LogWarning(
                    "Using the 'change detection' mode is not supported with disabled Destination cache expiration. "
                    + "Therefore, the default expiration duration ({Duration}) will be restored.",
                    DefaultExpirationDuration);
                _expirationDuration = DefaultExpirationDuration;
            }
            _allDestinationsCache = Replace(
                new ExpiringCache<IReadOnlyList<IDestinationProperties>>(null, _expirationDuration, sliding: false),
                _allDestinationsCache);
        }

        private static void RecreateIsolationLockCache()
        {
            var existing = _isolationLocks;
            if (existing is not null)
            {
                CacheManager.Unregister(existing.Memory);
                existing.Memory.Clear();
            }
            var created = new IsolationLocks();
            CacheManager.Register(created.Memory);
            _isolationLocks = created;
        }

        private static ExpiringCache<T> Replace<T>(ExpiringCache<T> created, ExpiringCache<T>? existing)
            where T : class
        {
            Discard(existing);
            CacheManager.Register(created.Memory);
            return created;
        }

        private static void Discard<T>(ExpiringCache<T>? cache)
            where T : class
        {
            if (cache is null)
            {
                return;
            }
            if (cache.Count > 0)
            {
                Log.LogWarning(
                    "The destination cache is changed even though there are already entries within the cache. "
                    + "Those entries will be deleted, which might result in performance degradation. "
                    + "Consider configuring the cache only once at application startup to avoid this issue.");
            }
            CacheManager.Unregister(cache.Memory);
            cache.InvalidateAll();
        }

        private static void ThrowIfDisabled()
        {
            if (!_cacheEnabled)
            {
                throw new InvalidOperationException("Attempted to access or configure the cache after disabling it.");
            }
        }
    }
}
