namespace Sap.Cloud.Sdk.CloudPlatform.Tenant;

/// <summary>Provides the tenant of the current request.</summary>
public interface ITenantFacade
{
    /// <summary>Gets the current tenant.</summary>
    /// <returns>The tenant, or a failure.</returns>
    Result<ITenant> TryGetCurrentTenant();
}
