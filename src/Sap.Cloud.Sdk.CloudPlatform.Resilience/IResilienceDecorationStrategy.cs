namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>
/// Applies resilience patterns to operations. Replace the strategy through
/// <see cref="ResilienceDecorator.DecorationStrategy"/>.
/// </summary>
/// <remarks>
/// Java's <c>decorateSupplier</c>/<c>decorateCallable</c> pairs collapse into one synchronous and one asynchronous
/// shape; <c>queueCallable</c>/<c>queueSupplier</c> (run on the SDK's executor) are not ported, use
/// <see cref="ResilienceDecorator.ExecuteAsync{T}"/> or <c>Task.Run</c>.
/// </remarks>
public interface IResilienceDecorationStrategy
{
    /// <summary>Removes the cache entries of the current isolation and the configured parameters.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <exception cref="NotSupportedException">If the strategy does not implement caching.</exception>
    void ClearCache(ResilienceConfiguration configuration) =>
        throw new NotSupportedException("No implementation provided.");

    /// <summary>Removes the cache entries selected by a filter.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="filter">The filter.</param>
    /// <exception cref="NotSupportedException">If the strategy does not implement caching.</exception>
    void ClearCache(ResilienceConfiguration configuration, CacheFilter filter) =>
        throw new NotSupportedException("No implementation provided.");

    /// <summary>Removes all cache entries of the configuration, for all tenants and principals.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <exception cref="NotSupportedException">If the strategy does not implement caching.</exception>
    void ClearAllCacheEntries(ResilienceConfiguration configuration) =>
        throw new NotSupportedException("No implementation provided.");

    /// <summary>Decorates an asynchronous operation.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation. It must observe the token to be cancellable by timeouts.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <returns>The decorated operation.</returns>
    Func<CancellationToken, Task<T>> DecorateAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback);

    /// <summary>Decorates a synchronous operation.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation. It must observe the token to be cancellable by timeouts.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="fallback">Produces a result from the failure; <see langword="null"/> for none.</param>
    /// <returns>The decorated operation.</returns>
    Func<CancellationToken, T> Decorate<T>(
        Func<CancellationToken, T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback);
}
