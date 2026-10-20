using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

/// <summary>
/// Reads and writes <see cref="DateTimeOffset"/> like Java's <c>ISO_OFFSET_DATE_TIME</c>: seconds are always written,
/// the fraction only if it is not zero, and a zero offset is written as <c>Z</c>.
/// </summary>
/// <remarks>
/// Port of <c>ODataGenericConverter.OFFSET_DATE_TIME</c> of the OData v4 core. The converter of <c>OData.Client</c>
/// mimics <c>OffsetDateTime.toString()</c>, which drops zero seconds (<c>2020-02-20T00:00Z</c>).
/// </remarks>
public sealed class IsoDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.TryParse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new JsonException("Invalid offset date-time value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var text = value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
            + IsoTimeOnlyJsonConverter.Fraction(value.Ticks % TimeSpan.TicksPerSecond)
            + (value.Offset == TimeSpan.Zero ? "Z" : value.ToString("zzz", CultureInfo.InvariantCulture));
        writer.WriteStringValue(text);
    }
}

/// <summary>
/// Reads and writes <see cref="TimeOnly"/> like Java's <c>ISO_LOCAL_TIME</c>: <c>HH:mm:ss</c> with a fraction only if
/// it is not zero.
/// </summary>
/// <remarks>Port of <c>ODataGenericConverter.LOCAL_TIME</c> of the OData v4 core.</remarks>
public sealed class IsoTimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    /// <inheritdoc />
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeOnly.TryParse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new JsonException("Invalid time value.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(
            value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + Fraction(value.Ticks % TimeSpan.TicksPerSecond));
    }

    internal static string Fraction(long ticks) =>
        ticks == 0 ? string.Empty : "." + ticks.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
}
