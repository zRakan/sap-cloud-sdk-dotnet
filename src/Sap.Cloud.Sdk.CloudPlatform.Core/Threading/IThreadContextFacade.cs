namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>Stores and retrieves the current <see cref="IThreadContext"/>.</summary>
public interface IThreadContextFacade
{
    /// <summary>Gets the current context.</summary>
    /// <returns>The current context, or a failure if there is none.</returns>
    Result<IThreadContext> TryGetCurrentContext();

    /// <summary>Gets the current context.</summary>
    /// <returns>The current context, or <see langword="null"/> if there is none.</returns>
    IThreadContext? GetCurrentContextOrNull() => TryGetCurrentContext().GetValueOrDefault();

    /// <summary>Sets the current context.</summary>
    /// <param name="threadContext">The context.</param>
    void SetCurrentContext(IThreadContext threadContext);

    /// <summary>Removes the current context.</summary>
    void RemoveCurrentContext();
}
