namespace Sap.Cloud.Sdk.DataModel.OData.Client;

/// <summary>Serializes primitive values to OData URL literals.</summary>
/// <remarks>Port of <c>ODataLiteralSerializer</c>. Java <c>Number</c> is represented by a boxed numeric value.</remarks>
public interface IODataLiteralSerializer
{
    /// <summary>Serializes a boxed number (<see cref="byte"/>, <see cref="sbyte"/>, <see cref="short"/>, <see cref="int"/>, <see cref="long"/>, <see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>).</summary>
    Func<object, string> NumberSerializer { get; }

    /// <summary>Serializes a <see cref="Guid"/> (Java <c>UUID</c>).</summary>
    Func<Guid, string> UuidSerializer { get; }

    /// <summary>Serializes a <see cref="DateTimeOffset"/> (Java <c>OffsetDateTime</c>).</summary>
    Func<DateTimeOffset, string> DateTimeOffsetSerializer { get; }

    /// <summary>Serializes a <see cref="TimeOnly"/> (Java <c>LocalTime</c>).</summary>
    Func<TimeOnly, string> TimeOfDaySerializer { get; }

    /// <summary>Serializes a <see cref="DateTime"/> (Java <c>LocalDateTime</c>).</summary>
    Func<DateTime, string> DateTimeSerializer { get; }
}

/// <summary>Describes where the parts of an OData response payload are located.</summary>
/// <remarks>Port of <c>ODataResponseDescriptor</c>.</remarks>
public interface IODataResponseDescriptor
{
    /// <summary>Location of the result set (array of entities).</summary>
    JsonLookup PathToResultSet { get; }

    /// <summary>Location of a single entity.</summary>
    JsonLookup PathToResultSingle { get; }

    /// <summary>Location of a primitive result.</summary>
    JsonLookup PathToResultPrimitive { get; }

    /// <summary>Location of the inline count.</summary>
    JsonLookup PathToInlineCount { get; }

    /// <summary>Location of the next link.</summary>
    JsonLookup PathToNextLink { get; }

    /// <summary>Location of the delta link.</summary>
    JsonLookup PathToDeltaLink { get; }
}

/// <summary>An OData protocol version (V2 or V4).</summary>
/// <remarks>Port of <c>ODataProtocol</c>; the constants <c>V2</c> and <c>V4</c> are on <see cref="ODataProtocol"/>.</remarks>
public interface IODataProtocol : IODataResponseDescriptor, IODataLiteralSerializer
{
    /// <summary>The protocol version, for example <c>"4.0"</c>.</summary>
    string ProtocolVersion { get; }

    /// <summary>Gets the query option (name and value) that requests the inline count.</summary>
    /// <param name="optionEnabled">Whether the inline count is requested.</param>
    /// <returns>The query option.</returns>
    KeyValuePair<string, string> GetQueryOptionInlineCount(bool optionEnabled);

    /// <summary>Whether a custom query parameter may be serialized for the given query.</summary>
    /// <param name="isRoot">Whether the query is the root query (not a nested expand query).</param>
    /// <param name="key">The parameter name.</param>
    /// <returns><see langword="true"/> if the parameter is allowed.</returns>
    bool AllowCustomQueryParameter(bool isRoot, string key) => isRoot;

    /// <summary>Whether the other protocol has the same version.</summary>
    /// <param name="otherProtocol">The other protocol.</param>
    /// <returns><see langword="true"/> if the versions are equal.</returns>
    bool IsEqualTo(IODataProtocol otherProtocol)
    {
        ArgumentNullException.ThrowIfNull(otherProtocol);
        return otherProtocol.ProtocolVersion == ProtocolVersion;
    }
}
