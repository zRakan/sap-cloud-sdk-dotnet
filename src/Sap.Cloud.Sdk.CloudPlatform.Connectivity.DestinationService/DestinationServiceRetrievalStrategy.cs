namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// The strategies for loading destinations in a multi-tenant application on SAP BTP Cloud Foundry. Destinations can be
/// defined on the tenant hosting the application (provider) or on the tenants that subscribed to it (subscribers). Not
/// setting a strategy results in the platform default, <see cref="CurrentTenant"/>.
/// </summary>
public enum DestinationServiceRetrievalStrategy
{
    /// <summary>
    /// Only load the destination from the provider's subaccount, regardless of whether subscribers have a destination of the
    /// same name. There is no fallback if the destination is not found there.
    /// </summary>
    AlwaysProvider,

    /// <summary>
    /// Only load the destination from the current tenant's subaccount, regardless of whether the provider has a destination
    /// of the same name. There is no fallback. If the current tenant is the provider, the provider's subaccount is used.
    /// </summary>
    CurrentTenant,

    /// <summary>
    /// Only load the destination from the subscriber's subaccount, checking that the current tenant is an actual
    /// subscriber and not the provider. There is no fallback.
    /// </summary>
    OnlySubscriber,
}

/// <summary>Identifier conversion for <see cref="DestinationServiceRetrievalStrategy"/>.</summary>
public static class DestinationServiceRetrievalStrategyExtensions
{
    /// <summary>Gets the string representation used by the Java SDK, for example <c>AlwaysProvider</c>.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this DestinationServiceRetrievalStrategy strategy) => strategy switch
    {
        DestinationServiceRetrievalStrategy.AlwaysProvider => "AlwaysProvider",
        DestinationServiceRetrievalStrategy.CurrentTenant => "CurrentTenant",
        DestinationServiceRetrievalStrategy.OnlySubscriber => "OnlySubscriber",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    /// <summary>Gets a strategy by its identifier, ignoring case.</summary>
    /// <param name="identifier">The string representation of a strategy.</param>
    /// <returns>The strategy, or <see langword="null"/> if the identifier is unknown.</returns>
    public static DestinationServiceRetrievalStrategy? OfIdentifier(string? identifier)
    {
        foreach (var strategy in Enum.GetValues<DestinationServiceRetrievalStrategy>())
        {
            if (string.Equals(strategy.GetIdentifier(), identifier, StringComparison.OrdinalIgnoreCase))
            {
                return strategy;
            }
        }
        return null;
    }
}
