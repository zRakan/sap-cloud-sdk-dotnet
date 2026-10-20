using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>
/// Runs an operation with a dedicated <see cref="IThreadContext"/> that is populated by
/// <see cref="IThreadContextListener"/>s.
/// </summary>
/// <remarks>
/// <para>
/// The operation always runs with a fresh duplicate of the configured context, so it cannot change the caller's
/// context. After a synchronous execution the caller's context is restored; for asynchronous executions this happens
/// automatically because <see cref="AsyncLocal{T}"/> changes do not flow back to the caller.
/// </para>
/// <para>
/// Exceptions thrown by the operation are wrapped in <see cref="ThreadContextExecutionException"/>, matching the Java
/// SDK. <see cref="OperationCanceledException"/> is not wrapped, so cancellation behaves as callers expect.
/// </para>
/// </remarks>
public sealed class ThreadContextExecutor
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ThreadContextExecutor>();

    private readonly IThreadContext _threadContext;
    private readonly DefaultThreadContextListenerChain _listenerChain = new();
    private bool _wrapExceptions = true;

    private ThreadContextExecutor(IThreadContext threadContext)
    {
        _threadContext = threadContext;
    }

    /// <summary>Creates an executor based on a copy of the current context.</summary>
    /// <returns>The executor.</returns>
    /// <exception cref="ThreadContextAccessException">If there is no current context.</exception>
    public static ThreadContextExecutor FromCurrentContext() =>
        new(ThreadContextAccessor.GetCurrentContext().Duplicate());

    /// <summary>Creates an executor based on a copy of the current context, or on a new context if there is none.</summary>
    /// <returns>The executor.</returns>
    public static ThreadContextExecutor FromCurrentOrNewContext()
    {
        var current = ThreadContextAccessor.GetCurrentContextOrNull();
        return current is not null ? new(current.Duplicate()) : FromNewContext();
    }

    /// <summary>Creates an executor based on a new, empty context.</summary>
    /// <returns>The executor.</returns>
    public static ThreadContextExecutor FromNewContext() => new(new DefaultThreadContext());

    /// <summary>Creates an executor based on a copy of the given context.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The executor.</returns>
    public static ThreadContextExecutor Using(IThreadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(context.Duplicate());
    }

    /// <summary>
    /// Lets exceptions of the operation propagate unchanged instead of wrapping them in
    /// <see cref="ThreadContextExecutionException"/>. Use this where callers expect the original exception types, for
    /// example in middleware.
    /// </summary>
    /// <returns>This executor.</returns>
    public ThreadContextExecutor WithoutExceptionWrapping()
    {
        _wrapExceptions = false;
        return this;
    }

    /// <summary>Removes the default listeners.</summary>
    /// <returns>This executor.</returns>
    public ThreadContextExecutor WithoutDefaultListeners()
    {
        _listenerChain.RemoveDefaultListeners();
        return this;
    }

    /// <summary>Adds listeners, replacing any with the same priority.</summary>
    /// <param name="listeners">The listeners.</param>
    /// <returns>This executor.</returns>
    public ThreadContextExecutor WithListeners(params IEnumerable<IThreadContextListener> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        foreach (var listener in listeners)
        {
            _listenerChain.AddListener(listener);
        }
        return this;
    }

    /// <summary>Removes the listeners with the given priorities.</summary>
    /// <param name="priorities">The priorities.</param>
    /// <returns>This executor.</returns>
    public ThreadContextExecutor WithoutListeners(params IEnumerable<int> priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);
        foreach (var priority in priorities)
        {
            _listenerChain.RemoveListener(priority);
        }
        return this;
    }

    /// <summary>The listeners that will run, ordered by priority.</summary>
    public IReadOnlyList<IThreadContextListener> ListenersOrderedByPriority =>
        _listenerChain.ListenersOrderedByPriority;

    /// <summary>Gets the listener with the given priority.</summary>
    /// <typeparam name="T">The expected listener type.</typeparam>
    /// <param name="priority">The priority.</param>
    /// <returns>The listener, or <see langword="null"/> if absent or of another type.</returns>
    public T? GetListener<T>(int priority)
        where T : class, IThreadContextListener => _listenerChain.GetListener(priority) as T;

    /// <summary>Runs <paramref name="func"/> within the context.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation or a listener fails.</exception>
    public T Execute<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        var facade = ThreadContextAccessor.ThreadContextFacade;
        var initialContext = facade.GetCurrentContextOrNull();
        try
        {
            Initialize(facade);
            return func();
        }
        catch (Exception e) when (_wrapExceptions && e is not ThreadContextExecutionException and not OperationCanceledException)
        {
            throw new ThreadContextExecutionException(e.Message, e);
        }
        finally
        {
            ResetCurrentContext(facade, initialContext);
        }
    }

    /// <summary>Runs <paramref name="action"/> within the context.</summary>
    /// <param name="action">The operation.</param>
    /// <exception cref="ThreadContextExecutionException">If the operation or a listener fails.</exception>
    public void Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Execute<object?>(() =>
        {
            action();
            return null;
        });
    }

    /// <summary>Runs <paramref name="func"/> within the context.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="func">The asynchronous operation.</param>
    /// <returns>The result of <paramref name="func"/>.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation or a listener fails.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        // AsyncLocal changes made inside this async method do not flow back to the caller,
        // so the caller's context needs no explicit restore.
        try
        {
            Initialize(ThreadContextAccessor.ThreadContextFacade);
            return await func().ConfigureAwait(false);
        }
        catch (Exception e) when (_wrapExceptions && e is not ThreadContextExecutionException and not OperationCanceledException)
        {
            throw new ThreadContextExecutionException(e.Message, e);
        }
    }

    /// <summary>Runs <paramref name="func"/> within the context.</summary>
    /// <param name="func">The asynchronous operation.</param>
    /// <returns>A task that completes when <paramref name="func"/> completes.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation or a listener fails.</exception>
    public Task ExecuteAsync(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return ExecuteAsync<object?>(async () =>
        {
            await func().ConfigureAwait(false);
            return null;
        });
    }

    private void Initialize(IThreadContextFacade facade)
    {
        var executionContext = _threadContext.Duplicate();
        var listeners = _listenerChain.ListenersOrderedByPriority;

        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Notify before initialize: {Context}", executionContext);
        }
        foreach (var listener in listeners)
        {
            listener.BeforeInitialize(executionContext);
        }

        facade.SetCurrentContext(executionContext);

        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Notify after initialize: {Context}", executionContext);
        }
        foreach (var listener in listeners)
        {
            listener.AfterInitialize(executionContext);
        }
    }

    private static void ResetCurrentContext(IThreadContextFacade facade, IThreadContext? initialContext)
    {
        if (initialContext is not null)
        {
            facade.SetCurrentContext(initialContext);
        }
        else
        {
            facade.RemoveCurrentContext();
        }
    }
}
