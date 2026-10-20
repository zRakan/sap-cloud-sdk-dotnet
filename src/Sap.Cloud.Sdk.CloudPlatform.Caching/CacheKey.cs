using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Cache;

/// <summary>
/// A cache key made of an isolation part (tenant and principal identifiers) and an ordered list of components.
/// Two keys are equal if and only if tenant ID, principal ID and all components (in order) are equal. Therefore a key
/// created for one tenant can never address a cache entry created for another tenant.
/// </summary>
/// <remarks>
/// Merges Java's <c>GenericCacheKey</c>, <c>CacheKey</c> and <c>SerializableCacheKey</c>: no key serialization is
/// needed in .NET, so there is a single key type. The key is mutable through <c>Append</c> (as in Java); do not
/// modify it after it has been used as a dictionary or cache key. <see cref="ToString"/> does not print the
/// components because they may hold confidential values.
/// </remarks>
public sealed class CacheKey : IEquatable<CacheKey>
{
    private readonly List<object> _components = [];

    private CacheKey(string? tenantId, string? principalId)
    {
        TenantId = tenantId;
        PrincipalId = principalId;
    }

    /// <summary>The tenant identifier, or <see langword="null"/> if the key is not isolated by tenant.</summary>
    public string? TenantId { get; }

    /// <summary>The principal identifier, or <see langword="null"/> if the key is not isolated by principal.</summary>
    public string? PrincipalId { get; }

    /// <summary>The components appended to this key, in order.</summary>
    public IReadOnlyList<object> Components => _components;

    /// <summary>Appends components to this key.</summary>
    /// <param name="objects">The components.</param>
    /// <returns>This key.</returns>
    /// <exception cref="ArgumentException">If a component is <see langword="null"/>.</exception>
    public CacheKey Append(IEnumerable<object> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        // Validate first so a failing call does not leave the key half modified.
        var toAdd = objects.ToList();
        if (toAdd.Any(o => o is null))
        {
            throw new ArgumentException("Object must not be null.", nameof(objects));
        }
        _components.AddRange(toAdd);
        return this;
    }

    /// <inheritdoc cref="Append(IEnumerable{object})"/>
    public CacheKey Append(params object[] objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        return Append((IEnumerable<object>)objects);
    }

    /// <summary>Creates a key isolated by the given tenant and principal.</summary>
    /// <param name="tenant">The tenant, or <see langword="null"/> for no tenant isolation.</param>
    /// <param name="principal">The principal, or <see langword="null"/> for no principal isolation.</param>
    /// <returns>The key.</returns>
    public static CacheKey Of(ITenant? tenant, ISecurityPrincipal? principal) =>
        FromIds(tenant?.TenantId, principal?.PrincipalId);

    /// <summary>Creates a key isolated by the given identifiers.</summary>
    /// <param name="tenantId">The tenant identifier, or <see langword="null"/>.</param>
    /// <param name="principalId">The principal identifier, or <see langword="null"/>.</param>
    /// <returns>The key.</returns>
    public static CacheKey FromIds(string? tenantId, string? principalId) => new(tenantId, principalId);

    /// <summary>Creates a key without any isolation.</summary>
    /// <returns>The key.</returns>
    public static CacheKey OfNoIsolation() => new(null, null);

    /// <summary>Creates a key isolated by the current tenant.</summary>
    /// <returns>The key.</returns>
    /// <exception cref="Tenant.Exceptions.TenantAccessException">If there is no current tenant.</exception>
    public static CacheKey OfTenantIsolation() => Of(TenantAccessor.GetCurrentTenant(), null);

    /// <summary>Creates a key isolated by the current tenant if there is one, otherwise not isolated.</summary>
    /// <returns>The key.</returns>
    public static CacheKey OfTenantOptionalIsolation() =>
        Of(TenantAccessor.TryGetCurrentTenant().GetValueOrDefault(), null);

    /// <summary>Creates a key isolated by the current tenant and the current principal.</summary>
    /// <returns>The key.</returns>
    /// <exception cref="Tenant.Exceptions.TenantAccessException">If there is no current tenant.</exception>
    /// <exception cref="Security.Principal.Exceptions.PrincipalAccessException">If there is no current principal.</exception>
    public static CacheKey OfTenantAndPrincipalIsolation() =>
        Of(TenantAccessor.GetCurrentTenant(), PrincipalAccessor.GetCurrentPrincipal());

    /// <summary>Creates a key isolated by the current tenant and principal, each only if available.</summary>
    /// <returns>The key.</returns>
    public static CacheKey OfTenantAndPrincipalOptionalIsolation() =>
        Of(TenantAccessor.TryGetCurrentTenant().GetValueOrDefault(),
            PrincipalAccessor.TryGetCurrentPrincipal().GetValueOrDefault());

    /// <inheritdoc/>
    public bool Equals(CacheKey? other) =>
        other is not null
        && string.Equals(TenantId, other.TenantId, StringComparison.Ordinal)
        && string.Equals(PrincipalId, other.PrincipalId, StringComparison.Ordinal)
        && _components.SequenceEqual(other._components);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CacheKey);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TenantId, StringComparer.Ordinal);
        hash.Add(PrincipalId, StringComparer.Ordinal);
        foreach (var component in _components)
        {
            hash.Add(component);
        }
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"CacheKey {{ TenantId = {TenantId ?? "(none)"}, PrincipalId = {PrincipalId ?? "(none)"}, Components = {_components.Count} }}";
}
