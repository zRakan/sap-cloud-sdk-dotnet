namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;

/// <summary>Configures the cache for OAuth 2.0 access tokens.</summary>
/// <param name="CacheDuration">How long a token response is kept at most.</param>
/// <param name="CacheSize">The maximum number of cached token responses.</param>
/// <param name="TokenExpirationDelta">
/// A cached token is renewed once its remaining lifetime is shorter than this value.
/// </param>
public sealed record TokenCacheParameters(TimeSpan CacheDuration, int CacheSize, TimeSpan TokenExpirationDelta)
    : IOptionsEnhancer<TokenCacheParameters>
{
    /// <summary>This instance; lets the parameters be passed as a service binding destination option.</summary>
    public TokenCacheParameters Value => this;

    /// <summary>The SDK defaults: one hour, 1000 entries, 30 seconds.</summary>
    public static TokenCacheParameters Default { get; } = new(TimeSpan.FromHours(1), 1000, TimeSpan.FromSeconds(30));

    /// <summary>Disables caching.</summary>
    public static TokenCacheParameters Disabled { get; } = new(TimeSpan.Zero, 0, TimeSpan.Zero);

    /// <summary>Whether caching is disabled.</summary>
    public bool IsCacheDisabled => CacheSize <= 0 || CacheDuration <= TimeSpan.Zero;
}
