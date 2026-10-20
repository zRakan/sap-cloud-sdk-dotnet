using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>
/// Stores the current <see cref="IThreadContext"/> in an <see cref="AsyncLocal{T}"/>.
/// </summary>
/// <remarks>
/// Replaces <c>ThreadLocalThreadContextFacade</c> from the Java SDK. The context flows into child tasks and across
/// <see langword="await"/> automatically, so the Java SDK's <c>ThreadContextExecutors</c> and
/// <c>ThreadContextExecutorService</c> are not needed. Changes made inside an <see langword="async"/> method do not
/// leak to its caller.
/// </remarks>
public class AsyncLocalThreadContextFacade : IThreadContextFacade
{
    private static readonly AsyncLocal<IThreadContext?> CurrentContext = new();

    /// <inheritdoc/>
    public Result<IThreadContext> TryGetCurrentContext()
    {
        var context = CurrentContext.Value;
        return context is not null
            ? Result.Success(context)
            : Result.Failure<IThreadContext>(new ThreadContextAccessException(
                $"No {nameof(IThreadContext)} available for the current execution flow."));
    }

    /// <inheritdoc/>
    public IThreadContext? GetCurrentContextOrNull() => CurrentContext.Value;

    /// <inheritdoc/>
    public void SetCurrentContext(IThreadContext threadContext)
    {
        ArgumentNullException.ThrowIfNull(threadContext);
        CurrentContext.Value = threadContext;
    }

    /// <inheritdoc/>
    public void RemoveCurrentContext() => CurrentContext.Value = null;
}
