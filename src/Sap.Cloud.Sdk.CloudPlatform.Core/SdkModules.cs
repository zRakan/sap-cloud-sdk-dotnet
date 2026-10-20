using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform;

/// <summary>
/// Loads the SDK assemblies that ship with the application so that their default registrations (listeners, loaders,
/// header providers) are in place.
/// </summary>
/// <remarks>
/// The Java SDK discovers such registrations with <c>ServiceLoader</c>, which sees every module on the classpath. .NET
/// only loads an assembly when code uses one of its types, so a module whose types are never referenced directly (for
/// example the Destination service loader) would never register itself. The SDK's registries call
/// <see cref="EnsureLoaded"/> before their first use; applications do not need to call it.
/// </remarks>
public static class SdkModules
{
    // Assemblies that register defaults from a module initializer. Missing ones (not deployed) are skipped.
    private static readonly string[] ModulesWithRegistrations =
    [
        "Sap.Cloud.Sdk.CloudPlatform.Security",
        "Sap.Cloud.Sdk.CloudPlatform.Tenant",
        "Sap.Cloud.Sdk.CloudPlatform.Connectivity",
        "Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http",
        "Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth",
        "Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationService",
    ];

    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(SdkModules).FullName!);
    private static readonly Lock Gate = new();
    private static volatile bool _loaded;
    private static bool _loading;

    /// <summary>Loads all deployed SDK modules and runs their registrations. Safe to call repeatedly and concurrently.</summary>
    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }
        lock (Gate)
        {
            // Re-entrant calls come from module initializers that use a registry while being loaded.
            if (_loaded || _loading)
            {
                return;
            }
            _loading = true;
            try
            {
                foreach (var name in ModulesWithRegistrations)
                {
                    Load(name);
                }
                _loaded = true;
            }
            finally
            {
                _loading = false;
            }
        }
    }

    private static void Load(string name)
    {
        try
        {
            var assembly = Assembly.Load(new AssemblyName(name));
            RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        }
        catch (FileNotFoundException)
        {
            // Module not deployed with this application.
        }
        catch (Exception e) when (e is FileLoadException or BadImageFormatException)
        {
            Log.LogWarning(e, "SDK module {Module} could not be loaded; its default registrations are missing.", name);
        }
    }
}
