using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Details;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Generation;

/// <summary>How a C# type is derived from an EDM primitive type.</summary>
internal sealed record PrimitiveMapping(string ClrType, string Descriptor);

internal static class PrimitiveMappings
{
    public static PrimitiveMapping? Get(EdmPrimitiveKind kind) => kind switch
    {
        EdmPrimitiveKind.String => new PrimitiveMapping("string", "String"),
        EdmPrimitiveKind.Boolean => new PrimitiveMapping("bool", "Boolean"),
        EdmPrimitiveKind.Byte => new PrimitiveMapping("byte", "NumericInteger"),
        EdmPrimitiveKind.SByte => new PrimitiveMapping("sbyte", "NumericInteger"),
        EdmPrimitiveKind.Int16 => new PrimitiveMapping("short", "NumericInteger"),
        EdmPrimitiveKind.Int32 => new PrimitiveMapping("int", "NumericInteger"),
        EdmPrimitiveKind.Int64 => new PrimitiveMapping("long", "NumericInteger"),
        EdmPrimitiveKind.Single => new PrimitiveMapping("float", "NumericDecimal"),
        EdmPrimitiveKind.Double => new PrimitiveMapping("double", "NumericDecimal"),
        EdmPrimitiveKind.Decimal => new PrimitiveMapping("decimal", "NumericDecimal"),
        EdmPrimitiveKind.Guid => new PrimitiveMapping("Guid", "Guid"),
        EdmPrimitiveKind.Binary => new PrimitiveMapping("byte[]", "Binary"),
        EdmPrimitiveKind.Date => new PrimitiveMapping("DateOnly", "Date"),
        EdmPrimitiveKind.TimeOfDay => new PrimitiveMapping("TimeOnly", "Time"),
        EdmPrimitiveKind.DateTimeOffset => new PrimitiveMapping("DateTimeOffset", "DateTime"),
        EdmPrimitiveKind.Duration => new PrimitiveMapping("TimeSpan", "Duration"),
        _ => null,
    };
}

/// <summary>A type that the generator emits.</summary>
internal abstract class GenType(string csName, string edmName, string fullEdmName)
{
    public string CsName { get; } = csName;

    public string EdmName { get; } = edmName;

    public string FullEdmName { get; } = fullEdmName;

    /// <summary>The documentation paragraphs (description, long description).</summary>
    public IReadOnlyList<string> Descriptions { get; set; } = [];
}

internal sealed class GenEnumMember(string csName, string edmName, long value)
{
    public string CsName { get; } = csName;

    public string EdmName { get; } = edmName;

    public long Value { get; } = value;
}

internal sealed class GenEnum(string csName, string edmName, string fullEdmName) : GenType(csName, edmName, fullEdmName)
{
    public List<GenEnumMember> Members { get; } = [];
}

internal enum GenPropertyKind
{
    Simple,
    Enum,
    Complex,
}

internal sealed class GenProperty
{
    public required string EdmName { get; init; }

    public required string CsName { get; init; }

    public required string FieldName { get; init; }

    public required GenPropertyKind Kind { get; init; }

    public required bool IsCollection { get; init; }

    public PrimitiveMapping? Primitive { get; init; }

    public GenEnum? EnumType { get; init; }

    public GenComplex? ComplexType { get; init; }

    public bool IsKey { get; set; }

    public string? BasicDescription { get; init; }

    public string? DetailedDescription { get; init; }

    public string? Constraints { get; init; }

    /// <summary>The C# type of one value, without nullability and collection: <c>string</c>, <c>Location</c>.</summary>
    public string ItemType => Kind switch
    {
        GenPropertyKind.Simple => Primitive!.ClrType,
        GenPropertyKind.Enum => EnumType!.CsName,
        _ => ComplexType!.CsName,
    };

    /// <summary>The type of the C# property: <c>string?</c>, <c>List&lt;Feature&gt;?</c>.</summary>
    public string PropertyType => (IsCollection ? $"List<{ItemType}>" : ItemType) + "?";

    /// <summary>The type of a key parameter in a service method (never nullable).</summary>
    public string KeyParameterType => ItemType;

    /// <summary>The type of the static descriptor in the <c>Fields</c> class.</summary>
    public string DescriptorType(string owner) => Kind switch
    {
        GenPropertyKind.Simple when IsCollection => $"SimpleProperty.Collection<{owner}, {ItemType}>",
        GenPropertyKind.Simple => $"SimpleProperty.{Primitive!.Descriptor}<{owner}>",
        GenPropertyKind.Enum when IsCollection => $"SimpleProperty.Collection<{owner}, {ItemType}>",
        GenPropertyKind.Enum => $"SimpleProperty.Enum<{owner}, {ItemType}>",
        _ when IsCollection => $"ComplexProperty.Collection<{owner}, {ItemType}>",
        _ => $"ComplexProperty.Single<{owner}, {ItemType}>",
    };

    public string DescriptorInitializer(string owner) =>
        Kind == GenPropertyKind.Enum && !IsCollection
            ? $"{DescriptorType(owner)}.Of({Emit.XmlDoc.Literal(EdmName)}, {Emit.XmlDoc.Literal(EnumType!.FullEdmName)})"
            : $"{DescriptorType(owner)}.Of({Emit.XmlDoc.Literal(EdmName)})";
}

internal sealed class GenNavigation
{
    public required string EdmName { get; init; }

    public required string CsName { get; init; }

    public required string FieldName { get; init; }

    public required bool IsCollection { get; init; }

    public required EdmEntityType TargetEdmType { get; init; }

    public GenEntity? Target { get; set; }

    public required string IfPresentName { get; init; }

    public required string SetName { get; init; }

    public string? AddName { get; init; }
}

internal sealed class GenParameter
{
    public required string EdmName { get; init; }

    public required string CsName { get; init; }

    /// <summary>The C# type including nullability, for example <c>string?</c> or <c>int</c>.</summary>
    public required string TypeText { get; init; }

    public string? Description { get; init; }
}

internal sealed class GenBoundOperation
{
    public required bool IsFunction { get; init; }

    public required string CsName { get; init; }

    public required string QualifiedName { get; init; }

    public required string ClassText { get; init; }

    public required IReadOnlyList<GenParameter> Parameters { get; init; }

    public IReadOnlyList<string> Descriptions { get; init; } = [];
}

internal abstract class GenStructured(string csName, string edmName, string fullEdmName) : GenType(csName, edmName, fullEdmName)
{
    /// <summary>The name of the nested builder class.</summary>
    public required string BuilderName { get; init; }

    public List<GenProperty> Properties { get; } = [];

    public NamingContext Members { get; init; } = new();
}

internal sealed class GenComplex(string csName, string edmName, string fullEdmName) : GenStructured(csName, edmName, fullEdmName);

internal sealed class GenEntity(string csName, string edmName, string fullEdmName) : GenStructured(csName, edmName, fullEdmName)
{
    public required string EntityCollection { get; init; }

    public required EdmEntityType EdmType { get; init; }

    public List<GenProperty> Keys { get; } = [];

    public List<GenNavigation> Navigations { get; } = [];

    public bool IsEntitySet { get; set; }

    public List<GenBoundOperation> BoundOperations { get; } = [];

    public Dictionary<string, string> OperationNames { get; } = new(StringComparer.Ordinal);

    public HashSet<string> BoundSignatures { get; } = new(StringComparer.Ordinal);

    public bool NavigationsLinked { get; set; }
}

internal enum ServiceMethodKind
{
    GetAll,
    Count,
    GetByKey,
    Create,
    Update,
    Delete,
    UnboundFunction,
    UnboundAction,
}

internal sealed class ServiceMethod
{
    public required ServiceMethodKind Kind { get; init; }

    public required string Name { get; init; }

    public GenEntity? Entity { get; init; }

    public string? EntitySetName { get; init; }

    /// <summary>For unbound operations: the name of the import.</summary>
    public string? ImportName { get; init; }

    public IReadOnlyList<GenParameter> Parameters { get; init; } = [];

    /// <summary>For unbound operations: the type argument of the request builder, for example <c>Person</c> or <c>VdmVoid</c>.</summary>
    public string? ResultType { get; init; }

    public bool ReturnsCollection { get; init; }

    public IReadOnlyList<string> Descriptions { get; init; } = [];
}

/// <summary>The result of processing one service: everything the emitters need.</summary>
internal sealed class GenService
{
    public required string Name { get; init; }

    public required string Title { get; init; }

    public required string ClassName { get; init; }

    public required string NamespaceSegment { get; init; }

    public required string RootNamespace { get; init; }

    public required ServiceDetails Details { get; init; }

    public required string SourceFile { get; init; }

    public string? Description { get; set; }

    public string InterfaceName => "I" + ClassName + "Service";

    public string ImplementationName => "Default" + ClassName + "Service";

    public string TypesNamespace => RootNamespace + ".Namespaces." + NamespaceSegment;

    /// <summary>The folder of the type files relative to the output directory.</summary>
    public string TypesFolder => "Namespaces/" + NamespaceSegment.Replace('.', '/');

    public string ServicesNamespace => RootNamespace + ".Services";

    public bool IsDeprecated { get; set; }

    public string? DeprecationMessage { get; set; }

    public List<GenEntity> Entities { get; } = [];

    public List<GenComplex> Complexes { get; } = [];

    public List<GenEnum> Enums { get; } = [];

    public List<ServiceMethod> Methods { get; } = [];

    public int OperationCount => Methods.Count(m => m.Kind is ServiceMethodKind.UnboundFunction or ServiceMethodKind.UnboundAction)
        + Entities.Sum(e => e.BoundOperations.Count);
}
