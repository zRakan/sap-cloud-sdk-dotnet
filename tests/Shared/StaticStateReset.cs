using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.TestSupport;

/// <summary>Base class that restores the Core module's static state after each test.</summary>
public abstract class StaticStateReset : IDisposable
{
    protected StaticStateReset()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
        GC.SuppressFinalize(this);
    }

    protected virtual void Reset()
    {
        ThreadContextAccessor.ThreadContextFacade.RemoveCurrentContext();
        RequestHeaderAccessor.HeaderFacade = null!;
        RequestHeaderAccessor.FallbackHeaderContainer = null;
        DefaultServiceBindingAccessor.Instance = new CombiningServiceBindingAccessor();
    }
}
