using System.Runtime.CompilerServices;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform;

/// <summary>Registers this assembly's default thread context listeners (Java: <c>META-INF/services</c>).</summary>
internal static class ModuleInitializer
{
#pragma warning disable CA2255 // Intentional: replaces Java's ServiceLoader registration of default listeners.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() =>
        DefaultThreadContextListenerChain.RegisterDefaultListener(new RequestHeaderThreadContextListener());
}
