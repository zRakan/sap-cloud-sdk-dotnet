using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>Creates and encodes the relative URIs of OData requests.</summary>
public static partial class ODataUriFactory
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(ODataUriFactory).FullName!);

    [GeneratedRegex(@"\$deltatoken=([^&]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeltaTokenPattern();

    [GeneratedRegex(@"\$skiptoken=([^&]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SkipTokenPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9/?:@\-._~!$&'()*+,;=%]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidUrlQueryPattern();

    [GeneratedRegex("//+", RegexOptions.CultureInvariant)]
    private static partial Regex DoubleSlashes();

    internal static Uri CreateAndEncodeUri(
        string servicePath,
        ODataResourcePath resourcePath,
        string? encodedQuery,
        UriEncodingStrategy strategy) =>
        CreateAndEncodeUri(servicePath, resourcePath.ToEncodedPathString(strategy), encodedQuery, strategy);

    internal static Uri CreateAndEncodeUri(
        string servicePath,
        string encodedResourcePath,
        string? encodedQuery,
        UriEncodingStrategy strategy)
    {
        var encodedPath = EncodePath(servicePath, strategy);
        encodedPath = SanitizeUrlPath(encodedPath);
        encodedPath += encodedResourcePath.StartsWith('/') ? encodedResourcePath : "/" + encodedResourcePath;

        var hasQuery = !string.IsNullOrEmpty(encodedQuery);
        if (hasQuery && !ValidUrlQueryPattern().IsMatch(encodedQuery!))
        {
            throw new ArgumentException($"The query part of OData request is not correctly encoded: \"{encodedQuery}\"");
        }
        var resultUrl = encodedPath + (hasQuery ? "?" + encodedQuery : string.Empty);
        if (!Uri.TryCreate(resultUrl, UriKind.Relative, out var uri))
        {
            Log.Error(
                "Failed to construct URI for OData request with service path '{ServicePath}', resource path '{ResourcePath}' and query '{Query}'.",
                servicePath,
                encodedResourcePath,
                encodedQuery);
            throw new ArgumentException("Failed to construct URI.");
        }
        return uri;
    }

    /// <summary>Percent-encodes a path, segment by segment.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The encoded path.</returns>
    public static string EncodePath(string path) => EncodePath(path, UriEncodingStrategy.Regular);

    /// <summary>Percent-encodes a path, segment by segment.</summary>
    /// <param name="path">The path.</param>
    /// <param name="strategy">The encoding strategy.</param>
    /// <returns>The encoded path.</returns>
    public static string EncodePath(string path, UriEncodingStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Join("/", path.Split('/').Select(segment => strategy.EscapePath(segment)));
    }

    /// <summary>Percent-encodes a single path segment.</summary>
    /// <param name="path">The segment.</param>
    /// <returns>The encoded segment.</returns>
    public static string EncodePathSegment(string path) => EncodePathSegment(path, UriEncodingStrategy.Regular);

    /// <summary>Percent-encodes a single path segment.</summary>
    /// <param name="path">The segment.</param>
    /// <param name="strategy">The encoding strategy.</param>
    /// <returns>The encoded segment.</returns>
    public static string EncodePathSegment(string path, UriEncodingStrategy strategy) => strategy.EscapePath(path);

    /// <summary>Percent-encodes a query value.</summary>
    /// <param name="input">The value.</param>
    /// <returns>The encoded value.</returns>
    public static string EncodeQuery(string input) => EncodeQuery(input, UriEncodingStrategy.Regular);

    /// <summary>Percent-encodes a query value.</summary>
    /// <param name="input">The value.</param>
    /// <param name="strategy">The encoding strategy.</param>
    /// <returns>The encoded value.</returns>
    public static string EncodeQuery(string input, UriEncodingStrategy strategy) => strategy.EscapeQuery(input);

    /// <summary>Extracts the <c>$deltatoken</c> from a URL.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    public static string? ExtractDeltaToken(string? url) => ExtractToken(DeltaTokenPattern(), url);

    /// <summary>Extracts the <c>$skiptoken</c> from a URL.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    public static string? ExtractSkipToken(string? url) => ExtractToken(SkipTokenPattern(), url);

    private static string? ExtractToken(Regex pattern, string? url)
    {
        if (url is null)
        {
            return null;
        }
        var match = pattern.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string SanitizeUrlPath(string path)
    {
        var withoutDoubleSlashes = DoubleSlashes().Replace("/" + path, "/");
        return withoutDoubleSlashes.EndsWith('/') ? withoutDoubleSlashes[..^1] : withoutDoubleSlashes;
    }
}
