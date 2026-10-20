using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>Gives access to the authorization token of the current request.</summary>
public static class AuthTokenAccessor
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(AuthTokenAccessor).FullName!);
    private static IAuthTokenFacade _authTokenFacade = new DefaultAuthTokenFacade();

    /// <summary>
    /// The facade providing the token. Setting <see langword="null"/> restores <see cref="DefaultAuthTokenFacade"/>.
    /// </summary>
    public static IAuthTokenFacade AuthTokenFacade
    {
        get => _authTokenFacade;
        set => _authTokenFacade = value ?? new DefaultAuthTokenFacade();
    }

    /// <summary>Provides a token to use when none is available from <see cref="AuthTokenFacade"/>.</summary>
    public static Func<AuthToken>? FallbackToken { get; set; }

    /// <summary>Gets the current token.</summary>
    /// <returns>The token.</returns>
    /// <exception cref="AuthTokenAccessException">If no token is available.</exception>
    public static AuthToken GetCurrentToken() =>
        TryGetCurrentToken().GetValueOrThrow(e => e as AuthTokenAccessException
            ?? new AuthTokenAccessException("Failed to get current authorization token.", e));

    /// <summary>Gets the current token, falling back to <see cref="FallbackToken"/>.</summary>
    /// <returns>The token, or a failure.</returns>
    public static Result<AuthToken> TryGetCurrentToken()
    {
        var token = _authTokenFacade.TryGetCurrentToken();
        var fallback = FallbackToken;
        if (token.IsSuccess || fallback is null)
        {
            return token;
        }
        return token.Recover(failure =>
        {
            var fallbackToken = fallback();
            Log.LogWarning(failure, "Recovering with fallback token: {Token}.", fallbackToken);
            return fallbackToken;
        });
    }

    /// <summary>Runs <paramref name="func"/> with the given token as current token.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="authToken">The token.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation fails.</exception>
    public static T ExecuteWithAuthToken<T>(AuthToken authToken, Func<T> func) =>
        CreateExecutor(authToken).Execute(func);

    /// <inheritdoc cref="ExecuteWithAuthToken{T}(AuthToken, Func{T})"/>
    public static void ExecuteWithAuthToken(AuthToken authToken, Action action) =>
        CreateExecutor(authToken).Execute(action);

    /// <inheritdoc cref="ExecuteWithAuthToken{T}(AuthToken, Func{T})"/>
    public static Task<T> ExecuteWithAuthTokenAsync<T>(AuthToken authToken, Func<Task<T>> func) =>
        CreateExecutor(authToken).ExecuteAsync(func);

    /// <inheritdoc cref="ExecuteWithAuthToken{T}(AuthToken, Func{T})"/>
    public static Task ExecuteWithAuthTokenAsync(AuthToken authToken, Func<Task> func) =>
        CreateExecutor(authToken).ExecuteAsync(func);

    /// <summary>
    /// Runs <paramref name="func"/> with the current token if there is one, otherwise with the token from
    /// <paramref name="fallbackToken"/>.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="fallbackToken">Provides the fallback token.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    public static T ExecuteWithFallbackAuthToken<T>(Func<AuthToken> fallbackToken, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackToken);
        ArgumentNullException.ThrowIfNull(func);
        return TryGetCurrentToken().IsSuccess
            ? ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().Execute(func)
            : ExecuteWithAuthToken(fallbackToken(), func);
    }

    /// <inheritdoc cref="ExecuteWithFallbackAuthToken{T}(Func{AuthToken}, Func{T})"/>
    public static Task<T> ExecuteWithFallbackAuthTokenAsync<T>(Func<AuthToken> fallbackToken, Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackToken);
        ArgumentNullException.ThrowIfNull(func);
        return TryGetCurrentToken().IsSuccess
            ? ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().ExecuteAsync(func)
            : ExecuteWithAuthTokenAsync(fallbackToken(), func);
    }

    private static ThreadContextExecutor CreateExecutor(AuthToken authToken)
    {
        ArgumentNullException.ThrowIfNull(authToken);
        if (_authTokenFacade is not DefaultAuthTokenFacade)
        {
            throw new ThreadContextExecutionException(
                $"The 'ExecuteWith...' API is only supported with {nameof(DefaultAuthTokenFacade)}, "
                + $"but {_authTokenFacade.GetType().FullName} is configured.");
        }
        return DefaultAuthTokenFacade.CreateExecutor(authToken);
    }
}
