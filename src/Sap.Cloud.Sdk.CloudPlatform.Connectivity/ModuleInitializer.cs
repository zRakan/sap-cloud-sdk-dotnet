using System.Runtime.CompilerServices;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Registers this assembly's default services (Java: <c>META-INF/services</c>).</summary>
internal static class ModuleInitializer
{
#pragma warning disable CA2255 // Intentional: replaces Java's ServiceLoader registration of the ERP header provider.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() => DestinationHeaderProviders.Register(new ErpDestinationHeaderProvider());
}
