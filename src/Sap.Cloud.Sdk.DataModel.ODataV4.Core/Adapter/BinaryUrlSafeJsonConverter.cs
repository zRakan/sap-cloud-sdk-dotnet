using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

/// <summary>
/// Reads and writes <c>byte[]</c> as Base64 string. Both Base64 alphabets are accepted when reading (the URL-safe
/// <c>-</c> and <c>_</c> are mapped to <c>+</c> and <c>/</c>); the standard alphabet is written. Undecodable values
/// are read as <see langword="null"/>.
/// </summary>
/// <remarks>
/// Port of <c>ODataGenericConverter.BINARY</c> of the OData v4 core. The converter of <c>OData.Client</c> only accepts the
/// standard alphabet, so typed entities use this one.
/// </remarks>
internal sealed class BinaryUrlSafeJsonConverter : JsonConverter<byte[]>
{
    internal static BinaryUrlSafeJsonConverter Instance { get; } = new();

    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is null)
        {
            return null;
        }

        var normalized = text.Replace('-', '+').Replace('_', '/');
        if (normalized.Length % 4 != 0)
        {
            normalized = normalized.PadRight(normalized.Length + 4 - (normalized.Length % 4), '=');
        }

        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException e)
        {
            SdkLogging.CreateLogger<BinaryUrlSafeJsonConverter>().LogWarning(e, "Not deserializable binary value.");
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Convert.ToBase64String(value));
}
