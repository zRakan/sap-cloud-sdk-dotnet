using System.Collections;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Gets a single destination from the cache or computes it, honoring the isolation that the token exchange strategy
/// requires. Concurrent callers for the same lock key wait for one computation; failures are returned and never cached.
/// </summary>
/// <remarks>
/// <para>
/// How the token exchange strategy affects the isolation (lock) key and the cache key; "tenant" means the current tenant if
/// there is one, "principal" the current principal:
/// </para>
/// <code>
/// | #  | Token exchange strategy      | Destination type    | Isolation key        | Cache key            | Comment                    |
/// | 1  | LookupOnly                   | user propagation    | tenant               | tenant               | the destination is unusable |
/// | 2  | LookupThenExchange, JWT      | user propagation    | tenant               | tenant + principal   |                            |
/// | 2  | LookupThenExchange, no JWT   | user propagation    | tenant               | -                    | execution fails            |
/// | 3  | ExchangeOnly                 | user propagation    | tenant + principal   | tenant + principal   |                            |
/// | 4  | ForwardUserToken, JWT        | user propagation    | tenant               | tenant + principal   |                            |
/// | 4  | ForwardUserToken, no JWT     | user propagation    | tenant               | -                    | execution fails            |
/// | 6  | LookupOnly                   | client credentials  | tenant               | tenant               |                            |
/// | 7  | LookupThenExchange           | client credentials  | tenant               | tenant               |                            |
/// | 8  | ExchangeOnly                 | client credentials  | tenant + principal   | tenant + principal   | warning logged             |
/// | 9  | ForwardUserToken             | client credentials  | tenant               | tenant               |                            |
/// </code>
/// </remarks>
internal sealed class GetOrComputeSingleDestinationCommand
{
    private static readonly TimeSpan ExpirationBufferTime = TimeSpan.FromSeconds(10);
    private static readonly ILogger Log = SdkLogging.CreateLogger<GetOrComputeSingleDestinationCommand>();

    private readonly string _destinationName;
    private readonly SemaphoreSlim _isolationLock;
    private readonly ExpiringCache<IDestination> _destinationCache;
    private readonly Func<CancellationToken, Task<IDestination>> _destinationSupplier;
    private readonly GetOrComputeAllDestinationsCommand? _getAllCommand;

    private GetOrComputeSingleDestinationCommand(
        string destinationName,
        CacheKey cacheKey,
        CacheKey? additionalKeyWithTenantAndPrincipal,
        SemaphoreSlim isolationLock,
        ExpiringCache<IDestination> destinationCache,
        Func<CancellationToken, Task<IDestination>> destinationSupplier,
        DestinationServiceTokenExchangeStrategy exchangeStrategy,
        GetOrComputeAllDestinationsCommand? getAllCommand)
    {
        _destinationName = destinationName;
        CacheKey = cacheKey;
        AdditionalKeyWithTenantAndPrincipal = additionalKeyWithTenantAndPrincipal;
        _isolationLock = isolationLock;
        _destinationCache = destinationCache;
        _destinationSupplier = destinationSupplier;
        ExchangeStrategy = exchangeStrategy;
        _getAllCommand = getAllCommand;
    }

    internal CacheKey CacheKey { get; }

    internal CacheKey? AdditionalKeyWithTenantAndPrincipal { get; }

    internal DestinationServiceTokenExchangeStrategy ExchangeStrategy { get; }

    /// <summary>Prepares the command; fails if the isolation the strategy requires cannot be determined.</summary>
    public static Result<GetOrComputeSingleDestinationCommand> PrepareCommand(
        string destinationName,
        DestinationOptions destinationOptions,
        ExpiringCache<IDestination> destinationCache,
        IsolationLocks isolationLocks,
        Func<string, DestinationOptions, CancellationToken, Task<IDestination>> destinationRetriever,
        GetOrComputeAllDestinationsCommand? getAllCommand)
    {
        var exchangeStrategy = DestinationServiceOptionsAugmenter.GetTokenExchangeStrategy(destinationOptions)
            ?? DestinationServiceTokenExchangeStrategy.LookupThenExchange;

        CacheKey cacheKey;
        CacheKey? additionalKeyWithTenantAndPrincipal = null;
        if (exchangeStrategy == DestinationServiceTokenExchangeStrategy.ExchangeOnly)
        {
            try
            {
                cacheKey = CacheKey.OfTenantAndPrincipalIsolation();
            }
            catch (Exception e) when (e is TenantAccessException or PrincipalAccessException)
            {
                return Result.Failure<GetOrComputeSingleDestinationCommand>(new DestinationAccessException(
                    "Failed to determine tenant/principal information required for destination token exchange strategy "
                    + DestinationServiceTokenExchangeStrategy.ExchangeOnly.GetIdentifier(),
                    e));
            }
        }
        else
        {
            cacheKey = CacheKey.OfTenantOptionalIsolation();
            if (exchangeStrategy is DestinationServiceTokenExchangeStrategy.LookupThenExchange
                or DestinationServiceTokenExchangeStrategy.ForwardUserToken)
            {
                additionalKeyWithTenantAndPrincipal =
                    CacheKey.OfTenantAndPrincipalOptionalIsolation().Append(destinationName, destinationOptions);
            }
        }
        // in case the options contain a refresh token it will be included with the cache key
        // that way we don't have to explicitly know if the refresh token is user-specific
        cacheKey.Append(destinationName, destinationOptions);

        var isolationLock = isolationLocks.GetOrCreate(cacheKey);

        return Result.Success(new GetOrComputeSingleDestinationCommand(
            destinationName,
            cacheKey,
            additionalKeyWithTenantAndPrincipal,
            isolationLock,
            destinationCache,
            cancellationToken => destinationRetriever(destinationName, destinationOptions, cancellationToken),
            exchangeStrategy,
            getAllCommand));
    }

    public async Task<Result<IDestination>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await GetCachedDestinationAsync(cancellationToken).ConfigureAwait(false);
        if (result is not null)
        {
            return Result.Success(result);
        }

        await _isolationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // double-checked locking
            result = await GetCachedDestinationAsync(cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return Result.Success(result);
            }

            try
            {
                result = await _destinationSupplier(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                return Result.Failure<IDestination>(e);
            }

            switch (ExchangeStrategy)
            {
                case DestinationServiceTokenExchangeStrategy.LookupOnly:
                case DestinationServiceTokenExchangeStrategy.ExchangeOnly:
                    _destinationCache.Put(CacheKey, result);
                    LogErroneousCombinations(result);
                    break;
                case DestinationServiceTokenExchangeStrategy.LookupThenExchange:
                case DestinationServiceTokenExchangeStrategy.ForwardUserToken:
                    if (!RequiresPrincipalForDestinationRetrieval(result))
                    {
                        _destinationCache.Put(CacheKey, result);
                    }
                    else
                    {
                        if (AdditionalKeyWithTenantAndPrincipal!.PrincipalId is null)
                        {
                            return Result.Failure<IDestination>(new DestinationAccessException(
                                $"No principal is available in the current ThreadContext, but a principal is required for fetching the destination {_destinationName}. "
                                + "This typically means that the current context is malformed, containing inconsistent information about the authentication token, tenant and principal."));
                        }
                        _destinationCache.Put(AdditionalKeyWithTenantAndPrincipal, result);
                    }
                    break;
                default:
                    break;
            }
            return Result.Success(result);
        }
        finally
        {
            _isolationLock.Release();
        }
    }

    private static bool RequiresPrincipalForDestinationRetrieval(IDestinationProperties destination)
    {
        try
        {
            return DestinationUtility.RequiresUserTokenExchange(destination);
        }
        catch (ArgumentException)
        {
            // an unknown authentication type cannot be known to need a user token
            return false;
        }
    }

    private void LogErroneousCombinations(IDestinationProperties result)
    {
        var requiresPrincipal = RequiresPrincipalForDestinationRetrieval(result);
        if (requiresPrincipal && ExchangeStrategy == DestinationServiceTokenExchangeStrategy.LookupOnly)
        {
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(
                    "The destination {Name} was retrieved with strategy {Strategy}, but it requires user token exchange. "
                    + "Hence, the destination cannot be used to connect to the target system successfully, please refer to the documentation of {Type} to find the recommended strategy. "
                    + "Caching the destination for all users of the current tenant since it was obtained without user token exchange.",
                    _destinationName,
                    DestinationServiceTokenExchangeStrategy.LookupOnly.GetIdentifier(),
                    nameof(DestinationServiceTokenExchangeStrategy));
            }
        }

        if (!requiresPrincipal && ExchangeStrategy == DestinationServiceTokenExchangeStrategy.ExchangeOnly)
        {
            Log.LogWarning(
                "The destination {Name} was retrieved with strategy {Strategy}, but doesn't require user token exchange. "
                + "This is not recommended, please refer to the documentation of {Type}. "
                + "Caching the destination for the current user of the current tenant since it was obtained with a user token.",
                _destinationName,
                DestinationServiceTokenExchangeStrategy.ExchangeOnly.GetIdentifier(),
                nameof(DestinationServiceTokenExchangeStrategy));
        }
    }

    private async Task<IDestination?> GetCachedDestinationAsync(CancellationToken cancellationToken)
    {
        var cached = _destinationCache.GetIfPresent(CacheKey);
        if (cached is null && AdditionalKeyWithTenantAndPrincipal is not null)
        {
            cached = _destinationCache.GetIfPresent(AdditionalKeyWithTenantAndPrincipal);
        }
        if (cached is null)
        {
            return null;
        }
        if (AuthTokenIsExpired(cached) || CertificateIsExpired(cached))
        {
            return null;
        }
        if (_getAllCommand is null)
        {
            return cached;
        }

        var allDestinations = await _getAllCommand.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (allDestinations.IsFailure)
        {
            const string Message =
                "Failed to resolve all destinations of the current tenant from the destination service. "
                + "Cannot perform change detection. {Name} will therefore be assumed to remain unchanged.";
            Log.LogWarning(Message, _destinationName);
            // log the message two times because many lines might be inserted between the logs because of multi-threading
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(allDestinations.Exception, Message, _destinationName);
            }
            return cached;
        }
        return DestinationIsChanged(allDestinations.GetValueOrThrow(), cached) ? null : cached;
    }

    /// <summary>
    /// Currently this effectively limits the cache duration to the change detection interval, because change detection on
    /// certificates is not possible in all cases. See the note in <see cref="DestinationServiceFactory"/> where this property
    /// is set.
    /// </summary>
    private static bool CertificateIsExpired(IDestinationProperties destination) =>
        destination.TryGet(DestinationProperty.Certificates, out var certificates)
        && IsExpired(certificates.OfType<DestinationServiceV1Response.DestinationCertificate>().Select(c => c.ExpiryTimestamp));

    private static bool AuthTokenIsExpired(IDestinationProperties destination) =>
        destination.TryGet(DestinationProperty.AuthTokens, out var tokens)
        && IsExpired(tokens.OfType<DestinationServiceV1Response.DestinationAuthToken>().Select(t => t.ExpiryTimestamp));

    private static bool IsExpired(IEnumerable<DateTimeOffset?> expiryTimestamps)
    {
        var earliest = expiryTimestamps.Where(t => t is not null).Min();
        return earliest is { } min && DateTimeOffset.UtcNow.Add(ExpirationBufferTime) > min;
    }

    private static bool DestinationIsChanged(IReadOnlyList<IDestinationProperties> allDestinations, IDestination cachedDestination)
    {
        cachedDestination.TryGet(DestinationProperty.Name, out var destinationName);
        var matching = allDestinations.FirstOrDefault(
            d => d.TryGet(DestinationProperty.Name, out var name) && string.Equals(name, destinationName, StringComparison.Ordinal));
        return matching is null || !DestinationIsEqualTo(matching, cachedDestination);
    }

    /// <summary>
    /// Checks whether all properties of <paramref name="destinationFromGetAllEndPoint"/> are also contained in
    /// <paramref name="individuallyRetrievedDestination"/> and, vice versa, all properties listed in its
    /// <c>cloudsdk.propertiesForChangeDetection</c> are contained in the former. Other properties of
    /// the individually retrieved destination are not considered.
    /// </summary>
    internal static bool DestinationIsEqualTo(
        IDestinationProperties destinationFromGetAllEndPoint,
        IDestinationProperties individuallyRetrievedDestination)
    {
        foreach (var propertyName in destinationFromGetAllEndPoint.PropertyNames)
        {
            if (!PropertyEquals(
                destinationFromGetAllEndPoint.GetProperty(propertyName),
                individuallyRetrievedDestination.GetProperty(propertyName)))
            {
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Detected change in destination property {Property}", propertyName);
                }
                return false;
            }
        }
        var relevantProperties = individuallyRetrievedDestination.TryGet(DestinationProperty.PropertiesForChangeDetection, out var names)
            ? names
            : [];
        foreach (var propertyName in relevantProperties)
        {
            if (!PropertyEquals(
                individuallyRetrievedDestination.GetProperty(propertyName),
                destinationFromGetAllEndPoint.GetProperty(propertyName)))
            {
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug("Detected change in destination property {Property}", propertyName);
                }
                return false;
            }
        }
        return true;
    }

    /// <summary>Compares property values; collections are compared by their elements, as Java's <c>equals</c> does.</summary>
    private static bool PropertyEquals(object? expected, object? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }
        if (expected is string || actual is string)
        {
            return Equals(expected, actual);
        }
        if (expected is IEnumerable expectedItems && actual is IEnumerable actualItems)
        {
            var left = expectedItems.Cast<object?>().ToList();
            var right = actualItems.Cast<object?>().ToList();
            return left.Count == right.Count && left.Zip(right).All(pair => PropertyEquals(pair.First, pair.Second));
        }
        return Equals(expected, actual);
    }
}
