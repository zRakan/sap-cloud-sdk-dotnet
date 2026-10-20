using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Cloud.Sdk.DataModel.OData.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Adapter;

/// <summary>
/// Creates the cached <see cref="JsonSerializerOptions"/> for (de)serializing the typed OData V2 model: entities and complex types
/// (<see cref="VdmObject"/>) with their type metadata, change tracking aware custom fields, <c>__metadata</c>, <c>__deferred</c> and
/// the V2 representations of EDM values.
/// </summary>
/// <remarks>
/// <para>Replaces <c>ODataVdmEntityAdapterFactory</c> and the two <c>Gson</c> instances of <c>ODataEntitySerializer</c>.</para>
/// <para>
/// Reading expects the object of the entity (not the <c>d</c> envelope). Writing omits properties that are
/// <see langword="null"/> unless <c>serializeNulls</c> is set. The returned instances are shared and read-only.
/// </para>
/// </remarks>
public static class ODataVdmJsonOptions
{
    private static readonly JsonSerializerOptions Omitting = Create(serializeNulls: false);
    private static readonly JsonSerializerOptions Serializing = Create(serializeNulls: true);

    /// <summary>Gets the shared options.</summary>
    /// <param name="serializeNulls">Whether <see langword="null"/> properties are written (as for update requests) or omitted.</param>
    /// <returns>The options.</returns>
    public static JsonSerializerOptions Get(bool serializeNulls = false) => serializeNulls ? Serializing : Omitting;

    private static JsonSerializerOptions Create(bool serializeNulls)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        foreach (var converter in ODataValueConverters.All)
        {
            options.Converters.Add(converter);
        }
        options.Converters.Add(new VdmJsonConverterFactory(serializeNulls));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

/// <summary>Creates the converters for all <see cref="VdmObject"/> types.</summary>
internal sealed class VdmJsonConverterFactory(bool serializeNulls) : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsClass && !typeToConvert.IsAbstract && typeof(VdmObject).IsAssignableFrom(typeToConvert);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        VdmTypeMetadata metadata;
        try
        {
            metadata = ((VdmObject)Activator.CreateInstance(typeToConvert, nonPublic: true)!).GetMetadata();
        }
        catch (MissingMethodException e)
        {
            throw new ODataPayloadParsingFailedException(
                $"Could not instantiate '{typeToConvert.FullName}'. Entities and complex types need a parameterless constructor.",
                e);
        }
        return (JsonConverter)Activator.CreateInstance(
            typeof(VdmJsonConverter<>).MakeGenericType(typeToConvert),
            metadata,
            serializeNulls)!;
    }
}

/// <summary>Reads and writes one entity or complex type according to its metadata.</summary>
/// <remarks>Port of <c>ODataVdmEntityAdapter</c> (and, for lists of navigation properties, <c>ODataVdmEntityListAdapter</c>).</remarks>
internal sealed class VdmJsonConverter<T>(VdmTypeMetadata metadata, bool serializeNulls) : JsonConverter<T>
    where T : VdmObject
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }
        var entity = (T)metadata.Factory();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!;
            reader.Read();
            switch (name)
            {
                case "__metadata":
                    ReadMetadata(ref reader, entity);
                    break;
                case "__deferred":
                    // A navigation property that has not been loaded.
                    reader.Skip();
                    ODataCustomFieldReader.SkipRemaining(ref reader);
                    return null;
                default:
                    if (metadata.TryGetProperty(name, out var property))
                    {
                        property.Read(ref reader, entity, options);
                    }
                    else
                    {
                        entity.CustomFieldsInternal[name] = ODataCustomFieldReader.Read(ref reader);
                    }
                    break;
            }
        }
        return entity;
    }

    private static void ReadMetadata(ref Utf8JsonReader reader, T entity)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }
        string? etag = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString();
            reader.Read();
            if (key == "etag" && reader.TokenType == JsonTokenType.String)
            {
                etag = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }
        if (!string.IsNullOrEmpty(etag) && entity is VdmEntity vdmEntity)
        {
            vdmEntity.VersionIdentifier = etag;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var property in metadata.Properties)
        {
            property.Write(writer, value, options, serializeNulls);
        }
        foreach (var (name, customValue) in value.CustomFieldsInternal)
        {
            if (customValue is null)
            {
                if (serializeNulls)
                {
                    writer.WriteNull(name);
                }
                continue;
            }
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, customValue, customValue.GetType(), options);
        }
        writer.WriteEndObject();
    }
}
