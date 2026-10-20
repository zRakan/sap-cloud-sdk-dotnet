using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal;

/// <summary>Stores the <see cref="ISecurityPrincipal"/> in the <see cref="IThreadContext"/>.</summary>
public class PrincipalThreadContextListener : IThreadContextListener
{
    /// <summary>The name of the thread context property holding the <see cref="ISecurityPrincipal"/>.</summary>
    public const string PropertyPrincipal =
        "Sap.Cloud.Sdk.CloudPlatform.Security.Principal.PrincipalThreadContextListener:principal";

    private readonly ISecurityPrincipal? _principal;

    /// <summary>Creates a listener that resolves the current principal when the context is initialized.</summary>
    public PrincipalThreadContextListener()
    {
    }

    /// <summary>Creates a listener that stores the given principal.</summary>
    /// <param name="principal">The principal.</param>
    public PrincipalThreadContextListener(ISecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        _principal = principal;
    }

    /// <inheritdoc/>
    public int Priority => ThreadContextListenerPriorities.PrincipalListener;

    /// <inheritdoc/>
    public void AfterInitialize(IThreadContext threadContext)
    {
        ArgumentNullException.ThrowIfNull(threadContext);
        if (_principal is not null)
        {
            threadContext.SetProperty(PropertyPrincipal, ThreadContextProperty.Of(_principal));
        }
        else
        {
            threadContext.SetPropertyIfAbsent(
                PropertyPrincipal,
                () => ThreadContextProperty.Capture(PrincipalAccessor.GetCurrentPrincipal));
        }
    }
}
