namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// The strategies for performing a user token exchange, if necessary, when retrieving a destination from the
/// destination service on SAP BTP Cloud Foundry.
/// </summary>
/// <remarks>
/// <see cref="LookupThenExchange"/> and <see cref="LookupOnly"/> are deprecated in Java (since 5.0.1). They are not
/// marked <see cref="ObsoleteAttribute"/> here because the SDK itself and many applications still use them.
/// </remarks>
public enum DestinationServiceTokenExchangeStrategy
{
    /// <summary>
    /// Forward the access token of the current user to the destination lookup. The destination service performs token
    /// exchanges automatically, so only one outbound request per destination lookup is needed. This is the default if
    /// the current token originates from XSUAA.
    /// </summary>
    ForwardUserToken,

    /// <summary>
    /// Legacy strategy: first look up the destination with a client credentials request; if its authentication type
    /// needs a user token exchange, perform a second request on behalf of the named user. Deprecated since 5.0.1 in
    /// Java; use <see cref="ForwardUserToken"/>.
    /// </summary>
    LookupThenExchange,

    /// <summary>
    /// Legacy strategy to explicitly not perform a user token exchange. The destination may be unusable for
    /// authentication types that need one. Deprecated since 5.0.1 in Java; use <see cref="ForwardUserToken"/> or
    /// <see cref="DestinationService.GetDestinationPropertiesAsync"/>.
    /// </summary>
    LookupOnly,

    /// <summary>
    /// Legacy strategy for destinations that need a user token for the authentication flow. It is slower and subject to
    /// stricter rate limits than <see cref="ForwardUserToken"/>.
    /// </summary>
    ExchangeOnly,
}

/// <summary>Identifier conversion for <see cref="DestinationServiceTokenExchangeStrategy"/>.</summary>
public static class DestinationServiceTokenExchangeStrategyExtensions
{
    /// <summary>Gets the string representation used by the Java SDK, for example <c>ForwardUserToken</c>.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this DestinationServiceTokenExchangeStrategy strategy) => strategy switch
    {
        DestinationServiceTokenExchangeStrategy.ForwardUserToken => "ForwardUserToken",
        DestinationServiceTokenExchangeStrategy.LookupThenExchange => "LookupThenExchange",
        DestinationServiceTokenExchangeStrategy.LookupOnly => "LookupOnly",
        DestinationServiceTokenExchangeStrategy.ExchangeOnly => "ExchangeOnly",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    /// <summary>Gets a strategy by its identifier, ignoring case.</summary>
    /// <param name="identifier">The string representation of a strategy.</param>
    /// <returns>The strategy, or <see langword="null"/> if the identifier is unknown.</returns>
    public static DestinationServiceTokenExchangeStrategy? OfIdentifier(string? identifier)
    {
        foreach (var strategy in Enum.GetValues<DestinationServiceTokenExchangeStrategy>())
        {
            if (string.Equals(strategy.GetIdentifier(), identifier, StringComparison.OrdinalIgnoreCase))
            {
                return strategy;
            }
        }
        return null;
    }
}
