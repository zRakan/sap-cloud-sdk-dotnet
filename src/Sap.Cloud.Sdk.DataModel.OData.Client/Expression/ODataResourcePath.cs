using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A resource path: a sequence of segments, each optionally with parameters (for example an entity key).</summary>
public sealed class ODataResourcePath : IEquatable<ODataResourcePath>
{
    private readonly List<(string Segment, AbstractODataParameters? Parameters)> _segments = [];

    /// <summary>The segments and their parameters.</summary>
    public IReadOnlyList<(string Segment, AbstractODataParameters? Parameters)> Segments => _segments;

    /// <summary>Creates a path with one segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns>The path.</returns>
    public static ODataResourcePath Of(string segment) => new ODataResourcePath().AddSegment(segment);

    /// <summary>Creates a path with one segment and parameters.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="segmentParameter">The parameters of the segment.</param>
    /// <returns>The path.</returns>
    public static ODataResourcePath Of(string segment, AbstractODataParameters segmentParameter) =>
        new ODataResourcePath().AddSegment(segment, segmentParameter);

    /// <summary>Appends a segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns>This path.</returns>
    public ODataResourcePath AddSegment(string segment) => AddSegment(segment, null);

    /// <summary>Appends a segment with parameters.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="parameters">The parameters of the segment.</param>
    /// <returns>This path.</returns>
    public ODataResourcePath AddSegment(string segment, AbstractODataParameters? parameters)
    {
        ArgumentNullException.ThrowIfNull(segment);
        _segments.Add((segment, parameters));
        return this;
    }

    /// <summary>Sets the parameters of the last segment.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>This path.</returns>
    /// <exception cref="InvalidOperationException">The path is empty or the last segment already has parameters.</exception>
    public ODataResourcePath AddParameterToLastSegment(AbstractODataParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (_segments.Count == 0)
        {
            throw new InvalidOperationException("Cannot add parameter to the last segment because the current path is empty.");
        }
        var lastSegment = _segments[^1];
        if (lastSegment.Parameters is not null)
        {
            throw new InvalidOperationException(
                $"Cannot add parameter for path segment \"{lastSegment.Parameters}\". The segment already contains a parameter expression.");
        }
        _segments[^1] = (lastSegment.Segment, parameters);
        return this;
    }

    /// <summary>Serializes the path percent-encoded.</summary>
    /// <returns>The path, starting with <c>/</c>.</returns>
    public string ToEncodedPathString() => ToEncodedPathString(UriEncodingStrategy.Regular);

    /// <summary>Serializes the path percent-encoded with the given strategy.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>The path, starting with <c>/</c>.</returns>
    public string ToEncodedPathString(UriEncodingStrategy strategy) =>
        "/" + string.Join("/", _segments.Select(t => strategy.EscapePath(t.Segment) + (t.Parameters?.ToEncodedString(strategy) ?? string.Empty)));

    /// <summary>Creates a copy of this path.</summary>
    /// <returns>The copy.</returns>
    public ODataResourcePath Copy()
    {
        var copy = new ODataResourcePath();
        foreach (var (segment, parameters) in _segments)
        {
            copy.AddSegment(segment, parameters);
        }
        return copy;
    }

    /// <inheritdoc />
    public bool Equals(ODataResourcePath? other) => other is not null && ToString() == other.ToString();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ODataResourcePath);

    /// <inheritdoc />
    public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() =>
        "/" + string.Join("/", _segments.Select(t => t.Segment + (t.Parameters?.ToString() ?? string.Empty)));
}
