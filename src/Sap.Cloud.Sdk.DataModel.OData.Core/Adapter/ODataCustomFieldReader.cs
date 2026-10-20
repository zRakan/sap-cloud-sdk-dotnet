using System.Text.Json;

namespace Sap.Cloud.Sdk.DataModel.OData.Adapter;

/// <summary>
/// Reads the value of a custom field (a property that is not part of the entity model) as plain .NET value.
/// </summary>
/// <remarks>
/// Port of <c>ODataCustomFieldAdapter</c>. Numbers become <see cref="int"/>, <see cref="long"/> or <see cref="double"/>;
/// strings of the form <c>/Date(ms)/</c> become a UTC <see cref="DateTimeOffset"/> (Java: <c>Calendar</c> in GMT); arrays become
/// <see cref="List{T}"/>, objects become <see cref="Dictionary{TKey, TValue}"/>. Within objects, <c>__metadata</c> is dropped and
/// <c>__deferred</c> turns the object into <see langword="null"/>; an object with a <c>results</c> member is replaced by it.
/// </remarks>
internal static class ODataCustomFieldReader
{
    /// <summary>Reads the value at the reader's current token and leaves the reader on its last token.</summary>
    internal static object? Read(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return ReadNumber(ref reader);
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
                var text = reader.GetString()!;
                if (!ODataWireFormats.DatePattern().IsMatch(text))
                {
                    return text;
                }
                // A date string that cannot be parsed (for example a timestamp that is out of range) is read as null.
                return ODataWireFormats.TryParseDateTime(text, out var dateTime)
                    ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc))
                    : null;
            case JsonTokenType.StartArray:
                var list = new List<object?>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(Read(ref reader));
                }
                return list;
            case JsonTokenType.StartObject:
                return ReadObject(ref reader);
            default:
                // Null and everything else.
                reader.Skip();
                return null;
        }
    }

    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt32(out var i))
        {
            return i;
        }
        if (reader.TryGetInt64(out var l))
        {
            return l;
        }
        var d = reader.GetDouble();
        // Like Gson, integral values written with a fraction or exponent are still integers.
        if (d == Math.Floor(d) && !double.IsInfinity(d))
        {
            if (d is >= int.MinValue and <= int.MaxValue)
            {
                return (int)d;
            }
            if (d is >= -9.2e18 and <= 9.2e18)
            {
                return (long)d;
            }
        }
        return d;
    }

    private static object? ReadObject(ref Utf8JsonReader reader)
    {
        var map = new Dictionary<string, object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            switch (key)
            {
                case "__deferred":
                    reader.Skip();
                    SkipRemaining(ref reader);
                    return null;
                case "__metadata":
                    reader.Skip();
                    break;
                case "results":
                    var results = Read(ref reader);
                    SkipRemaining(ref reader);
                    return results;
                default:
                    map[key] = Read(ref reader);
                    break;
            }
        }
        return map;
    }

    /// <summary>Skips the remaining members of the current object and leaves the reader on its end.</summary>
    internal static void SkipRemaining(ref Utf8JsonReader reader)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            reader.Read();
            reader.Skip();
        }
    }
}
