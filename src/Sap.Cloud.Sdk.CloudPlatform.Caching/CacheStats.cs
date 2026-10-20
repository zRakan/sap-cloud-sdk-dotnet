using Microsoft.Extensions.Caching.Memory;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache;

/// <summary>A snapshot of cache statistics.</summary>
/// <remarks>
/// Java's <c>CacheStatsMXBean</c> (JMX registration) is not ported. Statistics that
/// <see cref="MemoryCache"/> does not track are reported as zero.
/// </remarks>
/// <param name="CacheSize">The (estimated) number of entries.</param>
/// <param name="HitCount">The number of cache hits.</param>
/// <param name="MissCount">The number of cache misses.</param>
/// <param name="LoadSuccessCount">The number of successful loads (not tracked, always zero).</param>
/// <param name="LoadExceptionCount">The number of failed loads (not tracked, always zero).</param>
/// <param name="TotalLoadTime">The total load time (not tracked, always zero).</param>
/// <param name="EvictionCount">The number of evictions (not tracked, always zero).</param>
public sealed record CacheStats(
    long CacheSize,
    long HitCount,
    long MissCount,
    long LoadSuccessCount,
    long LoadExceptionCount,
    long TotalLoadTime,
    long EvictionCount)
{
    /// <summary>Creates a snapshot from a memory cache created with <c>TrackStatistics = true</c>.</summary>
    /// <param name="cache">The cache.</param>
    /// <returns>The snapshot. Hit and miss counts are zero if statistics tracking is off.</returns>
    public static CacheStats From(MemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var stats = cache.GetCurrentStatistics();
        return new CacheStats(cache.Count, stats?.TotalHits ?? 0, stats?.TotalMisses ?? 0, 0, 0, 0, 0);
    }
}
