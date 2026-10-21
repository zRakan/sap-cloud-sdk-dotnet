using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;

/// <summary>Reads an OData v4 EDMX/CSDL (XML) document into an <see cref="EdmModel"/>.</summary>
internal static class CsdlReader
{
    private static readonly (string Alias, string Namespace)[] s_defaultAliases =
    [
        ("Core", EdmTerms.CoreNamespace),
        ("SAP__core", EdmTerms.CoreNamespace),
        ("SAP__Core", EdmTerms.CoreNamespace),
        ("Common", EdmTerms.CommonNamespace),
        ("SAP__common", EdmTerms.CommonNamespace),
        ("SAP__Common", EdmTerms.CommonNamespace),
        ("Capabilities", EdmTerms.CapabilitiesNamespace),
        ("SAP__capabilities", EdmTerms.CapabilitiesNamespace),
        ("SAP__Capabilities", EdmTerms.CapabilitiesNamespace),
    ];

    /// <summary>Checks whether the document is an EDMX document of OData version 4 (<c>Edmx/@Version</c> starts with <c>4</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> for OData v4.</returns>
    public static bool IsODataV4(XDocument document)
    {
        var edmx = document.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Edmx", StringComparison.OrdinalIgnoreCase));
        var version = edmx?.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase))?.Value;
        return version is not null && version.Split('.')[0] == "4";
    }

    /// <summary>Loads an XML document without DTD processing.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ODataGeneratorReadException">The file is not well-formed XML.</exception>
    public static async Task<XDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            Async = true,
        };
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                using var reader = XmlReader.Create(stream, settings);
                return await XDocument.LoadAsync(reader, LoadOptions.SetLineInfo, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            throw new ODataGeneratorReadException("Problem reading metadata file " + path, e);
        }
    }

    /// <summary>Reads the model.</summary>
    /// <param name="document">The EDMX document.</param>
    /// <param name="requireODataV4">Whether a document that does not declare OData version 4 is rejected.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ODataGeneratorReadException">The document is not a valid OData v4 EDMX document.</exception>
    public static EdmModel Read(XDocument document, bool requireODataV4 = true)
    {
        if (requireODataV4 && !IsODataV4(document))
        {
            throw new ODataGeneratorReadException("Metadata file being read is not an OData V4 service.");
        }

        var model = new EdmModel();
        foreach (var (alias, ns) in s_defaultAliases)
        {
            model.RegisterAlias(alias, ns);
        }

        var root = document.Root!;
        foreach (var include in root.Descendants().Where(e => e.Name.LocalName == "Include"))
        {
            var alias = include.Attribute("Alias")?.Value;
            var ns = include.Attribute("Namespace")?.Value;
            if (!string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(ns))
            {
                model.RegisterAlias(alias, ns);
            }
        }

        var schemaElements = root.Descendants().Where(e => e.Name.LocalName == "Schema").ToList();
        foreach (var schemaElement in schemaElements)
        {
            var ns = schemaElement.Attribute("Namespace")?.Value;
            var alias = schemaElement.Attribute("Alias")?.Value;
            if (!string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(alias))
            {
                model.RegisterAlias(alias, ns);
            }
        }

        var externalAnnotations = new List<(XElement Element, string Namespace)>();
        foreach (var schemaElement in schemaElements)
        {
            var ns = schemaElement.Attribute("Namespace")?.Value ?? string.Empty;
            var schema = new EdmSchema(ns, schemaElement.Attribute("Alias")?.Value);
            model.Schemas.Add(schema);
            ReadSchema(model, schema, schemaElement, externalAnnotations);
        }

        Link(model);
        foreach (var (element, ns) in externalAnnotations)
        {
            ApplyExternalAnnotations(model, element, ns);
        }

        return model;
    }

    private static void ReadSchema(EdmModel model, EdmSchema schema, XElement element, List<(XElement, string)> externalAnnotations)
    {
        var ns = schema.Namespace;
        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "EntityType":
                    var entity = new EdmEntityType(Required(child, "Name"), ns)
                    {
                        BaseTypeName = child.Attribute("BaseType")?.Value,
                        IsAbstract = Flag(child, "Abstract"),
                        HasStream = Flag(child, "HasStream"),
                    };
                    ReadStructuredMembers(model, entity, child);
                    var key = child.Elements().FirstOrDefault(e => e.Name.LocalName == "Key");
                    if (key is not null)
                    {
                        foreach (var propertyRef in key.Elements().Where(e => e.Name.LocalName == "PropertyRef"))
                        {
                            entity.OwnKey.Add(new EdmKeyRef(Required(propertyRef, "Name"), propertyRef.Attribute("Alias")?.Value));
                        }
                    }

                    schema.EntityTypes.Add(entity);
                    model.AddType(entity);
                    break;
                case "ComplexType":
                    var complex = new EdmComplexType(Required(child, "Name"), ns)
                    {
                        BaseTypeName = child.Attribute("BaseType")?.Value,
                        IsAbstract = Flag(child, "Abstract"),
                    };
                    ReadStructuredMembers(model, complex, child);
                    schema.ComplexTypes.Add(complex);
                    model.AddType(complex);
                    break;
                case "EnumType":
                    var enumType = new EdmEnumType(Required(child, "Name"), ns) { IsFlags = Flag(child, "IsFlags") };
                    foreach (var member in child.Elements().Where(e => e.Name.LocalName == "Member"))
                    {
                        var enumMember = new EdmEnumMember(Required(member, "Name"), member.Attribute("Value")?.Value);
                        ReadAnnotations(model, member, enumMember.Annotations);
                        enumType.Members.Add(enumMember);
                    }

                    ReadAnnotations(model, child, enumType.Annotations);
                    schema.EnumTypes.Add(enumType);
                    model.AddType(enumType);
                    break;
                case "TypeDefinition":
                    var definition = new EdmTypeDefinition(Required(child, "Name"), ns, Required(child, "UnderlyingType"));
                    ReadAnnotations(model, child, definition.Annotations);
                    schema.TypeDefinitions.Add(definition);
                    model.AddType(definition);
                    break;
                case "Function":
                case "Action":
                    schema.Operations.Add(ReadOperation(model, child, ns));
                    break;
                case "EntityContainer":
                    schema.Containers.Add(ReadContainer(model, child, ns));
                    break;
                case "Annotations":
                    externalAnnotations.Add((child, ns));
                    break;
                default:
                    break;
            }
        }
    }

    private static void ReadStructuredMembers(EdmModel model, EdmStructuredType type, XElement element)
    {
        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Property":
                    var property = new EdmProperty(Required(child, "Name"), Required(child, "Type"), ReadFacets(child));
                    ReadAnnotations(model, child, property.Annotations);
                    type.OwnProperties.Add(property);
                    break;
                case "NavigationProperty":
                    var navigation = new EdmNavigationProperty(
                        Required(child, "Name"),
                        Required(child, "Type"),
                        !string.Equals(child.Attribute("Nullable")?.Value, "false", StringComparison.OrdinalIgnoreCase));
                    ReadAnnotations(model, child, navigation.Annotations);
                    type.OwnNavigationProperties.Add(navigation);
                    break;
                default:
                    break;
            }
        }

        ReadAnnotations(model, element, type.Annotations);
    }

    private static EdmOperation ReadOperation(EdmModel model, XElement element, string ns)
    {
        var operation = new EdmOperation(Required(element, "Name"), ns, element.Name.LocalName == "Function")
        {
            IsBound = Flag(element, "IsBound"),
            IsComposable = Flag(element, "IsComposable"),
            EntitySetPath = element.Attribute("EntitySetPath")?.Value,
        };
        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Parameter":
                    var parameter = new EdmParameter(Required(child, "Name"), Required(child, "Type"), ReadFacets(child));
                    ReadAnnotations(model, child, parameter.Annotations);
                    operation.Parameters.Add(parameter);
                    break;
                case "ReturnType":
                    operation.ReturnType = new EdmReturnType(Required(child, "Type"), ReadFacets(child));
                    break;
                default:
                    break;
            }
        }

        ReadAnnotations(model, element, operation.Annotations);
        return operation;
    }

    private static EdmEntityContainer ReadContainer(EdmModel model, XElement element, string ns)
    {
        var container = new EdmEntityContainer(Required(element, "Name"), ns)
        {
            IsDefault = element.Attributes().Any(a =>
                a.Name.LocalName == "IsDefaultEntityContainer" && string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase)),
        };
        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "EntitySet":
                    var entitySet = new EdmEntitySet(Required(child, "Name"), Required(child, "EntityType"));
                    ReadAnnotations(model, child, entitySet.Annotations);
                    container.EntitySets.Add(entitySet);
                    break;
                case "Singleton":
                    container.Singletons.Add(Required(child, "Name"));
                    break;
                case "FunctionImport":
                    var functionImport = new EdmFunctionImport(Required(child, "Name"), Required(child, "Function"));
                    ReadAnnotations(model, child, functionImport.Annotations);
                    container.FunctionImports.Add(functionImport);
                    break;
                case "ActionImport":
                    var actionImport = new EdmActionImport(Required(child, "Name"), Required(child, "Action"));
                    ReadAnnotations(model, child, actionImport.Annotations);
                    container.ActionImports.Add(actionImport);
                    break;
                default:
                    break;
            }
        }

        ReadAnnotations(model, element, container.Annotations);
        return container;
    }

    private static EdmFacets ReadFacets(XElement element) =>
        new(
            !string.Equals(element.Attribute("Nullable")?.Value, "false", StringComparison.OrdinalIgnoreCase),
            element.Attribute("DefaultValue")?.Value,
            ParseInt(element.Attribute("MaxLength")?.Value),
            ParseInt(element.Attribute("Precision")?.Value),
            ParseInt(element.Attribute("Scale")?.Value));

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static bool Flag(XElement element, string attribute) =>
        string.Equals(element.Attribute(attribute)?.Value, "true", StringComparison.OrdinalIgnoreCase);

    private static string Required(XElement element, string attribute) =>
        element.Attribute(attribute)?.Value
        ?? throw new ODataGeneratorReadException(
            $"Element '{element.Name.LocalName}' is missing the attribute '{attribute}'{LineInfo(element)}.");

    private static string LineInfo(XElement element) =>
        element is IXmlLineInfo info && info.HasLineInfo() ? $" (line {info.LineNumber})" : string.Empty;

    private static void ReadAnnotations(EdmModel model, XElement element, EdmAnnotations target)
    {
        foreach (var annotation in element.Elements().Where(e => e.Name.LocalName == "Annotation"))
        {
            var term = annotation.Attribute("Term")?.Value;
            if (string.IsNullOrEmpty(term))
            {
                continue;
            }

            target.Add(new EdmAnnotation(model.ResolveQualifiedName(term), ReadValue(annotation)));
        }
    }

    private static EdmValue? ReadValue(XElement element)
    {
        var stringAttribute = element.Attribute("String");
        if (stringAttribute is not null)
        {
            return new EdmStringValue(stringAttribute.Value);
        }

        var boolAttribute = element.Attribute("Bool");
        if (boolAttribute is not null)
        {
            return bool.TryParse(boolAttribute.Value, out var b) ? new EdmBoolValue(b) : new EdmInvalidValue(boolAttribute.Value);
        }

        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "String":
                    return new EdmStringValue(child.Value);
                case "Bool":
                    return bool.TryParse(child.Value.Trim(), out var b) ? new EdmBoolValue(b) : new EdmInvalidValue(child.Value);
                case "Record":
                    var properties = new Dictionary<string, EdmValue>(StringComparer.Ordinal);
                    foreach (var propertyValue in child.Elements().Where(e => e.Name.LocalName == "PropertyValue"))
                    {
                        var name = propertyValue.Attribute("Property")?.Value;
                        var value = ReadValue(propertyValue);
                        if (!string.IsNullOrEmpty(name) && value is not null)
                        {
                            properties[name] = value;
                        }
                    }

                    return new EdmRecordValue(properties);
                default:
                    break;
            }
        }

        return null;
    }

    private static void Link(EdmModel model)
    {
        foreach (var schema in model.Schemas)
        {
            foreach (var type in schema.EntityTypes.Cast<EdmStructuredType>().Concat(schema.ComplexTypes))
            {
                if (type.BaseTypeName is null)
                {
                    continue;
                }

                type.BaseType = model.FindType(type.BaseTypeName) as EdmStructuredType
                    ?? throw new ODataGeneratorReadException($"The base type '{type.BaseTypeName}' of '{type.FullName}' could not be found.");
            }
        }

        foreach (var type in model.Schemas.SelectMany(s => s.EntityTypes.Cast<EdmStructuredType>().Concat(s.ComplexTypes)))
        {
            var seen = new HashSet<EdmStructuredType>();
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (!seen.Add(current))
                {
                    throw new ODataGeneratorReadException($"The base type hierarchy of '{type.FullName}' is cyclic.");
                }
            }
        }
    }

    private static void ApplyExternalAnnotations(EdmModel model, XElement element, string schemaNamespace)
    {
        var target = element.Attribute("Target")?.Value;
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        var annotatables = ResolveTarget(model, target, schemaNamespace).ToList();
        foreach (var annotatable in annotatables)
        {
            ReadAnnotations(model, element, annotatable.Annotations);
        }
    }

    private static IEnumerable<EdmAnnotatable> ResolveTarget(EdmModel model, string target, string schemaNamespace)
    {
        var parenthesis = target.IndexOf('(', StringComparison.Ordinal);
        var signatureFree = parenthesis < 0 ? target : target[..parenthesis];
        var closing = parenthesis < 0 ? -1 : target.LastIndexOf(')');
        var tail = closing > 0 && closing + 1 < target.Length ? target[(closing + 1)..] : string.Empty;
        var segments = (signatureFree + tail).Split('/');

        var qualified = model.ResolveQualifiedName(segments[0].Contains('.', StringComparison.Ordinal) ? segments[0] : schemaNamespace + "." + segments[0]);
        var memberName = segments.Length > 1 ? segments[1] : null;

        if (model.FindType(qualified) is { } type)
        {
            if (memberName is null)
            {
                yield return type;
                yield break;
            }

            switch (type)
            {
                case EdmStructuredType structured:
                    foreach (var property in structured.AllProperties().Where(p => p.Name == memberName))
                    {
                        yield return property;
                    }

                    foreach (var navigation in structured.AllNavigationProperties().Where(p => p.Name == memberName))
                    {
                        yield return navigation;
                    }

                    break;
                case EdmEnumType enumType:
                    foreach (var member in enumType.Members.Where(m => m.Name == memberName))
                    {
                        yield return member;
                    }

                    break;
                default:
                    break;
            }

            yield break;
        }

        foreach (var container in model.Schemas.SelectMany(s => s.Containers).Where(c => c.Namespace + "." + c.Name == qualified))
        {
            if (memberName is null)
            {
                yield return container;
                continue;
            }

            foreach (var set in container.EntitySets.Where(s => s.Name == memberName))
            {
                yield return set;
            }

            foreach (var import in container.FunctionImports.Where(s => s.Name == memberName))
            {
                yield return import;
            }

            foreach (var import in container.ActionImports.Where(s => s.Name == memberName))
            {
                yield return import;
            }
        }

        foreach (var operation in model.Operations.Where(o => o.QualifiedName == qualified))
        {
            if (memberName is null)
            {
                yield return operation;
                continue;
            }

            foreach (var parameter in operation.Parameters.Where(p => p.Name == memberName))
            {
                yield return parameter;
            }
        }
    }
}
