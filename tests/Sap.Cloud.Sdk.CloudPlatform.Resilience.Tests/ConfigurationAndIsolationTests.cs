using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Tests;

public class ConfigurationAndIsolationTests : ResilienceTestBase
{
    [Fact]
    public void Defaults_MatchJava()
    {
        var config = ResilienceConfiguration.Of("foo");

        Assert.Equal(ResilienceIsolationMode.TenantAndUserOptional, config.IsolationMode);
        Assert.True(config.TimeLimiter.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(30), config.TimeLimiter.TimeoutDuration);
        Assert.True(config.TimeLimiter.ShouldCancelRunningFuture);
        Assert.True(config.CircuitBreaker.IsEnabled);
        Assert.Equal(50f, config.CircuitBreaker.FailureRateThreshold);
        Assert.Equal(10, config.CircuitBreaker.ClosedBufferSize);
        Assert.Equal(5, config.CircuitBreaker.HalfOpenBufferSize);
        Assert.Equal(TimeSpan.FromSeconds(10), config.CircuitBreaker.WaitDuration);
        Assert.Equal(TimeSpan.FromSeconds(60), config.CircuitBreaker.SlowCallDurationThreshold);
        Assert.Equal(100f, config.CircuitBreaker.SlowCallRateThreshold);
        Assert.True(config.Bulkhead.IsEnabled);
        Assert.Equal(50, config.Bulkhead.MaxConcurrentCalls);
        Assert.Equal(TimeSpan.FromSeconds(60), config.Bulkhead.MaxWaitDuration);
        Assert.False(config.Cache.IsEnabled);
        Assert.False(config.Retry.IsEnabled);
        Assert.False(config.RateLimiter.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(5), ResilienceConfiguration.RateLimiterConfiguration.Of().TimeoutDuration);
        Assert.Equal(TimeSpan.FromSeconds(1), ResilienceConfiguration.RateLimiterConfiguration.Of().LimitRefreshPeriod);
        Assert.Equal(20, ResilienceConfiguration.RateLimiterConfiguration.Of().LimitForPeriod);
        Assert.Equal(TimeSpan.FromMilliseconds(500), ResilienceConfiguration.RetryConfiguration.Of(3).WaitDuration);
    }

    [Fact]
    public void Empty_DisablesEverything()
    {
        var config = ResilienceConfiguration.Empty("foo");

        Assert.False(config.TimeLimiter.IsEnabled);
        Assert.False(config.CircuitBreaker.IsEnabled);
        Assert.False(config.Bulkhead.IsEnabled);
        Assert.False(config.Cache.IsEnabled);
        Assert.False(config.Retry.IsEnabled);
        Assert.False(config.RateLimiter.IsEnabled);
    }

    [Fact]
    public void Of_Type_UsesFullName() =>
        Assert.Equal(ResilienceConfiguration.Of(typeof(ConfigurationAndIsolationTests).FullName!),
            ResilienceConfiguration.Of(typeof(ConfigurationAndIsolationTests)));

    [Fact]
    public void EqualsAndHashCode_AreValueBased()
    {
        var config1 = ResilienceConfiguration.Of("foo");
        var config2 = ResilienceConfiguration.Of("foo");

        Assert.Equal(config1, config2);
        Assert.Equal(config1.GetHashCode(), config2.GetHashCode());
        Assert.NotEqual(config1, ResilienceConfiguration.Of("bar"));
        Assert.NotEqual(config1, config1 with { IsolationMode = ResilienceIsolationMode.NoIsolation });
        Assert.Equal(ResilienceConfiguration.RetryConfiguration.Of(5), ResilienceConfiguration.RetryConfiguration.Of(5));
        Assert.Equal(
            ResilienceConfiguration.RetryConfiguration.Of(5).GetHashCode(),
            ResilienceConfiguration.RetryConfiguration.Of(5).GetHashCode());
    }

    [Fact]
    public void CacheConfiguration_EqualsAndHashCode_ConsiderParameters()
    {
        var builder = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromDays(1));

        var none = builder.WithoutParameters();
        Assert.Equal(ResilienceConfiguration.CacheConfiguration.DefaultExpirationStrategy, none.ExpirationStrategy);
        Assert.Equal(none, builder.WithoutParameters());
        Assert.Equal(none.GetHashCode(), builder.WithoutParameters().GetHashCode());

        var one = builder.WithParameters("one");
        Assert.Equal(one, builder.WithParameters("one"));
        Assert.Equal(one.GetHashCode(), builder.WithParameters("one").GetHashCode());

        var three = builder.WithParameters("one", "two", "three");
        Assert.NotEqual(one, three);
        Assert.NotEqual(one.GetHashCode(), three.GetHashCode());
        Assert.NotEqual(builder.WithParameters("one", "three", "two"), three);
        Assert.NotEqual(
            builder.WithExpirationStrategy(CacheExpirationStrategy.WhenCreated).WithoutParameters(), none);
    }

    [Fact]
    public void CacheConfiguration_ToString_DoesNotRevealParameters()
    {
        var config = ResilienceConfiguration.CacheConfiguration.Of(TimeSpan.FromDays(1)).WithParameters("top-secret-token");

        Assert.DoesNotContain("top-secret-token", config.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret-token", (ResilienceConfiguration.Of("x") with { Cache = config }).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IsolationKey_NoIsolation_HasNoTenantOrPrincipal()
    {
        var key = InTenantAndPrincipal("t", "u", () => ResilienceIsolationKey.Of(ResilienceIsolationMode.NoIsolation));

        Assert.Null(key.Tenant);
        Assert.Null(key.Principal);
    }

    [Theory]
    [InlineData(ResilienceIsolationMode.TenantRequired, true, false)]
    [InlineData(ResilienceIsolationMode.TenantOptional, true, false)]
    [InlineData(ResilienceIsolationMode.PrincipalRequired, false, true)]
    [InlineData(ResilienceIsolationMode.PrincipalOptional, false, true)]
    [InlineData(ResilienceIsolationMode.TenantAndUserRequired, true, true)]
    [InlineData(ResilienceIsolationMode.TenantAndUserOptional, true, true)]
    public void IsolationKey_WithTenantAndPrincipal_ContainsOnlyWhatTheModeIsolates(
        ResilienceIsolationMode mode, bool expectTenant, bool expectPrincipal)
    {
        var key = InTenantAndPrincipal("t", "u", () => ResilienceIsolationKey.Of(mode));

        Assert.Equal(expectTenant ? "t" : null, key.Tenant?.TenantId);
        Assert.Equal(expectPrincipal ? "u" : null, key.Principal?.PrincipalId);
    }

    [Theory]
    [InlineData(ResilienceIsolationMode.TenantRequired)]
    [InlineData(ResilienceIsolationMode.TenantAndUserRequired)]
    public void IsolationKey_RequiredTenantMissing_ThrowsResilienceRuntimeException(ResilienceIsolationMode mode)
    {
        TenantAccessor.TenantFacade = new NoTenant();

        var exception = Assert.Throws<ResilienceRuntimeException>(() => ResilienceIsolationKey.Of(mode));

        Assert.IsType<TenantAccessException>(exception.InnerException);
    }

    [Theory]
    [InlineData(ResilienceIsolationMode.PrincipalRequired)]
    [InlineData(ResilienceIsolationMode.TenantAndUserRequired)]
    public void IsolationKey_RequiredPrincipalMissing_ThrowsResilienceRuntimeException(ResilienceIsolationMode mode)
    {
        TenantAccessor.TenantFacade = new FixedTenant();
        PrincipalAccessor.PrincipalFacade = new NoPrincipal();

        var exception = Assert.Throws<ResilienceRuntimeException>(() => ResilienceIsolationKey.Of(mode));

        Assert.IsType<PrincipalAccessException>(exception.InnerException);
    }

    [Theory]
    [InlineData(ResilienceIsolationMode.TenantOptional)]
    [InlineData(ResilienceIsolationMode.PrincipalOptional)]
    [InlineData(ResilienceIsolationMode.TenantAndUserOptional)]
    public void IsolationKey_OptionalMissing_FallsBackToNull(ResilienceIsolationMode mode)
    {
        TenantAccessor.TenantFacade = new NoTenant();
        PrincipalAccessor.PrincipalFacade = new NoPrincipal();

        var key = ResilienceIsolationKey.Of(mode);

        Assert.Null(key.Tenant);
        Assert.Null(key.Principal);
    }

    [Fact]
    public void IsolationKey_Equality_ComparesIdentifiers()
    {
        var a1 = InTenantAndPrincipal("t1", "u", () => ResilienceIsolationKey.Of(ResilienceIsolationMode.TenantAndUserRequired));
        var a2 = InTenantAndPrincipal("t1", "u", () => ResilienceIsolationKey.Of(ResilienceIsolationMode.TenantAndUserRequired));
        var otherTenant = InTenantAndPrincipal("t2", "u", () => ResilienceIsolationKey.Of(ResilienceIsolationMode.TenantAndUserRequired));
        var otherUser = InTenantAndPrincipal("t1", "v", () => ResilienceIsolationKey.Of(ResilienceIsolationMode.TenantAndUserRequired));

        Assert.Equal(a1, a2);
        Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
        Assert.NotEqual(a1, otherTenant);
        Assert.NotEqual(a1, otherUser);
    }

    private sealed class NoTenant : ITenantFacade
    {
        public Result<ITenant> TryGetCurrentTenant() => Result.Failure<ITenant>(new TenantAccessException("none"));
    }

    private sealed class FixedTenant : ITenantFacade
    {
        public Result<ITenant> TryGetCurrentTenant() => Result.Success<ITenant>(new DefaultTenant("t"));
    }

    private sealed class NoPrincipal : IPrincipalFacade
    {
        public Result<ISecurityPrincipal> TryGetCurrentPrincipal() =>
            Result.Failure<ISecurityPrincipal>(new PrincipalAccessException("none"));
    }
}
