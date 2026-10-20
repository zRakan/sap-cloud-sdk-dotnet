using Polly;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>Options for <see cref="BulkheadStrategy"/>.</summary>
internal sealed class BulkheadStrategyOptions : ResilienceStrategyOptions
{
    public int MaxConcurrentCalls { get; init; }

    public TimeSpan MaxWaitDuration { get; init; }
}

/// <summary>
/// Limits concurrent executions with a semaphore (Polly.Core has no bulkhead; resilience4j's
/// <c>SemaphoreBulkhead</c> semantics: wait up to the maximum wait duration for a free slot).
/// </summary>
internal sealed class BulkheadStrategy : ResilienceStrategy, IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _maxWait;
    private readonly string _name;

    public BulkheadStrategy(string name, BulkheadStrategyOptions options)
    {
        _name = name;
        _maxWait = options.MaxWaitDuration;
        _semaphore = new SemaphoreSlim(options.MaxConcurrentCalls, options.MaxConcurrentCalls);
    }

    public void Dispose() => _semaphore.Dispose();

    protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(
        Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback,
        ResilienceContext context,
        TState state)
    {
        bool acquired;
        try
        {
            // Also used for synchronous pipeline executions, where Polly blocks on the returned task.
            acquired = await _semaphore.WaitAsync(_maxWait, context.CancellationToken).ConfigureAwait(context.ContinueOnCapturedContext);
        }
        catch (OperationCanceledException e)
        {
            return Outcome.FromException<TResult>(e);
        }

        if (!acquired)
        {
            return Outcome.FromException<TResult>(new BulkheadFullException(
                $"Bulkhead '{_name}' is full and does not permit further calls."));
        }

        try
        {
            return await callback(context, state).ConfigureAwait(context.ContinueOnCapturedContext);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
