using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Cloud.Sdk.DataModel.OData.Client.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

/// <summary>
/// Entry points and cached options for (de)serializing generated entities and complex types.
/// </summary>
/// <remarks>
/// Replaces the Gson adapters (<c>GsonVdmAdapterFactory</c> and friends). The options are derived from
/// <see cref="ODataJsonOptions"/> so that dates, durations, GUIDs and binary values use the same formats as the protocol layer.
/// </remarks>
public static class VdmJson
{
    private static readonly JsonSerializerOptions DefaultOptions = Create(JsonIgnoreCondition.WhenWritingNull);
    private static readonly JsonSerializerOptions NullsOptions = Create(JsonIgnoreCondition.Never);

    /// <summary>Options that omit <see langword="null"/> properties when writing, like Gson's default. The instance is cached.</summary>
    public static JsonSerializerOptions Options => DefaultOptions;

    /// <summary>Options that write <see langword="null"/> properties (<c>serializeNulls</c>). The instance is cached.</summary>
    public static JsonSerializerOptions OptionsWithNulls => NullsOptions;

    private static JsonSerializerOptions Create(JsonIgnoreCondition ignoreCondition)
    {
        var options = new JsonSerializerOptions(ODataJsonOptions.Get())
        {
            DefaultIgnoreCondition = ignoreCondition,
        };
        options.Converters.Remove(options.Converters.First(c => c is DateTimeOffsetJsonConverter));
        options.Converters.Remove(options.Converters.First(c => c is TimeOnlyJsonConverter));
        options.Converters.Add(new IsoDateTimeOffsetJsonConverter());
        options.Converters.Add(new IsoTimeOnlyJsonConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    /// <summary>Serializes an entity or complex value completely (all properties, navigation properties and custom fields).</summary>
    /// <param name="value">The value.</param>
    /// <param name="includeNulls">Whether <see langword="null"/> properties are written.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(VdmObject value, bool includeNulls = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = includeNulls ? NullsOptions : DefaultOptions;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = options.Encoder }))
        {
            WriteObject(writer, value, options);
        }

        return System.Text.Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>Deserializes an entity or complex value. Recorded changes are reset.</summary>
    /// <typeparam name="T">The generated type.</typeparam>
    /// <param name="json">The JSON text.</param>
    /// <returns>The value.</returns>
    public static T? Deserialize<T>(string json)
        where T : VdmObject, new()
    {
        ArgumentNullException.ThrowIfNull(json);
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        if (!reader.Read())
        {
            throw new JsonException("The JSON text is empty.");
        }

        return ReadObject<T>(ref reader, DefaultOptions);
    }

    /// <summary>Creates an independent copy of an entity or complex value by a round trip through JSON.</summary>
    /// <typeparam name="T">The generated type.</typeparam>
    /// <param name="value">The value to copy.</param>
    /// <returns>The copy, without recorded changes.</returns>
    public static T Copy<T>(T value)
        where T : VdmObject
    {
        ArgumentNullException.ThrowIfNull(value);
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(Serialize(value)));
        reader.Read();
        return (T)ReadObjectCore(ref reader, DefaultOptions, value.TypeInfo.CreateInstance)!;
    }

    internal static T? ReadObject<T>(ref Utf8JsonReader reader, JsonSerializerOptions options)
        where T : VdmObject, new() =>
        (T?)ReadObjectCore(ref reader, options, static () => new T());

    private static VdmObject? ReadObjectCore(ref Utf8JsonReader reader, JsonSerializerOptions options, Func<VdmObject> factory)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected a JSON object but found {reader.TokenType}.");
        }

        var result = factory();
        var typeInfo = result.TypeInfo;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!;
            reader.Read();
            if (typeInfo.TryGetProperty(name, out var property))
            {
                property.ReadJson(result, ref reader, options);
            }
            else if (VdmObject.ODataTypeAnnotations.Contains(name))
            {
                reader.Skip();
            }
            else if (result is VdmEntity entity && VdmObject.ODataVersionAnnotations.Contains(name))
            {
                entity.VersionIdentifier = reader.TokenType == JsonTokenType.String ? reader.GetString() : ReadAsText(ref reader);
            }
            else
            {
                result.SetCustomField(name, JsonSerializer.Deserialize<object?>(ref reader, options));
            }
        }

        result.ResetChangedFields();
        return result;
    }

    private static string? ReadAsText(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.ValueKind == JsonValueKind.Null ? null : document.RootElement.ToString();
    }

    internal static void WriteObject(Utf8JsonWriter writer, VdmObject value, JsonSerializerOptions options)
    {
        var includeNulls = options.DefaultIgnoreCondition == JsonIgnoreCondition.Never;
        writer.WriteStartObject();
        foreach (var (name, annotation) in value.GetAnnotationProperties())
        {
            writer.WriteString(name, annotation);
        }

        if (value is VdmEntity { VersionIdentifier: { } versionIdentifier })
        {
            writer.WriteString(VdmObject.ODataVersionAnnotations[0], versionIdentifier);
        }

        foreach (var property in value.TypeInfo.Properties)
        {
            if (!includeNulls && property.GetValue(value) is null)
            {
                continue;
            }

            writer.WritePropertyName(property.Name);
            property.WriteJson(value, writer, options);
        }

        foreach (var (name, fieldValue) in value.GetCustomFields())
        {
            if (fieldValue is null)
            {
                if (includeNulls)
                {
                    writer.WriteNull(name);
                }

                continue;
            }

            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, fieldValue, fieldValue.GetType(), options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// Reads and writes a generated entity or complex type using its <see cref="VdmTypeInfo"/>. Apply it to the generated
/// class with <c>[JsonConverter(typeof(VdmObjectJsonConverter&lt;Person&gt;))]</c>.
/// </summary>
/// <typeparam name="T">The generated type.</typeparam>
public sealed class VdmObjectJsonConverter<T> : JsonConverter<T>
    where T : VdmObject, new()
{
    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        VdmJson.ReadObject<T>(ref reader, options);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        VdmJson.WriteObject(writer, value, options);
    }
}
