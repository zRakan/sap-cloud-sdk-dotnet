using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>
/// Applies resilience patterns with Polly. Replaces the resilience4j based <c>Resilience4jDecorationStrategy</c>.
/// </summary>
/// <remarks>
/// <para>
/// Layering is identical to Java (outermost first): retry, cache, circuit breaker, rate limiter, timeout, bulkhead.
/// Circuit breaker, rate limiter and bulkhead state is kept per configuration identifier and
/// <see cref="ResilienceIsolationKey"/>, i.e. two tenants never share a circuit breaker or bulkhead (in modes that
/// isolate by tenant). State is created when a configuration is first used and replaced if the configuration for
/// that identifier changes. Pipelines for tenants that are no longer used are not evicted (as in Java).
/// </para>
/// <para>
/// Every failure that is not handled by a fallback is wrapped in a <see cref="ResilienceRuntimeException"/>
/// (inner exception: the original failure; for an open circuit the last failure that opened it). A
/// <see cref="OperationCanceledException"/> caused by the caller's token is never wrapped, retried or passed to the
/// fallback. Timeouts surface as <see cref="TimeoutRejectedException"/>.
/// </para>
/// <para>
/// Differences to resilience4j: the circuit breaker uses Polly's time-based sampling window (a count-based window is
/// not available) with a minimum of 2 calls and a minimum break duration of 500 ms; slow-call detection and the
/// half-open buffer size are not applied (Polly probes with a single call).
/// </para>
/// </remarks>
public sealed class PollyResilienceDecorationStrategy : IResilienceDecorationStrategy, IDisposable
{
    /// <summary>The sampling window of the circuit breaker.</summary>
    internal static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan MinimumBreakDuration = TimeSpan.FromMilliseconds(500);
    private static readonly ILogger Log = SdkLogging.CreateLogger<PollyResilienceDecorationStrategy>();

    private readonly ConcurrentDictionary<(string Identifier, ResilienceIsolationKey Isolation), PipelineSet> _pipelines = new();
    private readonly ConcurrentDictionary<string, CacheHolder> _caches = new();
    // Serializes the computation of a cached value per cache key, so the operation runs once per key.
    private readonly MemoryCache _locks = new(new MemoryCacheOptions());
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        _locks.Dispose();
        foreach (var holder in _caches.Values)
        {
            holder.Cache.Dispose();
        }
        _caches.Clear();
        _pipelines.Clear();
    }

    /// <inheritdoc/>
    public void ClearCache(ResilienceConfiguration configuration) =>
        ClearCache(
            configuration,
            CacheFilters.And(CacheFilters.KeyMatchesTenant(), CacheFilters.KeyMatchesPrincipal(), CacheFilters.KeyMatchesParameters()));

    /// <inheritdoc/>
    public void ClearCache(ResilienceConfiguration configuration, CacheFilter filter)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(filter);
        if (!_caches.TryGetValue(configuration.Identifier, out var holder))
        {
            return;
        }

        var keysToClear = new List<CacheKey>();
        foreach (var key in holder.Cache.Keys.OfType<CacheKey>())
        {
            if (holder.Cache.TryGetValue(key, out var value) && filter(configuration, key, value))
            {
                keysToClear.Add(key);
            }
        }
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Removing {Count} entries from the cache with configuration id \"{Identifier}\".",
                keysToClear.Count,
                configuration.Identifier);
        }
        foreach (var key in keysToClear)
        {
            holder.Cache.Remove(key);
        }
    }

    /// <inheritdoc/>
    public void ClearAllCacheEntries(ResilienceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (_caches.TryGetValue(configuration.Identifier, out var holder))
        {
            holder.Cache.Clear();
        }
    }

    /// <inheritdoc/>
    public Func<CancellationToken, Task<T>> DecorateAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);
        var pipelines = GetPipelines(configuration);

        async Task<T> InnerAsync(CancellationToken ct)
        {
            try
            {
                return await pipelines.Inner.ExecuteAsync(
                    async innerCt => await operation(innerCt).ConfigureAwait(false), ct).ConfigureAwait(false);
            }
            catch (BrokenCircuitException e)
            {
                throw ToCircuitOpenException(configuration, e);
            }
        }

        return async ct =>
        {
            try
            {
                return await pipelines.Outer.ExecuteAsync(
                    async outerCt => await CachedAsync(configuration, InnerAsync, outerCt).ConfigureAwait(false), ct).ConfigureAwait(false);
            }
            catch (Exception e) when (!IsCallerCancellation(e, ct))
            {
                return HandleFailure(e, fallback);
            }
        };
    }

    /// <inheritdoc/>
    public Func<CancellationToken, T> Decorate<T>(
        Func<CancellationToken, T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);
        var pipelines = GetPipelines(configuration);

        T Inner(CancellationToken ct)
        {
            try
            {
                return pipelines.Inner.Execute(operation, ct);
            }
            catch (BrokenCircuitException e)
            {
                throw ToCircuitOpenException(configuration, e);
            }
        }

        return ct =>
        {
            try
            {
                return pipelines.Outer.Execute(outerCt => Cached(configuration, Inner, outerCt), ct);
            }
            catch (Exception e) when (!IsCallerCancellation(e, ct))
            {
                return HandleFailure(e, fallback);
            }
        };
    }

    private static bool IsCallerCancellation(Exception exception, CancellationToken token) =>
        exception is OperationCanceledException && token.IsCancellationRequested;

    private static T HandleFailure<T>(Exception failure, Func<Exception, T>? fallback)
    {
        if (fallback is not null)
        {
            try
            {
                return fallback(failure);
            }
            catch (Exception fallbackFailure) when (fallbackFailure is not OperationCanceledException)
            {
                throw Wrap(fallbackFailure);
            }
        }
        if (failure is OperationCanceledException)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        throw Wrap(failure);
    }

    private static ResilienceRuntimeException Wrap(Exception failure) =>
        failure as ResilienceRuntimeException ?? new ResilienceRuntimeException(failure.Message, failure);

    private static ResilienceRuntimeException ToCircuitOpenException(
        ResilienceConfiguration configuration,
        BrokenCircuitException exception)
    {
        var message = $"CircuitBreaker '{configuration.Identifier}' is OPEN and does not permit further calls";
        var lastFailure = exception.InnerException;
        return lastFailure is null
            ? new ResilienceRuntimeException(message, exception)
            : new ResilienceRuntimeException($"{message}. Triggered by {lastFailure.Message}", lastFailure);
    }

    // ---------------------------------------------------------------- pipelines

    private PipelineSet GetPipelines(ResilienceConfiguration configuration)
    {
        var isolationKey = ResilienceIsolationKey.Of(configuration.IsolationMode);
        var key = (configuration.Identifier, isolationKey);
        var signature = new PipelineSignature(
            configuration.CircuitBreaker,
            configuration.Bulkhead,
            configuration.RateLimiter,
            configuration.TimeLimiter,
            configuration.Retry);

        if (_pipelines.TryGetValue(key, out var existing) && existing.Signature == signature)
        {
            return existing;
        }
        lock (_gate)
        {
            if (_pipelines.TryGetValue(key, out existing) && existing.Signature == signature)
            {
                return existing;
            }
            if (existing is not null && Log.IsEnabled(LogLevel.Information))
            {
                Log.LogInformation(
                    "ResilienceConfiguration {Identifier}: Replacing resilience state since a new configuration was detected.",
                    configuration.Identifier);
            }
            var created = CreatePipelines(configuration, signature);
            _pipelines[key] = created;
            return created;
        }
    }

    private static PipelineSet CreatePipelines(ResilienceConfiguration configuration, PipelineSignature signature)
    {
        var name = configuration.Identifier;

        // Outermost first: circuit breaker, rate limiter, timeout, bulkhead (the inverse of Java's wrapping order).
        var inner = new ResiliencePipelineBuilder();
        var circuitBreaker = configuration.CircuitBreaker;
        if (circuitBreaker.IsEnabled)
        {
            inner.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                Name = name,
                FailureRatio = Math.Clamp(circuitBreaker.FailureRateThreshold / 100.0, 0.001, 1.0),
                MinimumThroughput = Math.Max(2, circuitBreaker.ClosedBufferSize),
                SamplingDuration = CircuitBreakerSamplingDuration,
                BreakDuration = circuitBreaker.WaitDuration < MinimumBreakDuration ? MinimumBreakDuration : circuitBreaker.WaitDuration,
                ShouldHandle = args => ValueTask.FromResult(IsFailure(args.Outcome.Exception, args.Context)),
            });
        }

        var rateLimiter = configuration.RateLimiter;
        if (rateLimiter.IsEnabled)
        {
            var options = new RateLimiterStrategyOptions
            {
                Name = name,
                LimitForPeriod = rateLimiter.LimitForPeriod,
                LimitRefreshPeriod = rateLimiter.LimitRefreshPeriod,
                TimeoutDuration = rateLimiter.TimeoutDuration,
            };
            inner.AddStrategy(_ => new RateLimiterStrategy(name, options), options);
        }

        var timeLimiter = configuration.TimeLimiter;
        if (timeLimiter.IsEnabled)
        {
            inner.AddTimeout(new TimeoutStrategyOptions { Name = name, Timeout = timeLimiter.TimeoutDuration });
        }

        var bulkhead = configuration.Bulkhead;
        if (bulkhead.IsEnabled)
        {
            var options = new BulkheadStrategyOptions
            {
                Name = name,
                MaxConcurrentCalls = bulkhead.MaxConcurrentCalls,
                MaxWaitDuration = bulkhead.MaxWaitDuration,
            };
            inner.AddStrategy(_ => new BulkheadStrategy(name, options), options);
        }

        var outer = new ResiliencePipelineBuilder();
        var retry = configuration.Retry;
        if (retry.IsEnabled && retry.MaxAttempts > 1)
        {
            var predicate = retry.RetryOnExceptionPredicate;
            outer.AddRetry(new RetryStrategyOptions
            {
                Name = name,
                MaxRetryAttempts = retry.MaxAttempts - 1,
                Delay = retry.WaitDuration,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                ShouldHandle = args => ValueTask.FromResult(
                    IsFailure(args.Outcome.Exception, args.Context) && predicate(args.Outcome.Exception!)),
            });
        }

        return new PipelineSet(signature, outer.Build(), inner.Build());
    }

    private static bool IsFailure(Exception? exception, ResilienceContext context) =>
        exception is not null
        && !(exception is OperationCanceledException && context.CancellationToken.IsCancellationRequested);

    private sealed record PipelineSignature(
        ResilienceConfiguration.CircuitBreakerConfiguration CircuitBreaker,
        ResilienceConfiguration.BulkheadConfiguration Bulkhead,
        ResilienceConfiguration.RateLimiterConfiguration RateLimiter,
        ResilienceConfiguration.TimeLimiterConfiguration TimeLimiter,
        ResilienceConfiguration.RetryConfiguration Retry);

    private sealed record PipelineSet(PipelineSignature Signature, ResiliencePipeline Outer, ResiliencePipeline Inner);

    // ---------------------------------------------------------------- caching

    private async Task<T> CachedAsync<T>(
        ResilienceConfiguration configuration,
        Func<CancellationToken, Task<T>> inner,
        CancellationToken cancellationToken)
    {
        var holder = GetCacheHolder(configuration);
        if (holder is null)
        {
            return await inner(cancellationToken).ConfigureAwait(false);
        }

        var dataKey = DetermineCacheKey(configuration, includeIdentifier: false);
        var gate = GetLock(DetermineCacheKey(configuration, includeIdentifier: true));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (holder.Cache.TryGetValue(dataKey, out var cached) && cached is T cachedValue)
            {
                return cachedValue;
            }
            var value = await inner(cancellationToken).ConfigureAwait(false);
            holder.Store(dataKey, value);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private T Cached<T>(ResilienceConfiguration configuration, Func<CancellationToken, T> inner, CancellationToken cancellationToken)
    {
        var holder = GetCacheHolder(configuration);
        if (holder is null)
        {
            return inner(cancellationToken);
        }

        var dataKey = DetermineCacheKey(configuration, includeIdentifier: false);
        var gate = GetLock(DetermineCacheKey(configuration, includeIdentifier: true));
        gate.Wait(cancellationToken);
        try
        {
            if (holder.Cache.TryGetValue(dataKey, out var cached) && cached is T cachedValue)
            {
                return cachedValue;
            }
            var value = inner(cancellationToken);
            holder.Store(dataKey, value);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private static CacheKey DetermineCacheKey(ResilienceConfiguration configuration, bool includeIdentifier)
    {
        var isolation = ResilienceIsolationKey.Of(configuration.IsolationMode);
        var key = CacheKey.Of(isolation.Tenant, isolation.Principal).Append(configuration.Cache.Parameters);
        return includeIdentifier ? key.Append(configuration.Identifier) : key;
    }

    private SemaphoreSlim GetLock(CacheKey lockKey) =>
        _locks.GetOrCreate(lockKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return new SemaphoreSlim(1, 1);
        })!;

    private CacheHolder? GetCacheHolder(ResilienceConfiguration configuration)
    {
        var cacheConfiguration = configuration.Cache;
        if (!cacheConfiguration.IsEnabled)
        {
            return null;
        }
        var holder = _caches.AddOrUpdate(
            configuration.Identifier,
            _ => new CacheHolder(cacheConfiguration),
            (identifier, current) =>
            {
                if (current.Matches(cacheConfiguration))
                {
                    return current;
                }
                if (Log.IsEnabled(LogLevel.Information))
                {
                    Log.LogInformation(
                        "ResilienceConfiguration: {Identifier}: Destroying cache since a new cache configuration was detected.",
                        identifier);
                }
                current.Cache.Clear();
                return new CacheHolder(cacheConfiguration);
            });
        // A duration of zero or less expires entries immediately, so there is nothing to look up.
        return holder.ExpirationDuration > TimeSpan.Zero ? holder : null;
    }

    private sealed class CacheHolder
    {
        private readonly CacheExpirationStrategy _strategy;

        public CacheHolder(ResilienceConfiguration.CacheConfiguration configuration)
        {
            ExpirationDuration = configuration.ExpirationDuration;
            _strategy = configuration.ExpirationStrategy;
            Cache = new MemoryCache(new MemoryCacheOptions());
        }

        public MemoryCache Cache { get; }

        public TimeSpan ExpirationDuration { get; }

        public bool Matches(ResilienceConfiguration.CacheConfiguration configuration) =>
            ExpirationDuration == configuration.ExpirationDuration && _strategy == configuration.ExpirationStrategy;

        public void Store<T>(CacheKey key, T value)
        {
            // Null results are not cached (Java would fail with a NullPointerException).
            if (value is null)
            {
                return;
            }
            var options = new MemoryCacheEntryOptions();
            if (_strategy is CacheExpirationStrategy.WhenLastAccessed or CacheExpirationStrategy.WhenLastTouched)
            {
                options.SlidingExpiration = ExpirationDuration;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = ExpirationDuration;
            }
            Cache.Set(key, value, options);
        }
    }
}
