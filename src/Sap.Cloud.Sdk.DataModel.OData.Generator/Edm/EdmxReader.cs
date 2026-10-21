using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// Reads an OData V2 EDMX document into an <see cref="EdmModel"/> and resolves type, association and entity set references.
/// Replaces Olingo's <c>EdmxProvider</c> / <c>EdmImplProv</c>; accepts every EDM namespace version of the CSDL.
/// </summary>
internal static class EdmxReader
{
    internal const string SapNamespace = "http://www.sap.com/Protocols/SAPData";

    private static readonly Dictionary<string, EdmSimpleKind> SimpleKinds = Enum
        .GetValues<EdmSimpleKind>()
        .ToDictionary(k => "Edm." + k.ToString(), k => k, StringComparer.Ordinal);

    /// <summary>Reads the file.</summary>
    /// <exception cref="ODataGeneratorReadException">The file is not well-formed or not a valid OData V2 model.</exception>
    internal static EdmModel ReadFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }
        catch (IOException e)
        {
            throw new ODataGeneratorReadException("The metadata file cannot be read: " + path, e);
        }
        catch (ODataGeneratorReadException e)
        {
            throw new ODataGeneratorReadException($"Parsing of the metadata file failed. {e.Message}", e);
        }
    }

    /// <summary>Reads the document.</summary>
    internal static EdmModel Read(Stream stream)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException e)
        {
            throw new ODataGeneratorReadException(
                "Please check that the metadata file is valid and belongs to an OData Version 2.0 service. " + e.Message,
                e);
        }

        return Read(document);
    }

    internal static EdmModel Read(XDocument document)
    {
        var root = document.Root;
        if (root is null || root.Name.LocalName != "Edmx")
        {
            throw new ODataGeneratorReadException("The document is not an EDMX document: the root element is not 'Edmx'.");
        }
        var dataServices = root.Elements().FirstOrDefault(e => e.Name.LocalName == "DataServices")
            ?? throw new ODataGeneratorReadException("The EDMX document has no 'DataServices' element.");
        var version = AttributeByLocalName(dataServices, "DataServiceVersion") ?? "";
        if (!IsSupportedDataServiceVersion(version))
        {
            throw new ODataGeneratorReadException(
                $"The metadata file is not an OData V2 service (DataServiceVersion '{version}'; expected 1.0 or 2.0).");
        }

        var model = new EdmModel { DataServiceVersion = version, Source = document };
        foreach (var schemaElement in dataServices.Elements().Where(e => e.Name.LocalName == "Schema"))
        {
            model.Schemas.Add(ReadSchema(schemaElement));
        }
        Resolve(model);
        return model;
    }

    internal static bool IsSupportedDataServiceVersion(string version) => version is "1.0" or "2.0";

    private static EdmSchema ReadSchema(XElement element)
    {
        var ns = (string?)element.Attribute("Namespace") ?? "";
        var schema = new EdmSchema
        {
            Namespace = ns,
            Alias = (string?)element.Attribute("Alias"),
            SelfLink = element.Elements()
                .Where(e => e.Name.LocalName == "link" && (string?)e.Attribute("rel") == "self")
                .Select(e => (string?)e.Attribute("href"))
                .FirstOrDefault(),
        };

        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "EntityType":
                    schema.EntityTypes.Add(ReadEntityType(child, ns));
                    break;
                case "ComplexType":
                    schema.ComplexTypes.Add(ReadComplexType(child, ns));
                    break;
                case "Association":
                    schema.Associations.Add(ReadAssociation(child, ns));
                    break;
                case "EntityContainer":
                    schema.Containers.Add(ReadContainer(child));
                    break;
                default:
                    break;
            }
        }
        return schema;
    }

    private static EdmEntityType ReadEntityType(XElement element, string ns)
    {
        var name = Required(element, "Name", "EntityType");
        var type = new EdmEntityType
        {
            Name = name,
            QualifiedName = Qualify(ns, name),
            Label = SapAttribute(element, "label"),
            QuickInfo = SapAttribute(element, "quickinfo"),
            Documentation = ReadDocumentation(element),
            HasStream = string.Equals(AttributeByLocalName(element, "HasStream"), "true", StringComparison.OrdinalIgnoreCase),
            BaseTypeName = (string?)element.Attribute("BaseType"),
        };
        foreach (var key in element.Elements().Where(e => e.Name.LocalName == "Key").SelectMany(k => k.Elements()).Where(e => e.Name.LocalName == "PropertyRef"))
        {
            type.DeclaredKeys.Add(Required(key, "Name", "PropertyRef"));
        }
        ReadProperties(element, type, name);
        foreach (var nav in element.Elements().Where(e => e.Name.LocalName == "NavigationProperty"))
        {
            var navName = Required(nav, "Name", "NavigationProperty");
            type.DeclaredNavigationProperties.Add(new EdmNavigationProperty
            {
                Name = navName,
                RelationshipName = Required(nav, "Relationship", "NavigationProperty " + navName),
                FromRole = Required(nav, "FromRole", "NavigationProperty " + navName),
                ToRole = Required(nav, "ToRole", "NavigationProperty " + navName),
                Label = SapAttribute(nav, "label"),
                QuickInfo = SapAttribute(nav, "quickinfo"),
                Documentation = ReadDocumentation(nav),
            });
        }
        return type;
    }

    private static EdmComplexType ReadComplexType(XElement element, string ns)
    {
        var name = Required(element, "Name", "ComplexType");
        var type = new EdmComplexType
        {
            Name = name,
            QualifiedName = Qualify(ns, name),
            Label = SapAttribute(element, "label"),
            QuickInfo = SapAttribute(element, "quickinfo"),
            Documentation = ReadDocumentation(element),
            BaseTypeName = (string?)element.Attribute("BaseType"),
        };
        ReadProperties(element, type, name);
        return type;
    }

    private static void ReadProperties(XElement element, EdmStructuralType type, string typeName)
    {
        foreach (var property in element.Elements().Where(e => e.Name.LocalName == "Property"))
        {
            var propertyName = Required(property, "Name", "Property of " + typeName);
            type.DeclaredProperties.Add(new EdmProperty
            {
                Name = propertyName,
                TypeName = Required(property, "Type", "Property " + propertyName),
                Label = SapAttribute(property, "label"),
                QuickInfo = SapAttribute(property, "quickinfo"),
                Documentation = ReadDocumentation(property),
                Facets = ReadFacets(property),
            });
        }
    }

    private static EdmFacets ReadFacets(XElement element) =>
        new(
            ParseBool((string?)element.Attribute("Nullable")),
            (string?)element.Attribute("DefaultValue"),
            ParseInt((string?)element.Attribute("MaxLength")),
            ParseInt((string?)element.Attribute("Precision")),
            ParseInt((string?)element.Attribute("Scale")));

    private static bool? ParseBool(string? text) => text is null ? null : string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);

    private static int? ParseInt(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static EdmAssociation ReadAssociation(XElement element, string ns)
    {
        var name = Required(element, "Name", "Association");
        var association = new EdmAssociation { Name = name, QualifiedName = Qualify(ns, name) };
        foreach (var end in element.Elements().Where(e => e.Name.LocalName == "End"))
        {
            var multiplicityText = (string?)end.Attribute("Multiplicity") ?? "*";
            var multiplicity = multiplicityText switch
            {
                "*" => EdmMultiplicity.Many,
                "1" => EdmMultiplicity.One,
                "0..1" => EdmMultiplicity.ZeroToOne,
                _ => throw new ODataGeneratorReadException($"Unknown multiplicity '{multiplicityText}' in association '{name}'."),
            };
            association.Ends.Add(new EdmAssociationEnd(
                Required(end, "Type", "End of association " + name),
                multiplicity,
                Required(end, "Role", "End of association " + name)));
        }
        return association;
    }

    private static EdmEntityContainer ReadContainer(XElement element)
    {
        var container = new EdmEntityContainer
        {
            Name = Required(element, "Name", "EntityContainer"),
            IsDefault = string.Equals(AttributeByLocalName(element, "IsDefaultEntityContainer"), "true", StringComparison.OrdinalIgnoreCase),
        };
        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "EntitySet":
                    var entitySet = new EdmEntitySet
                    {
                        Name = Required(child, "Name", "EntitySet"),
                        EntityTypeName = Required(child, "EntityType", "EntitySet " + (string?)child.Attribute("Name")),
                        Label = SapAttribute(child, "label"),
                        QuickInfo = SapAttribute(child, "quickinfo"),
                        Documentation = ReadDocumentation(child),
                    };
                    foreach (var attribute in child.Attributes().Where(a => a.Name.Namespace.NamespaceName == SapNamespace))
                    {
                        entitySet.SapAttributes[attribute.Name.LocalName] = attribute.Value;
                    }
                    container.EntitySets.Add(entitySet);
                    break;
                case "FunctionImport":
                    container.FunctionImports.Add(ReadFunctionImport(child));
                    break;
                default:
                    break;
            }
        }
        return container;
    }

    private static EdmFunctionImport ReadFunctionImport(XElement element)
    {
        var name = Required(element, "Name", "FunctionImport");
        var function = new EdmFunctionImport
        {
            Name = name,
            ReturnTypeName = (string?)element.Attribute("ReturnType"),
            HttpMethod = AttributeByLocalName(element, "HttpMethod"),
            Label = SapAttribute(element, "label"),
            QuickInfo = SapAttribute(element, "quickinfo"),
            Documentation = ReadDocumentation(element),
        };
        foreach (var parameter in element.Elements().Where(e => e.Name.LocalName == "Parameter"))
        {
            var parameterName = Required(parameter, "Name", "Parameter of function import " + name);
            function.Parameters.Add(new EdmParameter
            {
                Name = parameterName,
                TypeName = Required(parameter, "Type", "Parameter " + parameterName),
                Label = SapAttribute(parameter, "label"),
                QuickInfo = SapAttribute(parameter, "quickinfo"),
                Documentation = ReadDocumentation(parameter),
                Facets = ReadFacets(parameter),
            });
        }
        return function;
    }

    private static EdmDocumentation? ReadDocumentation(XElement element)
    {
        var documentation = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Documentation");
        if (documentation is null)
        {
            return null;
        }
        return new EdmDocumentation(
            documentation.Elements().FirstOrDefault(e => e.Name.LocalName == "Summary")?.Value,
            documentation.Elements().FirstOrDefault(e => e.Name.LocalName == "LongDescription")?.Value);
    }

    private static string Required(XElement element, string attribute, string context) =>
        (string?)element.Attribute(attribute)
        ?? throw new ODataGeneratorReadException($"The attribute '{attribute}' of '{context}' is missing.");

    private static string Qualify(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;

    private static string? SapAttribute(XElement element, string name) => (string?)element.Attribute(XName.Get(name, SapNamespace));

    private static string? AttributeByLocalName(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    // ----- Resolution -----------------------------------------------------------------------------------------------

    private static void Resolve(EdmModel model)
    {
        var types = new Dictionary<string, EdmType>(StringComparer.Ordinal);
        var associations = new Dictionary<string, EdmAssociation>(StringComparer.Ordinal);
        foreach (var schema in model.Schemas)
        {
            foreach (var type in schema.EntityTypes.Cast<EdmType>().Concat(schema.ComplexTypes))
            {
                types[type.QualifiedName] = type;
                if (!string.IsNullOrEmpty(schema.Alias))
                {
                    types[schema.Alias + "." + type.Name] = type;
                }
            }
            foreach (var association in schema.Associations)
            {
                associations[association.QualifiedName] = association;
                if (!string.IsNullOrEmpty(schema.Alias))
                {
                    associations[schema.Alias + "." + association.Name] = association;
                }
            }
        }

        // A name without namespace is looked up in the schema it appears in (lenient, like many hand-written metadata documents).
        EdmType ResolveType(string typeName, string context, string currentNamespace)
        {
            if (typeName.StartsWith("Edm.", StringComparison.Ordinal))
            {
                if (SimpleKinds.TryGetValue(typeName, out var kind))
                {
                    return new EdmSimpleType { Name = typeName[4..], QualifiedName = typeName, SimpleKind = kind };
                }
                throw new ODataGeneratorReadException($"The type '{typeName}' of {context} is not a supported primitive type.");
            }
            if (types.TryGetValue(typeName, out var type)
                || (!typeName.Contains('.', StringComparison.Ordinal) && types.TryGetValue(Qualify(currentNamespace, typeName), out type)))
            {
                return type;
            }
            throw new ODataGeneratorReadException($"The type '{typeName}' of {context} cannot be resolved.");
        }

        foreach (var schema in model.Schemas)
        {
            foreach (var structural in schema.EntityTypes.Cast<EdmStructuralType>().Concat(schema.ComplexTypes))
            {
                if (structural.BaseTypeName is not null)
                {
                    structural.BaseType = ResolveType(structural.BaseTypeName, $"the base type of '{structural.Name}'", schema.Namespace) as EdmStructuralType
                        ?? throw new ODataGeneratorReadException($"The base type '{structural.BaseTypeName}' of '{structural.Name}' is not a structured type.");
                }
            }
        }

        foreach (var schema in model.Schemas)
        {
            foreach (var structural in schema.EntityTypes.Cast<EdmStructuralType>().Concat(schema.ComplexTypes))
            {
                foreach (var property in structural.DeclaredProperties)
                {
                    property.Type = ResolveType(property.TypeName, $"the property '{property.Name}' of '{structural.Name}'", schema.Namespace);
                    if (property.Type.Kind == EdmTypeKind.Entity)
                    {
                        throw new ODataGeneratorReadException($"The property '{property.Name}' of '{structural.Name}' has the entity type '{property.TypeName}'.");
                    }
                }
            }

            foreach (var entity in schema.EntityTypes)
            {
                foreach (var navigation in entity.DeclaredNavigationProperties)
                {
                    if (!associations.TryGetValue(navigation.RelationshipName, out var association)
                        && !associations.TryGetValue(Qualify(schema.Namespace, navigation.RelationshipName), out association))
                    {
                        throw new ODataGeneratorReadException(
                            $"The association '{navigation.RelationshipName}' of the navigation property '{navigation.Name}' of '{entity.Name}' cannot be resolved.");
                    }
                    var end = association.Ends.FirstOrDefault(e => e.Role == navigation.ToRole)
                        ?? throw new ODataGeneratorReadException(
                            $"The association '{association.Name}' has no end with the role '{navigation.ToRole}' (navigation property '{navigation.Name}' of '{entity.Name}').");
                    navigation.TargetType = ResolveType(end.TypeName, $"the association end '{end.Role}'", schema.Namespace) as EdmEntityType
                        ?? throw new ODataGeneratorReadException($"The target '{end.TypeName}' of the navigation property '{navigation.Name}' is not an entity type.");
                    navigation.Multiplicity = end.Multiplicity;
                }

                var propertyNames = entity.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var key in entity.Keys)
                {
                    if (!propertyNames.Contains(key))
                    {
                        throw new ODataGeneratorReadException($"The key property '{key}' of the entity type '{entity.Name}' does not exist.");
                    }
                }
            }

            foreach (var container in schema.Containers)
            {
                foreach (var entitySet in container.EntitySets)
                {
                    entitySet.EntityType = ResolveType(entitySet.EntityTypeName, $"the entity set '{entitySet.Name}'", schema.Namespace) as EdmEntityType
                        ?? throw new ODataGeneratorReadException($"The type '{entitySet.EntityTypeName}' of the entity set '{entitySet.Name}' is not an entity type.");
                }

                foreach (var function in container.FunctionImports)
                {
                    if (function.ReturnTypeName is { } returnTypeName)
                    {
                        var isCollection = returnTypeName.StartsWith("Collection(", StringComparison.Ordinal) && returnTypeName.EndsWith(')');
                        var elementTypeName = isCollection ? returnTypeName["Collection(".Length..^1] : returnTypeName;
                        function.ReturnType = ResolveType(elementTypeName, $"the return type of the function import '{function.Name}'", schema.Namespace);
                        function.ReturnMultiplicity = isCollection ? EdmMultiplicity.Many : EdmMultiplicity.ZeroToOne;
                    }
                    foreach (var parameter in function.Parameters)
                    {
                        parameter.Type = ResolveType(parameter.TypeName, $"the parameter '{parameter.Name}' of the function import '{function.Name}'", schema.Namespace);
                    }
                }
            }
        }
    }
}
