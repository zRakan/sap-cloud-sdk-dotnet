namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>The source of the names of generated classes, properties and methods.</summary>
/// <remarks>Port of Java's <c>NameSource</c> (<c>NAME</c> and <c>LABEL</c>).</remarks>
public enum NameSource
{
    /// <summary>Use the OData name (<c>Name</c> attribute).</summary>
    Name,

    /// <summary>Use the <c>sap:label</c> if there is one, otherwise the OData name.</summary>
    Label,
}
