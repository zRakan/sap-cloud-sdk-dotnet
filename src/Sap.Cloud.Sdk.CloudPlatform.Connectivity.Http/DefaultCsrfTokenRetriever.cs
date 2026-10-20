using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Default implementation for retrieving a CSRF token from a remote system.</summary>
/// <remarks>All instances are equal, as in Java (<c>@EqualsAndHashCode</c> without fields).</remarks>
public sealed partial class DefaultCsrfTokenRetriever : ICsrfTokenRetriever, IEquatable<DefaultCsrfTokenRetriever>
{
    /// <summary>The HTTP header name for loading the CSRF token.</summary>
    public const string XCsrfTokenHeaderKey = "x-csrf-token";

    private const string FetchValue = "fetch";
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultCsrfTokenRetriever>();

    /// <inheritdoc/>
    public async Task<CsrfToken> RetrieveCsrfTokenAsync(
        HttpClient httpClient,
        string servicePath,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(servicePath);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(servicePath, UriKind.RelativeOrAbsolute));
            request.Headers.TryAddWithoutValidation(XCsrfTokenHeaderKey, FetchValue);
            if (headers is not null)
            {
                foreach (var (name, values) in headers)
                {
                    foreach (var value in values)
                    {
                        request.Headers.TryAddWithoutValidation(name, value);
                    }
                }
            }

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.Headers.TryGetValues(XCsrfTokenHeaderKey, out var tokens) || tokens.FirstOrDefault() is not { } raw)
            {
                var message = $"CSRF token retrieval failed: The server did not respond with the {XCsrfTokenHeaderKey} header.";
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.LogDebug(
                        "{Message} The subsequent request to service path {ServicePath} is bound to fail if the OData service requires a CSRF token in request header.",
                        message,
                        servicePath);
                }
                throw new CsrfTokenRetrievalException(message);
            }
            var token = new CsrfToken(NonPrintableChars().Replace(raw, string.Empty));
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Successfully retrieved CSRF token from service path {ServicePath}", servicePath);
            }
            return token;
        }
        catch (CsrfTokenRetrievalException)
        {
            throw;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            const string message = "CSRF token retrieval failed: The HTTP request was not successful.";
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(e, "{Message} The affected service path is: {ServicePath}", message, servicePath);
            }
            throw new CsrfTokenRetrievalException(message, e);
        }
    }

    /// <inheritdoc/>
    public bool Equals(DefaultCsrfTokenRetriever? other) => other is not null;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DefaultCsrfTokenRetriever;

    /// <inheritdoc/>
    public override int GetHashCode() => typeof(DefaultCsrfTokenRetriever).GetHashCode();

    [GeneratedRegex("[^ -~]")]
    private static partial Regex NonPrintableChars();
}
