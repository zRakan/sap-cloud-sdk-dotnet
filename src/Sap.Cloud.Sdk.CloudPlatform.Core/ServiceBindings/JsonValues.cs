using System.Text.Json;

namespace Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

/// <summary>Converts <see cref="JsonElement"/>s into plain .NET values.</summary>
internal static class JsonValues
{
    public static object? ToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ToDictionary(element),
        JsonValueKind.Array => element.EnumerateArray().Select(ToValue).ToList().AsReadOnly(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : (object)element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static IReadOnlyDictionary<string, object?> ToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = ToValue(property.Value);
            }
        }
        return result.AsReadOnly();
    }

    public static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static IReadOnlyList<string> GetStrings(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!)]
            : [];
}
