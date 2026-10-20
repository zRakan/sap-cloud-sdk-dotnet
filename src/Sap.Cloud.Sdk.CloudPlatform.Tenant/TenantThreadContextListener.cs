using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Tenant;

/// <summary>Stores the <see cref="ITenant"/> in the <see cref="IThreadContext"/>.</summary>
public class TenantThreadContextListener : IThreadContextListener
{
    /// <summary>The name of the thread context property holding the <see cref="ITenant"/>.</summary>
    public const string PropertyTenant = "Sap.Cloud.Sdk.CloudPlatform.Tenant.TenantThreadContextListener:tenant";

    private readonly ITenant? _tenant;

    /// <summary>Creates a listener that resolves the current tenant when the context is initialized.</summary>
    public TenantThreadContextListener()
    {
    }

    /// <summary>Creates a listener that stores the given tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    public TenantThreadContextListener(ITenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenant = tenant;
    }

    /// <inheritdoc/>
    public int Priority => ThreadContextListenerPriorities.TenantListener;

    /// <inheritdoc/>
    public void AfterInitialize(IThreadContext threadContext)
    {
        ArgumentNullException.ThrowIfNull(threadContext);
        if (_tenant is not null)
        {
            threadContext.SetProperty(PropertyTenant, ThreadContextProperty.Of(_tenant));
        }
        else
        {
            threadContext.SetPropertyIfAbsent(
                PropertyTenant,
                () => ThreadContextProperty.Capture(TenantAccessor.GetCurrentTenant));
        }
    }
}
