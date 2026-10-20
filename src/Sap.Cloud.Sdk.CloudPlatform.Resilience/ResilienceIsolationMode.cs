namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>
/// Defines which part of the request context separates resilience state (circuit breakers, bulkheads, rate limiters)
/// and cached values of the same <see cref="ResilienceConfiguration"/>.
/// </summary>
public enum ResilienceIsolationMode
{
    /// <summary>All callers share the same state.</summary>
    NoIsolation,

    /// <summary>State is isolated per tenant; fails if there is no current tenant.</summary>
    TenantRequired,

    /// <summary>State is isolated per tenant if there is one; otherwise shared.</summary>
    TenantOptional,

    /// <summary>State is isolated per principal; fails if there is no current principal.</summary>
    PrincipalRequired,

    /// <summary>State is isolated per principal if there is one; otherwise shared.</summary>
    PrincipalOptional,

    /// <summary>State is isolated per tenant and principal; fails if either is missing.</summary>
    TenantAndUserRequired,

    /// <summary>State is isolated per tenant and principal, each only if available. This is the default.</summary>
    TenantAndUserOptional,
}
