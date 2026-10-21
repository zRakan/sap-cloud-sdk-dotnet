namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;

/// <summary>The primitive types of the EDM that the generator distinguishes.</summary>
internal enum EdmPrimitiveKind
{
    Binary,
    Boolean,
    Byte,
    Date,
    DateTimeOffset,
    Decimal,
    Double,
    Duration,
    Guid,
    Int16,
    Int32,
    Int64,
    SByte,
    Single,
    Stream,
    String,
    TimeOfDay,

    /// <summary>Geography and geometry types and the abstract primitive types: not supported by the runtime.</summary>
    Unsupported,
}

/// <summary>The value of an annotation expression.</summary>
internal abstract record EdmValue;

internal sealed record EdmStringValue(string Value) : EdmValue;

internal sealed record EdmBoolValue(bool Value) : EdmValue;

/// <summary>A constant of the expected kind whose text is not valid.</summary>
internal sealed record EdmInvalidValue(string Text) : EdmValue;

internal sealed record EdmRecordValue(IReadOnlyDictionary<string, EdmValue> Properties) : EdmValue;

/// <summary>One annotation; <see cref="Term"/> is the qualified term name with the namespace (not the alias) resolved.</summary>
internal sealed record EdmAnnotation(string Term, EdmValue? Value);

/// <summary>The terms of the vocabularies the generator reads.</summary>
internal static class EdmTerms
{
    public const string CoreNamespace = "Org.OData.Core.V1";
    public const string CommonNamespace = "com.sap.vocabularies.Common.v1";
    public const string CapabilitiesNamespace = "Org.OData.Capabilities.V1";

    public const string Description = CoreNamespace + ".Description";
    public const string LongDescription = CoreNamespace + ".LongDescription";
    public const string Label = CommonNamespace + ".Label";
    public const string QuickInfo = CommonNamespace + ".QuickInfo";
    public const string ReadRestrictions = CapabilitiesNamespace + ".ReadRestrictions";
    public const string InsertRestrictions = CapabilitiesNamespace + ".InsertRestrictions";
    public const string UpdateRestrictions = CapabilitiesNamespace + ".UpdateRestrictions";
    public const string DeleteRestrictions = CapabilitiesNamespace + ".DeleteRestrictions";
}

/// <summary>The annotations of an EDM element.</summary>
internal sealed class EdmAnnotations
{
    private readonly List<EdmAnnotation> _items = [];

    public IReadOnlyList<EdmAnnotation> Items => _items;

    public void Add(EdmAnnotation annotation) => _items.Add(annotation);

    public EdmAnnotation? Find(string term) => _items.FirstOrDefault(a => string.Equals(a.Term, term, StringComparison.Ordinal));

    public string? GetString(string term) => Find(term)?.Value is EdmStringValue s ? s.Value : null;

    public string? Label => GetString(EdmTerms.Label);

    public string? QuickInfo => GetString(EdmTerms.QuickInfo);

    public string? Description => GetString(EdmTerms.Description);

    public string? LongDescription => GetString(EdmTerms.LongDescription);
}

internal abstract class EdmAnnotatable
{
    public EdmAnnotations Annotations { get; } = new();
}

/// <summary>The facets of a property, parameter or return type.</summary>
internal sealed record EdmFacets(bool Nullable, string? DefaultValue, int? MaxLength, int? Precision, int? Scale);

/// <summary>A resolved type reference.</summary>
internal sealed record EdmTypeReference(EdmType Type, bool IsCollection);

internal abstract class EdmType : EdmAnnotatable
{
    protected EdmType(string name, string ns)
    {
        Name = name;
        Namespace = ns;
    }

    public string Name { get; }

    public string Namespace { get; }

    public string FullName => Namespace.Length == 0 ? Name : Namespace + "." + Name;
}

internal sealed class EdmPrimitiveType(EdmPrimitiveKind kind, string name) : EdmType(name, "Edm")
{
    public EdmPrimitiveKind Kind { get; } = kind;

    public bool IsSupported => Kind != EdmPrimitiveKind.Unsupported && Kind != EdmPrimitiveKind.Stream;
}

internal sealed class EdmEnumMember(string name, string? value) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string? Value { get; } = value;
}

internal sealed class EdmEnumType(string name, string ns) : EdmType(name, ns)
{
    public bool IsFlags { get; set; }

    public List<EdmEnumMember> Members { get; } = [];
}

internal sealed class EdmTypeDefinition(string name, string ns, string underlyingType) : EdmType(name, ns)
{
    public string UnderlyingType { get; } = underlyingType;
}

internal sealed class EdmProperty(string name, string rawType, EdmFacets facets) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string RawType { get; } = rawType;

    public EdmFacets Facets { get; } = facets;
}

internal sealed class EdmNavigationProperty(string name, string rawType, bool nullable) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string RawType { get; } = rawType;

    public bool Nullable { get; } = nullable;
}

internal sealed record EdmKeyRef(string Name, string? Alias)
{
    public string EffectiveName => Alias ?? Name;
}

internal abstract class EdmStructuredType(string name, string ns) : EdmType(name, ns)
{
    public string? BaseTypeName { get; set; }

    public EdmStructuredType? BaseType { get; set; }

    public bool IsAbstract { get; set; }

    public bool HasStream { get; set; }

    public List<EdmProperty> OwnProperties { get; } = [];

    public List<EdmNavigationProperty> OwnNavigationProperties { get; } = [];

    /// <summary>The properties including the inherited ones, base type first (as Olingo returns them).</summary>
    public IEnumerable<EdmProperty> AllProperties() =>
        (BaseType?.AllProperties() ?? []).Concat(OwnProperties);

    /// <summary>The navigation properties including the inherited ones, base type first.</summary>
    public IEnumerable<EdmNavigationProperty> AllNavigationProperties() =>
        (BaseType?.AllNavigationProperties() ?? []).Concat(OwnNavigationProperties);
}

internal sealed class EdmComplexType(string name, string ns) : EdmStructuredType(name, ns);

internal sealed class EdmEntityType(string name, string ns) : EdmStructuredType(name, ns)
{
    public List<EdmKeyRef> OwnKey { get; } = [];

    /// <summary>The key including the inherited one.</summary>
    public IReadOnlyList<EdmKeyRef> Key =>
        OwnKey.Count > 0 || BaseType is not EdmEntityType baseEntity ? OwnKey : baseEntity.Key;
}

internal sealed class EdmParameter(string name, string rawType, EdmFacets facets) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string RawType { get; } = rawType;

    public EdmFacets Facets { get; } = facets;
}

internal sealed record EdmReturnType(string RawType, EdmFacets Facets);

internal sealed class EdmOperation(string name, string ns, bool isFunction) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string Namespace { get; } = ns;

    public string QualifiedName => Namespace + "." + Name;

    public bool IsFunction { get; } = isFunction;

    public bool IsBound { get; set; }

    public bool IsComposable { get; set; }

    public string? EntitySetPath { get; set; }

    public List<EdmParameter> Parameters { get; } = [];

    public EdmReturnType? ReturnType { get; set; }

    public EdmParameter? BindingParameter => IsBound && Parameters.Count > 0 ? Parameters[0] : null;

    /// <summary>The parameters without the binding parameter.</summary>
    public IEnumerable<EdmParameter> OperationParameters => Parameters.Skip(IsBound ? 1 : 0);
}

internal sealed class EdmEntitySet(string name, string entityTypeName) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string EntityTypeName { get; } = entityTypeName;
}

internal sealed class EdmFunctionImport(string name, string functionName) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string FunctionName { get; } = functionName;
}

internal sealed class EdmActionImport(string name, string actionName) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string ActionName { get; } = actionName;
}

internal sealed class EdmEntityContainer(string name, string ns) : EdmAnnotatable
{
    public string Name { get; } = name;

    public string Namespace { get; } = ns;

    public bool IsDefault { get; set; }

    public List<EdmEntitySet> EntitySets { get; } = [];

    public List<string> Singletons { get; } = [];

    public List<EdmFunctionImport> FunctionImports { get; } = [];

    public List<EdmActionImport> ActionImports { get; } = [];
}

internal sealed class EdmSchema(string ns, string? alias)
{
    public string Namespace { get; } = ns;

    public string? Alias { get; } = alias;

    public List<EdmEntityType> EntityTypes { get; } = [];

    public List<EdmComplexType> ComplexTypes { get; } = [];

    public List<EdmEnumType> EnumTypes { get; } = [];

    public List<EdmTypeDefinition> TypeDefinitions { get; } = [];

    public List<EdmOperation> Operations { get; } = [];

    public List<EdmEntityContainer> Containers { get; } = [];
}

/// <summary>An EDM model that was read from a CSDL document (the internal metadata model of the generator).</summary>
internal sealed class EdmModel
{
    private static readonly Dictionary<string, EdmPrimitiveType> s_primitives = CreatePrimitives();

    private readonly Dictionary<string, EdmType> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _namespaceByAlias = new(StringComparer.Ordinal);

    public List<EdmSchema> Schemas { get; } = [];

    /// <summary>Observations that are not errors, for example skipped constructs.</summary>
    public List<string> Notes { get; } = [];

    /// <summary>The default entity container (the one used by the generator), if there is one.</summary>
    public EdmEntityContainer? Container =>
        Schemas.SelectMany(s => s.Containers).FirstOrDefault(c => c.IsDefault)
        ?? Schemas.SelectMany(s => s.Containers).FirstOrDefault();

    public IEnumerable<EdmOperation> Operations => Schemas.SelectMany(s => s.Operations);

    public void RegisterAlias(string alias, string ns) => _namespaceByAlias[alias] = ns;

    public string ResolveNamespace(string aliasOrNamespace) =>
        _namespaceByAlias.TryGetValue(aliasOrNamespace, out var ns) ? ns : aliasOrNamespace;

    /// <summary>Resolves an alias-qualified name to a namespace-qualified one.</summary>
    /// <param name="qualifiedName">The name, for example <c>self.Person</c>.</param>
    /// <returns>The name with the namespace.</returns>
    public string ResolveQualifiedName(string qualifiedName)
    {
        var dot = qualifiedName.LastIndexOf('.');
        return dot < 0 ? qualifiedName : ResolveNamespace(qualifiedName[..dot]) + qualifiedName[dot..];
    }

    public void AddType(EdmType type)
    {
        _types[type.FullName] = type;
    }

    public EdmType? FindType(string qualifiedName)
    {
        var name = ResolveQualifiedName(qualifiedName);
        if (_types.TryGetValue(name, out var type))
        {
            return type;
        }

        return name.StartsWith("Edm.", StringComparison.Ordinal) && s_primitives.TryGetValue(name[4..], out var primitive)
            ? primitive
            : null;
    }

    /// <summary>Resolves the type reference of a property, parameter or return type.</summary>
    /// <param name="rawType">The type as written in the CSDL, for example <c>Collection(Edm.String)</c>.</param>
    /// <returns>The type; type definitions are replaced by their underlying type.</returns>
    /// <exception cref="ODataGeneratorReadException">The type is unknown.</exception>
    public EdmTypeReference ResolveType(string rawType)
    {
        var isCollection = false;
        var name = rawType.Trim();
        if (name.StartsWith("Collection(", StringComparison.Ordinal) && name.EndsWith(')'))
        {
            isCollection = true;
            name = name["Collection(".Length..^1].Trim();
        }

        var type = FindType(name) ?? throw new ODataGeneratorReadException($"Unknown type '{rawType}'.");
        var guard = 0;
        while (type is EdmTypeDefinition definition)
        {
            if (++guard > 16)
            {
                throw new ODataGeneratorReadException($"Type definition '{rawType}' is cyclic.");
            }

            type = FindType(definition.UnderlyingType)
                ?? throw new ODataGeneratorReadException($"Unknown underlying type '{definition.UnderlyingType}' of type definition '{definition.FullName}'.");
        }

        return new EdmTypeReference(type, isCollection);
    }

    private static Dictionary<string, EdmPrimitiveType> CreatePrimitives()
    {
        var result = new Dictionary<string, EdmPrimitiveType>(StringComparer.Ordinal);
        foreach (var kind in Enum.GetValues<EdmPrimitiveKind>())
        {
            if (kind != EdmPrimitiveKind.Unsupported)
            {
                result[kind.ToString()] = new EdmPrimitiveType(kind, kind.ToString());
            }
        }

        foreach (var name in new[]
        {
            "Geography", "GeographyPoint", "GeographyLineString", "GeographyPolygon", "GeographyMultiPoint",
            "GeographyMultiLineString", "GeographyMultiPolygon", "GeographyCollection", "Geometry", "GeometryPoint",
            "GeometryLineString", "GeometryPolygon", "GeometryMultiPoint", "GeometryMultiLineString",
            "GeometryMultiPolygon", "GeometryCollection", "PrimitiveType", "Untyped", "ComplexType", "EntityType",
        })
        {
            result[name] = new EdmPrimitiveType(EdmPrimitiveKind.Unsupported, name);
        }

        return result;
    }
}
