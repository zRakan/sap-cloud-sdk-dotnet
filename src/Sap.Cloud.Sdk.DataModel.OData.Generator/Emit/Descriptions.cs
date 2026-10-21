using System.Globalization;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// The texts of the documentation comments, from <c>Documentation</c> elements, <c>sap:quickinfo</c> and <c>sap:label</c>.
/// Port of <c>JavadocUtils</c>.
/// </summary>
internal static class Descriptions
{
    /// <summary>The summary of the documentation element, else the quick info, else the label; empty if there is none.</summary>
    internal static string Basic(EdmAnnotated element)
    {
        var summary = element.Documentation?.Summary?.Trim();
        if (!string.IsNullOrEmpty(summary))
        {
            return summary;
        }
        var text = (element.QuickInfo ?? element.Label)?.Trim();
        return text ?? "";
    }

    /// <summary>The long description of the documentation element; empty if there is none.</summary>
    internal static string Detailed(EdmAnnotated element)
    {
        var text = element.Documentation?.LongDescription?.Trim();
        return string.IsNullOrEmpty(text) ? "" : text;
    }

    /// <summary>The facets in words, for example <c>Not nullable, Maximum length: 10</c>; empty if no facet is set.</summary>
    internal static string Constraints(EdmFacets facets)
    {
        var parts = new List<string>();
        if (facets.Nullable is { } nullable)
        {
            parts.Add(nullable ? "Nullable" : "Not nullable");
        }
        if (!string.IsNullOrEmpty(facets.DefaultValue))
        {
            parts.Add("Default value: " + facets.DefaultValue);
        }
        if (facets.MaxLength is { } maxLength)
        {
            parts.Add("Maximum length: " + maxLength.ToString(CultureInfo.InvariantCulture));
        }
        if (facets.Precision is { } precision)
        {
            parts.Add("Precision: " + precision.ToString(CultureInfo.InvariantCulture));
        }
        if (facets.Scale is { } scale)
        {
            parts.Add("Scale: " + scale.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// The lines of the summary of a documented element: the description (if any), the long description, the constraints and the
    /// reference to the original EDM name.
    /// </summary>
    /// <param name="prefix">Text before the first line, for example <c>(Key Field) </c>.</param>
    /// <param name="basic">The summary text.</param>
    /// <param name="detailed">The long description.</param>
    /// <param name="constraints">The constraints.</param>
    /// <param name="original">The text naming the original EDM element, already escaped.</param>
    internal static List<string> SummaryLines(string prefix, string basic, string detailed, string constraints, string original)
    {
        var lines = new List<string>();
        if (basic.Length > 0)
        {
            lines.Add(prefix + CSharp.Xml(basic));
            AddParagraph(lines, detailed.Length > 0 ? CSharp.Xml(detailed) : "");
            AddParagraph(lines, constraints.Length > 0 ? "Constraints: " + CSharp.Xml(constraints) : "");
            AddParagraph(lines, original);
        }
        else
        {
            lines.Add(prefix + original);
            AddParagraph(lines, detailed.Length > 0 ? CSharp.Xml(detailed) : "");
            AddParagraph(lines, constraints.Length > 0 ? "Constraints: " + CSharp.Xml(constraints) : "");
        }
        return lines;
    }

    private static void AddParagraph(List<string> lines, string text)
    {
        if (text.Length > 0)
        {
            lines.Add($"<para>{text}</para>");
        }
    }
}
