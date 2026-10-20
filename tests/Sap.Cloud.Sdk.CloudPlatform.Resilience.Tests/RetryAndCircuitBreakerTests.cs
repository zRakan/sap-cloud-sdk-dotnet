using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Tests;

public class RetryAndCircuitBreakerTests : ResilienceTestBase
{
    private static ResilienceConfiguration.CircuitBreakerConfiguration Breaker(int closedBuffer, TimeSpan wait) =>
        ResilienceConfiguration.CircuitBreakerConfiguration.Of() with
        {
            ClosedBufferSize = closedBuffer,
            FailureRateThreshold = 50f,
            WaitDuration = wait,
        };

    private static bool TryRun(Func<int> call)
    {
        try
        {
            call();
            return true;
        }
        catch (ResilienceRuntimeException)
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ retry

    [Fact]
    public void Retry_Disabled_ExecutesExactlyOnce()
    {
        var operation = new ScriptedOperation(_ => false);

        var thrown = Assert.Throws<ResilienceRuntimeException>(
            () => ResilienceDecorator.Execute(operation.Call, Config("retry.disabled")));

        Assert.IsType<TestException>(thrown.InnerException);
        Assert.Equal(1, operation.Attempts);
    }

    [Fact]
    public void Retry_AlwaysFailing_ExecutesMaxAttemptsTimes()
    {
        var operation = new ScriptedOperation(_ => false);
        var config = Config("retry.failure") with { Retry = ResilienceConfiguration.RetryConfiguration.Of(5, TimeSpan.Zero) };

        var thrown = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(operation.Call, config));

        Assert.IsType<TestException>(thrown.InnerException);
        Assert.Equal(5, operation.Attempts);
    }

    [Fact]
    public void Retry_SucceedsOnLastAttempt_ReturnsValue()
    {
        var operation = new ScriptedOperation(attempt => attempt >= 5);
        var config = Config("retry.success") with { Retry = ResilienceConfiguration.RetryConfiguration.Of(5, TimeSpan.Zero) };

        Assert.Equal(5, ResilienceDecorator.Execute(operation.Call, config));
        Assert.Equal(5, operation.Attempts);
    }

    [Fact]
    public async Task Retry_Async_ExecutesMaxAttemptsTimes()
    {
        var attempts = 0;
        var config = Config("retry.async") with { Retry = ResilienceConfiguration.RetryConfiguration.Of(4, TimeSpan.Zero) };

        var thrown = await Assert.ThrowsAsync<ResilienceRuntimeException>(() => ResilienceDecorator.ExecuteAsync<int>(
            async ct =>
            {
                attempts++;
                await Task.Yield();
                throw new TestException("fail");
            }, config));

        Assert.IsType<TestException>(thrown.InnerException);
        Assert.Equal(4, attempts);
    }

    [Fact]
    public void Retry_Predicate_StopsRetryingWhenItReturnsFalse()
    {
        var operation = new ScriptedOperation(_ => false);
        var retry = ResilienceConfiguration.RetryConfiguration.Of(3, TimeSpan.Zero) with
        {
            RetryOnExceptionPredicate = e => e is not TestException,
        };

        var thrown = Assert.Throws<ResilienceRuntimeException>(
            () => ResilienceDecorator.Execute(operation.Call, Config("retry.predicate") with { Retry = retry }));

        Assert.IsType<TestException>(thrown.InnerException);
        Assert.Equal(1, operation.Attempts);
    }

    [Fact]
    public void Retry_WaitsBetweenAttempts()
    {
        var operation = new ScriptedOperation(_ => false);
        var config = Config("retry.wait") with { Retry = ResilienceConfiguration.RetryConfiguration.Of(3, TimeSpan.FromMilliseconds(100)) };
        var start = DateTime.UtcNow;

        Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(operation.Call, config));

        Assert.True(DateTime.UtcNow - start >= TimeSpan.FromMilliseconds(180));
    }

    [Fact]
    public void Retry_TimeoutAppliesToEachAttempt()
    {
        var attempts = 0;
        var config = Config("retry.timeout") with
        {
            Retry = ResilienceConfiguration.RetryConfiguration.Of(3, TimeSpan.Zero),
            TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromMilliseconds(50)),
        };

        Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute<int>(ct =>
        {
            attempts++;
            ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            ct.ThrowIfCancellationRequested();
            return 1;
        }, config, null, CancellationToken.None));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public void Retry_TriggersCircuitBreaker_OperationIsNotCalledOnceItIsOpen()
    {
        var closedBuffer = ResilienceConfiguration.CircuitBreakerConfiguration.DefaultClosedBufferSize;
        var operation = new ScriptedOperation(_ => false);
        var config = Config("retry.circuit_breaker") with
        {
            Retry = ResilienceConfiguration.RetryConfiguration.Of(closedBuffer + 5, TimeSpan.Zero),
            CircuitBreaker = ResilienceConfiguration.CircuitBreakerConfiguration.Of(),
        };

        var thrown = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(operation.Call, config));

        Assert.Contains("is OPEN", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(closedBuffer, operation.Attempts);
    }

    // --------------------------------------------------------- circuit breaker

    [Fact]
    public void CircuitBreaker_StaysClosed_WhenFailureRateStaysBelowThreshold()
    {
        var script = new[] { true, true, false, true };
        var operation = new ScriptedOperation(attempt => script[attempt - 1]);
        var config = Config("cb.closed") with { CircuitBreaker = Breaker(3, TimeSpan.FromHours(1)) };

        var results = script.Select(_ => TryRun(() => ResilienceDecorator.Execute(operation.Call, config))).ToList();

        Assert.Equal(script, results);
        Assert.Equal(script.Length, operation.Attempts);
    }

    [Fact]
    public void CircuitBreaker_Opens_AndReportsLastFailure()
    {
        var script = new[] { false, true, false };
        var operation = new ScriptedOperation(attempt => script[attempt - 1]);
        var config = Config("cb.opens") with { CircuitBreaker = Breaker(3, TimeSpan.FromHours(1)) };

        var results = Enumerable.Range(0, 6).Select(_ => TryRun(() => ResilienceDecorator.Execute(operation.Call, config))).ToList();

        Assert.Equal([false, true, false, false, false, false], results);
        Assert.Equal(3, operation.Attempts);

        var thrown = Assert.Throws<ResilienceRuntimeException>(() => ResilienceDecorator.Execute(operation.Call, config));
        Assert.Equal(
            "CircuitBreaker 'cb.opens' is OPEN and does not permit further calls. Triggered by Simulated failure, attempt nr: 3",
            thrown.Message);
        Assert.IsType<TestException>(thrown.InnerException);
        Assert.Equal(3, operation.Attempts);
    }

    [Fact]
    public void CircuitBreaker_OpenCircuit_UsesFallback()
    {
        var operation = new ScriptedOperation(_ => false);
        var config = Config("cb.fallback") with { CircuitBreaker = Breaker(2, TimeSpan.FromHours(1)) };
        Assert.False(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        Assert.False(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));

        var result = ResilienceDecorator.Execute(operation.Call, config, e => e is ResilienceRuntimeException ? -1 : -2);

        Assert.Equal(-1, result);
        Assert.Equal(2, operation.Attempts);
    }

    [Fact]
    public async Task CircuitBreaker_HalfOpens_ThenClosesAfterSuccessfulProbe()
    {
        var script = new[] { false, false, true, true };
        var operation = new ScriptedOperation(attempt => script[attempt - 1]);
        var config = Config("cb.closes_again") with { CircuitBreaker = Breaker(2, TimeSpan.FromMilliseconds(500)) };

        Assert.False(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        Assert.False(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        Assert.False(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        Assert.Equal(2, operation.Attempts);

        await Task.Delay(TimeSpan.FromMilliseconds(700));

        Assert.True(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        Assert.True(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        Assert.Equal(4, operation.Attempts);
    }

    [Fact]
    public void CircuitBreaker_Disabled_NeverOpens()
    {
        var operation = new ScriptedOperation(_ => false);
        var config = Config("cb.disabled") with { CircuitBreaker = ResilienceConfiguration.CircuitBreakerConfiguration.Disabled() };

        for (var i = 0; i < 20; i++)
        {
            Assert.False(TryRun(() => ResilienceDecorator.Execute(operation.Call, config)));
        }

        Assert.Equal(20, operation.Attempts);
    }

    [Fact]
    public void CircuitBreaker_TwoTenants_NeverShareState()
    {
        var failing = new ScriptedOperation(_ => false);
        var healthy = new ScriptedOperation(_ => true);
        var config = ResilienceConfiguration.Of("cb.tenants") with
        {
            IsolationMode = ResilienceIsolationMode.TenantRequired,
            CircuitBreaker = Breaker(2, TimeSpan.FromHours(1)),
        };

        // Tenant A trips its breaker.
        InTenant("A", () =>
        {
            Assert.False(TryRun(() => ResilienceDecorator.Execute(failing.Call, config)));
            Assert.False(TryRun(() => ResilienceDecorator.Execute(failing.Call, config)));
            Assert.False(TryRun(() => ResilienceDecorator.Execute(failing.Call, config)));
            return 0;
        });
        Assert.Equal(2, failing.Attempts);

        // Tenant B is unaffected, tenant A is still blocked.
        Assert.True(InTenant("B", () => TryRun(() => ResilienceDecorator.Execute(healthy.Call, config))));
        Assert.Equal(1, healthy.Attempts);
        Assert.False(InTenant("A", () => TryRun(() => ResilienceDecorator.Execute(healthy.Call, config))));
        Assert.Equal(1, healthy.Attempts);
    }

    [Fact]
    public void CircuitBreaker_PrincipalIsolation_SeparatesPrincipalsOfTheSameTenant()
    {
        var failing = new ScriptedOperation(_ => false);
        var healthy = new ScriptedOperation(_ => true);
        var config = ResilienceConfiguration.Of("cb.principals") with
        {
            IsolationMode = ResilienceIsolationMode.TenantAndUserRequired,
            CircuitBreaker = Breaker(2, TimeSpan.FromHours(1)),
        };

        InTenantAndPrincipal("T", "u1", () =>
        {
            TryRun(() => ResilienceDecorator.Execute(failing.Call, config));
            TryRun(() => ResilienceDecorator.Execute(failing.Call, config));
            return 0;
        });

        Assert.True(InTenantAndPrincipal("T", "u2", () => TryRun(() => ResilienceDecorator.Execute(healthy.Call, config))));
        Assert.False(InTenantAndPrincipal("T", "u1", () => TryRun(() => ResilienceDecorator.Execute(healthy.Call, config))));
    }

    [Fact]
    public void CircuitBreaker_NoIsolation_SharesStateAcrossTenants()
    {
        var failing = new ScriptedOperation(_ => false);
        var healthy = new ScriptedOperation(_ => true);
        var config = Config("cb.shared") with { CircuitBreaker = Breaker(2, TimeSpan.FromHours(1)) };

        InTenant("A", () => TryRun(() => ResilienceDecorator.Execute(failing.Call, config)));
        InTenant("A", () => TryRun(() => ResilienceDecorator.Execute(failing.Call, config)));

        Assert.False(InTenant("B", () => TryRun(() => ResilienceDecorator.Execute(healthy.Call, config))));
        Assert.Equal(0, healthy.Attempts);
    }
}
