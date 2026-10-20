using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Identifies a kind of BTP service by the name under which it is bound.</summary>
/// <param name="Id">The service name, for example <c>destination</c>.</param>
/// <remarks>Port of <c>com.sap.cloud.environment.servicebinding.api.ServiceIdentifier</c>.</remarks>
public sealed record ServiceIdentifier(string Id)
{
    /// <summary>The SAP BTP, ABAP environment (<c>abap</c>).</summary>
    public static readonly ServiceIdentifier AbapEnvironment = new("abap");

    /// <summary>SAP AI Core (<c>aicore</c>).</summary>
    public static readonly ServiceIdentifier AiCore = new("aicore");

    /// <summary>The audit log retrieval API (<c>auditlog-management</c>).</summary>
    public static readonly ServiceIdentifier AuditLogRetrieval = new("auditlog-management");

    /// <summary>Business logging (<c>business-logging</c>).</summary>
    public static readonly ServiceIdentifier BusinessLogging = new("business-logging");

    /// <summary>Business rules (<c>business-rules</c>).</summary>
    public static readonly ServiceIdentifier BusinessRules = new("business-rules");

    /// <summary>The connectivity service (<c>connectivity</c>).</summary>
    public static readonly ServiceIdentifier Connectivity = new("connectivity");

    /// <summary>The destination service (<c>destination</c>).</summary>
    public static readonly ServiceIdentifier Destination = new("destination");

    /// <summary>The identity authentication service, IAS (<c>identity</c>).</summary>
    public static readonly ServiceIdentifier IdentityAuthentication = new("identity");

    /// <summary>The service manager (<c>service-manager</c>).</summary>
    public static readonly ServiceIdentifier ServiceManager = new("service-manager");

    /// <summary>Workflow (<c>workflow</c>).</summary>
    public static readonly ServiceIdentifier Workflow = new("workflow");

    /// <summary>The authorization and trust management service, XSUAA (<c>xsuaa</c>).</summary>
    public static readonly ServiceIdentifier Xsuaa = new("xsuaa");

    /// <summary>Returns the id.</summary>
    /// <returns>The id.</returns>
    public override string ToString() => Id;
}

/// <summary>Service identifier lookup for <see cref="IServiceBinding"/>.</summary>
public static class ServiceBindingExtensions
{
    /// <summary>Gets the identifier of the bound service.</summary>
    /// <param name="serviceBinding">The binding.</param>
    /// <returns>The identifier, or <see langword="null"/> if the binding has no service name.</returns>
    public static ServiceIdentifier? GetServiceIdentifier(this IServiceBinding serviceBinding)
    {
        ArgumentNullException.ThrowIfNull(serviceBinding);
        return serviceBinding.ServiceName is { Length: > 0 } name ? new ServiceIdentifier(name) : null;
    }
}
