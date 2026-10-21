namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// Converts names by removing invalid characters and applying PascalCase; keeps prefixes and suffixes such as <c>A_</c>
/// or <c>Type</c>.
/// </summary>
/// <remarks>Port of <c>SimpleNamingStrategy</c>.</remarks>
public class SimpleNamingStrategy : NamingStrategyBase
{
    /// <summary>Initializes a new instance of the <see cref="SimpleNamingStrategy"/> class using <see cref="NameSource.Label"/>.</summary>
    public SimpleNamingStrategy()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimpleNamingStrategy"/> class.</summary>
    /// <param name="nameSource">The source of the names.</param>
    public SimpleNamingStrategy(NameSource nameSource)
        : base(nameSource)
    {
    }
}
