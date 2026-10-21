using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>Case formats of Guava's <c>CaseFormat</c> that the name conversions use.</summary>
internal enum CaseFormat
{
    LowerCamel,
    LowerUnderscore,
    UpperCamel,
    UpperUnderscore,
}

/// <summary>A port of the parts of Guava's <c>CaseFormat</c> that the Java generator relies on, including its treatment of consecutive separators.</summary>
internal static class CaseFormats
{
    internal static string Convert(CaseFormat from, CaseFormat to, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (from == to)
        {
            return text;
        }

        var sourceSeparatorLength = Separator(from).Length;
        StringBuilder? result = null;
        var i = 0;
        var j = -1;
        while (true)
        {
            j++;
            j = IndexOfBoundary(from, text, j);
            if (j == -1)
            {
                break;
            }
            if (i == 0)
            {
                result = new StringBuilder(text.Length + 4);
                result.Append(NormalizeFirstWord(to, text[i..j]));
            }
            else
            {
                result!.Append(NormalizeWord(to, text[i..j]));
            }
            result.Append(Separator(to));
            i = j + sourceSeparatorLength;
        }
        return i == 0 ? NormalizeFirstWord(to, text) : result!.Append(NormalizeWord(to, text[i..])).ToString();
    }

    private static int IndexOfBoundary(CaseFormat format, string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            var c = text[index];
            if (format is CaseFormat.LowerCamel or CaseFormat.UpperCamel ? c is >= 'A' and <= 'Z' : c == '_')
            {
                return index;
            }
        }
        return -1;
    }

    private static string Separator(CaseFormat format) => format is CaseFormat.LowerCamel or CaseFormat.UpperCamel ? "" : "_";

    private static string NormalizeFirstWord(CaseFormat format, string word) =>
        format == CaseFormat.LowerCamel ? AsciiLower(word) : NormalizeWord(format, word);

    private static string NormalizeWord(CaseFormat format, string word) => format switch
    {
        CaseFormat.LowerCamel or CaseFormat.UpperCamel => FirstCharOnlyToUpper(word),
        CaseFormat.LowerUnderscore => AsciiLower(word),
        _ => AsciiUpper(word),
    };

    private static string FirstCharOnlyToUpper(string word) =>
        word.Length == 0 ? word : AsciiUpper(word[..1]) + AsciiLower(word[1..]);

    private static string AsciiLower(string text) => string.Create(text.Length, text, static (span, source) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = source[i];
            span[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
        }
    });

    private static string AsciiUpper(string text) => string.Create(text.Length, text, static (span, source) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = source[i];
            span[i] = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
        }
    });
}

/// <summary>Helpers for the conversion of OData names to C# identifiers. Port of <c>NamingUtils</c>.</summary>
internal static partial class NamingUtils
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while",
    };

    [GeneratedRegex(@"^\s*|\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WordStart();

    [GeneratedRegex(@"^[A-Z]+(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingAcronym();

    [GeneratedRegex(@"\(.*\)", RegexOptions.CultureInvariant)]
    private static partial Regex Parentheses();

    [GeneratedRegex(@"_+", RegexOptions.CultureInvariant)]
    private static partial Regex Underscores();

    [GeneratedRegex(@"O(data|DATA|Data)S(ervice|ERVICE)F(or|OR)", RegexOptions.CultureInvariant)]
    private static partial Regex ODataServiceFor();

    [GeneratedRegex(@"R(emote|EMOTE)A(pi|PI)F(or|OR)", RegexOptions.CultureInvariant)]
    private static partial Regex RemoteApiFor();

    [GeneratedRegex(@"A(pi|PI)F(or|OR)", RegexOptions.CultureInvariant)]
    private static partial Regex ApiFor();

    [GeneratedRegex(@"A(pi|PI)", RegexOptions.CultureInvariant)]
    private static partial Regex Api();

    [GeneratedRegex(@"S(ervice|ERVICE)$", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceSuffix();

    /// <summary>Whether <paramref name="name"/> is a reserved C# keyword (case sensitive).</summary>
    internal static bool IsKeyword(string name) => Keywords.Contains(name);

    /// <summary>The title of a service: the API name with spaces instead of underscores.</summary>
    internal static string ApiNameToServiceTitle(string apiName) => apiName.Replace('_', ' ');

    /// <summary>Derives a PascalCase base class name from a service title or name, for example <c>Business Partner</c> to <c>BusinessPartner</c>.</summary>
    internal static string ServiceNameToBaseClassName(string serviceName)
    {
        var formatted = serviceName.Trim().Replace("/", "Or", StringComparison.Ordinal).Replace("&", "And", StringComparison.Ordinal);
        formatted = Parentheses().Replace(formatted, "");
        if (formatted.Length == 0)
        {
            return formatted;
        }
        formatted = PrependValidCharacterIfNecessary(formatted, 'A');
        formatted = ReplaceInvalidIdentifierCharacters(formatted, "_");
        formatted = Underscores().Replace(formatted, "_");

        // To keep the CamelCase portions of the name as is we need to format them as snake_case first ...
        formatted = CaseFormats.Convert(CaseFormat.UpperCamel, CaseFormat.LowerUnderscore, formatted);
        // ... and then format this together with the replacements made above back to CamelCase.
        formatted = CaseFormats.Convert(CaseFormat.LowerUnderscore, CaseFormat.UpperCamel, formatted);

        formatted = ODataServiceFor().Replace(formatted, "");
        formatted = RemoteApiFor().Replace(formatted, "");
        formatted = ApiFor().Replace(formatted, "");
        formatted = Api().Replace(formatted, "");
        return ServiceSuffix().Replace(formatted, "");
    }

    private static string PrependValidCharacterIfNecessary(string text, char prefix) =>
        IsIdentifierStart(text[0]) ? text : prefix + text;

    internal static bool IsIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_' || char.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber;

    internal static bool IsIdentifierPart(char c)
    {
        if (IsIdentifierStart(c))
        {
            return true;
        }
        return char.GetUnicodeCategory(c) is UnicodeCategory.DecimalDigitNumber or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.Format;
    }

    /// <summary>
    /// Removes characters that are not valid in C# identifiers, or replaces them. Leading characters that cannot start an
    /// identifier are dropped.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    /// <param name="partReplacement">Replacement for invalid characters inside the name; <see langword="null"/> removes them.</param>
    internal static string ReplaceInvalidIdentifierCharacters(string text, string? partReplacement)
    {
        var result = new StringBuilder();
        var index = 0;
        while (result.Length == 0 && index < text.Length)
        {
            var c = text[index];
            if (IsIdentifierStart(c))
            {
                result.Append(c);
            }
            index++;
        }
        for (var i = index; i < text.Length; i++)
        {
            var c = text[i];
            if (IsIdentifierPart(c))
            {
                result.Append(c);
            }
            else if (partReplacement is not null)
            {
                result.Append(partReplacement);
            }
        }
        return result.ToString();
    }

    /// <summary>Capitalizes the first character of every whitespace separated word.</summary>
    internal static string Capitalize(string words) => ModifyFirstCharacterAfterMatch(words, char.ToUpperInvariant);

    /// <summary>Lower-cases the first character of every whitespace separated word.</summary>
    internal static string Uncapitalize(string words) => ModifyFirstCharacterAfterMatch(words, char.ToLowerInvariant);

    private static string ModifyFirstCharacterAfterMatch(string words, Func<char, char> modify)
    {
        if (words.Length == 0)
        {
            return words;
        }
        var result = words.ToCharArray();
        foreach (Match match in WordStart().Matches(words))
        {
            var nextEnd = match.Index + match.Length;
            if (nextEnd >= words.Length)
            {
                // we reached the end of the string
                break;
            }
            result[nextEnd] = modify(result[nextEnd]);
        }
        return new string(result);
    }

    /// <summary>Lower-cases a leading acronym (<c>URLAddress</c> to <c>urlAddress</c>) or else the first character.</summary>
    internal static string UncapitalizeLeadingAcronym(string name)
    {
        var match = LeadingAcronym().Match(name);
        if (match.Success)
        {
            return match.Value.ToLowerInvariant() + name[match.Length..];
        }
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Upper-cases the first character.</summary>
    internal static string CapitalizeFirst(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>Lower-cases the first character.</summary>
    internal static string UncapitalizeFirst(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>A parameter name for a PascalCase member name: camelCase, never a keyword.</summary>
    internal static string ToParameterName(string memberName)
    {
        var name = UncapitalizeFirst(memberName);
        return IsKeyword(name) ? name + "Parameter" : name;
    }

    /// <summary>Removes <paramref name="prefix"/> ignoring case.</summary>
    internal static string RemoveStartIgnoreCase(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? text[prefix.Length..] : text;

    /// <summary>Removes <paramref name="suffix"/> ignoring case.</summary>
    internal static string RemoveEndIgnoreCase(string text, string suffix) =>
        text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? text[..^suffix.Length] : text;
}
