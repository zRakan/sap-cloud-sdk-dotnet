using Microsoft.Extensions.Caching.Memory;
using Sap.Cloud.Sdk.CloudPlatform.Cache.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache.Tests;

public class CacheManagerTests : CachingStateReset
{
    private static MemoryCache NewCache() => CacheManager.Register(new MemoryCache(new MemoryCacheOptions()));

    private static void Put(MemoryCache cache, string? tenant, string? principal, string component) =>
        cache.Set(CacheKey.FromIds(tenant, principal).Append(component), "value");

    [Fact]
    public void RegisterAndUnregister_UpdatesList()
    {
        using var cache = NewCache();
        Assert.Single(CacheManager.GetCacheList());

        CacheManager.Unregister(cache);

        Assert.Empty(CacheManager.GetCacheList());
    }

    [Fact]
    public void InvalidateTenantCaches_RemovesOnlyThatTenantsEntries()
    {
        using var cache = NewCache();
        Put(cache, "t1", "u1", "a");
        Put(cache, "t1", null, "b");
        Put(cache, "t2", "u1", "a");
        Put(cache, null, null, "a");

        Assert.Equal(2, CacheManager.InvalidateTenantCaches("t1"));

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGetValue(CacheKey.FromIds("t2", "u1").Append("a"), out _));
        Assert.True(cache.TryGetValue(CacheKey.FromIds(null, null).Append("a"), out _));
    }

    [Fact]
    public void InvalidateTenantCaches_CurrentTenant_UsesTenantFromContext()
    {
        using var cache = NewCache();
        Put(cache, "t1", null, "a");
        Put(cache, "t2", null, "a");

        var removed = WithTenantAndPrincipal("t1", "u1", CacheManager.InvalidateTenantCaches);

        Assert.Equal(1, removed);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void InvalidateTenantCaches_WithoutCurrentTenant_ReturnsZero()
    {
        TenantAccessorWithoutTenant();
        using var cache = NewCache();
        Put(cache, "t1", null, "a");

        Assert.Equal(0, CacheManager.InvalidateTenantCaches());
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void InvalidatePrincipalCaches_RemovesOnlyMatchingTenantAndPrincipal()
    {
        using var cache = NewCache();
        Put(cache, "t1", "u1", "a");
        Put(cache, "t1", "u2", "a");
        Put(cache, "t2", "u1", "a");

        Assert.Equal(1, CacheManager.InvalidatePrincipalCaches("t1", "u1"));

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGetValue(CacheKey.FromIds("t1", "u1").Append("a"), out _));
    }

    [Fact]
    public void InvalidatePrincipalCaches_CurrentContext_UsesContext()
    {
        using var cache = NewCache();
        Put(cache, "t1", "u1", "a");
        Put(cache, "t1", "u2", "a");

        Assert.Equal(1, WithTenantAndPrincipal("t1", "u2", CacheManager.InvalidatePrincipalCaches));
        Assert.Equal(1, WithTenantAndPrincipal("t1", "u1", () => CacheManager.InvalidatePrincipalEntries(cache)));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void InvalidateAll_ClearsAllRegisteredCaches()
    {
        using var first = NewCache();
        using var second = NewCache();
        Put(first, "t1", null, "a");
        Put(second, "t2", null, "a");
        Put(second, "t2", null, "b");

        Assert.Equal(3, CacheManager.InvalidateAll());

        Assert.Equal(0, first.Count + second.Count);
    }

    [Fact]
    public void CacheStats_FromCache_ReportsSizeHitsAndMisses()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { TrackStatistics = true });
        var key = CacheKey.OfNoIsolation().Append("a");
        cache.Set(key, 1);
        cache.TryGetValue(key, out _);
        cache.TryGetValue(CacheKey.OfNoIsolation().Append("missing"), out _);

        var stats = CacheStats.From(cache);

        Assert.Equal(1, stats.CacheSize);
        Assert.Equal(1, stats.HitCount);
        Assert.Equal(1, stats.MissCount);
    }

    [Fact]
    public void CacheUtil_WrapCallable_WrapsFailures()
    {
        var ok = CacheUtil.WrapCallableAsFunction<string, int>(() => 7);
        var failure = new InvalidOperationException("boom");
        var failing = CacheUtil.WrapCallableAsFunction<string, int>(() => throw failure);

        Assert.Equal(7, ok("ignored"));
        var thrown = Assert.Throws<CacheRuntimeException>(() => failing("ignored"));
        Assert.Same(failure, thrown.InnerException);
        Assert.Equal(3, CacheUtil.WrapSupplierAsFunction<string, int>(() => 3)("ignored"));
    }

    private static void TenantAccessorWithoutTenant() =>
        Sap.Cloud.Sdk.CloudPlatform.Tenant.TenantAccessor.TenantFacade = new CacheKeyTests.NoTenantFacade();
}
