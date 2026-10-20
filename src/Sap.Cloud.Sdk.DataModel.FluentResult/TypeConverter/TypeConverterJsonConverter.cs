using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sap.Cloud.Sdk.TypeConverter;

/// <summary>A <see cref="JsonConverter{T}"/> that reads a JSON string through an <see cref="ITypeConverter{T, TDomain}"/>.</summary>
/// <remarks>Replaces <c>TypeConverterGsonDeserializer</c> (read only, as in Java).</remarks>
/// <typeparam name="T">The target type.</typeparam>
/// <param name="typeConverter">The converter from the string domain type.</param>
public sealed class TypeConverterJsonConverter<T>(ITypeConverter<T, string> typeConverter) : JsonConverter<T>
{
    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        typeConverter.FromDomain(reader.GetString()).OrNull();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization through a type converter is not supported.");
}
