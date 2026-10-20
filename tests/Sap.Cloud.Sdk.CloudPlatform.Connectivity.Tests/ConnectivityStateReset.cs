using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

/// <summary>Restores the static state of the Core, Security and Connectivity modules after each test.</summary>
public abstract class ConnectivityStateReset : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        AuthTokenAccessor.AuthTokenFacade = null!;
        AuthTokenAccessor.FallbackToken = null;
        DestinationAccessor.Loader = null!;
        foreach (var loader in DestinationAccessorTestHooks.RegisteredDefaultLoaders)
        {
            DestinationAccessor.UnregisterDefaultLoader(loader);
        }
        DestinationAccessorTestHooks.RegisteredDefaultLoaders.Clear();
        ServiceBindingDestinationLoader.ClearDefaultLoaders();
        DestinationHeaderProviders.UnregisterAll<IDestinationHeaderProvider>();
        DestinationHeaderProviders.Register(new ErpDestinationHeaderProvider());
    }
}

/// <summary>Tracks default loaders registered by tests so that they can be removed again.</summary>
internal static class DestinationAccessorTestHooks
{
    public static List<IDestinationLoader> RegisteredDefaultLoaders { get; } = [];

    public static void RegisterDefaultLoader(IDestinationLoader loader)
    {
        DestinationAccessor.RegisterDefaultLoader(loader);
        RegisteredDefaultLoaders.Add(loader);
    }
}
