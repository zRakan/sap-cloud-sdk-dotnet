using Sap.Cloud.Sdk.CloudPlatform.Cache.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache;

/// <summary>Helpers for adapting operations to cache loader functions.</summary>
public static class CacheUtil
{
    /// <summary>Wraps a function as a cache loader that ignores the key and wraps failures.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="callable">The operation.</param>
    /// <returns>A function calling <paramref name="callable"/>; its failures are wrapped in <see cref="CacheRuntimeException"/>.</returns>
    public static Func<TKey, TValue> WrapCallableAsFunction<TKey, TValue>(Func<TValue> callable)
    {
        ArgumentNullException.ThrowIfNull(callable);
        return _ =>
        {
            try
            {
                return callable();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw new CacheRuntimeException("Failed to load cache value.", e);
            }
        };
    }

    /// <summary>Wraps a function as a cache loader that ignores the key.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="supplier">The operation.</param>
    /// <returns>A function calling <paramref name="supplier"/>.</returns>
    public static Func<TKey, TValue> WrapSupplierAsFunction<TKey, TValue>(Func<TValue> supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        return _ => supplier();
    }
}
