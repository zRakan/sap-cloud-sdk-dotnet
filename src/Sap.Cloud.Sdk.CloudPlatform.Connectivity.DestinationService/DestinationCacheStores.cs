using Microsoft.Extensions.Caching.Memory;
using Sap.Cloud.Sdk.CloudPlatform.Cache;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// A <see cref="MemoryCache"/> of <see cref="CacheKey"/> to value with a fixed size limit and expiration policy.
/// </summary>
/// <typeparam name="TValue">The value type.</typeparam>
/// <remarks>
/// Replaces the Caffeine caches of the Java module. Every entry counts as size 1. With an absolute expiration an entry
/// expires a fixed time after it was written, with a sliding expiration after it was last read or written.
/// </remarks>
internal sealed class ExpiringCache<TValue>
    where TValue : class
{
    private readonly TimeSpan? _expiration;
    private readonly bool _sliding;

    public ExpiringCache(long? sizeLimit, TimeSpan? expiration, bool sliding)
    {
        _expiration = expiration;
        _sliding = sliding;
        Memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
    }

    /// <summary>The underlying cache, registered with the <see cref="CacheManager"/> by <see cref="DestinationService.Cache"/>.</summary>
    public MemoryCache Memory { get; }

    public int Count => Memory.Count;

    public IReadOnlyCollection<CacheKey> Keys => [.. Memory.Keys.OfType<CacheKey>()];

    public TValue? GetIfPresent(CacheKey key) => Memory.TryGetValue(key, out var value) ? value as TValue : null;

    public void Put(CacheKey key, TValue value)
    {
        var options = new MemoryCacheEntryOptions { Size = 1 };
        if (_expiration is { } expiration)
        {
            if (expiration <= TimeSpan.Zero)
            {
                // all entries expire immediately: nothing to store
                return;
            }
            if (_sliding)
            {
                options.SlidingExpiration = expiration;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
        }
        Memory.Set(key, value, options);
    }

    public void InvalidateAll() => Memory.Clear();
}

/// <summary>
/// One <see cref="SemaphoreSlim"/> per cache key, handed out to serialize the computation of the same cache entry.
/// Unused entries are dropped 30 minutes after their last use.
/// </summary>
internal sealed class IsolationLocks
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(30);

    private readonly Lock _gate = new();

    public MemoryCache Memory { get; } = new(new MemoryCacheOptions());

    public IReadOnlyCollection<CacheKey> Keys => [.. Memory.Keys.OfType<CacheKey>()];

    public SemaphoreSlim GetOrCreate(CacheKey key)
    {
        lock (_gate)
        {
            // MemoryCache.GetOrCreate is not atomic; two callers must never end up with different locks.
            if (Memory.TryGetValue(key, out var existing) && existing is SemaphoreSlim semaphore)
            {
                return semaphore;
            }
            var created = new SemaphoreSlim(1, 1);
            Memory.Set(key, created, new MemoryCacheEntryOptions { SlidingExpiration = Expiration });
            return created;
        }
    }

    public SemaphoreSlim? GetIfPresent(CacheKey key) => Memory.TryGetValue(key, out var value) ? value as SemaphoreSlim : null;

    public void Put(CacheKey key, SemaphoreSlim semaphore) =>
        Memory.Set(key, semaphore, new MemoryCacheEntryOptions { SlidingExpiration = Expiration });
}
