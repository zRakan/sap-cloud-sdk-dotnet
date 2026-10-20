using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Merges the URI and the <c>URL.queries.*</c> properties of an HTTP destination into request URIs.</summary>
/// <remarks>
/// Port of the URI handling of Java's <c>ApacheHttpClient5Wrapper</c> together with <c>QueryParamGetter</c>.
/// The same logic is applied by <see cref="DestinationRequestHandler"/> to every outgoing request.
/// </remarks>
public sealed class DestinationUriQueryMerger : IUriQueryMerger
{
    private const string UrlQueries = "URL.queries.";
    private const string SafeChars = "_*-:,/'().|";
    private static readonly ILogger Log = SdkLogging.CreateLogger<DestinationUriQueryMerger>();

    private readonly IHttpDestinationProperties _destination;

    /// <summary>Initializes a new instance of the <see cref="DestinationUriQueryMerger"/> class.</summary>
    /// <param name="destination">The destination.</param>
    public DestinationUriQueryMerger(IHttpDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        _destination = destination;
    }

    /// <inheritdoc/>
    public Uri MergeRequestUri(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        return Merge(requestUri);
    }

    /// <summary>Merges the destination URI, the request URI (may be <see langword="null"/>) and the destination's query properties.</summary>
    internal Uri Merge(Uri? requestUri)
    {
        var merged = UriPathMerger.Merge(_destination.Uri, requestUri);
        var queryString = string.Join("&", GetQueryParameters(_destination));
        return UriPathMerger.Merge(merged, new Uri("/?" + queryString, UriKind.Relative));
    }

    /// <summary>Gets the query parameters defined by the <c>URL.queries.*</c> properties, escaped as Java does.</summary>
    internal static List<string> GetQueryParameters(IDestinationProperties properties)
    {
        var result = new List<string>();
        foreach (var key in properties.PropertyNames)
        {
            if (!key.StartsWith(UrlQueries, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (properties.GetProperty(key) is string value)
            {
                result.Add(Escape(key[UrlQueries.Length..]) + "=" + Escape(value));
            }
            else if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Cannot find value for property \"{Property}\" defined in destination. Skipping the query parameter.", key);
            }
        }
        return result;
    }

    // Guava PercentEscaper("_*-:,/'().|", plusForSpace: false): ASCII letters and digits and the safe characters stay.
    private static string Escape(string value)
    {
        var sb = new StringBuilder();
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.IsAscii && (char.IsAsciiLetterOrDigit((char)rune.Value) || SafeChars.Contains((char)rune.Value, StringComparison.Ordinal)))
            {
                sb.Append((char)rune.Value);
                continue;
            }
            var length = rune.EncodeToUtf8(bytes);
            foreach (var b in bytes[..length])
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }
}
