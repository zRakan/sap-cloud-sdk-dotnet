using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;

/// <summary>
/// Requests access tokens from an OAuth 2.0 token endpoint (XSUAA or IAS) and caches them.
/// </summary>
/// <remarks>
/// Port of <c>AbstractOAuth2TokenService</c> (SAP token-client) and the SDK's <c>HttpClient5OAuth2TokenService</c>.
/// Token responses are cached per endpoint, headers and parameters. A cached token is renewed once its remaining
/// lifetime drops below <see cref="TokenCacheParameters.TokenExpirationDelta"/>.
/// </remarks>
internal sealed class OAuth2TokenService : IDisposable
{
    internal const string GrantTypeClientCredentials = "client_credentials";
    internal const string GrantTypeJwtBearer = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    internal const string ZoneIdHeader = "X-zid";
    internal const string CorrelationIdHeader = "X-CorrelationID";

    private static readonly ILogger Log = SdkLogging.CreateLogger<OAuth2TokenService>();
    private static readonly string[] SecretParameters = ["password", "client_secret", "assertion"];
    private static readonly ProductInfoHeaderValue UserAgent = new("sap-cloud-sdk-dotnet", "1.0");

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly TokenCacheParameters _cacheParameters;
    private readonly TimeProvider _timeProvider;
    private readonly MemoryCache? _cache;

    public OAuth2TokenService(
        HttpClient httpClient,
        TokenCacheParameters cacheParameters,
        TimeProvider? timeProvider = null,
        bool ownsHttpClient = false)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _cacheParameters = cacheParameters;
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (!cacheParameters.IsCacheDisabled)
        {
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = cacheParameters.CacheSize, Clock = new TimeProviderClock(_timeProvider) });
        }
    }

    /// <summary>Client credentials grant. Tenant via <c>X-zid</c> header (XSUAA) or subdomain (IAS).</summary>
    public Task<OAuth2TokenResponse> RetrieveAccessTokenViaClientCredentialsGrantAsync(
        Uri tokenEndpoint,
        OAuth2ClientIdentity identity,
        string? zoneId,
        string? subdomain,
        IReadOnlyDictionary<string, string>? optionalParameters,
        bool disableCacheForRequest,
        CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(GrantTypeClientCredentials, identity, null, optionalParameters);
        var headers = zoneId is null ? [] : new SortedDictionary<string, string>(StringComparer.Ordinal) { [ZoneIdHeader] = zoneId };
        return GetTokenResponseAsync(ReplaceSubdomain(tokenEndpoint, subdomain), headers, parameters, disableCacheForRequest, cancellationToken);
    }

    /// <summary>JWT bearer grant (user token exchange), tenant via subdomain.</summary>
    public Task<OAuth2TokenResponse> RetrieveAccessTokenViaJwtBearerGrantAsync(
        Uri tokenEndpoint,
        OAuth2ClientIdentity identity,
        string token,
        string? subdomain,
        IReadOnlyDictionary<string, string>? optionalParameters,
        bool disableCacheForRequest,
        CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(GrantTypeJwtBearer, identity, token, optionalParameters);
        return GetTokenResponseAsync(ReplaceSubdomain(tokenEndpoint, subdomain), [], parameters, disableCacheForRequest, cancellationToken);
    }

    /// <summary>JWT bearer grant (user token exchange), tenant via <c>X-zid</c> header. No subdomain replacement.</summary>
    public Task<OAuth2TokenResponse> RetrieveAccessTokenViaJwtBearerGrantWithZoneIdAsync(
        Uri tokenEndpoint,
        OAuth2ClientIdentity identity,
        string token,
        IReadOnlyDictionary<string, string>? optionalParameters,
        bool disableCacheForRequest,
        string zoneId,
        CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(GrantTypeJwtBearer, identity, token, optionalParameters);
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal) { [ZoneIdHeader] = zoneId };
        return GetTokenResponseAsync(tokenEndpoint, headers, parameters, disableCacheForRequest, cancellationToken);
    }

    public void ClearCache() => _cache?.Clear();

    public void Dispose()
    {
        _cache?.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>Replaces the first label of the host with <paramref name="subdomain"/>, if both are present.</summary>
    internal static Uri ReplaceSubdomain(Uri uri, string? subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain) || !uri.IsAbsoluteUri || !uri.Host.Contains('.', StringComparison.Ordinal))
        {
            return uri;
        }
        var builder = new UriBuilder(uri) { Host = subdomain + uri.Host[uri.Host.IndexOf('.', StringComparison.Ordinal)..] };
        return builder.Uri;
    }

    private static SortedDictionary<string, string> BuildParameters(
        string grantType,
        OAuth2ClientIdentity identity,
        string? assertion,
        IReadOnlyDictionary<string, string>? optionalParameters)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = grantType,
            ["client_id"] = identity.Id,
        };
        if (!identity.IsCertificateBased)
        {
            parameters["client_secret"] = identity.Secret ?? "";
        }
        if (assertion is not null)
        {
            parameters["assertion"] = assertion;
        }
        // Optional parameters never override the ones above (token-client: putIfAbsent).
        foreach (var (key, value) in optionalParameters ?? new Dictionary<string, string>())
        {
            parameters.TryAdd(key, value);
        }
        return parameters;
    }

    private async Task<OAuth2TokenResponse> GetTokenResponseAsync(
        Uri tokenEndpoint,
        SortedDictionary<string, string> headers,
        SortedDictionary<string, string> parameters,
        bool disableCacheForRequest,
        CancellationToken cancellationToken)
    {
        if (_cache is null || disableCacheForRequest)
        {
            return await RequestAccessTokenAsync(tokenEndpoint, headers, parameters, cancellationToken).ConfigureAwait(false);
        }

        var key = CacheKey(tokenEndpoint, headers, parameters);
        if (_cache.TryGetValue(key, out OAuth2TokenResponse? cached)
            && cached is not null
            && cached.ExpiresAt - _cacheParameters.TokenExpirationDelta > _timeProvider.GetUtcNow())
        {
            return cached;
        }

        var response = await RequestAccessTokenAsync(tokenEndpoint, headers, parameters, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, response, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = _cacheParameters.CacheDuration,
        });
        return response;
    }

    private static string CacheKey(Uri tokenEndpoint, SortedDictionary<string, string> headers, SortedDictionary<string, string> parameters) =>
        JsonSerializer.Serialize(new object[] { tokenEndpoint.AbsoluteUri, headers, parameters });

    private async Task<OAuth2TokenResponse> RequestAccessTokenAsync(
        Uri tokenEndpoint,
        SortedDictionary<string, string> headers,
        SortedDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Requesting access token from {TokenEndpoint} with headers {Headers} and parameters {Parameters}.",
                tokenEndpoint,
                string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")),
                string.Join(", ", parameters.Select(p => $"{p.Key}={(SecretParameters.Any(s => p.Key.Contains(s, StringComparison.Ordinal)) ? "****" : p.Value)}")));
        }

        var configuration = TokenClientConfiguration.Instance;
        var attemptsLeft = configuration.IsRetryEnabled ? configuration.MaxRetryAttempts : 0;
        while (true)
        {
            using var request = CreateRequest(tokenEndpoint, headers, parameters);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                throw new OAuth2ServiceException("Error requesting access token!", e) { TokenEndpoint = tokenEndpoint, ResponseBody = e.Message };
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return ParseResponse(body, tokenEndpoint);
                }
                if (attemptsLeft > 0 && configuration.RetryStatusCodes.Contains(response.StatusCode))
                {
                    Log.LogWarning("Token request failed with status {StatusCode} but is retryable. Retrying...", (int)response.StatusCode);
                    attemptsLeft--;
                    await Task.Delay(configuration.RetryDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                throw new OAuth2ServiceException("Error requesting access token!")
                {
                    StatusCode = response.StatusCode,
                    TokenEndpoint = tokenEndpoint,
                    ResponseBody = body,
                };
            }
        }
    }

    private static HttpRequestMessage CreateRequest(
        Uri tokenEndpoint,
        SortedDictionary<string, string> headers,
        SortedDictionary<string, string> parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.TryAddWithoutValidation(
            CorrelationIdHeader,
            Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString());
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
        return request;
    }

    private OAuth2TokenResponse ParseResponse(string body, Uri tokenEndpoint)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e : default;
            long seconds = expiresIn.ValueKind switch
            {
                JsonValueKind.Number => expiresIn.GetInt64(),
                JsonValueKind.String when long.TryParse(expiresIn.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
                _ => throw new OAuth2ServiceException($"Cannot convert expires_in from response ({expiresIn}) to long")
                {
                    TokenEndpoint = tokenEndpoint,
                },
            };
            return new OAuth2TokenResponse(
                GetString(root, "access_token"),
                _timeProvider.GetUtcNow().AddSeconds(seconds),
                GetString(root, "refresh_token"),
                GetString(root, "token_type"));
        }
        catch (JsonException ex)
        {
            throw new OAuth2ServiceException("Token endpoint returned an invalid JSON response.", ex) { TokenEndpoint = tokenEndpoint };
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed class TimeProviderClock(TimeProvider timeProvider) : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
