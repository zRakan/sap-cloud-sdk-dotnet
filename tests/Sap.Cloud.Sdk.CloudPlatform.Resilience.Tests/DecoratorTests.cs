using Polly.Timeout;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Tests;

public class DecoratorTests : ResilienceTestBase
{
    [Fact]
    public void DecorationStrategy_DefaultsToPolly_SetterAcceptsCustom_NullRestoresDefault()
    {
        Assert.IsType<PollyResilienceDecorationStrategy>(ResilienceDecorator.DecorationStrategy);

        var custom = new NoResilienceDecorationStrategy();
        ResilienceDecorator.DecorationStrategy = custom;
        Assert.Same(custom, ResilienceDecorator.DecorationStrategy);

        ResilienceDecorator.DecorationStrategy = null!;
        Assert.IsType<PollyResilienceDecorationStrategy>(ResilienceDecorator.DecorationStrategy);
    }

    [Fact]
    public void NoResilienceStrategy_RunsOperationUnchanged()
    {
        ResilienceDecorator.DecorationStrategy = new NoResilienceDecorationStrategy();

        Assert.Equal(5, ResilienceDecorator.Execute(() => 5, Config("none"), _ => -1));
        Assert.Throws<TestException>(() => ResilienceDecorator.Execute<int>(() => throw new TestException("x"), Config("none")));
    }

    [Fact]
    public void ClearCache_WithStrategyWithoutCacheSupport_ThrowsNotSupported()
    {
        ResilienceDecorator.DecorationStrategy = new NoResilienceDecorationStrategy();

        Assert.Throws<NotSupportedException>(() => ResilienceDecorator.ClearCache(Config("x")));
        Assert.Throws<NotSupportedException>(() => ResilienceDecorator.ClearAllCacheEntries(Config("x")));
    }

    [Fact]
    public void Execute_Success_ReturnsValueWithoutFallback()
    {
        var result = ResilienceDecorator.Execute(() => "Should return me.", Config("success"), _ => "Fallback function.");

        Assert.Equal("Should return me.", result);
    }

    [Fact]
    public void Execute_Failure_UsesFallbackWithOriginalException()
    {
        Exception? seen = null;
        var failure = new TestException("Simulated exception.");

        var result = ResilienceDecorator.Execute<string>(
            () => throw failure, Config("fallback"), e =>
            {
                seen = e;
                return "Fallback function.";
            });

        Assert.Equal("Fallback function.", result);
        Assert.Same(failure, seen);
    }

    [Fact]
    public void Execute_FailureWithoutFallback_WrapsInResilienceRuntimeException()
    {
        var failure = new TestException("boom");

        var thrown = Assert.Throws<ResilienceRuntimeException>(
            () => ResilienceDecorator.Execute<int>(() => throw failure, Config("wrap")));

        Assert.Same(failure, thrown.InnerException);
        Assert.Contains("boom", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_ResilienceRuntimeExceptionFromOperation_IsNotWrappedAgain()
    {
        var failure = new ResilienceRuntimeException("already");

        var thrown = Assert.Throws<ResilienceRuntimeException>(
            () => ResilienceDecorator.Execute<int>(() => throw failure, Config("nowrap")));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Execute_FallbackThrows_ResultsInResilienceRuntimeException()
    {
        var thrown = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute<string>(
            () => throw new TestException("x"), Config("badfallback"),
            e => throw new InvalidOperationException("Bad example", e)));

        Assert.IsType<InvalidOperationException>(thrown.InnerException);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsValue()
    {
        var result = await ResilienceDecorator.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                return 42;
            }, Config("async.success"));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_RunsOperation()
    {
        var ran = false;

        await ResilienceDecorator.ExecuteAsync(async ct =>
        {
            await Task.Delay(1, ct);
            ran = true;
        }, Config("async.void"));

        Assert.True(ran);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_FaultsTaskWithWrappedException()
    {
        var thrown = await Assert.ThrowsAsync<ResilienceRuntimeException>(() => ResilienceDecorator.ExecuteAsync<int>(
            _ => Task.FromException<int>(new TestException("async boom")), Config("async.fail")));

        Assert.IsType<TestException>(thrown.InnerException);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_UsesFallback()
    {
        var result = await ResilienceDecorator.ExecuteAsync<string>(
            _ => throw new TestException("sync throw in async operation"), Config("async.fallback"), _ => "Fallback");

        Assert.Equal("Fallback", result);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledByCaller_ThrowsUnwrappedAndSkipsFallback()
    {
        using var cts = new CancellationTokenSource();
        var fallbackCalled = false;

        var task = ResilienceDecorator.ExecuteAsync<int>(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 1;
        }, Config("async.cancel"), _ =>
        {
            fallbackCalled = true;
            return -1;
        }, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(fallbackCalled);
    }

    [Fact]
    public void DecorateAsync_RequiredTenantMissing_ThrowsAtDecorationTime()
    {
        TenantAccessor.TenantFacade = new NoTenantFacade();
        var config = ResilienceConfiguration.Of("required") with { IsolationMode = ResilienceIsolationMode.TenantRequired };

        Assert.Throws<ResilienceRuntimeException>(
            () => ResilienceDecorator.DecorateAsync<int>(_ => Task.FromResult(1), config));
    }

    [Fact]
    public void Decorate_EvaluatesIsolationAtDecorationTime_NotAtCallTime()
    {
        var config = ResilienceConfiguration.Of("decoration.time") with { IsolationMode = ResilienceIsolationMode.TenantRequired };
        var decorated = InTenant("t1", () => ResilienceDecorator.Decorate(() => 1, config));

        // Calling outside of any tenant context works because the isolation was determined when decorating.
        TenantAccessor.TenantFacade = new NoTenantFacade();
        Assert.Equal(1, decorated());
    }

    [Fact]
    public void Execute_VoidOperation_Runs()
    {
        var count = 0;

        ResilienceDecorator.Execute(() => count++, Config("void"));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TenantContext_IsVisibleInsideDecoratedOperationWithTimeout()
    {
        var config = Config("context.flow") with { TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(10)) };

        var tenants = await InTenantAsync("tenant-A", () => ResilienceDecorator.ExecuteAsync(async ct =>
        {
            var before = TenantAccessor.GetCurrentTenant().TenantId;
            await Task.Delay(20, ct);
            var afterAwait = TenantAccessor.GetCurrentTenant().TenantId;
            var inTask = await Task.Run(() => TenantAccessor.GetCurrentTenant().TenantId, ct);
            return new[] { before, afterAwait, inTask };
        }, config));

        Assert.Equal(["tenant-A", "tenant-A", "tenant-A"], tenants);
    }

    [Fact]
    public async Task TenantContext_IsVisibleInsideDecoratedOperationWithTimeout_ForParallelTenants()
    {
        var config = Config("context.parallel") with { TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(10)) };

        async Task<string> Run(string tenant) => await InTenantAsync(tenant, () => ResilienceDecorator.ExecuteAsync(async ct =>
        {
            await Task.Delay(Random.Shared.Next(1, 30), ct);
            return TenantAccessor.GetCurrentTenant().TenantId;
        }, config));

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Run("tenant-" + (i % 4))));

        Assert.Equal(Enumerable.Range(0, 20).Select(i => "tenant-" + (i % 4)), results);
    }

    [Fact]
    public async Task Timeout_CancelsOperationThroughToken()
    {
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = Config("timeout.cancel") with { TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromMilliseconds(100)) };

        var thrown = await Assert.ThrowsAsync<ResilienceRuntimeException>(() => ResilienceDecorator.ExecuteAsync<string>(async ct =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return "Should not happen.";
            }
            catch (OperationCanceledException)
            {
                cancelled.SetResult(true);
                throw;
            }
        }, config));

        Assert.IsType<TimeoutRejectedException>(thrown.InnerException);
        Assert.True(await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Timeout_UsesFallbackWithTimeoutException()
    {
        var config = Config("timeout.fallback") with { TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromMilliseconds(50)) };

        var result = await ResilienceDecorator.ExecuteAsync<string>(
            async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return "late";
            }, config, e => e.GetType().Name);

        Assert.Equal(nameof(TimeoutRejectedException), result);
    }

    [Fact]
    public async Task Timeout_Disabled_DoesNotCancel()
    {
        var config = Config("timeout.disabled") with { TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Disabled() };

        var result = await ResilienceDecorator.ExecuteAsync(async ct =>
        {
            await Task.Delay(100, ct);
            return "done";
        }, config);

        Assert.Equal("done", result);
    }

    [Fact]
    public void Timeout_SyncOperationObservingToken_IsCancelled()
    {
        var config = Config("timeout.sync") with { TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromMilliseconds(100)) };

        var thrown = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute<int>(ct =>
        {
            ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            ct.ThrowIfCancellationRequested();
            return 1;
        }, config, null, CancellationToken.None));

        Assert.IsType<TimeoutRejectedException>(thrown.InnerException);
    }

    private sealed class NoTenantFacade : ITenantFacade
    {
        public Result<ITenant> TryGetCurrentTenant() =>
            Result.Failure<ITenant>(new Tenant.Exceptions.TenantAccessException("none"));
    }
}
