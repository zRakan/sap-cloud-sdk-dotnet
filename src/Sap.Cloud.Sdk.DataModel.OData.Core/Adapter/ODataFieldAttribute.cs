namespace Sap.Cloud.Sdk.DataModel.OData.Adapter;

/// <summary>Declares the name of a property of a generated entity or complex type in the OData service.</summary>
/// <remarks>
/// Port of the <c>@ODataField</c> annotation (which also replaces Gson's <c>@SerializedName</c> and <c>@ElementName</c>).
/// The runtime reads the name from the type metadata; the attribute documents it for tools and is verified against the
/// metadata in the tests.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ODataFieldAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ODataFieldAttribute"/> class.</summary>
    /// <param name="odataName">The property name in the OData service.</param>
    public ODataFieldAttribute(string odataName)
    {
        ArgumentNullException.ThrowIfNull(odataName);
        ODataName = odataName;
    }

    /// <summary>The property name in the OData service.</summary>
    public string ODataName { get; }
}
