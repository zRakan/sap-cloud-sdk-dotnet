using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>Base class of the naming strategies: converts names and labels to PascalCase and camelCase identifiers.</summary>
/// <remarks>
/// Port of <c>AbstractNamingStrategy</c>. Deviations: the Java variants for field, constant, method and builder method names are
/// reduced to PascalCase property names; the fluent helper class name gets the suffix <c>FluentHelper</c> (Java's simple
/// strategy returned the plain class name, which clashes with the entity class when a function import is named like an entity).
/// </remarks>
public abstract partial class NamingStrategyBase : INamingStrategy
{
    /// <summary>Initializes a new instance of the <see cref="NamingStrategyBase"/> class using <see cref="Generator.NameSource.Label"/>.</summary>
    protected NamingStrategyBase()
        : this(Generator.NameSource.Label)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NamingStrategyBase"/> class.</summary>
    /// <param name="nameSource">The source of the names.</param>
    protected NamingStrategyBase(NameSource nameSource)
    {
        NameSource = nameSource;
    }

    /// <inheritdoc />
    public NameSource NameSource { get; set; }

    [GeneratedRegex(@"\s", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <inheritdoc />
    public virtual bool IsReservedKeyword(string name) => NamingUtils.IsKeyword(name);

    /// <inheritdoc />
    public virtual string GenerateClassName(string name, string? label)
    {
        var className = ConvertToClassName(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, className, "class name");
        ThrowIfConversionResultIsReservedKeyword(name, label, className, "class name");
        return className;
    }

    /// <inheritdoc />
    public virtual string GeneratePropertyName(string name, string? label)
    {
        var propertyName = ConvertToClassName(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, propertyName, "property name");
        ThrowIfConversionResultIsReservedKeyword(name, label, propertyName, "property name");
        return propertyName;
    }

    /// <inheritdoc />
    public virtual string GenerateNavigationPropertyName(string name)
    {
        var propertyName = ConvertToClassName(name, null);
        ThrowIfConversionResultIsNullOrEmpty(name, null, propertyName, "navigation property name");
        ThrowIfConversionResultIsReservedKeyword(name, null, propertyName, "navigation property name");
        return propertyName;
    }

    /// <inheritdoc />
    public virtual string GenerateOperationMethodName(string name, string? label)
    {
        var methodName = ConvertToClassName(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, methodName, "function import method name");
        ThrowIfConversionResultIsReservedKeyword(name, label, methodName, "function import method name");
        return methodName;
    }

    /// <inheritdoc />
    public virtual string GenerateParameterName(string name, string? label)
    {
        var parameterName = ConvertToParameterName(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, parameterName, "method parameter name");
        ThrowIfConversionResultIsReservedKeyword(name, label, parameterName, "method parameter name");
        return parameterName;
    }

    /// <inheritdoc />
    public virtual string GenerateFluentHelperClassName(string name, string? label)
    {
        var className = ConvertToClassName(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, className, "fluent helper class name");
        return className + "FluentHelper";
    }

    /// <summary>Converts a name or label to a PascalCase identifier.</summary>
    /// <param name="name">The OData name.</param>
    /// <param name="label">The label.</param>
    /// <returns>The identifier; may be empty if the input has no valid characters.</returns>
    protected virtual string ConvertToClassName(string name, string? label)
    {
        var result = ChooseBetweenNameAndLabel(name, label);
        result = RemoveWhiteSpaces(result);
        result = RemoveInvalidCharacters(result);
        result = UncapitalizeLeadingAcronym(result);
        return NamingUtils.Capitalize(result);
    }

    /// <summary>Converts a name or label to a camelCase identifier.</summary>
    /// <param name="name">The OData name.</param>
    /// <param name="label">The label.</param>
    /// <returns>The identifier; may be empty if the input has no valid characters.</returns>
    protected virtual string ConvertToParameterName(string name, string? label)
    {
        var result = ChooseBetweenNameAndLabel(name, label);
        result = RemoveWhiteSpaces(result);
        result = RemoveInvalidCharacters(result);
        result = UncapitalizeLeadingAcronym(result);
        return NamingUtils.Uncapitalize(result);
    }

    /// <summary>Returns the label if it is not blank and the name source is <see cref="Generator.NameSource.Label"/>, else the name.</summary>
    /// <param name="name">The OData name.</param>
    /// <param name="label">The label.</param>
    protected string ChooseBetweenNameAndLabel(string name, string? label) =>
        string.IsNullOrWhiteSpace(label) || NameSource == NameSource.Name ? name : label;

    /// <summary>Removes whitespace and capitalizes the words that are joined.</summary>
    /// <param name="name">The text.</param>
    protected static string RemoveWhiteSpaces(string name)
    {
        var result = new StringBuilder();
        foreach (var word in Whitespace().Split(name.Trim()))
        {
            result.Append(NamingUtils.Capitalize(word));
        }
        return result.ToString();
    }

    /// <summary>Removes the characters that are invalid in identifiers.</summary>
    /// <param name="name">The text.</param>
    protected static string RemoveInvalidCharacters(string name) => NamingUtils.ReplaceInvalidIdentifierCharacters(name, null);

    /// <summary>Lower-cases a leading acronym, for example <c>URLAddress</c> to <c>urlAddress</c>.</summary>
    /// <param name="name">The text.</param>
    protected static string UncapitalizeLeadingAcronym(string name) => NamingUtils.UncapitalizeLeadingAcronym(name);

    /// <summary>Appends <paramref name="suffix"/> if the lower-cased name is a keyword.</summary>
    /// <param name="name">The name.</param>
    /// <param name="suffix">The suffix.</param>
    protected string AppendSuffixIfNameIsReservedKeyword(string name, string suffix) =>
        IsReservedKeyword(name.ToLowerInvariant()) ? name + suffix : name;

    /// <summary>Throws <see cref="InvalidOperationException"/> if the result is empty.</summary>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputLabel">The input label.</param>
    /// <param name="output">The result of the conversion.</param>
    /// <param name="target">What was converted, for the message.</param>
    protected void ThrowIfConversionResultIsNullOrEmpty(string inputName, string? inputLabel, string? output, string target)
    {
        if (!string.IsNullOrWhiteSpace(output))
        {
            return;
        }
        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The conversion of the name \"{inputName}\"{LabelPortion(inputLabel)} to a {target} resulted in an empty string."));
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> if the result is a reserved keyword.</summary>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputLabel">The input label.</param>
    /// <param name="output">The result of the conversion.</param>
    /// <param name="target">What was converted, for the message.</param>
    protected void ThrowIfConversionResultIsReservedKeyword(string inputName, string? inputLabel, string output, string target)
    {
        if (!IsReservedKeyword(output))
        {
            return;
        }
        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The conversion of the name \"{inputName}\"{LabelPortion(inputLabel)} to a {target} resulted in \"{output}\", which is a reserved keyword."));
    }

    private string LabelPortion(string? label) =>
        label is null ? "" : string.Create(CultureInfo.InvariantCulture, $" or the label \"{label}\" (using {nameof(NameSource)} \"{NameSource}\")");
}
