using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Tenant;

/// <summary>Gives access to the tenant of the current request.</summary>
public static class TenantAccessor
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(TenantAccessor).FullName!);
    private static ITenantFacade _tenantFacade = new DefaultTenantFacade();

    /// <summary>
    /// The facade providing the tenant. Setting <see langword="null"/> restores <see cref="DefaultTenantFacade"/>.
    /// </summary>
    public static ITenantFacade TenantFacade
    {
        get => _tenantFacade;
        set => _tenantFacade = value ?? new DefaultTenantFacade();
    }

    /// <summary>
    /// Provides a tenant to use when none is available from <see cref="TenantFacade"/>. If it returns
    /// <see langword="null"/>, the lookup fails.
    /// </summary>
    public static Func<ITenant?>? FallbackTenant { get; set; }

    /// <summary>Gets the current tenant.</summary>
    /// <returns>The tenant.</returns>
    /// <exception cref="TenantAccessException">If no tenant is available.</exception>
    public static ITenant GetCurrentTenant() =>
        TryGetCurrentTenant().GetValueOrThrow(e => e as TenantAccessException
            ?? new TenantAccessException("Failed to get current tenant.", e));

    /// <summary>Gets the current tenant, falling back to <see cref="FallbackTenant"/>.</summary>
    /// <returns>The tenant, or a failure.</returns>
    public static Result<ITenant> TryGetCurrentTenant()
    {
        var tenant = _tenantFacade.TryGetCurrentTenant();
        var fallbackFactory = FallbackTenant;
        if (tenant.IsSuccess || fallbackFactory is null)
        {
            return tenant;
        }
        var fallback = fallbackFactory();
        if (fallback is null)
        {
            return Result.Failure<ITenant>(new TenantAccessException(
                "Failed to get current tenant, and the fallback provided none.", tenant.Exception));
        }
        Log.LogWarning(tenant.Exception, "Recovering with fallback tenant: {Tenant}.", fallback);
        return Result.Success(fallback);
    }

    /// <summary>Runs <paramref name="func"/> with the given tenant as current tenant.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="tenant">The tenant.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    /// <exception cref="ThreadContextExecutionException">If the operation fails.</exception>
    public static T ExecuteWithTenant<T>(ITenant tenant, Func<T> func) => CreateExecutor(tenant).Execute(func);

    /// <inheritdoc cref="ExecuteWithTenant{T}(ITenant, Func{T})"/>
    public static void ExecuteWithTenant(ITenant tenant, Action action) => CreateExecutor(tenant).Execute(action);

    /// <inheritdoc cref="ExecuteWithTenant{T}(ITenant, Func{T})"/>
    public static Task<T> ExecuteWithTenantAsync<T>(ITenant tenant, Func<Task<T>> func) =>
        CreateExecutor(tenant).ExecuteAsync(func);

    /// <inheritdoc cref="ExecuteWithTenant{T}(ITenant, Func{T})"/>
    public static Task ExecuteWithTenantAsync(ITenant tenant, Func<Task> func) =>
        CreateExecutor(tenant).ExecuteAsync(func);

    /// <summary>
    /// Runs <paramref name="func"/> with the current tenant if there is one, otherwise with the tenant from
    /// <paramref name="fallbackTenant"/>.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="fallbackTenant">Provides the fallback tenant.</param>
    /// <param name="func">The operation.</param>
    /// <returns>The return value of <paramref name="func"/>.</returns>
    public static T ExecuteWithFallbackTenant<T>(Func<ITenant> fallbackTenant, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackTenant);
        ArgumentNullException.ThrowIfNull(func);
        return TryGetCurrentTenant().IsSuccess
            ? ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().Execute(func)
            : ExecuteWithTenant(fallbackTenant(), func);
    }

    /// <inheritdoc cref="ExecuteWithFallbackTenant{T}(Func{ITenant}, Func{T})"/>
    public static Task<T> ExecuteWithFallbackTenantAsync<T>(Func<ITenant> fallbackTenant, Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(fallbackTenant);
        ArgumentNullException.ThrowIfNull(func);
        return TryGetCurrentTenant().IsSuccess
            ? ThreadContextExecutor.FromCurrentOrNewContext().WithoutDefaultListeners().ExecuteAsync(func)
            : ExecuteWithTenantAsync(fallbackTenant(), func);
    }

    private static ThreadContextExecutor CreateExecutor(ITenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (_tenantFacade is not DefaultTenantFacade)
        {
            throw new ThreadContextExecutionException(
                $"The 'ExecuteWith...' API is only supported with {nameof(DefaultTenantFacade)}, "
                + $"but {_tenantFacade.GetType().FullName} is configured.");
        }
        return DefaultTenantFacade.CreateExecutor(tenant);
    }
}
