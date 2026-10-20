namespace Sap.Cloud.Sdk.DataModel.OData.Annotation;

/// <summary>Marks a property of a generated entity as part of the entity key.</summary>
/// <remarks>
/// Port of the <c>@Key</c> annotation (renamed to avoid a clash with <c>System.ComponentModel.DataAnnotations.KeyAttribute</c>).
/// Like in Java the attribute is documentation for tools. The runtime reads the key from the type metadata
/// (<c>VdmTypeMetadataBuilder{T}.Property</c> with <c>key: true</c>).
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ODataKeyAttribute : Attribute
{
}
