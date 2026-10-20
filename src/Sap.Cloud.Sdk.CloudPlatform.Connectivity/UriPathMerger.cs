using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Merges the URI of a destination with the (usually relative) URI of a request.</summary>
/// <remarks>
/// <para>
/// The components are taken from the original strings of the URIs (not from the normalized <see cref="Uri"/> members), as
/// <c>java.net.URI</c> does. Java's <c>UriBuilder</c>, which only concatenates the components, is not ported; the merged string is
/// parsed with <see cref="Uri"/>.
/// </para>
/// <para>
/// A relative request URI must be created with <see cref="UriKind.Relative"/>.
/// </para>
/// </remarks>
public static partial class UriPathMerger
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(UriPathMerger).FullName!);

    /// <summary>Merges two URIs.</summary>
    /// <param name="primaryUri">The destination URI: absolute, or empty if the request URI is absolute.</param>
    /// <param name="secondaryUri">The request URI, or <see langword="null"/>.</param>
    /// <returns>
    /// The merged absolute URI. An absolute request URI wins over the destination's path; a relative one is appended to
    /// the destination's path. Queries are concatenated with <c>&amp;</c>; the request's fragment wins.
    /// </returns>
    /// <exception cref="DestinationPathsNotMergeableException">The URIs cannot be merged.</exception>
    public static Uri Merge(Uri primaryUri, Uri? secondaryUri)
    {
        ArgumentNullException.ThrowIfNull(primaryUri);
        var primary = UriHelper.Split(primaryUri);
        var secondary = secondaryUri is null ? null : UriHelper.Split(secondaryUri);

        if (primaryUri.OriginalString.Length == 0)
        {
            if (secondary is null || !secondary.IsAbsolute)
            {
                throw new DestinationPathsNotMergeableException(
                    "The destination URI may be empty ONLY if the request URI is absolute.");
            }
            return secondary.Path.Length == 0 ? Parse(secondary.Scheme + "://" + secondary.Authority + "/") : secondaryUri!;
        }
        if (!primary.IsAbsolute)
        {
            throw new DestinationPathsNotMergeableException("The destination URI must be absolute OR empty.");
        }
        if (secondary is null)
        {
            return primary.Path.Length == 0 ? Parse(primary.Scheme + "://" + primary.Authority + "/") : primaryUri;
        }

        AssertSecondaryHostMatchesPrimaryHost(primaryUri, primary, secondaryUri!, secondary);
        var mergedPath = GetMergedPath(primary, secondary);
        var mergedQuery = string.Join("&", new[] { primary.Query, secondary.Query }.Where(q => !string.IsNullOrWhiteSpace(q)));

        var merged = new System.Text.StringBuilder()
            .Append(primary.Scheme).Append(':')
            .Append("//").Append(primary.Authority)
            .Append(mergedPath);
        if (mergedQuery.Trim().Length > 0)
        {
            merged.Append('?').Append(mergedQuery.Trim());
        }
        var fragment = secondary.Fragment ?? primary.Fragment;
        if (fragment is not null)
        {
            merged.Append('#').Append(fragment);
        }
        var result = Parse(merged.ToString());
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Merged {Primary} and {Secondary} into {Result}", primaryUri, secondaryUri, mergedPath);
        }
        return result;
    }

    private static Uri Parse(string uri)
    {
        try
        {
            return new Uri(uri, UriKind.Absolute);
        }
        catch (UriFormatException e)
        {
            throw new DestinationPathsNotMergeableException("Failed to merge request URI.", e);
        }
    }

    private static void AssertSecondaryHostMatchesPrimaryHost(
        Uri primaryUri,
        UriHelper.RawUri primary,
        Uri secondaryUri,
        UriHelper.RawUri secondary)
    {
        if (secondary.Authority is not null && !string.Equals(primary.Authority, secondary.Authority, StringComparison.Ordinal))
        {
            throw new DestinationPathsNotMergeableException(
                $"URIs defined in destination '{primaryUri}' and supplied by application code '{secondaryUri}' differ.");
        }
    }

    private static string GetMergedPath(UriHelper.RawUri destinationUri, UriHelper.RawUri requestUri)
    {
        if (requestUri.IsAbsolute)
        {
            if (Log.IsEnabled(LogLevel.Trace))
            {
                Log.LogTrace("Using specific request path: {Path}", requestUri.Path);
            }
            return requestUri.Path.StartsWith('/') ? requestUri.Path : "/" + requestUri.Path;
        }
        var mergedPath = MergeUriRawPaths(destinationUri.Path, requestUri.Path);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Merging request path {RequestPath} into destination path {DestinationPath}: {Merged}", requestUri.Path, destinationUri.Path, mergedPath);
        }
        return mergedPath;
    }

    private static string MergeUriRawPaths(string firstRawPath, string secondRawPath)
    {
        var first = LeadingSlash().Replace(firstRawPath.Trim(), string.Empty, 1);
        var second = LeadingSlash().Replace(secondRawPath.Trim(), string.Empty, 1);
        string result;
        if (second.Length == 0)
        {
            result = first;
        }
        else if (second.StartsWith(first, StringComparison.Ordinal))
        {
            result = second;
        }
        else
        {
            result = (first.EndsWith('/') ? first : first + "/") + second;
        }
        return result.StartsWith('/') ? result : "/" + result;
    }

    [GeneratedRegex("^/")]
    private static partial Regex LeadingSlash();
}
