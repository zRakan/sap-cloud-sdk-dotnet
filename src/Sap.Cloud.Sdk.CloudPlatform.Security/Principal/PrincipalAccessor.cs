using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal;

/// <summary>Gives access to the principal of the current request.</summary>
public static class PrincipalAccessor
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(PrincipalAccessor).FullName!);
    private static IPrincipalFacade _principalFacade = new DefaultPrincipalFacade();

    /// <summary>
    /// The facade providing the principal. Setting <see langword="null"/> restores <see cref="DefaultPrincipalFacade"/>.
    /// </summary>
    public static IPrincipalFacade PrincipalFacade
    {
        get => _principalFacade;
        set => _principalFacade = value ?? new DefaultPrincipalFacade();
    }

    /// <summary>
    /// Provides a principal to use when none is available from <see cref="PrincipalFacade"/>. If it returns
    /// <see langword="null"/>, the lookup fails.
    /// </summary>
    public static Func<ISecurityPrincipal?>? FallbackPrincipal { get; set; }

    /// <summary>Gets the current principal.</summary>
    /// <returns>The principal.</returns>
    /// <exception cref="PrincipalAccessException">If no principal is available.</exception>
    public static ISecurityPrincipal GetCurrentPrincipal() =>
        TryGetCurrentPrincipal().GetValueOrThrow(e => e as PrincipalAccessException
            ?? new PrincipalAccessException("Failed to get current principal.", e));

    /// <summary>Gets the current principal, falling back to <see cref="FallbackPrincipal"/>.</summary>
    /// <returns>The principal, or a failure.</returns>
    public static Result<ISecurityPrincipal> TryGetCurrentPrincipal()
    {
        var principal = _principalFacade.TryGetCurrentPrincipal();
        var fallbackFactory = FallbackPrincipal;
        if (principal.IsSuccess || fallbackFactory is null)
        {
            return principal;
        }
        var fallback = fallbackFactory();
        if (fallback is null)
        {
            return Result.Failure<ISecurityPrincipal>(new PrincipalAccessException(
                "Failed to get current principal, and the fallback provided none.", principal.Exception));
        }
        Log.LogWarning(principal.Exception, "Recovering with fallback principal: {Principal}.", fallback);
        return Result.Success(fallback);
    }

    /// <summary>Runs <paramref name="func"/> with the given principal as current principal.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="principal">The principal.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation fails.</exception>
    public static T ExecuteWithPrincipal<T>(ISecurityPrincipal principal, Func<T> func) =>
        CreateExecutor(principal).Execute(func);

    /// <inheritdoc cref="ExecuteWithPrincipal{T}(ISecurityPrincipal, Func{T})"/>
    public static void ExecuteWithPrincipal(ISecurityPrincipal principal, Action action) =>
        CreateExecutor(principal).Execute(action);

    /// <inheritdoc cref="ExecuteWithPrincipal{T}(ISecurityPrincipal, Func{T})"/>
    public static Task<T> ExecuteWithPrincipalAsync<T>(ISecurityPrincipal principal, Func<Task<T>> func) =>
        CreateExecutor(principal).ExecuteAsync(func);

    /// <inheritdoc cref="ExecuteWithPrincipal{T}(ISecurityPrincipal, Func{T})"/>
    public static Task ExecuteWithPrincipalAsync(ISecurityPrincipal principal, Func<Task> func) =>
        CreateExecutor(principal).ExecuteAsync(func);

    /// <summary>
    /// Runs <paramref name="func"/> with the current principal if there is one, otherwise with the principal from
    /// <paramref name="fallbackPrincipal"/>.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="fallbackPrincipal">Provides the fallback principal.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    public static T ExecuteWithFallbackPrincipal<T>(Func<ISecurityPrincipal> fallbackPrincipal, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackPrincipal);
        ArgumentNullException.ThrowIfNull(func);
        return TryGetCurrentPrincipal().IsSuccess
            ? ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().Execute(func)
            : ExecuteWithPrincipal(fallbackPrincipal(), func);
    }

    /// <inheritdoc cref="ExecuteWithFallbackPrincipal{T}(Func{ISecurityPrincipal}, Func{T})"/>
    public static Task<T> ExecuteWithFallbackPrincipalAsync<T>(
        Func<ISecurityPrincipal> fallbackPrincipal,
        Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackPrincipal);
        ArgumentNullException.ThrowIfNull(func);
        return TryGetCurrentPrincipal().IsSuccess
            ? ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().ExecuteAsync(func)
            : ExecuteWithPrincipalAsync(fallbackPrincipal(), func);
    }

    private static ThreadContextExecutor CreateExecutor(ISecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (_principalFacade is not DefaultPrincipalFacade)
        {
            throw new ThreadContextExecutionException(
                $"The 'ExecuteWith...' API is only supported with {nameof(DefaultPrincipalFacade)}, "
                + $"but {_principalFacade.GetType().FullName} is configured.");
        }
        return DefaultPrincipalFacade.CreateExecutor(principal);
    }
}
