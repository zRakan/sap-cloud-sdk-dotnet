namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Settings of the <see cref="DefaultDestinationHttpClientFactory"/>.</summary>
/// <remarks>
/// Port of <c>ApacheHttpClient5FactoryBuilder</c> and <c>ApacheHttpClient5CacheBuilder</c>. Not ported:
/// <c>maxConnectionsTotal</c> (<see cref="SocketsHttpHandler"/> only limits connections per server) and <c>tlsUpgrade</c>
/// (the HTTP <c>Upgrade</c> header is not sent by .NET).
/// </remarks>
public sealed class DestinationHttpClientFactoryOptions
{
    /// <summary>The default timeout, two minutes.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The default maximum number of connections per route, 100.</summary>
    public const int DefaultMaxConnectionsPerRoute = 100;

    /// <summary>The default duration for which connection pools are cached, one hour.</summary>
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// The timeout of connecting, and of a complete request (<see cref="HttpClient.Timeout"/>). Java applies it to the
    /// connection, the socket and the connection request. Defaults to <see cref="DefaultTimeout"/>.
    /// </summary>
    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>The maximum number of parallel connections per route. Defaults to <see cref="DefaultMaxConnectionsPerRoute"/>.</summary>
    public int MaxConnectionsPerRoute { get; init; } = DefaultMaxConnectionsPerRoute;

    /// <summary>
    /// How long a connection is reused before it is closed, so that DNS changes are picked up. Defaults to five minutes.
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long an unused connection pool stays cached (sliding expiration). <see cref="TimeSpan.Zero"/> disables the
    /// cache (Java: <c>ApacheHttpClient5Cache.DISABLED</c>; testing only: every client gets its own connection pool,
    /// which the client disposes). Defaults to <see cref="DefaultCacheDuration"/>.
    /// </summary>
    public TimeSpan CacheDuration { get; init; } = DefaultCacheDuration;

    /// <summary>Whether <see cref="CsrfTokenHandler"/> is part of the pipeline, as in Java. Defaults to <see langword="true"/>.</summary>
    public bool EnableCsrfTokenHandling { get; init; } = true;
}
