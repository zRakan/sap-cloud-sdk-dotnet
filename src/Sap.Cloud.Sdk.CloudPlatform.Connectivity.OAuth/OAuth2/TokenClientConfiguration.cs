using System.Net;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;

/// <summary>
/// Process-wide retry settings for token requests.
/// </summary>
/// <remarks>
/// Port of <c>com.sap.cloud.security.client.DefaultTokenClientConfiguration</c>. Retries are disabled by default.
/// </remarks>
public sealed class TokenClientConfiguration
{
    private static TokenClientConfiguration _instance = new();

    /// <summary>The current configuration. Setting <see langword="null"/> restores the defaults.</summary>
    public static TokenClientConfiguration Instance
    {
        get => _instance;
        set => _instance = value ?? new TokenClientConfiguration();
    }

    /// <summary>Whether failed token requests with a status in <see cref="RetryStatusCodes"/> are retried.</summary>
    public bool IsRetryEnabled { get; init; }

    /// <summary>The maximum number of retries.</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>The delay between attempts.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The status codes that trigger a retry.</summary>
    public IReadOnlySet<HttpStatusCode> RetryStatusCodes { get; init; } = new HashSet<HttpStatusCode>
    {
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    };
}
