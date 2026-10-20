using System.Text;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>Determines how URI path segments and query values are percent-encoded.</summary>
public enum UriEncodingStrategy
{
    /// <summary>No encoding.</summary>
    None,

    /// <summary>Regular HTTP request encoding.</summary>
    Regular,

    /// <summary>Encoding for requests that are embedded in a <c>$batch</c> payload.</summary>
    Batch,
}

/// <summary>Percent-escaping for <see cref="UriEncodingStrategy"/>.</summary>
/// <remarks>Replaces the Guava <c>Escaper</c> instances held by the Java enum.</remarks>
public static class UriEncodingStrategyExtensions
{
    private const string SafeAlphanumeric = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    // Guava UrlEscapers.urlPathSegmentEscaper()
    private static readonly HashSet<char> RegularPathSafe = [.. SafeAlphanumeric, .. "-._~!$'()*,;&=@:+"];
    private static readonly HashSet<char> RegularQuerySafe = [.. SafeAlphanumeric, .. "_*-:,/'()."];
    private static readonly HashSet<char> BatchSafe = [.. SafeAlphanumeric, .. "_~-."];

    /// <summary>Escapes a path segment.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapePath(this UriEncodingStrategy strategy, string value) => strategy switch
    {
        UriEncodingStrategy.None => value,
        UriEncodingStrategy.Regular => Escape(value, RegularPathSafe),
        UriEncodingStrategy.Batch => Escape(value, BatchSafe),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    /// <summary>Escapes a query value.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapeQuery(this UriEncodingStrategy strategy, string value) => strategy switch
    {
        UriEncodingStrategy.None => value,
        UriEncodingStrategy.Regular => Escape(value, RegularQuerySafe),
        UriEncodingStrategy.Batch => Escape(value, BatchSafe),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    /// <summary>Percent-escapes everything except the safe characters, using the UTF-8 bytes in upper-case hex.</summary>
    private static string Escape(string value, HashSet<char> safe)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.All(safe.Contains))
        {
            return value;
        }
        var sb = new StringBuilder(value.Length + 16);
        Span<byte> buffer = stackalloc byte[4];
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.IsBmp && safe.Contains((char)rune.Value))
            {
                sb.Append((char)rune.Value);
                continue;
            }
            var length = rune.EncodeToUtf8(buffer);
            for (var i = 0; i < length; i++)
            {
                sb.Append('%').Append(buffer[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }
}
