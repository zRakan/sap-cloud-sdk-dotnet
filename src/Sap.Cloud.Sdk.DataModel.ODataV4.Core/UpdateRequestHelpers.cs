using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Serializes the payload of a PATCH request: only changed (or explicitly included) fields.</summary>
/// <remarks>Port of <c>UpdateRequestHelperPatch</c>.</remarks>
internal static class UpdateRequestHelperPatch
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(UpdateRequestHelperPatch).FullName!);

    internal static string ToJson(VdmEntity entity, IEnumerable<IFieldReference> includedFields)
    {
        var options = VdmJson.OptionsWithNulls;
        var fieldNamesToPatch = new HashSet<string>(entity.GetChangedFields().Keys, StringComparer.Ordinal);
        foreach (var field in includedFields)
        {
            fieldNamesToPatch.Add(field.FieldName);
        }

        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("The following fields are marked for updates: {Fields}.", string.Join(", ", fieldNamesToPatch));
        }

        var patchValues = entity.ToMap().Where(entry => fieldNamesToPatch.Contains(entry.Key));
        var context = new Context(options);
        var parents = new HashSet<VdmObject>(ReferenceEqualityComparer.Instance as IEqualityComparer<VdmObject>) { entity };
        var result = new JsonObject();
        context.Fill(result, patchValues, parents);
        if (result.Count < 1)
        {
            Log.LogWarning(
                "Update strategy is to modify with PATCH, but no fields have changed. " +
                "Make sure to modify the entity via its setters or by naming them explicitly via 'IncludingFields(fields ...)'. " +
                "This request may be bound to fail in the target system.");
        }

        foreach (var (name, value) in entity.GetAnnotationProperties())
        {
            result[name] = value;
        }

        return result.ToJsonString(options);
    }

    private sealed class Context(JsonSerializerOptions options)
    {
        private readonly Dictionary<VdmObject, JsonObject> _cache = new(ReferenceEqualityComparer.Instance as IEqualityComparer<VdmObject>);

        internal void Fill(JsonObject target, IEnumerable<KeyValuePair<string, object?>> entries, HashSet<VdmObject> parents)
        {
            foreach (var (key, value) in entries)
            {
                target[key] = Serialize(value, parents);
            }
        }

        private JsonNode? Serialize(object? input, HashSet<VdmObject> parents)
        {
            switch (input)
            {
                case null:
                    return null;
                case IDictionary dictionary:
                    var map = new JsonObject();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        map[entry.Key.ToString() ?? string.Empty] = Serialize(entry.Value, parents);
                    }

                    return map;
                case IList list when input is not byte[]:
                    var array = new JsonArray();
                    foreach (var item in list)
                    {
                        array.Add(Serialize(item, parents));
                    }

                    return array;
                case VdmObject vdmObject:
                    return SerializeVdmObject(vdmObject, parents);
                default:
                    return JsonSerializer.SerializeToNode(input, input.GetType(), options);
            }
        }

        private JsonObject SerializeVdmObject(VdmObject input, HashSet<VdmObject> parents)
        {
            if (!parents.Contains(input) && _cache.TryGetValue(input, out var cached))
            {
                return (JsonObject)cached.DeepClone();
            }

            var valueMap = new JsonObject();
            if (!parents.Contains(input))
            {
                _cache[input] = valueMap;
                IEnumerable<KeyValuePair<string, object?>> fields;
                if (input is VdmComplex)
                {
                    var all = input.ToMapOfFields();
                    foreach (var (key, value) in input.GetCustomFields())
                    {
                        all[key] = value;
                    }

                    fields = all;
                }
                else
                {
                    fields = input.GetChangedFields();
                }

                var nestedParents = new HashSet<VdmObject>(parents, ReferenceEqualityComparer.Instance as IEqualityComparer<VdmObject>) { input };
                Fill(valueMap, fields, nestedParents);
            }

            if (input is VdmEntity entity)
            {
                var key = entity.GetKey();
                if (!key.FieldNames.Any(valueMap.ContainsKey))
                {
                    valueMap["@id"] = entity.EntityCollection + key.ToEncodedString();
                }
            }

            return valueMap;
        }
    }
}

/// <summary>Serializes the payload of a PUT request: the whole entity without the excluded fields.</summary>
/// <remarks>Port of <c>UpdateRequestHelperPut</c>.</remarks>
internal static class UpdateRequestHelperPut
{
    internal static string ToJson(VdmEntity entity, IEnumerable<IFieldReference> excludedFields)
    {
        var json = (JsonObject)JsonNode.Parse(VdmJson.Serialize(entity))!;
        foreach (var field in excludedFields)
        {
            json.Remove(field.FieldName);
        }

        return json.ToJsonString(VdmJson.Options);
    }
}
