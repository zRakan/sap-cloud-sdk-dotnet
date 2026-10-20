using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>URI parsing helpers that avoid the implicit file path interpretation of <see cref="Uri"/> on Unix.</summary>
internal static partial class UriHelper
{
    /// <summary>
    /// Parses a destination URL: an absolute URI with a scheme, or (as Java accepts) a relative or empty URI. Relative
    /// URIs are parsed explicitly as such, because <see cref="Uri"/> would treat <c>/path</c> as a file URI on Unix.
    /// </summary>
    public static bool TryParseDestinationUri(string value, [NotNullWhen(true)] out Uri? uri)
    {
        if (SchemePrefix().IsMatch(value))
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri);
        }
        return Uri.TryCreate(value, UriKind.Relative, out uri);
    }

    /// <summary>Whether the original string of the URI has a scheme (independent of the platform's file path handling).</summary>
    public static bool HasScheme(Uri uri) => SchemePrefix().IsMatch(uri.OriginalString);

    /// <summary>Splits the original string of a URI into its raw RFC 3986 components, as java.net.URI exposes them.</summary>
    public static RawUri Split(Uri uri)
    {
        var match = Rfc3986().Match(uri.OriginalString);
        return new RawUri(
            match.Groups[2].Success ? match.Groups[2].Value : null,
            match.Groups[4].Success ? match.Groups[4].Value : null,
            match.Groups[5].Value,
            match.Groups[7].Success ? match.Groups[7].Value : null,
            match.Groups[9].Success ? match.Groups[9].Value : null);
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:")]
    private static partial Regex SchemePrefix();

    [GeneratedRegex(@"^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\?([^#]*))?(#(.*))?$", RegexOptions.Singleline)]
    private static partial Regex Rfc3986();

    /// <summary>The raw components of a URI.</summary>
    internal sealed record RawUri(string? Scheme, string? Authority, string Path, string? Query, string? Fragment)
    {
        public bool IsAbsolute => Scheme is not null;
    }
}
