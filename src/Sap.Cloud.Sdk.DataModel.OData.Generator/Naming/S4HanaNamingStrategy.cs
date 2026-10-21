namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// The default naming strategy: removes the prefixes and suffixes that SAP S/4HANA services add to entity and property names
/// (<c>A_BusinessPartnerType</c> becomes <c>BusinessPartner</c>, <c>to_Address</c> becomes <c>Address</c>), keeps the case of
/// the remaining name and appends a suffix if the result would be a keyword.
/// </summary>
/// <remarks>
/// <para>Port of <c>S4HanaNamingStrategy</c>. Deviations: the results are PascalCase; navigation properties do not get the Java prefix <c>to</c>
/// (the accessors are <c>FetchAddressAsync</c>, not <c>fetchToAddress</c>); the quirk that removed a leading <c>to</c> from method names
/// is not ported.</para>
/// </remarks>
public sealed class S4HanaNamingStrategy : NamingStrategyBase
{
    private static readonly string[] EntityNamePrefixesToRemove =
        ["A_", "C_", "D_", "E_", "I_", "P_", "R_", "S_", "to_", "To_", "X_", "YY1_", "Z_"];

    private static readonly string[] PropertyNamePrefixesToRemove = ["SAP_", "to_", "To_", "YY1_"];

    private static readonly string[] ClassNameSuffixesToRemove = ["Type", "_"];

    /// <summary>Initializes a new instance of the <see cref="S4HanaNamingStrategy"/> class using <see cref="NameSource.Label"/>.</summary>
    public S4HanaNamingStrategy()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S4HanaNamingStrategy"/> class.</summary>
    /// <param name="nameSource">The source of the names.</param>
    public S4HanaNamingStrategy(NameSource nameSource)
        : base(nameSource)
    {
    }

    /// <inheritdoc />
    public override string GenerateClassName(string name, string? label)
    {
        var className = GenerateNameFromEntity(name, label);
        className = RemoveAllSuffixes(className, ClassNameSuffixesToRemove);
        className = AppendSuffixIfNameIsReservedKeyword(className, "Entity");
        ThrowIfConversionResultIsNullOrEmpty(name, label, className, "class name");
        return className;
    }

    /// <inheritdoc />
    public override string GeneratePropertyName(string name, string? label)
    {
        var propertyName = GenerateNameFromProperty(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, propertyName, "property name");
        return propertyName;
    }

    /// <inheritdoc />
    public override string GenerateNavigationPropertyName(string name)
    {
        var propertyName = GenerateNameFromProperty(name, null);
        ThrowIfConversionResultIsNullOrEmpty(name, null, propertyName, "navigation property name");
        return propertyName;
    }

    /// <inheritdoc />
    public override string GenerateOperationMethodName(string name, string? label)
    {
        var methodName = GenerateNameFromEntity(name, label);
        ThrowIfConversionResultIsNullOrEmpty(name, label, methodName, "function import method name");
        return methodName;
    }

    /// <inheritdoc />
    public override string GenerateParameterName(string name, string? label)
    {
        var parameterName = NamingUtils.UncapitalizeFirst(GenerateNameFromProperty(name, label));
        if (IsReservedKeyword(parameterName))
        {
            parameterName += "Parameter";
        }
        ThrowIfConversionResultIsNullOrEmpty(name, label, parameterName, "method parameter name");
        return parameterName;
    }

    /// <inheritdoc />
    public override string GenerateFluentHelperClassName(string name, string? label)
    {
        var className = GenerateNameFromEntity(name, label);
        className = RemoveAllSuffixes(className, ClassNameSuffixesToRemove);
        className += "FluentHelper";
        ThrowIfConversionResultIsNullOrEmpty(name, label, className, "fluent helper class name");
        return className;
    }

    private string GenerateNameFromEntity(string name, string? label)
    {
        string result;
        if (NameSource == NameSource.Label && !string.IsNullOrWhiteSpace(label))
        {
            result = RemoveWhiteSpaces(label);
        }
        else
        {
            result = RemoveFirstPrefix(name, EntityNamePrefixesToRemove);
        }
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                $"Could not create a valid identifier based on the entity name '{name}' and the entity label '{label}'. Name source was '{NameSource}'");
        }
        result = RemoveInvalidCharacters(result);
        return NamingUtils.Capitalize(result);
    }

    private string GenerateNameFromProperty(string name, string? label)
    {
        string result;
        if (NameSource == NameSource.Label && !string.IsNullOrWhiteSpace(label))
        {
            result = RemoveWhiteSpaces(label);
        }
        else
        {
            result = RemoveFirstPrefix(name, PropertyNamePrefixesToRemove);
        }
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                $"Could not create a valid identifier based on the property name '{name}' and the property label '{label}'. Name source was '{NameSource}'");
        }
        result = RemoveInvalidCharacters(result);
        return NamingUtils.Capitalize(result);
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
            formatted = NamingUtils.RemoveEndIgnoreCase(formatted, suffix);
        }
        return formatted;
    }
}
