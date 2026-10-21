namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>The source from which a naming strategy derives C# names.</summary>
/// <remarks>
/// Port of <c>NameSource</c>. OData v4 has no <c>sap:label</c>; the label is the <c>Common.Label</c> annotation.
/// </remarks>
public enum NameSource
{
    /// <summary>Use the name of the EDM element.</summary>
    Name,

    /// <summary>Use the label (<c>Common.Label</c>) of the EDM element; fall back to the name if there is none.</summary>
    Label,
}

/// <summary>
/// Converts EDM names (and labels) into C# identifiers.
/// </summary>
/// <remarks>
/// Port of <c>NamingStrategy</c>. The C# members are PascalCase, so Java's field/constant/builder/method variants
/// collapse into <see cref="GeneratePropertyName"/> and <see cref="GenerateNavigationPropertyName"/> (the navigation
/// accessors are named after the navigation property, without Java's <c>to</c> prefix). A strategy returns names that
/// are valid on their own; the generator resolves clashes between names with <c>_2</c>, <c>_3</c>, ... suffixes.
/// </remarks>
public interface INamingStrategy
{
    /// <summary>Gets or sets the source of the names. The generator sets it from the generator options.</summary>
    NameSource NameSource { get; set; }

    /// <summary>Tests whether <paramref name="name"/> is a C# keyword.</summary>
    /// <param name="name">The identifier.</param>
    /// <returns><see langword="true"/> for a reserved keyword.</returns>
    bool IsReservedKeyword(string name);

    /// <summary>Converts the name of an entity type into a class name.</summary>
    /// <param name="name">The EDM name.</param>
    /// <param name="label">The label, if any.</param>
    /// <returns>The class name.</returns>
    string GenerateClassName(string name, string? label);

    /// <summary>Converts the name of a structural property (or key property) into a property name.</summary>
    /// <param name="name">The EDM name.</param>
    /// <param name="label">The label, if any.</param>
    /// <returns>The property name.</returns>
    string GeneratePropertyName(string name, string? label);

    /// <summary>Converts the name of a navigation property into the base name of its members.</summary>
    /// <param name="name">The EDM name.</param>
    /// <returns>The navigation property name.</returns>
    string GenerateNavigationPropertyName(string name);

    /// <summary>Converts the name of an enumeration member into a static field name.</summary>
    /// <param name="name">The EDM name.</param>
    /// <returns>The member name.</returns>
    string GenerateEnumMemberName(string name);

    /// <summary>Converts the name of a function, action or import into a method name.</summary>
    /// <param name="name">The EDM name.</param>
    /// <param name="label">The label, if any.</param>
    /// <returns>The method name.</returns>
    string GenerateOperationMethodName(string name, string? label);

    /// <summary>Converts the name of an operation parameter into a (camelCase) parameter name.</summary>
    /// <param name="name">The EDM name.</param>
    /// <param name="label">The label, if any.</param>
    /// <returns>The parameter name.</returns>
    string GenerateParameterName(string name, string? label);
}
