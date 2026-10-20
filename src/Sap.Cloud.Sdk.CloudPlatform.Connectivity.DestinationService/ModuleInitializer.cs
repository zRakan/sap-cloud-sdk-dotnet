using System.Runtime.CompilerServices;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Registers this assembly's default services (Java: <c>META-INF/services</c>).</summary>
internal static class ModuleInitializer
{
#pragma warning disable CA2255 // Intentional: replaces Java's ServiceLoader registration of the destination service loader.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        // The destination service is reached through a destination created from its service binding by the loaders that the
        // OAuth module registers. In Java, the ServiceLoader finds them because the module is on the classpath; here the
        // OAuth assembly would not even be loaded if no code of this assembly used one of its types.
        RuntimeHelpers.RunModuleConstructor(typeof(OAuth2ServiceBindingDestinationLoader).Module.ModuleHandle);

        DestinationService.EnsureRegisteredAsDefaultLoader();
    }
}
