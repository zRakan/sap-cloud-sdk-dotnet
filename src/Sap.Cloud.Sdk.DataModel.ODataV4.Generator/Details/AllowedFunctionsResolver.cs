using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Details;

/// <summary>The operations that the service methods of an entity set offer.</summary>
/// <remarks>Port of <c>ApiFunction</c>; the order is the order in which the service methods are generated.</remarks>
internal enum ApiFunction
{
    Read,
    ReadByKey,
    Create,
    Update,
    Delete,
    Navigate,
}

/// <summary>
/// Determines which operations are allowed on an entity set: from the paths of the swagger file if there is one, from
/// the <c>Capabilities</c> annotations of the EDMX otherwise.
/// </summary>
/// <remarks>Port of <c>AllowedFunctionsResolver</c>.</remarks>
internal static class AllowedFunctionsResolver
{
    public static Dictionary<string, SortedSet<ApiFunction>> Find(EdmModel model, byte[]? swagger, Action<string> warn)
    {
        Dictionary<string, SortedSet<ApiFunction>>? result = null;
        if (swagger is not null)
        {
            result = ReadFromSwagger(swagger, warn);
        }

        return result ?? ReadFromMetadata(model, warn);
    }

    private static Dictionary<string, SortedSet<ApiFunction>>? ReadFromSwagger(byte[] content, Action<string> warn)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException e)
        {
            throw new ODataGeneratorReadException("Could not read the swagger file: " + e.Message, e);
        }

        using (document)
        {
            var result = new Dictionary<string, SortedSet<ApiFunction>>(StringComparer.Ordinal);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("paths", out var paths)
                || paths.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var pathEntry in paths.EnumerateObject())
            {
                var trimmed = pathEntry.Name.StartsWith('/') ? pathEntry.Name[1..] : pathEntry.Name;
                var split = trimmed.Split('(');
                var entitySet = split[0];
                if (!result.TryGetValue(entitySet, out var functions))
                {
                    functions = [];
                    result[entitySet] = functions;
                }

                // Mark entity set as navigable.
                functions.Add(ApiFunction.Navigate);
                if (pathEntry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var operation in pathEntry.Value.EnumerateObject())
                {
                    switch (operation.Name)
                    {
                        case "get":
                            functions.Add(split.Length > 1 ? ApiFunction.ReadByKey : ApiFunction.Read);
                            break;
                        case "post":
                            functions.Add(ApiFunction.Create);
                            break;
                        case "patch":
                            functions.Add(ApiFunction.Update);
                            break;
                        case "delete":
                            functions.Add(ApiFunction.Delete);
                            break;
                        case "put":
                        case "options":
                        case "head":
                        case "trace":
                            warn($"Skipping unsupported operation '{operation.Name}'.");
                            break;
                        default:
                            break;
                    }
                }
            }

            return result.Count == 0 ? null : result;
        }
    }

    private static Dictionary<string, SortedSet<ApiFunction>> ReadFromMetadata(EdmModel model, Action<string> warn)
    {
        var result = new Dictionary<string, SortedSet<ApiFunction>>(StringComparer.Ordinal);
        foreach (var entitySet in model.Container?.EntitySets ?? [])
        {
            var functions = new SortedSet<ApiFunction> { ApiFunction.Navigate };
            if (IsReadByKeyPermitted(entitySet, warn))
            {
                functions.Add(ApiFunction.ReadByKey);
            }

            if (IsPermitted(entitySet, EdmTerms.ReadRestrictions, "Readable", warn))
            {
                functions.Add(ApiFunction.Read);
            }

            if (IsPermitted(entitySet, EdmTerms.InsertRestrictions, "Insertable", warn))
            {
                functions.Add(ApiFunction.Create);
            }

            if (IsPermitted(entitySet, EdmTerms.UpdateRestrictions, "Updatable", warn))
            {
                functions.Add(ApiFunction.Update);
            }

            if (IsPermitted(entitySet, EdmTerms.DeleteRestrictions, "Deletable", warn))
            {
                functions.Add(ApiFunction.Delete);
            }

            result[entitySet.Name] = functions;
        }

        return result;
    }

    private static bool IsPermitted(EdmEntitySet entitySet, string term, string property, Action<string> warn) =>
        Evaluate(entitySet, entitySet.Annotations.Find(term)?.Value as EdmRecordValue, term, property, warn);

    private static bool IsReadByKeyPermitted(EdmEntitySet entitySet, Action<string> warn)
    {
        if (entitySet.Annotations.Find(EdmTerms.ReadRestrictions)?.Value is EdmRecordValue record
            && record.Properties.TryGetValue("ReadByKeyRestrictions", out var nested)
            && nested is EdmRecordValue nestedRecord)
        {
            return Evaluate(entitySet, nestedRecord, EdmTerms.ReadRestrictions, "Readable", warn);
        }

        return true;
    }

    private static bool Evaluate(EdmEntitySet entitySet, EdmRecordValue? record, string term, string property, Action<string> warn)
    {
        if (record is null || !record.Properties.TryGetValue(property, out var value))
        {
            return true;
        }

        if (value is EdmBoolValue flag)
        {
            return flag.Value;
        }

        warn($"Annotation {term} of entity set {entitySet.Name} is in an unexpected format. Unable to determine if {property} operation is permitted.");
        return true;
    }
}
