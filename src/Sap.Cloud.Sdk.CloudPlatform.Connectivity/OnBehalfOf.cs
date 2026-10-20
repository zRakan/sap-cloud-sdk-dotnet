namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>On whose behalf a service binding based destination is created.</summary>
public enum OnBehalfOf
{
    /// <summary>A technical user of the provider tenant.</summary>
    TechnicalUserProvider,

    /// <summary>A technical user of the current tenant.</summary>
    TechnicalUserCurrentTenant,

    /// <summary>A named user of the current tenant.</summary>
    NamedUserCurrentTenant,
}
