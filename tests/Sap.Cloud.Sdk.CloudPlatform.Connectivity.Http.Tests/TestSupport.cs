using System.Net;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Cache;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string[]> Headers, string? Body)
{
    public string? Header(string name) =>
        Headers.TryGetValue(name, out var values) ? string.Join(",", values) : null;
}

/// <summary>A terminal handler that records requests and answers with a configurable response.</summary>
internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public bool Disposed { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = [.. header.Value];
        }
        string? body = null;
        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = [.. header.Value];
            }
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        lock (Requests)
        {
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, headers, body));
        }
        return respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK);
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>A header provider that records the URIs it is asked for and completes asynchronously.</summary>
internal sealed class AsyncHeaderProvider(Func<Uri, Header[]> headers) : IDestinationHeaderProvider
{
    public List<Uri> Requested { get; } = [];

    public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) =>
        throw new InvalidOperationException("The synchronous method must not be called by the HTTP pipeline.");

    public async ValueTask<IReadOnlyList<Header>> GetHeadersAsync(
        DestinationRequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        Requested.Add(requestContext.RequestUri);
        return headers(requestContext.RequestUri);
    }
}

/// <summary>A minimal destination whose proxy settings can be chosen freely (the default builder resolves them eagerly).</summary>
internal sealed class StubHttpDestination(Uri uri, ProxyType? proxyType, ProxyConfiguration? proxyConfiguration) : IHttpDestination
{
    public IReadOnlyCollection<string> PropertyNames => [];

    public object? GetProperty(string key) => null;

    public Uri Uri => uri;

    public string? TlsVersion => null;

    public ProxyConfiguration? ProxyConfiguration => proxyConfiguration;

    public System.Security.Cryptography.X509Certificates.X509Certificate2Collection? KeyStore => null;

    public string? KeyStorePassword => null;

    public bool IsTrustingAllCertificates => false;

    public Sap.Cloud.Sdk.CloudPlatform.Security.BasicCredentials? BasicCredentials => null;

    public AuthenticationType AuthenticationType => AuthenticationType.NoAuthentication;

    public ProxyType? ProxyType => proxyType;

    public System.Security.Cryptography.X509Certificates.X509Certificate2Collection? TrustStore => null;

    public string? TrustStorePassword => null;

    public IReadOnlyList<Header> GetHeaders(Uri requestUri) => [];
}

internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    public List<(LogLevel Level, string Category, string Message)> Entries { get; } = [];

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerFactory owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (owner.Entries)
            {
                owner.Entries.Add((logLevel, category, formatter(state, exception)));
            }
        }
    }
}

/// <summary>Restores the static state touched by the HTTP tests.</summary>
public abstract class HttpStateReset : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        HttpClientAccessor.HttpClientFactory = null!;
        DefaultDestinationHttpClientFactory.Shared = null!;
        DestinationAccessor.Loader = null!;
        SdkLogging.LoggerFactory = null!;
        CacheManager.InvalidateAll();
    }
}
