using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>A strategy that applies no resilience patterns at all; operations and fallbacks are ignored as in Java.</summary>
public class NoResilienceDecorationStrategy : IResilienceDecorationStrategy
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<NoResilienceDecorationStrategy>();

    /// <inheritdoc/>
    public Func<CancellationToken, Task<T>> DecorateAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Log.LogWarning("The resilience function decorator was called without a proper implementation.");
        return operation;
    }

    /// <inheritdoc/>
    public Func<CancellationToken, T> Decorate<T>(
        Func<CancellationToken, T> operation,
        ResilienceConfiguration configuration,
        Func<Exception, T>? fallback)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Log.LogWarning("The resilience function decorator was called without a proper implementation.");
        return operation;
    }
}
