using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache.Tests;

public class CacheKeyTests : CachingStateReset
{
    private sealed record Complex(string Val1, double Val2, float Val3, bool Val4, int Val5, object? Val6, long Val7);

    private static Complex NewComplex() => new("something", 1.23d, 4.56f, true, 12, null, 42L);

    [Fact]
    public void Equals_SameIsolationAndComponents_AreEqualWithSameHash()
    {
        var (first, second, otherOrder, tenantOnly) = WithTenantAndPrincipal("t1", "u1", () => (
            CacheKey.OfTenantAndPrincipalIsolation().Append("dummy", 42, NewComplex()),
            CacheKey.OfTenantAndPrincipalIsolation().Append("dummy", 42, NewComplex()),
            CacheKey.OfTenantAndPrincipalIsolation().Append(NewComplex(), 42, "dummy"),
            CacheKey.OfTenantIsolation().Append("dummy", 42, NewComplex())));

        Assert.Equal(["dummy", 42, NewComplex()], first.Components);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, otherOrder);
        Assert.NotEqual(first, tenantOnly);
    }

    [Fact]
    public void Equals_DifferentTenants_AreNotEqual()
    {
        var key1 = WithTenantAndPrincipal("t1", "u", () => CacheKey.OfTenantAndPrincipalIsolation().Append("x"));
        var key2 = WithTenantAndPrincipal("t2", "u", () => CacheKey.OfTenantAndPrincipalIsolation().Append("x"));
        var key3 = WithTenantAndPrincipal("t1", "other", () => CacheKey.OfTenantAndPrincipalIsolation().Append("x"));

        Assert.NotEqual(key1, key2);
        Assert.NotEqual(key1, key3);
        Assert.NotEqual(key1, CacheKey.OfNoIsolation().Append("x"));
    }

    [Fact]
    public void Append_NullComponent_ThrowsAndLeavesKeyUnchanged()
    {
        var key = CacheKey.OfNoIsolation();

        Assert.Throws<ArgumentException>(() => key.Append("string", null!));

        Assert.Empty(key.Components);
    }

    [Fact]
    public void Append_List_AppendsEachElement()
    {
        var key = CacheKey.OfNoIsolation().Append(new List<object> { "a", 1 });

        Assert.Equal(["a", 1], key.Components);
    }

    [Fact]
    public void Of_MapsTenantAndPrincipalIds()
    {
        var tenant = new DefaultTenant("tenantId");
        var principal = new DefaultPrincipal("user");

        Assert.Null(CacheKey.Of(null, null).TenantId);
        Assert.Null(CacheKey.Of(null, null).PrincipalId);
        Assert.Equal("tenantId", CacheKey.Of(tenant, null).TenantId);
        Assert.Null(CacheKey.Of(tenant, null).PrincipalId);
        Assert.Null(CacheKey.Of(null, principal).TenantId);
        Assert.Equal("user", CacheKey.Of(null, principal).PrincipalId);
        Assert.Equal("tenantId", CacheKey.Of(tenant, principal).TenantId);
        Assert.Equal("user", CacheKey.Of(tenant, principal).PrincipalId);
    }

    [Fact]
    public void OfNoIsolation_HasNoIds()
    {
        var key = CacheKey.OfNoIsolation();

        Assert.Null(key.TenantId);
        Assert.Null(key.PrincipalId);
    }

    [Fact]
    public void OfTenantIsolation_UsesCurrentTenantOnly()
    {
        var key = WithTenantAndPrincipal("t1", "u1", CacheKey.OfTenantIsolation);

        Assert.Equal("t1", key.TenantId);
        Assert.Null(key.PrincipalId);
    }

    [Fact]
    public void OfTenantAndPrincipalIsolation_UsesBoth()
    {
        var key = WithTenantAndPrincipal("t1", "u1", CacheKey.OfTenantAndPrincipalIsolation);

        Assert.Equal("t1", key.TenantId);
        Assert.Equal("u1", key.PrincipalId);
    }

    [Fact]
    public void RequiredIsolation_WithoutTenantOrPrincipal_Throws()
    {
        TenantAccessor.TenantFacade = new NoTenantFacade();
        PrincipalAccessor.PrincipalFacade = new NoPrincipalFacade();

        Assert.Throws<TenantAccessException>(CacheKey.OfTenantIsolation);
        Assert.Throws<TenantAccessException>(CacheKey.OfTenantAndPrincipalIsolation);
        TenantAccessor.TenantFacade = new FixedTenantFacade("t1");
        Assert.Throws<PrincipalAccessException>(CacheKey.OfTenantAndPrincipalIsolation);
    }

    [Fact]
    public void OptionalIsolation_WithoutTenantOrPrincipal_FallsBackToNoIsolation()
    {
        TenantAccessor.TenantFacade = new NoTenantFacade();
        PrincipalAccessor.PrincipalFacade = new NoPrincipalFacade();

        Assert.Equal(CacheKey.OfNoIsolation(), CacheKey.OfTenantOptionalIsolation());
        Assert.Equal(CacheKey.OfNoIsolation(), CacheKey.OfTenantAndPrincipalOptionalIsolation());

        TenantAccessor.TenantFacade = new FixedTenantFacade("t1");
        Assert.Equal(CacheKey.FromIds("t1", null), CacheKey.OfTenantAndPrincipalOptionalIsolation());
    }

    [Fact]
    public void ToString_DoesNotRevealComponents()
    {
        var key = CacheKey.FromIds("t1", "u1").Append("super-secret-token");

        Assert.DoesNotContain("super-secret-token", key.ToString(), StringComparison.Ordinal);
    }

    internal sealed class NoTenantFacade : ITenantFacade
    {
        public Result<ITenant> TryGetCurrentTenant() =>
            Result.Failure<ITenant>(new TenantAccessException("no tenant"));
    }

    internal sealed class FixedTenantFacade(string tenantId) : ITenantFacade
    {
        public Result<ITenant> TryGetCurrentTenant() => Result.Success<ITenant>(new DefaultTenant(tenantId));
    }

    internal sealed class NoPrincipalFacade : IPrincipalFacade
    {
        public Result<ISecurityPrincipal> TryGetCurrentPrincipal() =>
            Result.Failure<ISecurityPrincipal>(new PrincipalAccessException("no principal"));
    }
}
