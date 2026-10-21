using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Emit;

/// <summary>Helpers for XML documentation comments and string literals.</summary>
internal static partial class XmlDoc
{
    /// <summary>Escapes text for use inside an XML documentation comment.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text without line breaks.</returns>
    public static string Escape(string text)
    {
        var collapsed = WhitespaceRegex().Replace(text, " ").Trim();
        return collapsed.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    /// <summary>Writes <c>/// &lt;summary&gt;text&lt;/summary&gt;</c> on a single line.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="markup">The (already escaped) summary.</param>
    public static void Summary(CodeWriter writer, string markup)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Line($"/// <summary>{markup}</summary>");
    }

    /// <summary>Writes a summary with each line on its own line.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="markupLines">The (already escaped) lines.</param>
    public static void SummaryLines(CodeWriter writer, IReadOnlyList<string> markupLines)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(markupLines);
        writer.Line("/// <summary>");
        foreach (var line in markupLines)
        {
            writer.Line("/// " + line);
        }

        writer.Line("/// </summary>");
    }

    /// <summary>
    /// Writes a summary of one line if there is nothing else to say, and one line per paragraph otherwise.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="first">The first (already escaped) line.</param>
    /// <param name="paragraphs">Further paragraphs (already escaped).</param>
    public static void SummaryWithParagraphs(CodeWriter writer, string first, IReadOnlyList<string> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(paragraphs);
        if (paragraphs.Count == 0)
        {
            Summary(writer, first);
            return;
        }

        var lines = new List<string> { first };
        lines.AddRange(paragraphs.Select(p => $"<para>{p}</para>"));
        SummaryLines(writer, lines);
    }

    /// <summary>Formats a C# string literal.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The literal including the quotes.</returns>
    public static string Literal(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.Append('"').ToString();
    }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex WhitespaceRegex();
}
