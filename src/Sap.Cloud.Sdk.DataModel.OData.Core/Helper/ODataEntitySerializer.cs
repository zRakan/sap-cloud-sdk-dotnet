using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>Serializes entities as request payloads for create and the update variants (<c>PUT</c> and the <c>PATCH</c> strategies).</summary>
/// <remarks>Port of <c>ODataEntitySerializer</c>; the JSON tree is built with <see cref="JsonNode"/> instead of Gson's <c>JsonObject</c>.</remarks>
internal static class ODataEntitySerializer
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(ODataEntitySerializer).FullName!);

    private static JsonObject ToJsonObject(VdmObject entity, bool serializeNulls) =>
        (JsonObject)JsonSerializer.SerializeToNode(entity, entity.GetType(), ODataVdmJsonOptions.Get(serializeNulls))!;

    private static string ToJson(JsonNode node) => node.ToJsonString(ODataVdmJsonOptions.Get(serializeNulls: true));

    private static JsonNode? Copy(JsonObject source, string key) => source.TryGetPropertyValue(key, out var value) ? value?.DeepClone() : null;

    internal static string SerializeEntityForUpdatePut(VdmEntity entity, IEnumerable<string>? excludedFields)
    {
        var jsonObject = ToJsonObject(entity, serializeNulls: true);
        RemoveVersionIdentifier(jsonObject);
        if (excludedFields is not null)
        {
            foreach (var field in excludedFields)
            {
                jsonObject.Remove(field);
            }
        }
        return ToJson(jsonObject);
    }

    internal static string SerializeEntityForCreate(VdmEntity entity)
    {
        // When using the builder pattern, 1:n navigation properties are initialized with empty lists instead of null.
        var jsonObject = ToJsonObject(entity, serializeNulls: false);
        RemoveEmptyArrays(jsonObject);
        return jsonObject.ToJsonString(ODataVdmJsonOptions.Get());
    }

    internal static string SerializeEntityForUpdatePatchShallow(VdmEntity entity, IEnumerable<string> includedFields)
    {
        var fullEntity = ToJsonObject(entity, serializeNulls: true);
        var fieldNamesToPatch = new List<string>(entity.GetChangedFields().Keys);
        fieldNamesToPatch.AddRange(includedFields.Where(f => !fieldNamesToPatch.Contains(f)));
        if (Log.IsEnabled(LogLevel.Debug)) Log.LogDebug("The following fields are marked for updates: {Fields}.", string.Join(", ", fieldNamesToPatch));

        var partialEntity = new JsonObject();
        foreach (var key in fieldNamesToPatch)
        {
            partialEntity[key] = Copy(fullEntity, key);
        }
        return ToJson(partialEntity);
    }

    internal static string SerializeEntityForUpdatePatchRecursiveFull(VdmEntity entity, IEnumerable<string> includedFields)
    {
        var fullEntityJson = ToJsonObject(entity, serializeNulls: true);
        var patchObject = new JsonObject();

        var changedFieldNames = new List<string>(entity.GetChangedFields().Keys);
        changedFieldNames.AddRange(includedFields.Where(f => !changedFieldNames.Contains(f)));
        foreach (var key in changedFieldNames)
        {
            patchObject[key] = Copy(fullEntityJson, key);
        }
        foreach (var (name, value) in entity.ToMapOfFields())
        {
            if (!patchObject.ContainsKey(name) && ContainsNestedChangedFields(value))
            {
                patchObject[name] = Copy(fullEntityJson, name);
            }
        }
        if (Log.IsEnabled(LogLevel.Debug)) Log.LogDebug("The following object is serialized for update: {Payload}.", patchObject.ToJsonString());
        return ToJson(patchObject);
    }

    private static bool ContainsNestedChangedFields(object? obj)
    {
        if (obj is not VdmObject complex || obj is VdmEntity)
        {
            return false;
        }
        return complex.GetChangedFields().Count > 0 || complex.ToMapOfFields().Values.Any(ContainsNestedChangedFields);
    }

    internal static string SerializeEntityForUpdatePatchRecursiveDelta(VdmEntity entity, IEnumerable<string> includedFields)
    {
        var fullEntityJson = ToJsonObject(entity, serializeNulls: true);
        var patchObject = new JsonObject();

        // Recursively build the patch object from the changed fields.
        var tempPatchObject = CreatePatchObjectRecursiveDelta(entity, fullEntityJson);

        // Add the included fields (from the root only).
        foreach (var key in includedFields)
        {
            patchObject[key] = Copy(fullEntityJson, key);
        }

        // Merge all fields from the temporary patch object if not already present.
        foreach (var (key, value) in tempPatchObject)
        {
            if (!patchObject.ContainsKey(key))
            {
                patchObject[key] = value?.DeepClone();
            }
        }
        if (Log.IsEnabled(LogLevel.Debug)) Log.LogDebug("The following delta object is serialized for update: {Payload}.", patchObject.ToJsonString());
        return ToJson(patchObject);
    }

    private static JsonObject CreatePatchObjectRecursiveDelta(VdmObject vdmObject, JsonObject jsonObject)
    {
        var patch = new JsonObject();

        // Process all complex fields and recursively build the patch for the complex field.
        foreach (var (fieldName, value) in vdmObject.ToMapOfFields())
        {
            if (value is VdmObject complex and not VdmEntity && jsonObject[fieldName] is JsonObject childJsonObject)
            {
                var childDelta = CreatePatchObjectRecursiveDelta(complex, childJsonObject);
                if (childDelta.Count > 0)
                {
                    patch[fieldName] = childDelta;
                }
            }
        }

        // Add the explicitly changed fields.
        foreach (var key in vdmObject.GetChangedFields().Keys)
        {
            patch[key] = Copy(jsonObject, key);
        }
        return patch;
    }

    private static void RemoveVersionIdentifier(JsonObject jsonObject)
    {
        TraverseJsonObject(
            jsonObject,
            (element, name) => name == "versionIdentifier" && (element is null || (element is JsonValue value && value.GetValueKind() == JsonValueKind.String)));
    }

    private static void RemoveEmptyArrays(JsonObject jsonObject) =>
        TraverseJsonObject(jsonObject, (element, _) => element is JsonArray { Count: 0 });

    private static void TraverseJsonObject(JsonObject jsonObject, Func<JsonNode?, string, bool> filter)
    {
        var filteredChildKeys = new List<string>();
        foreach (var (key, value) in jsonObject)
        {
            if (value is JsonObject child)
            {
                // Apply this logic recursively for all nested objects.
                TraverseJsonObject(child, filter);
            }
            else if (filter(value, key))
            {
                filteredChildKeys.Add(key);
            }
            else if (value is JsonArray array)
            {
                // Apply this logic recursively for lists of nested objects.
                foreach (var item in array.OfType<JsonObject>())
                {
                    TraverseJsonObject(item, filter);
                }
            }
        }
        foreach (var key in filteredChildKeys)
        {
            jsonObject.Remove(key);
        }
    }
}
