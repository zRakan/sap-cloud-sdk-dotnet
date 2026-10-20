using System.Diagnostics;
using Polly;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>Options for <see cref="RateLimiterStrategy"/>.</summary>
internal sealed class RateLimiterStrategyOptions : ResilienceStrategyOptions
{
    public int LimitForPeriod { get; init; }

    public TimeSpan LimitRefreshPeriod { get; init; }

    public TimeSpan TimeoutDuration { get; init; }
}

/// <summary>
/// A port of resilience4j's <c>AtomicRateLimiter</c>: <c>LimitForPeriod</c> permits per refresh period; a caller
/// without permit reserves one of a following period and waits for it, if that takes at most the timeout.
/// (Polly.Core has no rate limiter.)
/// </summary>
internal sealed class RateLimiterStrategy : ResilienceStrategy
{
    private readonly Lock _gate = new();
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private readonly string _name;
    private readonly long _periodTicks;
    private readonly long _timeoutTicks;
    private readonly int _limit;
    private long _activeCycle;
    private long _activePermissions;

    public RateLimiterStrategy(string name, RateLimiterStrategyOptions options)
    {
        _name = name;
        _limit = options.LimitForPeriod;
        _periodTicks = Math.Max(1, options.LimitRefreshPeriod.Ticks);
        _timeoutTicks = options.TimeoutDuration.Ticks;
        _activePermissions = _limit;
    }

    protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(
        Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback,
        ResilienceContext context,
        TState state)
    {
        var wait = Reserve();
        if (wait is null)
        {
            return Outcome.FromException<TResult>(new RateLimitExceededException(
                $"RateLimiter '{_name}' does not permit further calls."));
        }

        if (wait.Value > TimeSpan.Zero)
        {
            try
            {
                // Also used for synchronous pipeline executions, where Polly blocks on the returned task.
                await Task.Delay(wait.Value, context.CancellationToken).ConfigureAwait(context.ContinueOnCapturedContext);
            }
            catch (OperationCanceledException e)
            {
                return Outcome.FromException<TResult>(e);
            }
        }

        return await callback(context, state).ConfigureAwait(context.ContinueOnCapturedContext);
    }

    /// <summary>Reserves a permit. Returns the time to wait for it, or <see langword="null"/> if it takes too long.</summary>
    private TimeSpan? Reserve()
    {
        lock (_gate)
        {
            var nowTicks = Stopwatch.GetElapsedTime(_startTimestamp).Ticks;
            var currentCycle = nowTicks / _periodTicks;
            if (currentCycle > _activeCycle)
            {
                var accumulated = (currentCycle - _activeCycle) * _limit;
                _activePermissions = Math.Min(_activePermissions + accumulated, _limit);
                _activeCycle = currentCycle;
            }

            long waitTicks;
            if (_activePermissions >= 1)
            {
                waitTicks = 0;
            }
            else
            {
                var ticksToNextCycle = ((currentCycle + 1) * _periodTicks) - nowTicks;
                var permissionsAtNextCycle = _activePermissions + _limit;
                var missing = 1 - permissionsAtNextCycle;
                var fullCyclesToWait = missing <= 0 ? 0 : (missing + _limit - 1) / _limit;
                waitTicks = (fullCyclesToWait * _periodTicks) + ticksToNextCycle;
            }

            if (waitTicks > _timeoutTicks)
            {
                return null;
            }
            _activePermissions--;
            return TimeSpan.FromTicks(waitTicks);
        }
    }
}
