namespace Sap.Cloud.Sdk.DataModel.OData.Client;

/// <summary>A path of property names into a JSON document. The node <c>*</c> matches the first property.</summary>
public sealed record JsonPath
{
    /// <summary>Wildcard node that matches the first property of an object.</summary>
    public const string Wildcard = "*";

    private JsonPath(IReadOnlyList<string> nodes)
    {
        Nodes = nodes;
    }

    /// <summary>The nodes of the path.</summary>
    public IReadOnlyList<string> Nodes { get; }

    /// <summary>Creates a path.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The path.</returns>
    public static JsonPath Of(params string[] nodes) => new(nodes.ToArray());

    /// <summary>Creates the path that points to the document root.</summary>
    /// <returns>The path.</returns>
    public static JsonPath OfRoot() => new(Array.Empty<string>());

    /// <inheritdoc />
    public bool Equals(JsonPath? other) => other is not null && Nodes.SequenceEqual(other.Nodes);

    /// <inheritdoc />
    public override int GetHashCode() => Nodes.Aggregate(17, (h, n) => HashCode.Combine(h, n));

    /// <inheritdoc />
    public override string ToString() => $"JsonPath(Nodes=[{string.Join(", ", Nodes)}])";
}

/// <summary>An ordered list of alternative <see cref="JsonPath"/>s; the first that resolves wins.</summary>
public sealed record JsonLookup
{
    private JsonLookup(IReadOnlyList<JsonPath> paths)
    {
        Paths = paths;
    }

    /// <summary>The alternative paths.</summary>
    public IReadOnlyList<JsonPath> Paths { get; }

    /// <summary>Creates a lookup.</summary>
    /// <param name="paths">The alternative paths.</param>
    /// <returns>The lookup.</returns>
    public static JsonLookup Of(params JsonPath[] paths) => new(paths.ToArray());

    /// <summary>Creates a lookup without any path.</summary>
    /// <returns>The lookup.</returns>
    public static JsonLookup Empty() => new(Array.Empty<JsonPath>());

    /// <inheritdoc />
    public bool Equals(JsonLookup? other) => other is not null && Paths.SequenceEqual(other.Paths);

    /// <inheritdoc />
    public override int GetHashCode() => Paths.Aggregate(17, (h, p) => HashCode.Combine(h, p));
}
