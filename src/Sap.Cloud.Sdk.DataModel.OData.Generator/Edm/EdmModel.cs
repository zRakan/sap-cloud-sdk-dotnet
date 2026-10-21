namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>The cardinality of an association end or a return type.</summary>
internal enum EdmMultiplicity
{
    ZeroToOne,
    One,
    Many,
}

/// <summary>The kinds of types in an OData V2 model.</summary>
internal enum EdmTypeKind
{
    Simple,
    Complex,
    Entity,
}

/// <summary>The primitive types of OData V2.</summary>
internal enum EdmSimpleKind
{
    Binary,
    Boolean,
    Byte,
    DateTime,
    DateTimeOffset,
    Decimal,
    Double,
    Guid,
    Int16,
    Int32,
    Int64,
    SByte,
    Single,
    String,
    Time,
}

/// <summary>The documentation element of an EDM construct: summary and long description.</summary>
internal sealed record EdmDocumentation(string? Summary, string? LongDescription);

/// <summary>The facets of a property or parameter.</summary>
internal sealed record EdmFacets(bool? Nullable, string? DefaultValue, int? MaxLength, int? Precision, int? Scale);

/// <summary>Base of the documented EDM constructs.</summary>
internal abstract class EdmAnnotated
{
    internal required string Name { get; init; }

    /// <summary>The value of <c>sap:label</c>.</summary>
    internal string? Label { get; init; }

    /// <summary>The value of <c>sap:quickinfo</c>.</summary>
    internal string? QuickInfo { get; init; }

    internal EdmDocumentation? Documentation { get; init; }
}

/// <summary>A type: primitive, complex or entity.</summary>
internal abstract class EdmType : EdmAnnotated
{
    internal abstract EdmTypeKind Kind { get; }

    /// <summary>The name with its namespace; for primitives <c>Edm.Int32</c>.</summary>
    internal required string QualifiedName { get; init; }
}

internal sealed class EdmSimpleType : EdmType
{
    internal override EdmTypeKind Kind => EdmTypeKind.Simple;

    internal required EdmSimpleKind SimpleKind { get; init; }
}

internal abstract class EdmStructuralType : EdmType
{
    internal string? BaseTypeName { get; set; }

    internal EdmStructuralType? BaseType { get; set; }

    internal List<EdmProperty> DeclaredProperties { get; } = [];

    /// <summary>All properties, those of the base type first.</summary>
    internal IReadOnlyList<EdmProperty> Properties
    {
        get
        {
            if (BaseType is null)
            {
                return DeclaredProperties;
            }
            var result = new List<EdmProperty>(BaseType.Properties);
            foreach (var declared in DeclaredProperties)
            {
                var existing = result.FindIndex(p => p.Name == declared.Name);
                if (existing >= 0)
                {
                    result[existing] = declared;
                }
                else
                {
                    result.Add(declared);
                }
            }
            return result;
        }
    }
}

internal sealed class EdmComplexType : EdmStructuralType
{
    internal override EdmTypeKind Kind => EdmTypeKind.Complex;
}

internal sealed class EdmEntityType : EdmStructuralType
{
    internal override EdmTypeKind Kind => EdmTypeKind.Entity;

    internal List<string> DeclaredKeys { get; } = [];

    internal List<EdmNavigationProperty> DeclaredNavigationProperties { get; } = [];

    internal bool HasStream { get; init; }

    /// <summary>The key property names; inherited if the type does not declare a key.</summary>
    internal IReadOnlyList<string> Keys =>
        DeclaredKeys.Count > 0 || BaseType is not EdmEntityType baseEntity ? DeclaredKeys : baseEntity.Keys;

    internal IReadOnlyList<EdmNavigationProperty> NavigationProperties
    {
        get
        {
            if (BaseType is not EdmEntityType baseEntity)
            {
                return DeclaredNavigationProperties;
            }
            var result = new List<EdmNavigationProperty>(baseEntity.NavigationProperties);
            result.AddRange(DeclaredNavigationProperties.Where(n => result.TrueForAll(r => r.Name != n.Name)));
            return result;
        }
    }
}

internal sealed class EdmProperty : EdmAnnotated
{
    internal required string TypeName { get; init; }

    internal EdmType? Type { get; set; }

    internal EdmFacets Facets { get; init; } = new(null, null, null, null, null);
}

internal sealed class EdmNavigationProperty : EdmAnnotated
{
    internal required string RelationshipName { get; init; }

    internal required string FromRole { get; init; }

    internal required string ToRole { get; init; }

    /// <summary>The entity type on the other end of the association.</summary>
    internal EdmEntityType? TargetType { get; set; }

    internal EdmMultiplicity Multiplicity { get; set; }
}

internal sealed record EdmAssociationEnd(string TypeName, EdmMultiplicity Multiplicity, string Role);

internal sealed class EdmAssociation
{
    internal required string Name { get; init; }

    internal required string QualifiedName { get; init; }

    internal List<EdmAssociationEnd> Ends { get; } = [];
}

internal sealed class EdmEntitySet : EdmAnnotated
{
    internal required string EntityTypeName { get; init; }

    internal EdmEntityType? EntityType { get; set; }

    /// <summary>The attributes of the entity set from the SAP annotation namespace, by local name.</summary>
    internal Dictionary<string, string> SapAttributes { get; } = new(StringComparer.Ordinal);
}

internal sealed class EdmParameter : EdmAnnotated
{
    internal required string TypeName { get; init; }

    internal EdmType? Type { get; set; }

    internal EdmFacets Facets { get; init; } = new(null, null, null, null, null);
}

internal sealed class EdmFunctionImport : EdmAnnotated
{
    internal string? ReturnTypeName { get; init; }

    internal EdmType? ReturnType { get; set; }

    internal EdmMultiplicity? ReturnMultiplicity { get; set; }

    internal string? HttpMethod { get; init; }

    internal List<EdmParameter> Parameters { get; } = [];
}

internal sealed class EdmEntityContainer
{
    internal required string Name { get; init; }

    internal bool IsDefault { get; init; }

    internal List<EdmEntitySet> EntitySets { get; } = [];

    internal List<EdmFunctionImport> FunctionImports { get; } = [];
}

internal sealed class EdmSchema
{
    internal required string Namespace { get; init; }

    internal string? Alias { get; init; }

    internal List<EdmEntityType> EntityTypes { get; } = [];

    internal List<EdmComplexType> ComplexTypes { get; } = [];

    internal List<EdmAssociation> Associations { get; } = [];

    internal List<EdmEntityContainer> Containers { get; } = [];

    /// <summary>The value of the first <c>atom:link rel="self"</c> in the schema, if any.</summary>
    internal string? SelfLink { get; init; }
}

/// <summary>An OData V2 service model: the schemas of an EDMX file with all references resolved.</summary>
internal sealed class EdmModel
{
    internal string DataServiceVersion { get; init; } = "";

    /// <summary>The document the model was read from; used for annotations that the model does not represent.</summary>
    internal System.Xml.Linq.XDocument? Source { get; init; }

    internal List<EdmSchema> Schemas { get; } = [];

    /// <summary>The entity sets of all containers, by name; a later definition replaces an earlier one.</summary>
    internal IReadOnlyList<EdmEntitySet> EntitySets =>
        Schemas.SelectMany(s => s.Containers).SelectMany(c => c.EntitySets).GroupBy(e => e.Name).Select(g => g.Last()).ToList();

    /// <summary>The function imports of all containers, by name; a later definition replaces an earlier one.</summary>
    internal IReadOnlyList<EdmFunctionImport> FunctionImports =>
        Schemas.SelectMany(s => s.Containers).SelectMany(c => c.FunctionImports).GroupBy(e => e.Name).Select(g => g.Last()).ToList();

    /// <summary>The namespace of the first schema that declares one.</summary>
    internal string? FirstNamespace => Schemas.Select(s => s.Namespace).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
}
