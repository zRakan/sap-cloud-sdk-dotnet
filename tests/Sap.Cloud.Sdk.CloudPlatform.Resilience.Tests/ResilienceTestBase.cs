using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Tests;

/// <summary>Restores the static state touched by resilience tests and offers helpers to run in a tenant context.</summary>
public abstract class ResilienceTestBase : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        TenantAccessor.TenantFacade = null!;
        TenantAccessor.FallbackTenant = null;
        PrincipalAccessor.PrincipalFacade = null!;
        PrincipalAccessor.FallbackPrincipal = null;
        // A fresh default strategy has no circuit breaker, bulkhead, rate limiter or cache state.
        var previous = ResilienceDecorator.DecorationStrategy;
        ResilienceDecorator.DecorationStrategy = null!;
        (previous as IDisposable)?.Dispose();
    }

    protected static T InTenant<T>(string tenantId, Func<T> func) =>
        TenantAccessor.ExecuteWithTenant(new DefaultTenant(tenantId), func);

    protected static Task<T> InTenantAsync<T>(string tenantId, Func<Task<T>> func) =>
        TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant(tenantId), func);

    protected static T InTenantAndPrincipal<T>(string tenantId, string principalId, Func<T> func) =>
        TenantAccessor.ExecuteWithTenant(new DefaultTenant(tenantId),
            () => PrincipalAccessor.ExecuteWithPrincipal(new DefaultPrincipal(principalId), func));

    protected static ResilienceConfiguration Config(string identifier) =>
        ResilienceConfiguration.Of(identifier) with { IsolationMode = ResilienceIsolationMode.NoIsolation };

    protected sealed class TestException(string message) : Exception(message);

    /// <summary>An operation that fails according to a script and counts its invocations.</summary>
    protected sealed class ScriptedOperation(Func<int, bool> succeedOnAttempt)
    {
        public int Attempts { get; private set; }

        public int Call()
        {
            Attempts++;
            return succeedOnAttempt(Attempts)
                ? Attempts
                : throw new TestException("Simulated failure, attempt nr: " + Attempts);
        }
    }

    protected static void AssertCause<TException>(Exception? thrown)
        where TException : Exception
    {
        var resilience = Assert.IsType<ResilienceRuntimeException>(thrown);
        Assert.IsType<TException>(resilience.InnerException);
    }
}
