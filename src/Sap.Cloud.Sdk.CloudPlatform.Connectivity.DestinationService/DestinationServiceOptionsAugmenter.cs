using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Sets and reads the <see cref="DestinationOptions"/> parameters that are specific to the SAP BTP Cloud Foundry
/// destination service.
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>DestinationServiceOptionsAugmenter</c>. Usage:
/// <c>DestinationOptions.CreateBuilder().AugmentBuilder(DestinationServiceOptionsAugmenter.Augmenter().RetrievalStrategy(...)).Build()</c>.
/// </para>
/// <para>
/// The parameters are part of the cache key of the destination cache, so the refresh token and custom header values may
/// end up in memory as keys; they are never printed (<see cref="DestinationOptions.ToString"/> lists parameter names only).
/// </para>
/// </remarks>
public sealed class DestinationServiceOptionsAugmenter : IDestinationOptionsAugmenter
{
    internal const string DestinationRetrievalStrategyKey = "scp.cf.destinationRetrievalStrategy";
    internal const string DestinationTokenExchangeStrategyKey = "scp.cf.destinationTokenExchangeStrategy";
    internal const string XRefreshTokenKey = "x-refresh-token";
    internal const string XFragmentKey = "X-fragment-name";
    internal const string CrossLevelSettingKey = "crossLevelSetting";
    internal const string CustomHeaderKey = "customHeader.";

    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationServiceOptionsAugmenter>();

    private readonly Dictionary<string, object> _parameters = [];

    /// <summary>
    /// The scope in which a destination (or fragment) is looked up, for cross-level consumption of destinations.
    /// </summary>
    /// <remarks>Experimental; may not work for all destination types.</remarks>
    public enum CrossLevelScope
    {
        /// <summary>Use the destination from the current subaccount.</summary>
        Subaccount,

        /// <summary>
        /// Use the destination from the provider subaccount. Behaves identical to <see cref="Subaccount"/> if the current
        /// tenant is the provider tenant or the strategy <see cref="DestinationServiceRetrievalStrategy.AlwaysProvider"/> is used.
        /// </summary>
        ProviderSubaccount,

        /// <summary>Use the destination from the destination service instance for the current tenant.</summary>
        Instance,

        /// <summary>
        /// Use the destination from the destination service instance for the provider tenant. Behaves identical to
        /// <see cref="Instance"/> if the current tenant is the provider tenant or the strategy
        /// <see cref="DestinationServiceRetrievalStrategy.AlwaysProvider"/> is used.
        /// </summary>
        ProviderInstance,
    }

    /// <summary>Creates an augmenter. Equivalent to <see cref="Augmenter"/>.</summary>
    public DestinationServiceOptionsAugmenter()
    {
    }

    /// <summary>Creates augmenters in a builder-like style.</summary>
    /// <returns>A new augmenter.</returns>
    public static DestinationServiceOptionsAugmenter Augmenter() => new();

    /// <summary>
    /// Sets the strategy to use when loading destinations in a multi-tenant subscription environment. Not setting a
    /// strategy results in the platform default, <see cref="DestinationServiceRetrievalStrategy.CurrentTenant"/>.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>This augmenter.</returns>
    public DestinationServiceOptionsAugmenter RetrievalStrategy(DestinationServiceRetrievalStrategy strategy)
    {
        _parameters[DestinationRetrievalStrategyKey] = strategy;
        return this;
    }

    /// <summary>Sets the <see cref="DestinationServiceTokenExchangeStrategy"/> to use when loading destinations.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>This augmenter.</returns>
    public DestinationServiceOptionsAugmenter TokenExchangeStrategy(DestinationServiceTokenExchangeStrategy strategy)
    {
        _parameters[DestinationTokenExchangeStrategyKey] = strategy;
        return this;
    }

    /// <summary>Sets the refresh token to be sent for destinations of type <see cref="AuthenticationType.OAuth2RefreshToken"/>.</summary>
    /// <param name="refreshToken">The refresh token as encoded string. Confidential.</param>
    /// <returns>This augmenter.</returns>
    public DestinationServiceOptionsAugmenter RefreshToken(string refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _parameters[XRefreshTokenKey] = refreshToken;
        return this;
    }

    /// <summary>Sets the fragment that enhances the destination to be fetched.</summary>
    /// <param name="fragmentName">The fragment name.</param>
    /// <returns>This augmenter.</returns>
    public DestinationServiceOptionsAugmenter FragmentName(string fragmentName)
    {
        ArgumentNullException.ThrowIfNull(fragmentName);
        _parameters[XFragmentKey] = fragmentName;
        if (DestinationService.Cache.IsEnabled && DestinationService.Cache.IsChangeDetectionEnabled)
        {
            Log.LogWarning(
                "A fragment was requested while change detection caching is enabled. "
                + "This is not recommended, as fragment-based destinations will effectively not be cached with this strategy. "
                + "Consider disabling change detection, if you frequently use destination fragments.");
        }
        return this;
    }

    /// <summary>
    /// Enables cross-level consumption of destinations. Experimental: may not work for all destination types.
    /// </summary>
    /// <param name="scope">
    /// The scope in which to look up the destination. It applies to the destination only; the scopes of fragments or of
    /// destination chain references have to be set separately.
    /// </param>
    /// <returns>This augmenter.</returns>
    public DestinationServiceOptionsAugmenter CrossLevelConsumption(CrossLevelScope scope)
    {
        _parameters[CrossLevelSettingKey] = scope;
        return this;
    }

    /// <summary>
    /// Adds custom headers to the destination request, for parameters that the SDK does not support directly, for
    /// example destination chaining. Values of secrets are not masked in log output on debug level.
    /// </summary>
    /// <param name="headers">The headers to add.</param>
    /// <returns>This augmenter.</returns>
    public DestinationServiceOptionsAugmenter CustomHeaders(params Header[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        foreach (var header in headers)
        {
            _parameters[CustomHeaderKey + header.Name] = header.Value ?? string.Empty;
        }
        return this;
    }

    /// <inheritdoc/>
    public void AugmentBuilder(DestinationOptions.Builder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (key, value) in _parameters)
        {
            builder.Parameter(key, value);
        }
    }

    /// <summary>Reads the configured retrieval strategy.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The strategy, or <see langword="null"/> if none is set (or a text value is unknown).</returns>
    public static DestinationServiceRetrievalStrategy? GetRetrievalStrategy(DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        switch (options.Get(DestinationRetrievalStrategyKey))
        {
            case DestinationServiceRetrievalStrategy strategy:
                return strategy;
            case string text:
                var parsed = DestinationServiceRetrievalStrategyExtensions.OfIdentifier(text);
                if (parsed is null)
                {
                    Log.LogWarning("Unsupported destination retrieval strategy: {Strategy}", text);
                }
                return parsed;
            case null:
                return null;
            case var other:
                throw new InvalidCastException(
                    $"The option {DestinationRetrievalStrategyKey} must be a {nameof(DestinationServiceRetrievalStrategy)}, but is a {other.GetType().Name}.");
        }
    }

    /// <summary>Reads the configured token exchange strategy.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The strategy, or <see langword="null"/> if none is set (or a text value is unknown).</returns>
    public static DestinationServiceTokenExchangeStrategy? GetTokenExchangeStrategy(DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        switch (options.Get(DestinationTokenExchangeStrategyKey))
        {
            case DestinationServiceTokenExchangeStrategy strategy:
                return strategy;
            case string text:
                var parsed = DestinationServiceTokenExchangeStrategyExtensions.OfIdentifier(text);
                if (parsed is null)
                {
                    Log.LogWarning("Unsupported token exchange strategy: {Strategy}", text);
                }
                return parsed;
            case null:
                return null;
            case var other:
                throw new InvalidCastException(
                    $"The option {DestinationTokenExchangeStrategyKey} must be a {nameof(DestinationServiceTokenExchangeStrategy)}, but is a {other.GetType().Name}.");
        }
    }

    internal static string? GetRefreshToken(DestinationOptions options) => options.Get(XRefreshTokenKey) as string;

    internal static string? GetFragmentName(DestinationOptions options) => options.Get(XFragmentKey) as string;

    internal static CrossLevelScope? GetCrossLevelScope(DestinationOptions options) =>
        options.Get(CrossLevelSettingKey) is CrossLevelScope scope ? scope : null;

    internal static List<Header> GetAdditionalHeaders(DestinationOptions options) =>
        [.. options.OptionKeys
            .Where(key => key.StartsWith(CustomHeaderKey, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(key => new Header(key[CustomHeaderKey.Length..], options.Get(key) as string ?? string.Empty))];

    /// <summary>The component that represents the (optional) retrieval strategy in cache keys.</summary>
    internal static object RetrievalStrategyCacheComponent(DestinationOptions options) =>
        (object?)GetRetrievalStrategy(options) ?? NoRetrievalStrategy;

    private static readonly object NoRetrievalStrategy = "(no retrieval strategy)";
}

/// <summary>Helpers for <see cref="DestinationServiceOptionsAugmenter.CrossLevelScope"/>.</summary>
public static class CrossLevelScopeExtensions
{
    /// <summary>Gets the identifier of the scope as used by the destination service, for example <c>provider_subaccount</c>.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this DestinationServiceOptionsAugmenter.CrossLevelScope scope) => scope switch
    {
        DestinationServiceOptionsAugmenter.CrossLevelScope.Subaccount => "subaccount",
        DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderSubaccount => "provider_subaccount",
        DestinationServiceOptionsAugmenter.CrossLevelScope.Instance => "instance",
        DestinationServiceOptionsAugmenter.CrossLevelScope.ProviderInstance => "provider_instance",
        _ => throw new ArgumentOutOfRangeException(nameof(scope)),
    };

    /// <summary>Gets the suffix to be appended to a destination or fragment name, including the leading <c>@</c>.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The suffix.</returns>
    public static string GetSuffix(this DestinationServiceOptionsAugmenter.CrossLevelScope scope) =>
        "@" + scope.GetIdentifier();
}
