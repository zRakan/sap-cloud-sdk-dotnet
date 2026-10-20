using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>Gives access to the headers of the current request.</summary>
public static class RequestHeaderAccessor
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(RequestHeaderAccessor).FullName!);
    private static IRequestHeaderFacade _headerFacade = new DefaultRequestHeaderFacade();

    /// <summary>
    /// The facade providing the headers. Setting <see langword="null"/> restores <see cref="DefaultRequestHeaderFacade"/>.
    /// </summary>
    public static IRequestHeaderFacade HeaderFacade
    {
        get => _headerFacade;
        set => _headerFacade = value ?? new DefaultRequestHeaderFacade();
    }

    /// <summary>Provides headers to use when none are available from <see cref="HeaderFacade"/>.</summary>
    public static Func<IRequestHeaderContainer>? FallbackHeaderContainer { get; set; }

    /// <summary>Gets the headers of the current request.</summary>
    /// <returns>The headers.</returns>
    /// <exception cref="RequestHeadersAccessException">If no headers are available.</exception>
    public static IRequestHeaderContainer GetHeaderContainer() =>
        TryGetHeaderContainer().GetValueOrThrow(e => e as RequestHeadersAccessException
            ?? new RequestHeadersAccessException("Failed to get current request headers.", e));

    /// <summary>Gets the headers of the current request, falling back to <see cref="FallbackHeaderContainer"/>.</summary>
    /// <returns>The headers, or a failure.</returns>
    public static Result<IRequestHeaderContainer> TryGetHeaderContainer()
    {
        var headers = _headerFacade.TryGetRequestHeaders();
        var fallback = FallbackHeaderContainer;
        if (headers.IsSuccess || fallback is null)
        {
            return headers;
        }
        return headers.Recover(failure =>
        {
            var fallbackHeaders = fallback();
            Log.LogWarning(failure, "Recovering with fallback request headers: {Headers}.", fallbackHeaders);
            return fallbackHeaders;
        });
    }

    /// <summary>Runs <paramref name="func"/> with the given request headers.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="headers">The headers.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation fails.</exception>
    public static T ExecuteWithHeaderContainer<T>(IRequestHeaderContainer headers, Func<T> func) =>
        CreateExecutor(headers).Execute(func);

    /// <inheritdoc cref="ExecuteWithHeaderContainer{T}(IRequestHeaderContainer, Func{T})"/>
    public static void ExecuteWithHeaderContainer(IRequestHeaderContainer headers, Action action) =>
        CreateExecutor(headers).Execute(action);

    /// <inheritdoc cref="ExecuteWithHeaderContainer{T}(IRequestHeaderContainer, Func{T})"/>
    public static Task<T> ExecuteWithHeaderContainerAsync<T>(IRequestHeaderContainer headers, Func<Task<T>> func) =>
        CreateExecutor(headers).ExecuteAsync(func);

    /// <inheritdoc cref="ExecuteWithHeaderContainer{T}(IRequestHeaderContainer, Func{T})"/>
    public static Task ExecuteWithHeaderContainerAsync(IRequestHeaderContainer headers, Func<Task> func) =>
        CreateExecutor(headers).ExecuteAsync(func);

    /// <summary>Runs <paramref name="func"/> with the given single-valued request headers.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="headers">The headers.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    public static T ExecuteWithHeaderContainer<T>(IReadOnlyDictionary<string, string?> headers, Func<T> func) =>
        ExecuteWithHeaderContainer(DefaultRequestHeaderContainer.FromSingleValueMap(headers), func);

    /// <summary>
    /// Runs <paramref name="func"/> with the current headers if there are any, otherwise with the headers from
    /// <paramref name="fallbackHeaders"/>.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="fallbackHeaders">Provides the fallback headers.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    public static T ExecuteWithFallbackHeaderContainer<T>(Func<IRequestHeaderContainer> fallbackHeaders, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackHeaders);
        ArgumentNullException.ThrowIfNull(func);
        if (TryGetHeaderContainer().IsSuccess)
        {
            return ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().Execute(func);
        }
        return ExecuteWithHeaderContainer(fallbackHeaders(), func);
    }

    /// <inheritdoc cref="ExecuteWithFallbackHeaderContainer{T}(Func{IRequestHeaderContainer}, Func{T})"/>
    public static Task<T> ExecuteWithFallbackHeaderContainerAsync<T>(
        Func<IRequestHeaderContainer> fallbackHeaders,
        Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackHeaders);
        ArgumentNullException.ThrowIfNull(func);
        if (TryGetHeaderContainer().IsSuccess)
        {
            return ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().ExecuteAsync(func);
        }
        return ExecuteWithHeaderContainerAsync(fallbackHeaders(), func);
    }

    private static ThreadContextExecutor CreateExecutor(IRequestHeaderContainer headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (_headerFacade is not DefaultRequestHeaderFacade)
        {
            throw new ThreadContextExecutionException(
                $"The 'ExecuteWith...' API is only supported with {nameof(DefaultRequestHeaderFacade)}, "
                + $"but {_headerFacade.GetType().FullName} is configured.");
        }
        return DefaultRequestHeaderFacade.CreateExecutor(headers);
    }
}
