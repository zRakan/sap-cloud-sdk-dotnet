using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Adapter;

/// <summary>Reads and writes <see cref="decimal"/> as JSON string (plain notation).</summary>
/// <remarks>Port of <c>BigDecimalAdapter</c> (Gson <c>TypeAdapter</c>).</remarks>
public sealed class BigDecimalJsonConverter : JsonConverter<decimal>
{
    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        decimal.Parse(JsonConverterHelper.ReadStringOrNumber(ref reader), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>Reads and writes <c>byte[]</c> as Base64 string. Undecodable values are read as <see langword="null"/>.</summary>
/// <remarks>Port of <c>BinaryTypeAdapter</c>.</remarks>
public sealed class BinaryJsonConverter : JsonConverter<byte[]>
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<BinaryJsonConverter>();

    /// <inheritdoc />
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        try
        {
            return value is null ? null : IValueBinary.DecodeFromString(value);
        }
        catch (FormatException e)
        {
            Log.Warn(e, "Failed to deserialize binary value.");
            return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
        writer.WriteStringValue(IValueBinary.EncodeToString(value));
}

/// <summary>Reads and writes <see cref="TimeSpan"/> as ISO-8601 duration string.</summary>
/// <remarks>Port of <c>DurationTypeAdapter</c>.</remarks>
public sealed class DurationJsonConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc />
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JavaFormat.TryParseDuration(reader.GetString()!, out var value)
            ? value
            : throw new JsonException("Invalid duration value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JavaFormat.ToIsoString(value));
}

/// <summary>Reads and writes <see cref="DateOnly"/> as ISO-8601 date string.</summary>
/// <remarks>Port of <c>LocalDateTypeAdapter</c>.</remarks>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    /// <inheritdoc />
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JavaFormat.TryParseDate(reader.GetString()!, out var value) ? value : throw new JsonException("Invalid date value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JavaFormat.ToIsoLocalDate(value));
}

/// <summary>Reads and writes <see cref="TimeOnly"/> as ISO-8601 time string.</summary>
/// <remarks>Port of <c>LocalTimeTypeAdapter</c>.</remarks>
public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    /// <inheritdoc />
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JavaFormat.TryParseTime(reader.GetString()!, out var value) ? value : throw new JsonException("Invalid time value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JavaFormat.ToString(value));
}

/// <summary>Reads and writes <see cref="DateTimeOffset"/> as ISO-8601 offset date-time string.</summary>
/// <remarks>Port of <c>OffsetDateTimeTypeAdapter</c>.</remarks>
public sealed class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JavaFormat.TryParseOffsetDateTime(reader.GetString()!, out var value)
            ? value
            : throw new JsonException("Invalid offset date-time value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JavaFormat.ToString(value));
}

/// <summary>Reads and writes <see cref="Guid"/> as string.</summary>
/// <remarks>Port of <c>UuidTypeAdapter</c>.</remarks>
public sealed class GuidJsonConverter : JsonConverter<Guid>
{
    /// <inheritdoc />
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Guid.TryParse(reader.GetString(), out var value) ? value : throw new JsonException("Invalid GUID value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("D", CultureInfo.InvariantCulture));
}

/// <summary>
/// Reads untyped JSON values (<see cref="object"/>) as plain .NET values, the way Gson reads <c>Object</c>:
/// objects become <see cref="Dictionary{TKey, TValue}"/>, arrays become <see cref="List{T}"/>, numbers become
/// <see cref="double"/> (or <see cref="decimal"/> if configured).
/// </summary>
public sealed class ObjectJsonConverter : JsonConverter<object>
{
    private readonly bool _useDecimal;

    /// <summary>Initializes a new instance of the <see cref="ObjectJsonConverter"/> class.</summary>
    /// <param name="useDecimal">Whether numbers are read as <see cref="decimal"/> instead of <see cref="double"/>.</param>
    public ObjectJsonConverter(bool useDecimal)
    {
        _useDecimal = useDecimal;
    }

    /// <inheritdoc />
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return _useDecimal
                    ? decimal.Parse(Encoding.UTF8.GetString(reader.ValueSpan), NumberStyles.Float, CultureInfo.InvariantCulture)
                    : reader.GetDouble();
            case JsonTokenType.StartArray:
                var list = new List<object?>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(Read(ref reader, typeof(object), options));
                }
                return list;
            case JsonTokenType.StartObject:
                var map = new Dictionary<string, object?>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var name = reader.GetString()!;
                    reader.Read();
                    map[name] = Read(ref reader, typeof(object), options);
                }
                return map;
            default:
                throw new JsonException($"Unexpected token {reader.TokenType}.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (value.GetType() == typeof(object))
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

internal static class JsonConverterHelper
{
    internal static string ReadStringOrNumber(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Number ? Encoding.UTF8.GetString(reader.ValueSpan) : reader.GetString()!;
}
