using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The type of proxy used to reach a destination.</summary>
public enum ProxyType
{
    /// <summary>Direct access over the internet.</summary>
    Internet,

    /// <summary>Access to an on-premise system through the Cloud Connector.</summary>
    OnPremise,

    /// <summary>Access through a private link.</summary>
    PrivateLink,
}

/// <summary>Identifier conversions for <see cref="ProxyType"/>.</summary>
public static class ProxyTypeExtensions
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(ProxyTypeExtensions).FullName!);

    /// <summary>Gets the identifier used in destination properties: <c>Internet</c>, <c>OnPremise</c> or <c>PrivateLink</c>.</summary>
    /// <param name="proxyType">The proxy type.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this ProxyType proxyType) => proxyType switch
    {
        ProxyType.Internet => "Internet",
        ProxyType.OnPremise => "OnPremise",
        ProxyType.PrivateLink => "PrivateLink",
        _ => throw new ArgumentOutOfRangeException(nameof(proxyType)),
    };

    /// <summary>Parses an identifier (case-sensitive).</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The proxy type.</returns>
    /// <exception cref="ArgumentException">The identifier is unknown.</exception>
    public static ProxyType OfIdentifier(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return ByIdentifier(identifier)
            ?? throw new ArgumentException($"Unknown {nameof(ProxyType)}: {identifier}.", nameof(identifier));
    }

    /// <summary>Parses an identifier, falling back to a default for <see langword="null"/> or unknown identifiers.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="defaultProxyType">The default.</param>
    /// <returns>The proxy type.</returns>
    public static ProxyType OfIdentifierOrDefault(string? identifier, ProxyType defaultProxyType)
    {
        if (identifier is null)
        {
            return defaultProxyType;
        }
        try
        {
            return OfIdentifier(identifier);
        }
        catch (ArgumentException)
        {
            if (Log.IsEnabled(LogLevel.Warning))
            {
                Log.LogWarning(
                    "Identifier '{Identifier}' is not supported. Falling back to {Default}.",
                    identifier,
                    defaultProxyType.GetIdentifier());
            }
            return defaultProxyType;
        }
    }

    /// <summary>
    /// Parses an identifier, additionally accepting the spellings <c>onpremise</c>, <c>on-premise</c> and
    /// <c>on_premise</c> (case-insensitive) for <see cref="ProxyType.OnPremise"/>.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The proxy type.</returns>
    /// <exception cref="ArgumentException">The identifier is unknown.</exception>
    public static ProxyType OfIdentifierSensitive(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var proxyType = ByIdentifier(identifier);
        if (proxyType is not null)
        {
            return proxyType.Value;
        }
        if (identifier.Equals("onpremise", StringComparison.OrdinalIgnoreCase)
            || identifier.Equals("on-premise", StringComparison.OrdinalIgnoreCase)
            || identifier.Equals("on_premise", StringComparison.OrdinalIgnoreCase))
        {
            return ProxyType.OnPremise;
        }
        throw new ArgumentException($"Unknown {nameof(ProxyType)}: {identifier}.", nameof(identifier));
    }

    private static ProxyType? ByIdentifier(string identifier)
    {
        foreach (var type in Enum.GetValues<ProxyType>())
        {
            if (string.Equals(type.GetIdentifier(), identifier, StringComparison.Ordinal))
            {
                return type;
            }
        }
        return null;
    }
}
