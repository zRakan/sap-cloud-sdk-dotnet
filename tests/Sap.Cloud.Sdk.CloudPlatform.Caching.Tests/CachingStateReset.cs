using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache.Tests;

/// <summary>Restores the static state touched by caching tests.</summary>
public abstract class CachingStateReset : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        TenantAccessor.TenantFacade = null!;
        TenantAccessor.FallbackTenant = null;
        PrincipalAccessor.PrincipalFacade = null!;
        PrincipalAccessor.FallbackPrincipal = null;
        foreach (var cache in CacheManager.GetCacheList())
        {
            CacheManager.Unregister(cache);
        }
    }

    protected static T WithTenantAndPrincipal<T>(string tenantId, string principalId, Func<T> func) =>
        TenantAccessor.ExecuteWithTenant(new DefaultTenant(tenantId),
            () => PrincipalAccessor.ExecuteWithPrincipal(new DefaultPrincipal(principalId), func));
}
