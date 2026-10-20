using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sap.Cloud.Sdk.DataModel.OData.Adapter;

/// <summary>
/// Base class of the converters for OData V2 value types. Unlike the strict <c>System.Text.Json</c> converters, a value that
/// cannot be converted yields <see langword="null"/> when read through <see cref="ODataNullableJsonConverter{T}"/>, as the Gson
/// adapters of the Java SDK do.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public abstract class ODataStructJsonConverter<T> : JsonConverter<T>
    where T : struct
{
    /// <summary>Reads the value at the current token. The reader must be left on the last token of the value.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="false"/> if the token cannot be converted; the value is skipped then.</returns>
    protected abstract bool TryReadValue(ref Utf8JsonReader reader, out T value);

    /// <summary>Writes the value.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    protected abstract void WriteValue(Utf8JsonWriter writer, T value);

    internal bool TryRead(ref Utf8JsonReader reader, out T value)
    {
        if (TryReadValue(ref reader, out value))
        {
            return true;
        }
        reader.Skip();
        return false;
    }

    /// <inheritdoc />
    public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TryRead(ref reader, out var value) ? value : throw new JsonException($"The JSON value cannot be converted to {typeof(T).Name}.");

    /// <inheritdoc />
    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteValue(writer, value);
    }

    /// <summary>Creates the converter for the nullable type that tolerates unconvertible values.</summary>
    /// <returns>The converter.</returns>
    public ODataNullableJsonConverter<T> ToNullable() => new(this);
}

/// <summary>Converter for <c>T?</c> that wraps an <see cref="ODataStructJsonConverter{T}"/>; unconvertible JSON values are read as <see langword="null"/>.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class ODataNullableJsonConverter<T> : JsonConverter<T?>
    where T : struct
{
    private readonly ODataStructJsonConverter<T> _inner;

    /// <summary>Initializes a new instance of the <see cref="ODataNullableJsonConverter{T}"/> class.</summary>
    /// <param name="inner">The converter of the underlying type.</param>
    public ODataNullableJsonConverter(ODataStructJsonConverter<T> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        return _inner.TryRead(ref reader, out var value) ? value : null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            _inner.Write(writer, value.Value, options);
        }
    }
}

/// <summary>Reads and writes <see cref="DateTime"/> (<c>Edm.DateTime</c>) as <c>/Date(milliseconds)/</c>.</summary>
/// <remarks>Port of <c>LocalDateTimeAdapter</c>; Java's <c>LocalDateTime</c> maps to a <see cref="DateTime"/> of unspecified kind.</remarks>
public sealed class ODataDateTimeJsonConverter : ODataStructJsonConverter<DateTime>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out DateTime value)
    {
        value = default;
        return reader.TokenType == JsonTokenType.String && ODataWireFormats.TryParseDateTime(reader.GetString()!, out value);
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, DateTime value) =>
        writer.WriteStringValue(ODataWireFormats.FormatDateTime(value));
}

/// <summary>Reads and writes <see cref="DateTimeOffset"/> (<c>Edm.DateTimeOffset</c>) as <c>/Date(milliseconds+offset)/</c>.</summary>
/// <remarks>Port of <c>ZonedDateTimeAdapter</c>; the offset (not a time zone id) is kept.</remarks>
public sealed class ODataDateTimeOffsetJsonConverter : ODataStructJsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out DateTimeOffset value)
    {
        value = default;
        return reader.TokenType == JsonTokenType.String && ODataWireFormats.TryParseDateTimeOffset(reader.GetString()!, out value);
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, DateTimeOffset value) =>
        writer.WriteStringValue(ODataWireFormats.FormatDateTimeOffset(value));
}

/// <summary>Reads and writes <see cref="TimeOnly"/> (<c>Edm.Time</c>) as an ISO-8601 duration such as <c>PT13H20M0S</c>.</summary>
/// <remarks>Port of <c>LocalTimeAdapter</c>.</remarks>
public sealed class ODataTimeJsonConverter : ODataStructJsonConverter<TimeOnly>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out TimeOnly value)
    {
        value = default;
        return reader.TokenType == JsonTokenType.String && ODataWireFormats.TryParseTime(reader.GetString()!, out value);
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, TimeOnly value) =>
        writer.WriteStringValue(ODataWireFormats.FormatTime(value));
}

/// <summary>Reads and writes <see cref="bool"/>; values that are not JSON booleans are read as <see langword="false"/>.</summary>
/// <remarks>Port of <c>ODataBooleanAdapter</c>.</remarks>
public sealed class ODataBooleanJsonConverter : ODataStructJsonConverter<bool>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out bool value)
    {
        value = false;
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
        {
            value = reader.GetBoolean();
        }
        else
        {
            reader.Skip();
        }
        return true;
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, bool value) => writer.WriteBooleanValue(value);
}

/// <summary>Reads <see cref="long"/> (<c>Edm.Int64</c>) from a number or string and writes it as a string.</summary>
/// <remarks>Port of <c>ODataNumberSerializer</c> for <c>Long</c>.</remarks>
public sealed class ODataInt64JsonConverter : ODataStructJsonConverter<long>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out long value)
    {
        value = 0;
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out value),
            JsonTokenType.String => long.TryParse(reader.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, long value) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>Reads <see cref="decimal"/> (<c>Edm.Decimal</c>) from a number or string and writes it as a string in plain notation.</summary>
/// <remarks>Port of <c>ODataNumberSerializer</c> for <c>BigDecimal</c>.</remarks>
public sealed class ODataDecimalJsonConverter : ODataStructJsonConverter<decimal>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out decimal value)
    {
        value = 0;
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetDecimal(out value),
            JsonTokenType.String => decimal.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, decimal value) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>Reads <see cref="double"/> (<c>Edm.Double</c>) from a number or string and writes it as a string.</summary>
/// <remarks>Port of <c>ODataNumberSerializer</c> for <c>Double</c>.</remarks>
public sealed class ODataDoubleJsonConverter : ODataStructJsonConverter<double>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out double value)
    {
        value = 0;
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetDouble(out value),
            JsonTokenType.String => TryParseSpecial(reader.GetString(), out value),
            _ => false,
        };
    }

    internal static bool TryParseSpecial(string? text, out double value)
    {
        switch (text)
        {
            case "NaN":
                value = double.NaN;
                return true;
            case "INF" or "Infinity":
                value = double.PositiveInfinity;
                return true;
            case "-INF" or "-Infinity":
                value = double.NegativeInfinity;
                return true;
            default:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, double value) =>
        writer.WriteStringValue(ODataWireFormats.FormatDouble(value));
}

/// <summary>Reads <see cref="float"/> (<c>Edm.Single</c>) from a number or string and writes it as a string.</summary>
/// <remarks>Port of <c>ODataNumberSerializer</c> for <c>Float</c>.</remarks>
public sealed class ODataSingleJsonConverter : ODataStructJsonConverter<float>
{
    /// <inheritdoc />
    protected override bool TryReadValue(ref Utf8JsonReader reader, out float value)
    {
        value = 0;
        double d;
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                value = reader.GetSingle();
                return true;
            case JsonTokenType.String when ODataDoubleJsonConverter.TryParseSpecial(reader.GetString(), out d):
                value = (float)d;
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, float value) =>
        writer.WriteStringValue(ODataWireFormats.FormatSingle(value));
}

/// <summary>Reads and writes <c>byte[]</c> (<c>Edm.Binary</c>) as a Base64 string. Undecodable values are read as <see langword="null"/>.</summary>
/// <remarks>Port of <c>ODataBinaryAdapter</c>.</remarks>
public sealed class ODataBinaryJsonConverter : JsonConverter<byte[]>
{
    /// <inheritdoc />
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }
        try
        {
            return Convert.FromBase64String(reader.GetString()!);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(Convert.ToBase64String(value));
    }
}

/// <summary>Reads and writes <see cref="string"/>; numbers and booleans are read as their text, other tokens as <see langword="null"/>.</summary>
/// <remarks>Mirrors Gson's lenient string adapter.</remarks>
public sealed class ODataStringJsonConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            default:
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}

/// <summary>The converters used for the value types of the entity model, with their nullable counterparts.</summary>
internal static class ODataValueConverters
{
    private static readonly ODataDateTimeJsonConverter DateTimeConverter = new();
    private static readonly ODataDateTimeOffsetJsonConverter DateTimeOffsetConverter = new();
    private static readonly ODataTimeJsonConverter TimeConverter = new();
    private static readonly ODataBooleanJsonConverter BooleanConverter = new();
    private static readonly ODataInt64JsonConverter Int64Converter = new();
    private static readonly ODataDecimalJsonConverter DecimalConverter = new();
    private static readonly ODataDoubleJsonConverter DoubleConverter = new();
    private static readonly ODataSingleJsonConverter SingleConverter = new();
    private static readonly ODataBinaryJsonConverter BinaryConverter = new();
    private static readonly ODataStringJsonConverter StringConverter = new();

    private static readonly Dictionary<Type, JsonConverter> ByType = new()
    {
        [typeof(DateTime)] = DateTimeConverter,
        [typeof(DateTime?)] = DateTimeConverter.ToNullable(),
        [typeof(DateTimeOffset)] = DateTimeOffsetConverter,
        [typeof(DateTimeOffset?)] = DateTimeOffsetConverter.ToNullable(),
        [typeof(TimeOnly)] = TimeConverter,
        [typeof(TimeOnly?)] = TimeConverter.ToNullable(),
        [typeof(bool)] = BooleanConverter,
        [typeof(bool?)] = BooleanConverter.ToNullable(),
        [typeof(long)] = Int64Converter,
        [typeof(long?)] = Int64Converter.ToNullable(),
        [typeof(decimal)] = DecimalConverter,
        [typeof(decimal?)] = DecimalConverter.ToNullable(),
        [typeof(double)] = DoubleConverter,
        [typeof(double?)] = DoubleConverter.ToNullable(),
        [typeof(float)] = SingleConverter,
        [typeof(float?)] = SingleConverter.ToNullable(),
        [typeof(byte[])] = BinaryConverter,
        [typeof(string)] = StringConverter,
    };

    /// <summary>Finds the converter for the value type, or <see langword="null"/> if the default converters of the options apply.</summary>
    internal static JsonConverter<T>? Find<T>() => ByType.TryGetValue(typeof(T), out var converter) ? (JsonConverter<T>)converter : null;

    internal static IEnumerable<JsonConverter> All => ByType.Values;
}
