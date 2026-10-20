using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Tenant;

/// <summary>Reads the provider tenant from the credentials of an XSUAA or IAS service binding.</summary>
internal sealed class ServiceBindingTenantExtractor
{
    public static readonly ServiceBindingTenantExtractor Xsuaa = new(
        "xsuaa",
        tenantIdKeys: ["identityzoneid", "zoneid", "tenantid"],
        subdomain: credentials => GetString(credentials, "identityzone"));

    public static readonly ServiceBindingTenantExtractor Ias = new(
        "identity",
        tenantIdKeys: ["app_tid", "zone_uuid"],
        subdomain: credentials => GetString(credentials, "url") is { } url ? SubdomainOf(url) : null);

    /// <summary>The extractors, in the order in which they are tried.</summary>
    public static readonly IReadOnlyList<ServiceBindingTenantExtractor> All = [Xsuaa, Ias];

    private readonly IReadOnlyList<string> _tenantIdKeys;
    private readonly Func<IReadOnlyDictionary<string, object?>, string?> _subdomain;

    private ServiceBindingTenantExtractor(
        string service,
        IReadOnlyList<string> tenantIdKeys,
        Func<IReadOnlyDictionary<string, object?>, string?> subdomain)
    {
        Service = service;
        _tenantIdKeys = tenantIdKeys;
        _subdomain = subdomain;
    }

    /// <summary>The service name of the bindings this extractor reads.</summary>
    public string Service { get; }

    /// <summary>Extracts the tenant, or returns <see langword="null"/> if tenant ID or subdomain are missing.</summary>
    public DefaultTenant? Extract(IReadOnlyDictionary<string, object?> credentials)
    {
        var tenantId = _tenantIdKeys.Select(key => GetString(credentials, key)).FirstOrDefault(v => v is not null);
        var subdomain = _subdomain(credentials);
        return tenantId is not null && subdomain is not null ? new DefaultTenant(tenantId, subdomain) : null;
    }

    public override string ToString() => $"ServiceBindingTenantExtractor({Service})";

    private static string? GetString(IReadOnlyDictionary<string, object?> credentials, string key) =>
        credentials.GetValueOrDefault(key) as string;

    // Matches the Java SDK: an unparsable URL or a host without a dot yields an empty subdomain.
    private static string SubdomainOf(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "";
        }
        var dot = uri.Host.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? "" : uri.Host[..dot];
    }
}
