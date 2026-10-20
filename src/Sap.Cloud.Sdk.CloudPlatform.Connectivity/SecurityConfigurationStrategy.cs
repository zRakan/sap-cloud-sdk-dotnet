namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Where the TLS security configuration (trust and key stores) of a destination comes from.</summary>
public enum SecurityConfigurationStrategy
{
    /// <summary>The configuration of the destination is used.</summary>
    FromDestination,

    /// <summary>The configuration of the platform is used.</summary>
    FromPlatform,
}

/// <summary>Identifier conversions for <see cref="SecurityConfigurationStrategy"/>.</summary>
public static class SecurityConfigurationStrategyExtensions
{
    /// <summary>The default strategy, <see cref="SecurityConfigurationStrategy.FromDestination"/>.</summary>
    public const SecurityConfigurationStrategy Default = SecurityConfigurationStrategy.FromDestination;

    /// <summary>Gets the identifier: <c>from_destination</c> or <c>from_platform</c>.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this SecurityConfigurationStrategy strategy) => strategy switch
    {
        SecurityConfigurationStrategy.FromDestination => "from_destination",
        SecurityConfigurationStrategy.FromPlatform => "from_platform",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    /// <summary>Parses an identifier; <see langword="null"/> yields the default.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The strategy.</returns>
    /// <exception cref="ArgumentException">The identifier is unknown.</exception>
    public static SecurityConfigurationStrategy OfIdentifierOrDefault(string? identifier)
    {
        if (identifier is null)
        {
            return Default;
        }
        foreach (var strategy in Enum.GetValues<SecurityConfigurationStrategy>())
        {
            if (string.Equals(strategy.GetIdentifier(), identifier, StringComparison.Ordinal))
            {
                return strategy;
            }
        }
        throw new ArgumentException(
            $"No {nameof(SecurityConfigurationStrategy)} found for identifier: {identifier}",
            nameof(identifier));
    }
}
