namespace Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

/// <summary>Reads the service bindings of the application.</summary>
public interface IServiceBindingAccessor
{
    /// <summary>Gets all service bindings.</summary>
    /// <returns>The bindings; empty if there are none.</returns>
    IReadOnlyList<IServiceBinding> GetServiceBindings();
}
