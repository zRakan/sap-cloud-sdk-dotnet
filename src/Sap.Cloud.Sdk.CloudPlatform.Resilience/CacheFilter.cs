using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>Decides whether a cache entry is selected, for example to be cleared.</summary>
/// <remarks>
/// A delegate instead of Java's functional interface; the static factories live in <see cref="CacheFilters"/>.
/// </remarks>
/// <param name="configuration">The resilience configuration owning the cache.</param>
/// <param name="cacheKey">The key of the entry.</param>
/// <param name="cacheEntry">The cached value.</param>
/// <returns><see langword="true"/> if the entry is selected.</returns>
public delegate bool CacheFilter(ResilienceConfiguration configuration, CacheKey cacheKey, object? cacheEntry);

/// <summary>Factories and combinators for <see cref="CacheFilter"/>.</summary>
public static class CacheFilters
{
    /// <summary>Matches entries of the given tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The filter.</returns>
    public static CacheFilter KeyMatchesTenant(ITenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return (_, cacheKey, _) => string.Equals(cacheKey.TenantId, tenant.TenantId, StringComparison.Ordinal);
    }

    /// <summary>Matches entries whose tenant equals the tenant determined by the configuration's isolation mode.</summary>
    /// <returns>The filter.</returns>
    public static CacheFilter KeyMatchesTenant() => (configuration, cacheKey, _) =>
    {
        var isolationKey = ResilienceIsolationKey.Of(configuration.IsolationMode);
        return string.Equals(cacheKey.TenantId, isolationKey.Tenant?.TenantId, StringComparison.Ordinal);
    };

    /// <summary>Matches entries of the given principal.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The filter.</returns>
    public static CacheFilter KeyMatchesPrincipal(ISecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return (_, cacheKey, _) => string.Equals(cacheKey.PrincipalId, principal.PrincipalId, StringComparison.Ordinal);
    }

    /// <summary>Matches entries whose principal equals the principal determined by the configuration's isolation mode.</summary>
    /// <returns>The filter.</returns>
    public static CacheFilter KeyMatchesPrincipal() => (configuration, cacheKey, _) =>
    {
        var isolationKey = ResilienceIsolationKey.Of(configuration.IsolationMode);
        return string.Equals(cacheKey.PrincipalId, isolationKey.Principal?.PrincipalId, StringComparison.Ordinal);
    };

    /// <summary>Matches entries whose key components equal the parameters of the configuration's cache configuration.</summary>
    /// <returns>The filter.</returns>
    public static CacheFilter KeyMatchesParameters() => (configuration, cacheKey, _) =>
        cacheKey.Components.SequenceEqual(configuration.Cache.Parameters);

    /// <summary>Matches entries whose key components equal the given parameters, in order.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The filter.</returns>
    public static CacheFilter KeyMatchesParameters(IEnumerable<object> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var expected = parameters.ToList();
        return (_, cacheKey, _) => cacheKey.Components.SequenceEqual(expected);
    }

    /// <summary>Matches entries selected by all filters.</summary>
    /// <param name="firstFilter">The first filter.</param>
    /// <param name="additionalFilters">More filters.</param>
    /// <returns>The filter.</returns>
    public static CacheFilter And(CacheFilter firstFilter, params CacheFilter[] additionalFilters)
    {
        ArgumentNullException.ThrowIfNull(firstFilter);
        ArgumentNullException.ThrowIfNull(additionalFilters);
        CacheFilter[] all = [firstFilter, .. additionalFilters];
        return (configuration, cacheKey, entry) => all.All(f => f(configuration, cacheKey, entry));
    }

    /// <summary>Matches entries selected by at least one filter.</summary>
    /// <param name="firstFilter">The first filter.</param>
    /// <param name="additionalFilters">More filters.</param>
    /// <returns>The filter.</returns>
    public static CacheFilter Or(CacheFilter firstFilter, params CacheFilter[] additionalFilters)
    {
        ArgumentNullException.ThrowIfNull(firstFilter);
        ArgumentNullException.ThrowIfNull(additionalFilters);
        CacheFilter[] all = [firstFilter, .. additionalFilters];
        return (configuration, cacheKey, entry) => all.Any(f => f(configuration, cacheKey, entry));
    }
}
