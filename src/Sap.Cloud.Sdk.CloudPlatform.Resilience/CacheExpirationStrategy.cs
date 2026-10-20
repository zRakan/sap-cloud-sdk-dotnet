namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>Defines when the expiration timer of a cached value starts.</summary>
/// <remarks>
/// Cached values are only ever created (never updated in place), so <see cref="WhenCreated"/> and
/// <see cref="WhenLastModified"/> behave identically (absolute expiration), as do <see cref="WhenLastAccessed"/> and
/// <see cref="WhenLastTouched"/> (sliding expiration).
/// </remarks>
public enum CacheExpirationStrategy
{
    /// <summary>The entry expires a fixed duration after it was last written.</summary>
    WhenLastModified,

    /// <summary>The entry expires a fixed duration after it was created.</summary>
    WhenCreated,

    /// <summary>The entry expires a fixed duration after it was last read.</summary>
    WhenLastAccessed,

    /// <summary>The entry expires a fixed duration after it was last read or written.</summary>
    WhenLastTouched,
}
