using System.Runtime.CompilerServices;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Registers this assembly's service binding destination loaders (Java: <c>META-INF/services</c>).</summary>
internal static class ModuleInitializer
{
#pragma warning disable CA2255 // Intentional: replaces Java's ServiceLoader registration.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        ServiceBindingDestinationLoader.RegisterDefaultLoader(new OAuth2ServiceBindingDestinationLoader());
        ServiceBindingDestinationLoader.RegisterDefaultLoader(new IdentityAuthenticationServiceBindingDestinationLoader());
    }
}
