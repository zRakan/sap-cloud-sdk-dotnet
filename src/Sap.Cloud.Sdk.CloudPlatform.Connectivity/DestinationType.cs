using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The type of a destination.</summary>
public enum DestinationType
{
    /// <summary>An HTTP destination.</summary>
    Http,

    /// <summary>An RFC destination. Only recognized, not supported by this SDK.</summary>
    Rfc,

    /// <summary>A mail destination.</summary>
    Mail,

    /// <summary>An LDAP destination.</summary>
    Ldap,
}

/// <summary>Identifier conversions for <see cref="DestinationType"/>.</summary>
public static class DestinationTypeExtensions
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(DestinationTypeExtensions).FullName!);

    /// <summary>Gets the identifier used in destination properties: <c>HTTP</c>, <c>RFC</c>, <c>MAIL</c> or <c>LDAP</c>.</summary>
    /// <param name="destinationType">The destination type.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this DestinationType destinationType) => destinationType switch
    {
        DestinationType.Http => "HTTP",
        DestinationType.Rfc => "RFC",
        DestinationType.Mail => "MAIL",
        DestinationType.Ldap => "LDAP",
        _ => throw new ArgumentOutOfRangeException(nameof(destinationType)),
    };

    /// <summary>Parses an identifier (case-sensitive).</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The destination type.</returns>
    /// <exception cref="ArgumentException">The identifier is unknown.</exception>
    public static DestinationType OfIdentifier(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        foreach (var type in Enum.GetValues<DestinationType>())
        {
            if (string.Equals(type.GetIdentifier(), identifier, StringComparison.Ordinal))
            {
                return type;
            }
        }
        throw new ArgumentException($"Unknown {nameof(DestinationType)}: {identifier}.", nameof(identifier));
    }

    /// <summary>Parses an identifier, falling back to a default for <see langword="null"/> or unknown identifiers.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="defaultDestinationType">The default.</param>
    /// <returns>The destination type.</returns>
    public static DestinationType OfIdentifierOrDefault(string? identifier, DestinationType defaultDestinationType)
    {
        if (identifier is null)
        {
            return defaultDestinationType;
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
                    defaultDestinationType.GetIdentifier());
            }
            return defaultDestinationType;
        }
    }
}
