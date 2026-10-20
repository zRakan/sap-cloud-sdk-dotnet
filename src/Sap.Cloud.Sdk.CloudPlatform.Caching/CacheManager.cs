using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache;

/// <summary>
/// Keeps track of registered caches keyed by <see cref="CacheKey"/> and invalidates entries by tenant or principal.
/// </summary>
/// <remarks>
/// Caffeine caches are replaced by <see cref="MemoryCache"/> instances (enumerating keys requires the concrete type).
/// </remarks>
public static class CacheManager
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(CacheManager).FullName!);
    private static readonly Lock Gate = new();
    private static MemoryCache[] _caches = [];

    /// <summary>The registered caches.</summary>
    /// <returns>A snapshot of the registered caches.</returns>
    public static IReadOnlyList<MemoryCache> GetCacheList() => Volatile.Read(ref _caches);

    /// <summary>Registers a cache.</summary>
    /// <param name="cache">The cache.</param>
    /// <returns>The cache.</returns>
    public static MemoryCache Register(MemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        lock (Gate)
        {
            _caches = [.. _caches, cache];
        }
        return cache;
    }

    /// <summary>Unregisters a cache.</summary>
    /// <param name="cache">The cache.</param>
    /// <returns>The cache.</returns>
    public static MemoryCache Unregister(MemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        lock (Gate)
        {
            _caches = _caches.Where(c => !ReferenceEquals(c, cache)).ToArray();
        }
        return cache;
    }

    /// <summary>Removes expired entries from all registered caches.</summary>
    public static void CleanUp()
    {
        foreach (var cache in GetCacheList())
        {
            cache.Compact(0);
        }
        Log.LogInformation("Clean up of invalidated caches finished successfully.");
    }

    /// <summary>Removes all entries from all registered caches.</summary>
    /// <returns>The approximate number of removed entries.</returns>
    public static long InvalidateAll()
    {
        long size = 0;
        var caches = GetCacheList();
        foreach (var cache in caches)
        {
            size += cache.Count;
            cache.Clear();
        }
        if (Log.IsEnabled(LogLevel.Information))
        {
            Log.LogInformation("Successfully invalidated roughly {Size} entries in {Count} caches.", size, caches.Count);
        }
        return size;
    }

    /// <summary>Removes the entries of the current tenant from all registered caches.</summary>
    /// <returns>The number of removed entries; zero if there is no current tenant.</returns>
    public static long InvalidateTenantCaches()
    {
        var tenant = TenantAccessor.TryGetCurrentTenant();
        if (tenant.IsSuccess)
        {
            return InvalidateTenantCaches(tenant.GetValueOrThrow().TenantId);
        }
        Log.LogDebug(tenant.Exception, "Cache could not be invalidated for tenant.");
        return 0;
    }

    /// <summary>Removes the entries of the given tenant from all registered caches.</summary>
    /// <param name="tenantId">The tenant identifier; <see langword="null"/> matches keys without tenant.</param>
    /// <returns>The number of removed entries.</returns>
    public static long InvalidateTenantCaches(string? tenantId)
    {
        long size = 0;
        foreach (var cache in GetCacheList())
        {
            size += RemoveMatching(cache, key => string.Equals(tenantId, key.TenantId, StringComparison.Ordinal));
        }
        if (Log.IsEnabled(LogLevel.Information))
        {
            Log.LogInformation("Successfully invalidated caches of tenant '{TenantId}': {Size}.", tenantId, size);
        }
        return size;
    }

    /// <summary>Removes the entries of the current principal within the current tenant from all registered caches.</summary>
    /// <returns>The number of removed entries; zero if there is no current tenant or principal.</returns>
    public static long InvalidatePrincipalCaches()
    {
        var tenant = TenantAccessor.TryGetCurrentTenant();
        var principal = PrincipalAccessor.TryGetCurrentPrincipal();
        return tenant.IsSuccess && principal.IsSuccess
            ? InvalidatePrincipalCaches(tenant.GetValueOrThrow().TenantId, principal.GetValueOrThrow().PrincipalId)
            : 0;
    }

    /// <summary>Removes the entries of the given principal within the given tenant from all registered caches.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="principalId">The principal identifier.</param>
    /// <returns>The number of removed entries.</returns>
    public static long InvalidatePrincipalCaches(string? tenantId, string? principalId)
    {
        long size = 0;
        foreach (var cache in GetCacheList())
        {
            size += InvalidatePrincipalEntries(tenantId, principalId, cache);
        }
        if (Log.IsEnabled(LogLevel.Information))
        {
            Log.LogInformation(
                "Successfully invalidated caches of principal {PrincipalId} within tenant {TenantId}: {Size}.",
                principalId, tenantId, size);
        }
        return size;
    }

    /// <summary>Removes the entries of the current principal within the current tenant from one cache.</summary>
    /// <param name="cache">The cache.</param>
    /// <returns>The number of removed entries; zero if there is no current tenant or principal.</returns>
    public static long InvalidatePrincipalEntries(MemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var tenant = TenantAccessor.TryGetCurrentTenant();
        tenant.OnFailure(e => Log.LogDebug(e, "Cache could not be invalidated for tenant."));
        var principal = PrincipalAccessor.TryGetCurrentPrincipal();
        principal.OnFailure(e => Log.LogDebug(e, "Cache could not be invalidated for principal."));
        return tenant.IsSuccess && principal.IsSuccess
            ? InvalidatePrincipalEntries(tenant.GetValueOrThrow().TenantId, principal.GetValueOrThrow().PrincipalId, cache)
            : 0;
    }

    /// <summary>Removes the entries of the given principal within the given tenant from one cache.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="principalId">The principal identifier.</param>
    /// <param name="cache">The cache.</param>
    /// <returns>The number of removed entries.</returns>
    public static long InvalidatePrincipalEntries(string? tenantId, string? principalId, MemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        return RemoveMatching(cache, key =>
            string.Equals(tenantId, key.TenantId, StringComparison.Ordinal)
            && string.Equals(principalId, key.PrincipalId, StringComparison.Ordinal));
    }

    private static long RemoveMatching(MemoryCache cache, Func<CacheKey, bool> predicate)
    {
        var keys = cache.Keys.OfType<CacheKey>().Where(predicate).ToList();
        foreach (var key in keys)
        {
            cache.Remove(key);
        }
        return keys.Count;
    }
}
