namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>
/// Populates an <see cref="IThreadContext"/> when <see cref="ThreadContextExecutor"/> starts an execution.
/// </summary>
/// <remarks>
/// Listeners are identified by <see cref="Priority"/>: a chain holds at most one listener per priority, and listeners
/// run in ascending priority order.
/// </remarks>
public interface IThreadContextListener
{
    /// <summary>The priority. Lower values run first. See <see cref="ThreadContextListenerPriorities"/>.</summary>
    int Priority { get; }

    /// <summary>Called before the context becomes current. The previous context is still current here.</summary>
    /// <param name="threadContext">The context about to become current.</param>
    void BeforeInitialize(IThreadContext threadContext)
    {
    }

    /// <summary>Called after the context became current.</summary>
    /// <param name="threadContext">The current context.</param>
    void AfterInitialize(IThreadContext threadContext)
    {
    }
}

/// <summary>Priorities of the SDK's built-in listeners.</summary>
public static class ThreadContextListenerPriorities
{
    /// <summary>Request header listener.</summary>
    public const int RequestHeaderListener = -7;

    /// <summary>Auth token listener.</summary>
    public const int AuthTokenListener = -6;

    /// <summary>Basic Authentication listener.</summary>
    public const int BasicAuthListener = -4;

    /// <summary>Principal listener.</summary>
    public const int PrincipalListener = -3;

    /// <summary>Tenant listener.</summary>
    public const int TenantListener = -2;

    /// <summary>Default priority for application-defined listeners.</summary>
    public const int CustomListener = 0;
}
