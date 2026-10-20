namespace Sap.Cloud.Sdk.CloudPlatform.Tenant;

/// <summary>A tenant (subaccount) of a multi-tenant application.</summary>
public interface ITenant
{
    /// <summary>The tenant identifier (zone ID).</summary>
    string TenantId { get; }
}

/// <summary>A tenant that also knows its subdomain.</summary>
public interface ITenantWithSubdomain : ITenant
{
    /// <summary>The subdomain of the tenant, if known.</summary>
    string? Subdomain { get; }
}

/// <summary>Default implementation of <see cref="ITenantWithSubdomain"/>.</summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Subdomain">The subdomain, if known.</param>
public sealed record DefaultTenant(string TenantId, string? Subdomain = null) : ITenantWithSubdomain;
