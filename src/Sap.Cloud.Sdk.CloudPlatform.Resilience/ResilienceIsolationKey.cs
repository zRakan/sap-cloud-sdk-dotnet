using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>
/// The tenant and principal that separate resilience state for a <see cref="ResilienceIsolationMode"/>.
/// </summary>
/// <remarks>
/// Equality compares tenant and principal identifiers (Java compares the tenant and principal objects), so
/// two keys with the same identifiers always share state regardless of the implementation type.
/// </remarks>
public sealed class ResilienceIsolationKey : IEquatable<ResilienceIsolationKey>
{
    private ResilienceIsolationKey(ITenant? tenant, ISecurityPrincipal? principal)
    {
        Tenant = tenant;
        Principal = principal;
    }

    /// <summary>The tenant, or <see langword="null"/> if the key is not isolated by tenant (or none was available).</summary>
    public ITenant? Tenant { get; }

    /// <summary>The principal, or <see langword="null"/> if the key is not isolated by principal (or none was available).</summary>
    public ISecurityPrincipal? Principal { get; }

    /// <summary>Determines the isolation key for the current context.</summary>
    /// <param name="isolationMode">The isolation mode.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ResilienceRuntimeException">If a required tenant or principal is not available.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the mode is unknown.</exception>
    public static ResilienceIsolationKey Of(ResilienceIsolationMode isolationMode) => isolationMode switch
    {
        ResilienceIsolationMode.NoIsolation => new(null, null),
        ResilienceIsolationMode.TenantRequired => new(GetTenantRequired(), null),
        ResilienceIsolationMode.TenantOptional => new(GetTenantOptional(), null),
        ResilienceIsolationMode.PrincipalRequired => new(null, GetPrincipalRequired()),
        ResilienceIsolationMode.PrincipalOptional => new(null, GetPrincipalOptional()),
        ResilienceIsolationMode.TenantAndUserRequired => new(GetTenantRequired(), GetPrincipalRequired()),
        ResilienceIsolationMode.TenantAndUserOptional => new(GetTenantOptional(), GetPrincipalOptional()),
        _ => throw new ArgumentOutOfRangeException(nameof(isolationMode), isolationMode, "Unsupported isolation mode."),
    };

    private static ITenant? GetTenantOptional() => TenantAccessor.TryGetCurrentTenant().GetValueOrDefault();

    private static ITenant GetTenantRequired() =>
        TenantAccessor.TryGetCurrentTenant().GetValueOrThrow(
            failure => new ResilienceRuntimeException(failure.Message, failure));

    private static ISecurityPrincipal? GetPrincipalOptional() =>
        PrincipalAccessor.TryGetCurrentPrincipal().GetValueOrDefault();

    private static ISecurityPrincipal GetPrincipalRequired() =>
        PrincipalAccessor.TryGetCurrentPrincipal().GetValueOrThrow(
            failure => new ResilienceRuntimeException(failure.Message, failure));

    /// <inheritdoc/>
    public bool Equals(ResilienceIsolationKey? other) =>
        other is not null
        && string.Equals(Tenant?.TenantId, other.Tenant?.TenantId, StringComparison.Ordinal)
        && string.Equals(Principal?.PrincipalId, other.Principal?.PrincipalId, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ResilienceIsolationKey);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Tenant?.TenantId, Principal?.PrincipalId);

    /// <inheritdoc/>
    public override string ToString() =>
        $"ResilienceIsolationKey {{ TenantId = {Tenant?.TenantId ?? "(none)"}, PrincipalId = {Principal?.PrincipalId ?? "(none)"} }}";
}
