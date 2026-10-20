using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Tests;

public class CachingTests : ResilienceTestBase
{
    private sealed class Counter
    {
        private int _count;

        public int Next() => Interlocked.Increment(ref _count);

        public int Count => Volatile.Read(ref _count);
    }

    private static ResilienceConfiguration CachedConfig(
        string id,
        ResilienceIsolationMode mode,
        ResilienceConfiguration.CacheConfiguration? cache = null) =>
        ResilienceConfiguration.Of(id) with
        {
            IsolationMode = mode,
            Cache = cache ?? ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(5)).WithoutParameters(),
        };

    [Fact]
    public void Cache_SecondCallReturnsCachedValue()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.basic", ResilienceIsolationMode.NoIsolation);

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task Cache_Async_SecondCallReturnsCachedValue()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.async", ResilienceIsolationMode.NoIsolation);

        Assert.Equal(1, await ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(counter.Next()), config));
        Assert.Equal(1, await ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(counter.Next()), config));
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public void Cache_Disabled_ExecutesEveryTime()
    {
        var counter = new Counter();
        var config = Config("cache.disabled");

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, config));
    }

    [Fact]
    public void Cache_TwoTenants_NeverShareEntries()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.tenants", ResilienceIsolationMode.TenantRequired);

        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public void Cache_TenantAndPrincipalIsolation_SeparatesUsersOfTheSameTenant()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.users", ResilienceIsolationMode.TenantAndUserRequired);

        Assert.Equal(1, InTenantAndPrincipal("T", "alice", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenantAndPrincipal("T", "bob", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(1, InTenantAndPrincipal("T", "alice", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(3, InTenantAndPrincipal("U", "alice", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public void Cache_TenantOnlyIsolation_SharesEntriesBetweenUsersOfTheSameTenant()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.tenant_only", ResilienceIsolationMode.TenantRequired);

        Assert.Equal(1, InTenantAndPrincipal("T", "alice", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(1, InTenantAndPrincipal("T", "bob", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public void Cache_NoIsolation_SharesEntriesAcrossTenants()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.shared", ResilienceIsolationMode.NoIsolation);

        Assert.Equal(1, InTenant("A", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(1, InTenant("B", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public async Task Cache_TwoTenantsInParallel_NeverSeeEachOthersValues()
    {
        var config = CachedConfig("cache.parallel", ResilienceIsolationMode.TenantRequired);

        async Task<string> Run(string tenant) => await InTenantAsync(tenant, () => ResilienceDecorator.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return "value-of-" + TenantAccessor.GetCurrentTenant().TenantId;
        }, config));

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Run("t" + (i % 5))));

        Assert.Equal(Enumerable.Range(0, 40).Select(i => "value-of-t" + (i % 5)), results);
    }

    [Fact]
    public void Cache_DifferentKeyParameters_UseDifferentEntries()
    {
        var counter = new Counter();
        var cacheA = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(5)).WithParameters("a", 1);
        var cacheB = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(5)).WithParameters("a", 2);
        var configA = CachedConfig("cache.params", ResilienceIsolationMode.NoIsolation, cacheA);
        var configB = CachedConfig("cache.params", ResilienceIsolationMode.NoIsolation, cacheB);

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, configA));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, configB));
        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, configA));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, configB));
    }

    [Fact]
    public async Task Cache_Expires_AfterDuration()
    {
        var counter = new Counter();
        var cache = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMilliseconds(200)).WithoutParameters();
        var config = CachedConfig("cache.expiry", ResilienceIsolationMode.NoIsolation, cache);

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
        await Task.Delay(400);

        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, config));
    }

    [Fact]
    public async Task Cache_WhenLastModified_DoesNotExtendLifetimeOnAccess()
    {
        var counter = new Counter();
        var cache = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMilliseconds(500))
            .WithExpirationStrategy(CacheExpirationStrategy.WhenCreated).WithoutParameters();
        var config = CachedConfig("cache.absolute", ResilienceIsolationMode.NoIsolation, cache);

        ResilienceDecorator.Execute(counter.Next, config);
        await Task.Delay(300);
        ResilienceDecorator.Execute(counter.Next, config);
        await Task.Delay(300);

        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, config));
    }

    [Fact]
    public async Task Cache_WhenLastAccessed_ExtendsLifetimeOnAccess()
    {
        var counter = new Counter();
        var cache = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMilliseconds(500))
            .WithExpirationStrategy(CacheExpirationStrategy.WhenLastAccessed).WithoutParameters();
        var config = CachedConfig("cache.sliding", ResilienceIsolationMode.NoIsolation, cache);

        ResilienceDecorator.Execute(counter.Next, config);
        await Task.Delay(300);
        ResilienceDecorator.Execute(counter.Next, config);
        await Task.Delay(300);

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
    }

    [Fact]
    public void Cache_ZeroDuration_NeverStores()
    {
        var counter = new Counter();
        var cache = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.Zero).WithoutParameters();
        var config = CachedConfig("cache.zero", ResilienceIsolationMode.NoIsolation, cache);

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, config));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, config));
    }

    [Fact]
    public void Cache_FailuresAndFallbackResults_AreNotCached()
    {
        var attempts = 0;
        var config = CachedConfig("cache.fallback", ResilienceIsolationMode.NoIsolation);

        Assert.Equal("fallback", ResilienceDecorator.Execute<string>(() =>
        {
            attempts++;
            throw new TestException("x");
        }, config, _ => "fallback"));
        Assert.Equal("real", ResilienceDecorator.Execute(() =>
        {
            attempts++;
            return "real";
        }, config, _ => "fallback"));
        Assert.Equal("real", ResilienceDecorator.Execute<string>(() => throw new InvalidOperationException(), config));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void Cache_NullResults_AreNotCached()
    {
        var calls = 0;
        var config = CachedConfig("cache.null", ResilienceIsolationMode.NoIsolation);

        ResilienceDecorator.Execute<string?>(() =>
        {
            calls++;
            return null;
        }, config);
        ResilienceDecorator.Execute<string?>(() =>
        {
            calls++;
            return null;
        }, config);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Cache_ConcurrentCallsWithSameKey_ComputeOnce()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.lock", ResilienceIsolationMode.NoIsolation);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ResilienceDecorator.ExecuteAsync(async ct =>
        {
            await Task.Delay(100, ct);
            return counter.Next();
        }, config)));

        Assert.All(results, r => Assert.Equal(1, r));
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task Cache_ConcurrentCallsWithDifferentKeys_DoNotBlockEachOther()
    {
        var config1 = CachedConfig("cache.lock.a", ResilienceIsolationMode.NoIsolation);
        var config2 = CachedConfig("cache.lock.b", ResilienceIsolationMode.NoIsolation);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = ResilienceDecorator.ExecuteAsync(async ct =>
        {
            await gate.Task.WaitAsync(ct);
            return 1;
        }, config1);

        Assert.Equal(2, await ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(2), config2).WaitAsync(TimeSpan.FromSeconds(5)));

        gate.SetResult();
        await slow;
    }

    [Fact]
    public void ClearAllCacheEntries_RemovesEntriesOfAllTenants()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.clear_all", ResilienceIsolationMode.TenantRequired);
        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));

        ResilienceDecorator.ClearAllCacheEntries(config);

        Assert.Equal(3, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(4, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public void ClearCache_RemovesOnlyEntriesOfTheCurrentTenant()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.clear_tenant", ResilienceIsolationMode.TenantRequired);
        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));

        InTenant("tenant_1", () =>
        {
            ResilienceDecorator.ClearCache(config);
            return 0;
        });

        Assert.Equal(3, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public void ClearCache_RemovesOnlyEntriesWithMatchingParameters()
    {
        var counter = new Counter();
        var configA = CachedConfig("cache.clear_params", ResilienceIsolationMode.NoIsolation,
            ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(5)).WithParameters("a", "b"));
        var configReordered = CachedConfig("cache.clear_params", ResilienceIsolationMode.NoIsolation,
            ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(5)).WithParameters("b", "a"));
        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, configA));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, configReordered));

        ResilienceDecorator.ClearCache(configA);

        Assert.Equal(3, ResilienceDecorator.Execute(counter.Next, configA));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, configReordered));
    }

    [Fact]
    public void ClearCache_WithFilter_RemovesOnlySelectedEntries()
    {
        var counter = new Counter();
        var config = CachedConfig("cache.clear_filter", ResilienceIsolationMode.TenantRequired);
        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(2, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));

        ResilienceDecorator.ClearCache(config, CacheFilters.KeyMatchesTenant(new DefaultTenant("tenant_2")));

        Assert.Equal(1, InTenant("tenant_1", () => ResilienceDecorator.Execute(counter.Next, config)));
        Assert.Equal(3, InTenant("tenant_2", () => ResilienceDecorator.Execute(counter.Next, config)));
    }

    [Fact]
    public void ClearCache_UnknownConfiguration_DoesNothing()
    {
        ResilienceDecorator.ClearCache(Config("cache.never_used"));
        ResilienceDecorator.ClearAllCacheEntries(Config("cache.never_used"));
    }

    [Fact]
    public void Cache_ConfigurationChange_RecreatesCache()
    {
        var counter = new Counter();
        var shortLived = CachedConfig("cache.recreate", ResilienceIsolationMode.NoIsolation,
            ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(1)).WithoutParameters());
        var longLived = CachedConfig("cache.recreate", ResilienceIsolationMode.NoIsolation,
            ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(2)).WithoutParameters());

        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, shortLived));
        Assert.Equal(1, ResilienceDecorator.Execute(counter.Next, shortLived));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, longLived));
        Assert.Equal(2, ResilienceDecorator.Execute(counter.Next, longLived));
    }

    // ------------------------------------------------------------ cache filters

    private static CacheKey Key(string? tenant, string? principal, params object[] components) =>
        CacheKey.FromIds(tenant, principal).Append(components);

    [Fact]
    public void Filters_MatchTenantAndPrincipalAndParameters()
    {
        var config = ResilienceConfiguration.Of("filters") with
        {
            Cache = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromMinutes(1)).WithParameters("p1", "p2"),
        };
        var tenantFilter = CacheFilters.KeyMatchesTenant(new DefaultTenant("t1"));
        var principalFilter = CacheFilters.KeyMatchesPrincipal(new DefaultPrincipal("u1"));
        var parameterFilter = CacheFilters.KeyMatchesParameters();

        Assert.True(tenantFilter(config, Key("t1", "u1"), null));
        Assert.False(tenantFilter(config, Key("t2", "u1"), null));
        Assert.True(principalFilter(config, Key("t1", "u1"), null));
        Assert.False(principalFilter(config, Key("t1", "u2"), null));
        Assert.True(parameterFilter(config, Key("t1", "u1", "p1", "p2"), null));
        Assert.False(parameterFilter(config, Key("t1", "u1", "p2", "p1"), null));
        Assert.True(CacheFilters.KeyMatchesParameters(["x"])(config, Key(null, null, "x"), null));
        Assert.False(CacheFilters.KeyMatchesParameters(["x"])(config, Key(null, null, "y"), null));
    }

    [Fact]
    public void Filters_DerivedFromCurrentIsolation_UseConfigurationsIsolationMode()
    {
        var config = ResilienceConfiguration.Of("filters.current") with { IsolationMode = ResilienceIsolationMode.TenantAndUserRequired };

        InTenantAndPrincipal("t1", "u1", () =>
        {
            Assert.True(CacheFilters.KeyMatchesTenant()(config, Key("t1", "u1"), null));
            Assert.False(CacheFilters.KeyMatchesTenant()(config, Key("t2", "u1"), null));
            Assert.True(CacheFilters.KeyMatchesPrincipal()(config, Key("t1", "u1"), null));
            Assert.False(CacheFilters.KeyMatchesPrincipal()(config, Key("t1", "u2"), null));
            return 0;
        });
    }

    [Fact]
    public void Filters_AndOr_CombineFilters()
    {
        var config = ResilienceConfiguration.Of("filters.combine");
        CacheFilter yes = (_, _, _) => true;
        CacheFilter no = (_, _, _) => false;
        var key = Key(null, null);

        Assert.True(CacheFilters.And(yes, yes, yes)(config, key, null));
        Assert.False(CacheFilters.And(yes, no, yes)(config, key, null));
        Assert.True(CacheFilters.Or(no, no, yes)(config, key, null));
        Assert.False(CacheFilters.Or(no, no)(config, key, null));
    }

    [Fact]
    public void Filters_ReceiveCachedValue()
    {
        var config = CachedConfig("filters.value", ResilienceIsolationMode.NoIsolation);
        ResilienceDecorator.Execute(() => "cached-value", config);
        object? seen = null;

        ResilienceDecorator.ClearCache(config, (_, _, entry) =>
        {
            seen = entry;
            return false;
        });

        Assert.Equal("cached-value", seen);
    }

    [Fact]
    public void RequiredIsolation_WithoutTenant_FailsInsteadOfSharingTheCache()
    {
        var config = CachedConfig("cache.required", ResilienceIsolationMode.TenantRequired);

        Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(() => 1, config));
    }
}
