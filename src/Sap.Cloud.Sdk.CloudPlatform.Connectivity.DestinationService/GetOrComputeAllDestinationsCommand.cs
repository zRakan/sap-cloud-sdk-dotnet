using Sap.Cloud.Sdk.CloudPlatform.Cache;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Gets the list of all destinations of a tenant from the cache or computes it. Concurrent callers for the same cache key
/// (tenant and retrieval strategy) wait for one computation instead of all calling the destination service; a failed
/// computation is returned to the caller that ran it and is not cached.
/// </summary>
internal sealed class GetOrComputeAllDestinationsCommand
{
    private readonly CacheKey _cacheKey;
    private readonly SemaphoreSlim _isolationLock;
    private readonly ExpiringCache<IReadOnlyList<IDestinationProperties>> _cache;
    private readonly Func<CancellationToken, Task<Result<IReadOnlyList<IDestinationProperties>>>> _destinationSupplier;

    private GetOrComputeAllDestinationsCommand(
        CacheKey cacheKey,
        SemaphoreSlim isolationLock,
        ExpiringCache<IReadOnlyList<IDestinationProperties>> cache,
        Func<CancellationToken, Task<Result<IReadOnlyList<IDestinationProperties>>>> destinationSupplier)
    {
        _cacheKey = cacheKey;
        _isolationLock = isolationLock;
        _cache = cache;
        _destinationSupplier = destinationSupplier;
    }

    internal CacheKey CacheKey => _cacheKey;

    public static GetOrComputeAllDestinationsCommand PrepareCommand(
        DestinationOptions destinationOptions,
        ExpiringCache<IReadOnlyList<IDestinationProperties>> destinationCache,
        IsolationLocks isolationLocks,
        Func<DestinationOptions, CancellationToken, Task<Result<IReadOnlyList<IDestinationProperties>>>> destinationRetriever)
    {
        var cacheKey = CacheKey.OfTenantOptionalIsolation()
            .Append(DestinationServiceOptionsAugmenter.RetrievalStrategyCacheComponent(destinationOptions));
        var isolationLock = isolationLocks.GetOrCreate(cacheKey);
        return new GetOrComputeAllDestinationsCommand(
            cacheKey,
            isolationLock,
            destinationCache,
            cancellationToken => destinationRetriever(destinationOptions, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<IDestinationProperties>>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var destinations = _cache.GetIfPresent(_cacheKey);
        if (destinations is not null)
        {
            return Result.Success(destinations);
        }

        await _isolationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // double-checked locking
            destinations = _cache.GetIfPresent(_cacheKey);
            if (destinations is not null)
            {
                return Result.Success(destinations);
            }

            var result = await _destinationSupplier(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }
            _cache.Put(_cacheKey, result.GetValueOrThrow());
            return result;
        }
        finally
        {
            _isolationLock.Release();
        }
    }
}
