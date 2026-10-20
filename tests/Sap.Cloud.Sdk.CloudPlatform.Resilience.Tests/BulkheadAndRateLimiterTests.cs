using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Tests;

public class BulkheadAndRateLimiterTests : ResilienceTestBase
{
    private static ResilienceConfiguration BulkheadConfig(string id, ResilienceIsolationMode mode, int maxCalls, TimeSpan maxWait) =>
        ResilienceConfiguration.Of(id) with
        {
            IsolationMode = mode,
            Bulkhead = ResilienceConfiguration.BulkheadConfiguration.Of() with { MaxConcurrentCalls = maxCalls, MaxWaitDuration = maxWait },
        };

    private static ResilienceConfiguration RateConfig(string id, ResilienceIsolationMode mode, int limit, TimeSpan period, TimeSpan timeout) =>
        ResilienceConfiguration.Of(id) with
        {
            IsolationMode = mode,
            RateLimiter = ResilienceConfiguration.RateLimiterConfiguration.Of(timeout, period, limit),
        };

    private static async Task<int> Blocked(TaskCompletionSource started, TaskCompletionSource release, CancellationToken ct)
    {
        started.SetResult();
        await release.Task.WaitAsync(ct);
        return 1;
    }

    // ---------------------------------------------------------------- bulkhead

    [Fact]
    public async Task Bulkhead_RejectsCallsBeyondTheLimit_AndReleasesSlots()
    {
        var config = BulkheadConfig("bh.limit", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.Zero);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = ResilienceDecorator.ExecuteAsync(ct => Blocked(started, release, ct), config);
        await started.Task;

        var rejected = await Assert.ThrowsAsync<ResilienceRuntimeException>(
            () => ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(2), config));
        Assert.IsType<BulkheadFullException>(rejected.InnerException);

        release.SetResult();
        Assert.Equal(1, await first);
        Assert.Equal(3, await ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(3), config));
    }

    [Fact]
    public async Task Bulkhead_WaitsForSlotWithinMaxWaitDuration()
    {
        var config = BulkheadConfig("bh.wait", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.FromSeconds(10));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = ResilienceDecorator.ExecuteAsync(ct => Blocked(started, release, ct), config);
        await started.Task;

        var second = ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(2), config);
        await Task.Delay(100);
        Assert.False(second.IsCompleted);
        release.SetResult();

        Assert.Equal(2, await second);
        Assert.Equal(1, await first);
    }

    [Fact]
    public async Task Bulkhead_TwoTenants_NeverShareSlots()
    {
        var config = BulkheadConfig("bh.tenants", ResilienceIsolationMode.TenantRequired, 1, TimeSpan.Zero);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tenantAFirst = InTenantAsync("A", () => ResilienceDecorator.ExecuteAsync(ct => Blocked(started, release, ct), config));
        await started.Task;

        // Tenant B has its own bulkhead and is not blocked by tenant A.
        Assert.Equal(7, await InTenantAsync("B", () => ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(7), config)));
        // Tenant A's bulkhead is full.
        await InTenantAsync("A", async () =>
        {
            await Assert.ThrowsAsync<ResilienceRuntimeException>(
                () => ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(8), config));
            return 0;
        });

        release.SetResult();
        await tenantAFirst;
    }

    [Fact]
    public async Task Bulkhead_NoIsolation_SharesSlotsAcrossTenants()
    {
        var config = BulkheadConfig("bh.shared", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.Zero);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = InTenantAsync("A", () => ResilienceDecorator.ExecuteAsync(ct => Blocked(started, release, ct), config));
        await started.Task;

        await InTenantAsync("B", async () =>
        {
            await Assert.ThrowsAsync<ResilienceRuntimeException>(
                () => ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(8), config));
            return 0;
        });

        release.SetResult();
        await first;
    }

    [Fact]
    public async Task Bulkhead_FailingOperation_ReleasesSlot()
    {
        var config = BulkheadConfig("bh.release", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.Zero);

        await Assert.ThrowsAsync<ResilienceRuntimeException>(
            () => ResilienceDecorator.ExecuteAsync<int>(_ => throw new TestException("x"), config));

        Assert.Equal(1, await ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(1), config));
    }

    [Fact]
    public async Task Bulkhead_Sync_RejectsCallsBeyondTheLimit()
    {
        var config = BulkheadConfig("bh.sync", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.Zero);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var first = Task.Run(() => ResilienceDecorator.Execute(() =>
        {
            entered.Set();
            release.Wait();
            return 1;
        }, config));
        entered.Wait();

        var rejected = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(() => 2, config));

        Assert.IsType<BulkheadFullException>(rejected.InnerException);
        release.Set();
        Assert.Equal(1, await first);
    }

    // ------------------------------------------------------------ rate limiter

    [Fact]
    public void RateLimiter_RejectsCallsBeyondTheLimit_WithinThePeriod()
    {
        var config = RateConfig("rl.limit", ResilienceIsolationMode.NoIsolation, 2, TimeSpan.FromHours(1), TimeSpan.Zero);

        Assert.Equal(1, ResilienceDecorator.Execute(() => 1, config));
        Assert.Equal(1, ResilienceDecorator.Execute(() => 1, config));
        var rejected = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(() => 1, config));

        Assert.IsType<RateLimitExceededException>(rejected.InnerException);
    }

    [Fact]
    public void RateLimiter_WaitsForPermitWithinTimeout()
    {
        var config = RateConfig("rl.wait", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5));
        var start = DateTime.UtcNow;

        ResilienceDecorator.Execute(() => 1, config);
        ResilienceDecorator.Execute(() => 1, config);
        ResilienceDecorator.Execute(() => 1, config);

        // The second and third call each had to wait for a following period.
        Assert.True(DateTime.UtcNow - start >= TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task RateLimiter_PermitsAreRefreshed()
    {
        var config = RateConfig("rl.refresh", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.FromMilliseconds(200), TimeSpan.Zero);

        Assert.Equal(1, ResilienceDecorator.Execute(() => 1, config));
        Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(() => 1, config));
        await Task.Delay(300);

        Assert.Equal(1, ResilienceDecorator.Execute(() => 1, config));
    }

    [Fact]
    public async Task RateLimiter_Async_RejectsCallsBeyondTheLimit()
    {
        var config = RateConfig("rl.async", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.FromHours(1), TimeSpan.Zero);

        await ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(1), config);

        var rejected = await Assert.ThrowsAsync<ResilienceRuntimeException>(
            () => ResilienceDecorator.ExecuteAsync(_ => Task.FromResult(1), config));
        Assert.IsType<RateLimitExceededException>(rejected.InnerException);
    }

    [Fact]
    public void RateLimiter_TwoTenants_NeverShareLimits()
    {
        var config = RateConfig("rl.tenants", ResilienceIsolationMode.TenantRequired, 1, TimeSpan.FromHours(1), TimeSpan.Zero);

        Assert.Equal(1, InTenant("A", () => ResilienceDecorator.Execute(() => 1, config)));
        InTenant("A", () => Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(() => 1, config)));

        Assert.Equal(1, InTenant("B", () => ResilienceDecorator.Execute(() => 1, config)));
    }

    [Fact]
    public void RateLimiter_SharedByConfigurationIdentifier()
    {
        var config1 = RateConfig("rl.shared", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.FromHours(1), TimeSpan.Zero);
        var config2 = RateConfig("rl.shared", ResilienceIsolationMode.NoIsolation, 1, TimeSpan.FromHours(1), TimeSpan.Zero);

        ResilienceDecorator.Execute(() => 1, config1);

        Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(() => 1, config2));
    }

    [Fact]
    public void RateLimiter_Disabled_DoesNotLimit()
    {
        var config = Config("rl.disabled");

        for (var i = 0; i < 100; i++)
        {
            ResilienceDecorator.Execute(() => 1, config);
        }
    }
}
