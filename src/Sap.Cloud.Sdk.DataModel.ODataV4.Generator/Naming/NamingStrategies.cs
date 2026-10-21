using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>
/// Base class of the naming strategies: converts a name or label into a syntactically valid C# identifier.
/// </summary>
/// <remarks>Port of <c>AbstractNamingStrategy</c>.</remarks>
public abstract partial class NamingStrategyBase : INamingStrategy
{
    /// <summary>Initializes a new instance of the <see cref="NamingStrategyBase"/> class.</summary>
    /// <param name="nameSource">The source of the names.</param>
    protected NamingStrategyBase(NameSource nameSource)
    {
        NameSource = nameSource;
    }

    /// <inheritdoc />
    public NameSource NameSource { get; set; }

    /// <inheritdoc />
    public virtual bool IsReservedKeyword(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return NamingUtils.IsCSharpKeyword(name);
    }

    /// <inheritdoc />
    public virtual string GenerateClassName(string name, string? label)
    {
        var result = ConvertToPascalCase(name, label);
        ThrowIfEmpty(name, label, result, "class name");
        ThrowIfReserved(name, label, result, "class name");
        return result;
    }

    /// <inheritdoc />
    public virtual string GeneratePropertyName(string name, string? label)
    {
        var result = ConvertToPascalCase(name, label);
        ThrowIfEmpty(name, label, result, "property name");
        ThrowIfReserved(name, label, result, "property name");
        return result;
    }

    /// <inheritdoc />
    public virtual string GenerateNavigationPropertyName(string name)
    {
        var result = ConvertToPascalCase(name, null);
        ThrowIfEmpty(name, null, result, "navigation property name");
        ThrowIfReserved(name, null, result, "navigation property name");
        return result;
    }

    /// <inheritdoc />
    public virtual string GenerateEnumMemberName(string name)
    {
        var result = ConvertToPascalCase(name, null);
        ThrowIfEmpty(name, null, result, "enum member name");
        ThrowIfReserved(name, null, result, "enum member name");
        return result;
    }

    /// <inheritdoc />
    public virtual string GenerateOperationMethodName(string name, string? label)
    {
        var result = ConvertToPascalCase(name, label);
        ThrowIfEmpty(name, label, result, "operation method name");
        ThrowIfReserved(name, label, result, "operation method name");
        return result;
    }

    /// <inheritdoc />
    public virtual string GenerateParameterName(string name, string? label)
    {
        var result = NamingUtils.ToCamelCase(ConvertToPascalCase(name, label));
        ThrowIfEmpty(name, label, result, "parameter name");
        ThrowIfReserved(name, label, result, "parameter name");
        return result;
    }

    /// <summary>Picks the label if the name source is <see cref="NameSource.Label"/> and a label exists.</summary>
    /// <param name="name">The name.</param>
    /// <param name="label">The label.</param>
    /// <returns>The text to convert.</returns>
    protected string ChooseBetweenNameAndLabel(string name, string? label) =>
        string.IsNullOrWhiteSpace(label) || NameSource == NameSource.Name ? name : label;

    /// <summary>Removes whitespace and capitalizes the words that were separated by it.</summary>
    /// <param name="name">The text.</param>
    /// <returns>The text without whitespace.</returns>
    protected static string RemoveWhiteSpaces(string name) =>
        string.Concat(WhitespaceRegex().Split(name.Trim()).Select(NamingUtils.Capitalize));

    /// <summary>Drops characters that cannot be part of an identifier.</summary>
    /// <param name="name">The text.</param>
    /// <returns>The cleaned text.</returns>
    protected static string RemoveInvalidCharacters(string name) => NamingUtils.ReplaceInvalidCharacters(name, null);

    /// <summary>Lower-cases a leading acronym (<c>URLAddress</c> to <c>urlAddress</c>) or the first letter.</summary>
    /// <param name="name">The text.</param>
    /// <returns>The text.</returns>
    protected static string UncapitalizeLeadingAcronym(string name)
    {
        var match = LeadingAcronymRegex().Match(name);
        if (match.Success)
        {
            return string.Concat(match.Value.ToLowerInvariant(), name.AsSpan(match.Length));
        }

        return name.Length == 0 ? name : string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    /// <summary>Appends <paramref name="suffix"/> if the name is a reserved keyword.</summary>
    /// <param name="name">The identifier.</param>
    /// <param name="suffix">The suffix.</param>
    /// <returns>The identifier.</returns>
    protected string AppendSuffixIfNameIsReservedKeyword(string name, string suffix) =>
        IsReservedKeyword(name) ? name + suffix : name;

    /// <summary>Fails if the conversion produced an empty string.</summary>
    /// <param name="name">The input name.</param>
    /// <param name="label">The input label.</param>
    /// <param name="output">The conversion result.</param>
    /// <param name="target">What was generated.</param>
    protected void ThrowIfEmpty(string name, string? label, string? output, string target)
    {
        if (!string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        throw new InvalidOperationException(
            $"The conversion of the name \"{name}\"{LabelPortion(label)} to a {target} resulted in an empty string.");
    }

    /// <summary>Fails if the conversion produced a reserved keyword.</summary>
    /// <param name="name">The input name.</param>
    /// <param name="label">The input label.</param>
    /// <param name="output">The conversion result.</param>
    /// <param name="target">What was generated.</param>
    protected void ThrowIfReserved(string name, string? label, string output, string target)
    {
        if (!IsReservedKeyword(output))
        {
            return;
        }

        throw new InvalidOperationException(
            $"The conversion of the name \"{name}\"{LabelPortion(label)} to a {target} resulted in \"{output}\", which is a reserved keyword.");
    }

    private string ConvertToPascalCase(string name, string? label)
    {
        var result = UncapitalizeLeadingAcronym(RemoveInvalidCharacters(RemoveWhiteSpaces(ChooseBetweenNameAndLabel(name, label))));
        return NamingUtils.Capitalize(result);
    }

    private string LabelPortion(string? label) =>
        label is null ? string.Empty : $" or the label \"{label}\" (using {nameof(NameSource)} \"{NameSource}\")";

    [GeneratedRegex(@"\s", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("^[A-Z]+(?=[A-Z])", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LeadingAcronymRegex();
}

/// <summary>
/// Generates syntactically correct C# names without doing any additional modifications.
/// </summary>
/// <remarks>Port of <c>SimpleNamingStrategy</c>: it throws <see cref="InvalidOperationException"/> if a name cannot be converted.</remarks>
public sealed class SimpleNamingStrategy : NamingStrategyBase
{
    /// <summary>Initializes a new instance of the <see cref="SimpleNamingStrategy"/> class with <see cref="NameSource.Label"/>.</summary>
    public SimpleNamingStrategy()
        : this(NameSource.Label)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimpleNamingStrategy"/> class.</summary>
    /// <param name="nameSource">The source of the names.</param>
    public SimpleNamingStrategy(NameSource nameSource)
        : base(nameSource)
    {
    }
}

/// <summary>
/// The default naming strategy. It removes the technical prefixes and suffixes of S/4HANA names (<c>A_</c>, <c>I_</c>,
/// <c>to_</c>, <c>SAP_</c>, <c>Type</c>, ...) and avoids C# keywords by appending a suffix.
/// </summary>
/// <remarks>
/// Port of <c>S4HanaNamingStrategy</c>, Java's default. Differences: names are PascalCase (camelCase for parameters);
/// navigation properties do not get a <c>to</c> prefix; PascalCase names never clash with C# keywords, so only
/// parameter names are suffixed (<c>Parameter</c>).
/// </remarks>
public sealed class DefaultNamingStrategy : NamingStrategyBase
{
    private static readonly string[] s_entityNamePrefixes =
        ["A_", "C_", "D_", "E_", "I_", "P_", "R_", "S_", "to_", "To_", "X_", "YY1_", "Z_"];

    private static readonly string[] s_propertyNamePrefixes = ["SAP_", "to_", "To_", "YY1_"];

    private static readonly string[] s_classNameSuffixes = ["Type", "_"];

    /// <summary>Initializes a new instance of the <see cref="DefaultNamingStrategy"/> class with <see cref="NameSource.Label"/>.</summary>
    public DefaultNamingStrategy()
        : this(NameSource.Label)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DefaultNamingStrategy"/> class.</summary>
    /// <param name="nameSource">The source of the names.</param>
    public DefaultNamingStrategy(NameSource nameSource)
        : base(nameSource)
    {
    }

    /// <inheritdoc />
    public override string GenerateClassName(string name, string? label)
    {
        var result = RemoveAllSuffixes(GenerateNameFromEntity(name, label), s_classNameSuffixes);
        result = AppendSuffixIfNameIsReservedKeyword(result, "Entity");
        ThrowIfEmpty(name, label, result, "class name");
        return result;
    }

    /// <inheritdoc />
    public override string GeneratePropertyName(string name, string? label)
    {
        var result = AppendSuffixIfNameIsReservedKeyword(GenerateNameFromProperty(name, label), "Property");
        ThrowIfEmpty(name, label, result, "property name");
        return result;
    }

    /// <inheritdoc />
    public override string GenerateNavigationPropertyName(string name)
    {
        var result = AppendSuffixIfNameIsReservedKeyword(GenerateNameFromProperty(name, null), "Property");
        ThrowIfEmpty(name, null, result, "navigation property name");
        return result;
    }

    /// <inheritdoc />
    public override string GenerateEnumMemberName(string name)
    {
        var result = AppendSuffixIfNameIsReservedKeyword(GenerateNameFromProperty(name, null), "Member");
        ThrowIfEmpty(name, null, result, "enum member name");
        return result;
    }

    /// <inheritdoc />
    public override string GenerateOperationMethodName(string name, string? label)
    {
        var result = AppendSuffixIfNameIsReservedKeyword(GenerateNameFromEntity(name, label), "Function");
        ThrowIfEmpty(name, label, result, "operation method name");
        return result;
    }

    /// <inheritdoc />
    public override string GenerateParameterName(string name, string? label)
    {
        var result = NamingUtils.ToCamelCase(GenerateNameFromProperty(name, label));
        if (IsReservedKeyword(result))
        {
            result += "Parameter";
        }

        ThrowIfEmpty(name, label, result, "parameter name");
        return result;
    }

    private static string RemoveFirstPrefix(string name, string[] prefixes)
    {
        var formatted = name.Trim();
        foreach (var prefix in prefixes)
        {
            if (formatted.StartsWith(prefix, StringComparison.Ordinal))
            {
                return formatted[prefix.Length..];
            }
        }

        return formatted;
    }

    private static string RemoveAllSuffixes(string name, string[] suffixes)
    {
        var formatted = name;
        foreach (var suffix in suffixes)
        {
            if (formatted.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                formatted = formatted[..^suffix.Length];
            }
        }

        return formatted;
    }

    private string GenerateNameFromEntity(string name, string? label) =>
        GenerateName(name, label, s_entityNamePrefixes, "entity");

    private string GenerateNameFromProperty(string name, string? label) =>
        GenerateName(name, label, s_propertyNamePrefixes, "property");

    private string GenerateName(string name, string? label, string[] prefixes, string kind)
    {
        var result = NameSource == NameSource.Label && !string.IsNullOrWhiteSpace(label)
            ? RemoveWhiteSpaces(label)
            : RemoveFirstPrefix(name, prefixes);

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                $"Could not create a valid C# identifier based on the {kind} name '{name}' and the {kind} label '{label}'. Name source was '{NameSource}'");
        }

        return NamingUtils.Capitalize(RemoveInvalidCharacters(result));
    }
}
