namespace Sap.Cloud.Sdk.Result;

/// <summary>Specifies the name of the result element a field or property is read from.</summary>
/// <remarks>Port of the <c>ElementName</c> annotation; honored by <see cref="ElementNameJsonTypeInfoResolver"/>.</remarks>
/// <param name="value">The element name.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ElementNameAttribute(string value) : Attribute
{
    /// <summary>The element name.</summary>
    public string Value { get; } = value;
}
