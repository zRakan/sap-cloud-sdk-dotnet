using System.Globalization;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;
using Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// A destination loader that routes all destination lookups through a single registered transparent proxy (destination
/// gateway).
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>TransparentProxy</c>. <see cref="RegisterAsync(string, int, string?, CancellationToken)"/> validates the host,
/// checks that it accepts connections and prepends a <see cref="TransparentProxy"/> to the chain of
/// <see cref="DestinationAccessor"/>, so it answers all lookups. A lookup creates a
/// <see cref="TransparentProxyDestination"/> for the destination name and verifies it with a <c>HEAD</c> request to the
/// gateway: <c>502</c> with <c>x-error-internal-code: 404</c> means the destination does not exist
/// (<see cref="DestinationNotFoundException"/>); any other gateway error, signalled by the <c>x-error-origin</c> header, is a
/// <see cref="DestinationAccessException"/>.
/// </para>
/// <para>
/// The gateway always needs an explicit tenant: if there is no current tenant and no provider tenant id was registered,
/// lookups throw a <see cref="TenantAccessException"/> (they do not return a failure, as in Java).
/// </para>
/// <para>
/// Differences from Java: registering performs I/O (a connection test), so only the asynchronous
/// <c>RegisterAsync</c> exists; only one proxy can be registered per process, as in Java. The state is static and
/// registration should happen during application startup.
/// </para>
/// </remarks>
public sealed class TransparentProxy : IDestinationLoader
{
    private const string XErrorInternalCodeHeader = "x-error-internal-code";
    private const string XErrorOriginHeader = "x-error-origin";
    private const string XErrorMessageHeader = "x-error-message";
    private const string SetCookieHeader = "Set-Cookie";
    private const int DefaultPort = 80;
    private const string SchemeSeparator = "://";
    private const string FailedToVerifyDestination = "Failed to verify destination. ";
    private const string NoTenantProvidedErrorMessage =
        "No current tenant defined and no provider tenant id configured. Transparent proxy always requires an explicit tenant ID. "
        + "Please use RegisterAsync(host, providerTenantId) in case provider tenant access is intended";

    private static readonly ILogger Log = SdkLogging.CreateLogger<TransparentProxy>();
    private static readonly Lock Gate = new();

    private static readonly ResilienceConfiguration ResilienceConfig =
        ResilienceConfiguration.Of(typeof(TransparentProxy).FullName + "destinationverifier") with
        {
            TimeLimiter = ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(10)),
        };

    private static string? _gatewayUri;
    private static string? _providerTenantId;

    /// <summary>Creates a loader. Loaders are registered with <c>RegisterAsync</c>; creating one directly is useful for composing chains.</summary>
    public TransparentProxy()
    {
    }

    /// <summary>The seam for checking connectivity; replaced by tests.</summary>
    internal static INetworkVerifier NetworkVerifier { get; set; } = new NetworkVerifier();

    /// <summary>The seam that creates the client for the gateway; replaced by tests.</summary>
    internal static Func<IHttpDestination, HttpClient> HttpClientSource { get; set; } =
        destination => HttpClientAccessor.GetHttpClient(destination);

    internal static string? GatewayUri
    {
        get
        {
            lock (Gate)
            {
                return _gatewayUri;
            }
        }
    }

    internal static string? ProviderTenantId
    {
        get
        {
            lock (Gate)
            {
                return _providerTenantId;
            }
        }
    }

    /// <summary>
    /// Registers a transparent proxy gateway that handles all subsequent destination lookups.
    /// </summary>
    /// <param name="host">
    /// The gateway host, for example <c>gateway.svc.cluster.local</c> or <c>https://gateway</c>. Without a scheme, HTTP is
    /// used. It must not contain a path.
    /// </param>
    /// <param name="port">The port of the gateway; the host is verified to be reachable on it.</param>
    /// <param name="providerTenantId">
    /// The tenant to use if there is no current tenant. Without it, lookups without a current tenant fail.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the gateway is registered.</returns>
    /// <exception cref="DestinationAccessException">
    /// A gateway is already registered, the host contains a path or has an invalid format, or it is not reachable.
    /// </exception>
    public static async Task RegisterAsync(
        string host,
        int port = DefaultPort,
        string? providerTenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (GatewayUri is not null)
        {
            throw new DestinationAccessException("TransparentProxy is already registered. Only one registration is allowed.");
        }

        var normalizedHost = host.Contains(SchemeSeparator, StringComparison.Ordinal) ? host : "http" + SchemeSeparator + host;
        var hostForVerification = GetHostForVerification(host, normalizedHost);
        await NetworkVerifier.VerifyHostConnectivityAsync(hostForVerification, port, cancellationToken).ConfigureAwait(false);

        lock (Gate)
        {
            if (_gatewayUri is not null)
            {
                throw new DestinationAccessException("TransparentProxy is already registered. Only one registration is allowed.");
            }
            _gatewayUri = string.Create(CultureInfo.InvariantCulture, $"{normalizedHost}:{port}");
            _providerTenantId = providerTenantId;
            DestinationAccessor.PrependDestinationLoader(new TransparentProxy());
        }
    }

    /// <summary>Registers a transparent proxy gateway on port 80 with a provider tenant.</summary>
    /// <param name="host">The gateway host; see <see cref="RegisterAsync(string, int, string?, CancellationToken)"/>.</param>
    /// <param name="providerTenantId">The tenant to use if there is no current tenant.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the gateway is registered.</returns>
    /// <exception cref="DestinationAccessException">The gateway cannot be registered.</exception>
    public static Task RegisterAsync(string host, string providerTenantId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerTenantId);
        return RegisterAsync(host, DefaultPort, providerTenantId, cancellationToken);
    }

    /// <summary>Forgets the registration. The loader remains in the chain of <see cref="DestinationAccessor"/>; intended for tests.</summary>
    internal static void Reset()
    {
        lock (Gate)
        {
            _gatewayUri = null;
            _providerTenantId = null;
        }
    }

    /// <summary>Looks up a destination. Blocks the calling thread; prefer <see cref="TryGetDestinationAsync"/>.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Options; fragment, cross-level scope and custom headers are forwarded to the gateway.</param>
    /// <returns>The destination or the failure.</returns>
    /// <exception cref="TenantAccessException">There is no current tenant and no provider tenant was registered.</exception>
    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options) =>
        TryGetDestinationAsync(destinationName, options).GetAwaiter().GetResult();

    /// <summary>Looks up a destination by asking the gateway whether it exists.</summary>
    /// <param name="destinationName">The name.</param>
    /// <param name="options">Options; fragment, cross-level scope and custom headers are forwarded to the gateway.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The destination or the failure.</returns>
    /// <exception cref="TenantAccessException">There is no current tenant and no provider tenant was registered.</exception>
    public async Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        ArgumentNullException.ThrowIfNull(options);
        var destination = PrepareDestination(destinationName, options);
        try
        {
            return Result.Success<IDestination>(await VerifyDestinationAsync(destination, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return Result.Failure<IDestination>(e);
        }
    }

    private static string GetHostForVerification(string host, string normalizedHost)
    {
        // Mirrors java.net.URI: the path is what follows the authority (up to a query or fragment).
        var authorityStart = normalizedHost.IndexOf(SchemeSeparator, StringComparison.Ordinal) + SchemeSeparator.Length;
        var authorityEnd = normalizedHost.AsSpan(authorityStart).IndexOfAny('/', '?', '#');
        authorityEnd = authorityEnd < 0 ? normalizedHost.Length : authorityStart + authorityEnd;
        if (authorityEnd < normalizedHost.Length && normalizedHost[authorityEnd] == '/')
        {
            var pathEnd = normalizedHost.AsSpan(authorityEnd).IndexOfAny('?', '#');
            var path = pathEnd < 0 ? normalizedHost[authorityEnd..] : normalizedHost[authorityEnd..(authorityEnd + pathEnd)];
            throw new DestinationAccessException(
                $"Host '{host}' contains a path '{path}'. Paths are not allowed in host registration.");
        }

        if (!Uri.TryCreate(normalizedHost, UriKind.Absolute, out var parsed))
        {
            throw new DestinationAccessException($"Invalid host format: [{host}]. Caused by: the URI is malformed.");
        }
        return parsed.Host.Length == 0
            ? throw new DestinationAccessException($"Invalid host format: [{host}]")
            : parsed.Host;
    }

    private static TransparentProxyDestination PrepareDestination(string destinationName, DestinationOptions options)
    {
        var gatewayUri = GatewayUri
            ?? throw new DestinationAccessException("No TransparentProxy has been registered. Call TransparentProxy.RegisterAsync first.");
        var tenant = RetrieveTenant();

        var gatewayBuilder = TransparentProxyDestination.CreateGatewayBuilder(destinationName, gatewayUri);
        gatewayBuilder.TenantId(tenant.TenantId);

        var fragmentName = DestinationServiceOptionsAugmenter.GetFragmentName(options);
        if (fragmentName is not null)
        {
            gatewayBuilder.FragmentName(fragmentName);
        }
        var crossLevelScope = DestinationServiceOptionsAugmenter.GetCrossLevelScope(options);
        if (crossLevelScope is { } scope)
        {
            if (fragmentName is not null)
            {
                gatewayBuilder.FragmentLevel(scope);
            }
            gatewayBuilder.DestinationLevel(scope);
        }
        foreach (var header in DestinationServiceOptionsAugmenter.GetAdditionalHeaders(options))
        {
            gatewayBuilder.Header(header);
        }
        return gatewayBuilder.Build();
    }

    private static ITenant RetrieveTenant()
    {
        var current = TenantAccessor.TryGetCurrentTenant();
        if (current.IsSuccess)
        {
            return current.GetValueOrThrow();
        }
        return ProviderTenantId is { } providerTenantId
            ? new DefaultTenant(providerTenantId)
            : throw new TenantAccessException(NoTenantProvidedErrorMessage, current.Exception);
    }

    private static async Task<TransparentProxyDestination> VerifyDestinationAsync(
        TransparentProxyDestination destination,
        CancellationToken cancellationToken)
    {
        var destinationUri = new Uri(GatewayUri!);
        var destinationName = GetDestinationName(destination, destinationUri);

        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Performing HEAD request to destination with name {Name} to verify the destination exists", destinationName);
        }

        try
        {
            using var response = await ResilienceDecorator.ExecuteAsync(
                async token =>
                {
                    try
                    {
                        using var client = HttpClientSource(destination);
                        using var request = new HttpRequestMessage(HttpMethod.Head, destinationUri);
                        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    }
                    catch (HttpRequestException e)
                    {
                        throw new DestinationAccessException(FailedToVerifyDestination, e);
                    }
                },
                ResilienceConfig,
                null,
                cancellationToken).ConfigureAwait(false);
            VerifyTransparentProxyResponse(response, destinationName);
        }
        catch (ResilienceRuntimeException e)
        {
            for (Exception? cause = e; cause is not null; cause = cause.InnerException)
            {
                if (cause is DestinationNotFoundException)
                {
                    throw new DestinationNotFoundException(null, cause.Message, e);
                }
            }
            throw new DestinationAccessException(e.Message, e);
        }

        return destination;
    }

    private static string GetDestinationName(TransparentProxyDestination destination, Uri destinationUri) =>
        destination.GetHeaders(destinationUri)
            .FirstOrDefault(h => string.Equals(h.Name, TransparentProxyDestination.DestinationNameHeaderKey, StringComparison.OrdinalIgnoreCase))
            ?.Value
        ?? throw new InvalidOperationException("Destination name header in Transparent Proxy loader is missing.");

    private static void VerifyTransparentProxyResponse(HttpResponseMessage response, string destinationName)
    {
        if (response.Headers.TryGetValues(SetCookieHeader, out var cookies))
        {
            var cookieNames = cookies.Select(c => c.Split('=', 2)[0]).ToList();
            Log.LogWarning(
                "received set-cookie headers as part of destination health check. This is unexpected and may have side effects for your application. The following cookies were set: {CookieNames}",
                string.Join(", ", cookieNames));
        }

        var statusCode = (int)response.StatusCode;
        var errorInternalCode = GetHeaderValue(response, XErrorInternalCodeHeader);
        var errorMessage = GetHeaderValue(response, XErrorMessageHeader);
        var errorOrigin = GetHeaderValue(response, XErrorOriginHeader);

        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "HEAD request to destination with name {Name} returned status code: {Status}, x-error-internal-code: {Code}",
                destinationName,
                statusCode,
                errorInternalCode);
        }
        if (statusCode == 502 && errorInternalCode == "404")
        {
            throw new DestinationNotFoundException(destinationName, errorMessage);
        }
        if (errorOrigin.Length != 0)
        {
            throw new DestinationAccessException(
                $"{FailedToVerifyDestination}Destination name: [{destinationName}], Origin: [{errorOrigin}], Code: [{errorInternalCode}], Message: [{errorMessage}]");
        }
    }

    private static string GetHeaderValue(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
}
