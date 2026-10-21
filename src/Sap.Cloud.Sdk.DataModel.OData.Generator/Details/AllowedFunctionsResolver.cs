using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>The operations the generator can emit for an entity set. The order is the order of the generated service methods.</summary>
internal enum ApiFunction
{
    Read,
    ReadByKey,
    Create,
    Update,
    Delete,
}

/// <summary>
/// Determines which operations an entity set supports, from (in this priority) the swagger file, the OData capability
/// annotations of the metadata, or the SAP annotations <c>sap:creatable</c>, <c>sap:updatable</c>, <c>sap:deletable</c>,
/// <c>sap:addressable</c>. Port of <c>AllowedFunctionsResolver</c>.
/// </summary>
internal static partial class AllowedFunctionsResolver
{
    [GeneratedRegex("^(.+)\\.([^./]+)(?:/(.+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex AnnotationTargetPattern();

    /// <summary>The allowed functions by entity set name.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyCollection<ApiFunction>> FindAllowedFunctions(EdmModel model, string? swaggerPath, ICollection<string> warnings)
    {
        Dictionary<string, SortedSet<ApiFunction>>? result = null;
        if (swaggerPath is not null && File.Exists(swaggerPath))
        {
            result = ReadFromSwaggerFile(swaggerPath, warnings);
        }
        result ??= ReadOdataSpecFromMetadata(model);
        result ??= ReadSapSpecFromMetadata(model);
        return result.ToDictionary(e => e.Key, e => (IReadOnlyCollection<ApiFunction>)e.Value.ToList(), StringComparer.Ordinal);
    }

    private static Dictionary<string, SortedSet<ApiFunction>>? ReadFromSwaggerFile(string swaggerPath, ICollection<string> warnings)
    {
        var result = new Dictionary<string, SortedSet<ApiFunction>>(StringComparer.Ordinal);
        try
        {
            using var stream = File.OpenRead(swaggerPath);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("paths", out var paths)
                || paths.ValueKind != JsonValueKind.Object)
            {
                throw new ODataGeneratorReadException("The swagger file has no 'paths' object: " + swaggerPath);
            }

            foreach (var path in paths.EnumerateObject())
            {
                var key = path.Name.Length > 0 ? path.Name[1..] : path.Name;
                var split = key.Split('(');
                var entity = split[0];
                if (path.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                foreach (var operation in path.Value.EnumerateObject())
                {
                    switch (operation.Name)
                    {
                        case "get":
                            Add(result, entity, split.Length > 1 ? ApiFunction.ReadByKey : ApiFunction.Read);
                            break;
                        case "post":
                            Add(result, entity, ApiFunction.Create);
                            break;
                        case "patch":
                            Add(result, entity, ApiFunction.Update);
                            break;
                        case "delete":
                            Add(result, entity, ApiFunction.Delete);
                            break;
                        case "put" or "options" or "head" or "trace":
                            warnings.Add($"Skipping unsupported operation '{operation.Name}' of the path '{path.Name}' in the swagger file.");
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new ODataGeneratorReadException("The swagger file cannot be read: " + swaggerPath, e);
        }
        return result.Count == 0 ? null : result;
    }

    private static void Add(Dictionary<string, SortedSet<ApiFunction>> result, string entity, ApiFunction function)
    {
        if (!result.TryGetValue(entity, out var functions))
        {
            functions = [];
            result[entity] = functions;
        }
        functions.Add(function);
    }

    private static Dictionary<string, SortedSet<ApiFunction>>? ReadOdataSpecFromMetadata(EdmModel model)
    {
        if (model.Source?.Root is not { } root)
        {
            return null;
        }
        var annotationsList = root.Descendants().Where(e => e.Name.LocalName == "Annotations").ToList();
        if (annotationsList.Count == 0)
        {
            return null;
        }

        var entitySetNames = model.EntitySets.Select(s => s.Name).ToList();
        var definedPerEntitySet = new Dictionary<string, List<(string Name, string Value)>>(StringComparer.Ordinal);
        foreach (var annotations in annotationsList)
        {
            var target = (string?)annotations.Attribute("Target");
            if (target is null)
            {
                continue;
            }
            var entitySet = TargetedEntitySet(target, entitySetNames);
            if (entitySet is null)
            {
                continue;
            }
            if (!definedPerEntitySet.TryGetValue(entitySet, out var defined))
            {
                defined = [];
                definedPerEntitySet[entitySet] = defined;
            }
            defined.AddRange(ReadDefinedProperties(annotations));
        }

        if (definedPerEntitySet.Values.All(v => v.Count == 0))
        {
            return null;
        }

        var result = new Dictionary<string, SortedSet<ApiFunction>>(StringComparer.Ordinal);
        foreach (var entitySetName in entitySetNames)
        {
            var supported = new SortedSet<ApiFunction>(Enum.GetValues<ApiFunction>());
            if (definedPerEntitySet.TryGetValue(entitySetName, out var defined))
            {
                foreach (var (name, value) in defined)
                {
                    if (value == "false" && UnsupportedFunction(name) is { } unsupported)
                    {
                        supported.Remove(unsupported);
                    }
                }
            }
            result[entitySetName] = supported;
        }
        return result;
    }

    private static ApiFunction? UnsupportedFunction(string propertyName) => propertyName switch
    {
        "readable" => ApiFunction.Read,
        "insertable" => ApiFunction.Create,
        "deletable" => ApiFunction.Delete,
        "updatable" => ApiFunction.Update,
        _ => null,
    };

    /// <summary>The entity set an annotation targets, or <see langword="null"/> if it targets something else (a property, for example).</summary>
    private static string? TargetedEntitySet(string annotationTarget, IReadOnlyCollection<string> entitySetNames)
    {
        var match = AnnotationTargetPattern().Match(annotationTarget);
        if (!match.Success)
        {
            return null;
        }
        var maybeEntitySet = match.Groups[2].Value;
        var third = match.Groups[3].Success ? match.Groups[3].Value : null;
        if (entitySetNames.Contains(maybeEntitySet))
        {
            // Namespace.EntitySet or Namespace.EntitySet/Property
            return string.IsNullOrWhiteSpace(third) ? maybeEntitySet : null;
        }
        // Namespace.Container/EntitySet
        return string.IsNullOrWhiteSpace(third) ? null : third;
    }

    private static IEnumerable<(string Name, string Value)> ReadDefinedProperties(XElement annotations)
    {
        foreach (var annotation in annotations.Elements().Where(e => string.Equals(e.Name.LocalName, "Annotation", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var record in annotation.Elements().Where(e => string.Equals(e.Name.LocalName, "Record", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var propertyValue in record.Elements().Where(e => string.Equals(e.Name.LocalName, "PropertyValue", StringComparison.OrdinalIgnoreCase)))
                {
                    var name = ((string?)propertyValue.Attribute("Property"))?.ToLowerInvariant();
                    var value = ((string?)propertyValue.Attribute("Bool"))?.ToLowerInvariant();
                    if (name is not null && value is not null)
                    {
                        yield return (name, value);
                    }
                }
            }
        }
    }

    private static Dictionary<string, SortedSet<ApiFunction>> ReadSapSpecFromMetadata(EdmModel model)
    {
        var result = new Dictionary<string, SortedSet<ApiFunction>>(StringComparer.Ordinal);
        foreach (var entitySet in model.EntitySets)
        {
            var functions = new SortedSet<ApiFunction> { ApiFunction.ReadByKey };
            if (AttributeValue("addressable", entitySet))
            {
                functions.Add(ApiFunction.Read);
            }
            if (AttributeValue("creatable", entitySet))
            {
                functions.Add(ApiFunction.Create);
            }
            if (AttributeValue("deletable", entitySet))
            {
                functions.Add(ApiFunction.Delete);
            }
            if (AttributeValue("updatable", entitySet))
            {
                functions.Add(ApiFunction.Update);
            }
            result[entitySet.Name] = functions;
        }
        return result;
    }

    // The default of the SAP annotations is true.
    private static bool AttributeValue(string attributeName, EdmEntitySet entitySet)
    {
        if (!entitySet.SapAttributes.TryGetValue(attributeName, out var text))
        {
            return true;
        }
        return text switch
        {
            "true" => true,
            "false" => false,
            _ => throw new ODataGeneratorReadException($"Could not parse attribute '{attributeName}' with value '{text}'"),
        };
    }
}
