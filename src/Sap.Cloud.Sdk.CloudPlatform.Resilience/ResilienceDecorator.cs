namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>
/// Entry point for applying resilience patterns (timeout, retry, circuit breaker, bulkhead, rate limiter and
/// caching) to operations, using <see cref="DecorationStrategy"/>.
/// </summary>
/// <remarks>
/// Differences to Java: the operations are asynchronous first (<c>ExecuteAsync</c>, with a
/// <see cref="CancellationToken"/> that the timeout cancels); <c>Callable</c> and <c>Supplier</c> collapse into
/// <c>Func</c>; the default strategy is <see cref="PollyResilienceDecorationStrategy"/> instead of a
/// <c>ServiceLoader</c> lookup; <c>queueCallable</c>/<c>queueSupplier</c> are not ported.
/// </remarks>
public static class ResilienceDecorator
{
    private static readonly Lock Gate = new();
    private static IResilienceDecorationStrategy? _decorationStrategy;

    /// <summary>
    /// The strategy that decorates operations. Defaults to a shared <see cref="PollyResilienceDecorationStrategy"/>;
    /// setting <see langword="null"/> restores a new default instance (and with it, empty resilience state).
    /// </summary>
    public static IResilienceDecorationStrategy DecorationStrategy
    {
        get
        {
            lock (Gate)
            {
                return _decorationStrategy ??= new PollyResilienceDecorationStrategy();
            }
        }
        set
        {
            lock (Gate)
            {
                _decorationStrategy = value;
            }
        }
    }

    /// <summary>Removes the cache entries of the current isolation and the configured cache parameters.</summary>
    /// <param name="configuration">The configuration.</param>
    public static void ClearCache(ResilienceConfiguration configuration) =>
        DecorationStrategy.ClearCache(Require(configuration));

    /// <summary>Removes the cache entries selected by <paramref name="filter"/>.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="filter">The filter.</param>
    public static void ClearCache(ResilienceConfiguration configuration, CacheFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        DecorationStrategy.ClearCache(Require(configuration), filter);
    }

    /// <summary>Removes all cache entries of the configuration, for all tenants and principals.</summary>
    /// <param name="configuration">The configuration.</param>
    public static void ClearAllCacheEntries(ResilienceConfiguration configuration) =>
        DecorationStrategy.ClearAllCacheEntries(Require(configuration));

    /// <summary>Decorates an asynchronous operation. The isolation (tenant, principal) is determined now.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation; it should observe the token.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <returns>The decorated operation.</returns>
    /// <exception cref="Exceptions.ResilienceRuntimeException">If a required tenant or principal is missing.</exception>
    public static Func<CancellationToken, Task<T>> DecorateAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return DecorationStrategy.DecorateAsync(operation, Require(configuration), fallback);
    }

    /// <inheritdoc cref="DecorateAsync{T}(Func{CancellationToken, Task{T}}, ResilienceConfiguration, Func{Exception, T}?)"/>
    public static Func<CancellationToken, Task> DecorateAsync(
        Func<CancellationToken, Task> operation,
        ResilienceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var decorated = DecorateAsync<object?>(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return null;
        }, configuration);
        return decorated;
    }

    /// <summary>Decorates a synchronous operation. The isolation (tenant, principal) is determined now.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation; it should observe the token.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <returns>The decorated operation.</returns>
    /// <exception cref="Exceptions.ResilienceRuntimeException">If a required tenant or principal is missing.</exception>
    public static Func<CancellationToken, T> Decorate<T>(
        Func<CancellationToken, T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return DecorationStrategy.Decorate(operation, Require(configuration), fallback);
    }

    /// <summary>
    /// Decorates a synchronous operation that cannot observe cancellation. A configured timeout cannot interrupt it.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <returns>The decorated operation.</returns>
    public static Func<T> Decorate<T>(
        Func<T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var decorated = Decorate<T>(_ => operation(), configuration, fallback);
        return () => decorated(CancellationToken.None);
    }

    /// <summary>Decorates a synchronous operation without result.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The decorated operation.</returns>
    public static Action Decorate(Action operation, ResilienceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var decorated = Decorate<object?>(
            () =>
            {
                operation();
                return null;
            },
            configuration);
        return () => decorated();
    }

    /// <summary>Decorates and runs an asynchronous operation.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation; it should observe the token.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the whole execution; the exception is not wrapped.</param>
    /// <returns>The result of the operation or fallback.</returns>
    /// <exception cref="Exceptions.ResilienceRuntimeException">
    /// If the operation fails and no fallback handles it. The original failure is the inner exception.
    /// </exception>
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback = null,
        CancellationToken cancellationToken = default)
    {
        var decorated = DecorateAsync(operation, configuration, fallback);
        return await decorated(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, ResilienceConfiguration, Func{Exception, T}?, CancellationToken)"/>
    public static async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        ResilienceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var decorated = DecorateAsync(operation, configuration);
        await decorated(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Decorates and runs a synchronous operation.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <returns>The result of the operation or fallback.</returns>
    /// <exception cref="Exceptions.ResilienceRuntimeException">
    /// If the operation fails and no fallback handles it. The original failure is the inner exception.
    /// </exception>
    public static T Execute<T>(
        Func<T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback = null) =>
        Decorate(operation, configuration, fallback)();

    /// <summary>Decorates and runs a synchronous operation that observes a cancellation token.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation; the timeout cancels its token.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the whole execution; the exception is not wrapped.</param>
    /// <returns>The result of the operation or fallback.</returns>
    public static T Execute<T>(
        Func<CancellationToken, T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback,
        CancellationToken cancellationToken) =>
        Decorate(operation, configuration, fallback)(cancellationToken);

    /// <summary>Decorates and runs a synchronous operation without result.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="configuration">The configuration.</param>
    public static void Execute(Action operation, ResilienceConfiguration configuration) =>
        Decorate(operation, configuration)();

    private static ResilienceConfiguration Require(ResilienceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration;
    }
}
