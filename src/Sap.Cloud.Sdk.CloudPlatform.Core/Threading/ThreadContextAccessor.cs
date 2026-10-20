using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>Gives access to the current <see cref="IThreadContext"/>.</summary>
public static class ThreadContextAccessor
{
    private static IThreadContextFacade _facade = new AsyncLocalThreadContextFacade();

    /// <summary>
    /// The facade used to store the current context. Setting <see langword="null"/> restores the default
    /// <see cref="AsyncLocalThreadContextFacade"/>.
    /// </summary>
    public static IThreadContextFacade ThreadContextFacade
    {
        get => _facade;
        set => _facade = value ?? new AsyncLocalThreadContextFacade();
    }

    /// <summary>Gets the current context.</summary>
    /// <returns>The current context.</returns>
    /// <exception cref="ThreadContextAccessException">If there is no current context.</exception>
    public static IThreadContext GetCurrentContext() =>
        TryGetCurrentContext().GetValueOrThrow(e => e as ThreadContextAccessException
            ?? new ThreadContextAccessException("Failed to get current thread context.", e));

    /// <summary>Gets the current context.</summary>
    /// <returns>The current context, or a failure.</returns>
    public static Result<IThreadContext> TryGetCurrentContext() => _facade.TryGetCurrentContext();

    /// <summary>Gets the current context.</summary>
    /// <returns>The current context, or <see langword="null"/>.</returns>
    public static IThreadContext? GetCurrentContextOrNull() => _facade.GetCurrentContextOrNull();
}
