namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// Converts the names and labels of OData entity types, properties and function imports into C# identifiers.
/// </summary>
/// <remarks>
/// <para>
/// Port of Java's <c>NamingStrategy</c>. The Java interface produced Java style names (<c>fieldName</c>, <c>FIELD_NAME</c>,
/// <c>getXyz</c>); the generated .NET code uses PascalCase for types, properties and methods and camelCase for parameters, so the
/// field name, constant name, method name and builder method name variants collapse to
/// <see cref="GeneratePropertyName"/> and <see cref="GenerateNavigationPropertyName"/>.
/// </para>
/// <para>
/// Implementations throw <see cref="InvalidOperationException"/> if no valid identifier can be created (Java: <c>IllegalStateException</c>).
/// Name clashes between the results are resolved by the generator, not by the strategy.
/// </para>
/// </remarks>
public interface INamingStrategy
{
    /// <summary>Gets or sets the source of the names. The generator sets it from <see cref="ODataGeneratorOptions.NameSource"/>.</summary>
    NameSource NameSource { get; set; }

    /// <summary>Determines whether <paramref name="name"/> is a C# keyword.</summary>
    /// <param name="name">The name to check.</param>
    /// <returns><see langword="true"/> for reserved keywords.</returns>
    bool IsReservedKeyword(string name);

    /// <summary>The name of the class of an entity type or complex type.</summary>
    /// <param name="name">The <c>Name</c> of the type.</param>
    /// <param name="label">The <c>sap:label</c> of the type, if any.</param>
    /// <returns>A PascalCase type name.</returns>
    string GenerateClassName(string name, string? label);

    /// <summary>The name of a property of an entity or complex type.</summary>
    /// <param name="name">The <c>Name</c> of the property.</param>
    /// <param name="label">The <c>sap:label</c> of the property, if any.</param>
    /// <returns>A PascalCase property name.</returns>
    string GeneratePropertyName(string name, string? label);

    /// <summary>The name of a navigation property; it also names the <c>Fetch…Async</c>, <c>Get…OrFetchAsync</c>, <c>Set…</c> and builder members.</summary>
    /// <param name="name">The <c>Name</c> of the navigation property.</param>
    /// <returns>A PascalCase name.</returns>
    string GenerateNavigationPropertyName(string name);

    /// <summary>The name of the service method that calls a function import.</summary>
    /// <param name="name">The <c>Name</c> of the function import.</param>
    /// <param name="label">The <c>sap:label</c> of the function import, if any.</param>
    /// <returns>A PascalCase method name.</returns>
    string GenerateOperationMethodName(string name, string? label);

    /// <summary>The name of a parameter of a function import.</summary>
    /// <param name="name">The <c>Name</c> of the parameter.</param>
    /// <param name="label">The <c>sap:label</c> of the parameter, if any.</param>
    /// <returns>A camelCase parameter name that is not a keyword.</returns>
    string GenerateParameterName(string name, string? label);

    /// <summary>The name of the fluent helper class of a function import.</summary>
    /// <param name="name">The <c>Name</c> of the function import.</param>
    /// <param name="label">The <c>sap:label</c> of the function import, if any.</param>
    /// <returns>A PascalCase type name ending in <c>FluentHelper</c>.</returns>
    string GenerateFluentHelperClassName(string name, string? label);
}
