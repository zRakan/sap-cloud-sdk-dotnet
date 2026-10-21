using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>Naming helpers shared by the naming strategies and the generator.</summary>
/// <remarks>Port of <c>NamingUtils</c> (and the parts of Guava's <c>CaseFormat</c> that it relies on).</remarks>
internal static partial class NamingUtils
{
    private static readonly HashSet<string> s_keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
        "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>Tests whether the name is a reserved C# keyword.</summary>
    /// <param name="name">The identifier.</param>
    /// <returns><see langword="true"/> for a keyword.</returns>
    public static bool IsCSharpKeyword(string name) => s_keywords.Contains(name);

    /// <summary>Prefixes a keyword with <c>@</c> so it can be used as an identifier.</summary>
    /// <param name="name">The identifier.</param>
    /// <returns>The identifier, escaped if necessary.</returns>
    public static string EscapeIdentifier(string name) => IsCSharpKeyword(name) ? "@" + name : name;

    /// <summary>Converts the name of an API into the title of a service.</summary>
    /// <param name="apiName">The name (usually the file name).</param>
    /// <returns>The title.</returns>
    public static string ApiNameToServiceTitle(string apiName) => apiName.Replace('_', ' ');

    /// <summary>Converts a service title into the base name of the service classes.</summary>
    /// <param name="serviceName">The title.</param>
    /// <returns>The base name, for example <c>BusinessPartner</c>.</returns>
    public static string ServiceNameToBaseClassName(string serviceName)
    {
        var formatted = serviceName.Trim().Replace("/", "Or", StringComparison.Ordinal).Replace("&", "And", StringComparison.Ordinal);
        formatted = ParenthesesRegex().Replace(formatted, string.Empty);

        formatted = PrependValidCharacterIfNecessary(formatted, 'A');
        formatted = ReplaceInvalidCharacters(formatted, "_");
        formatted = RepeatedUnderscoreRegex().Replace(formatted, "_");

        // To keep the CamelCase portions of the name as is, format them as snake_case first, then back to CamelCase.
        formatted = UpperCamelToLowerUnderscore(formatted);
        formatted = LowerUnderscoreToUpperCamel(formatted);

        formatted = ODataServiceForRegex().Replace(formatted, string.Empty);
        formatted = RemoteApiForRegex().Replace(formatted, string.Empty);
        formatted = ApiForRegex().Replace(formatted, string.Empty);
        formatted = ApiRegex().Replace(formatted, string.Empty);
        formatted = ServiceSuffixRegex().Replace(formatted, string.Empty);
        return formatted;
    }

    /// <summary>Whether the character may start an identifier.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter or an underscore.</returns>
    public static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    /// <summary>Whether the character may be part of an identifier.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter, digit or underscore.</returns>
    public static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Replaces characters that cannot be part of a C# identifier.</summary>
    /// <param name="value">The text.</param>
    /// <param name="replacement">The replacement for invalid characters, or <see langword="null"/> to drop them.</param>
    /// <returns>The cleaned text, starting with a valid identifier start character.</returns>
    public static string ReplaceInvalidCharacters(string value, string? replacement)
    {
        var result = new StringBuilder();
        var index = 0;
        while (result.Length == 0 && index < value.Length)
        {
            var c = value[index];
            if (IsIdentifierStart(c))
            {
                result.Append(c);
            }

            index++;
        }

        for (var i = index; i < value.Length; i++)
        {
            var c = value[i];
            if (IsIdentifierPart(c))
            {
                result.Append(c);
            }
            else if (replacement is not null)
            {
                result.Append(replacement);
            }
        }

        return result.ToString();
    }

    /// <summary>Upper-cases the first letter of every whitespace separated word.</summary>
    /// <param name="words">The text.</param>
    /// <returns>The text.</returns>
    public static string Capitalize(string words) => ModifyFirstCharacterAfterMatch(words, char.ToUpperInvariant);

    /// <summary>Lower-cases the first letter of every whitespace separated word.</summary>
    /// <param name="words">The text.</param>
    /// <returns>The text.</returns>
    public static string Uncapitalize(string words) => ModifyFirstCharacterAfterMatch(words, char.ToLowerInvariant);

    /// <summary>Converts a PascalCase name into a camelCase name; a leading acronym becomes lower case (<c>ID</c> to <c>id</c>).</summary>
    /// <param name="name">The name.</param>
    /// <returns>The camelCase name.</returns>
    public static string ToCamelCase(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        var upperRun = 0;
        while (upperRun < name.Length && char.IsUpper(name[upperRun]))
        {
            upperRun++;
        }

        // "URLAddress" -> the run is "URLA" and the last upper case letter starts the next word.
        var lowerCount = upperRun == name.Length || upperRun == 1 || !char.IsLower(name[upperRun]) ? upperRun : upperRun - 1;
        return string.Concat(name.AsSpan(0, lowerCount).ToString().ToLowerInvariant(), name.AsSpan(lowerCount));
    }

    /// <summary>The first lower case letter of the name is kept; see <see cref="ToCamelCase"/>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The name with an upper case first letter.</returns>
    public static string ToPascalCase(string name) =>
        name.Length == 0 ? name : string.Concat(char.ToUpperInvariant(name[0]).ToString(), name.AsSpan(1));

    /// <summary>Guava's <c>CaseFormat.UPPER_CAMEL.to(LOWER_UNDERSCORE, ...)</c>.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The converted text.</returns>
    public static string UpperCamelToLowerUnderscore(string value) =>
        ConvertCase(value, c => c is >= 'A' and <= 'Z', 0, "_", Lower, Lower);

    /// <summary>Guava's <c>CaseFormat.LOWER_UNDERSCORE.to(UPPER_CAMEL, ...)</c>.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The converted text.</returns>
    public static string LowerUnderscoreToUpperCamel(string value) =>
        ConvertCase(value, c => c == '_', 1, string.Empty, FirstCharOnlyToUpper, FirstCharOnlyToUpper);

    /// <summary>Appends <paramref name="suffix"/> if the name is a C# keyword.</summary>
    /// <param name="name">The identifier.</param>
    /// <param name="suffix">The suffix.</param>
    /// <returns>The identifier.</returns>
    public static string AppendSuffixIfKeyword(string name, string suffix) => IsCSharpKeyword(name) ? name + suffix : name;

    // service method names, as in NamingUtils.formatServiceMethodName
    public static string DeriveGetByKeyMethodName(string entityName) => "Get" + ToPascalCase(entityName) + "ByKey";

    public static string DeriveGetAllMethodName(string entityName) => "GetAll" + ToPascalCase(entityName);

    public static string DeriveCountMethodName(string entityName) => "Count" + ToPascalCase(entityName);

    public static string DeriveCreateMethodName(string entityName) => "Create" + ToPascalCase(entityName);

    public static string DeriveUpdateMethodName(string entityName) => "Update" + ToPascalCase(entityName);

    public static string DeriveDeleteMethodName(string entityName) => "Delete" + ToPascalCase(entityName);

    private static string PrependValidCharacterIfNecessary(string value, char prefix) =>
        value.Length == 0 || IsIdentifierStart(value[0]) ? value : prefix + value;

    private static string Lower(string word) => word.ToLowerInvariant();

    private static string FirstCharOnlyToUpper(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    private static string ConvertCase(
        string value,
        Func<char, bool> isBoundary,
        int separatorLength,
        string targetSeparator,
        Func<string, string> normalizeFirstWord,
        Func<string, string> normalizeWord)
    {
        var i = 0;
        var j = -1;
        StringBuilder? output = null;
        while (true)
        {
            j++;
            var next = -1;
            for (var k = j; k < value.Length; k++)
            {
                if (isBoundary(value[k]))
                {
                    next = k;
                    break;
                }
            }

            if (next == -1)
            {
                break;
            }

            j = next;
            if (i == 0)
            {
                output = new StringBuilder();
                output.Append(normalizeFirstWord(value[i..j]));
            }
            else
            {
                output!.Append(normalizeWord(value[i..j]));
            }

            output.Append(targetSeparator);
            i = j + separatorLength;
        }

        return i == 0 ? normalizeFirstWord(value) : output!.Append(normalizeWord(value[i..])).ToString();
    }

    private static string ModifyFirstCharacterAfterMatch(string words, Func<char, char> modify)
    {
        if (words.Length == 0)
        {
            return words;
        }

        var result = new StringBuilder(words);
        foreach (var match in LeadingOrWhitespaceRegex().Matches(words).Cast<Match>())
        {
            var end = match.Index + match.Length;
            if (end >= words.Length)
            {
                break;
            }

            result[end] = modify(result[end]);
        }

        return result.ToString();
    }

    [GeneratedRegex(@"\(.*\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ParenthesesRegex();

    [GeneratedRegex("_+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RepeatedUnderscoreRegex();

    [GeneratedRegex("O(data|DATA|Data)S(ervice|ERVICE)F(or|OR)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ODataServiceForRegex();

    [GeneratedRegex("R(emote|EMOTE)A(pi|PI)F(or|OR)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RemoteApiForRegex();

    [GeneratedRegex("A(pi|PI)F(or|OR)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ApiForRegex();

    [GeneratedRegex("A(pi|PI)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ApiRegex();

    [GeneratedRegex("S(ervice|ERVICE)$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ServiceSuffixRegex();

    [GeneratedRegex(@"^\s*|\s+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LeadingOrWhitespaceRegex();
}
