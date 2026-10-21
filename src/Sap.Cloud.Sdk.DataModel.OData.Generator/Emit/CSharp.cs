using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>C# specifics of the emitter: type mapping, literals, XML documentation and the names the generated code uses itself.</summary>
internal static partial class CSharp
{
    /// <summary>
    /// Names of types that the generated code refers to without qualification. A generated class with one of these names would
    /// hide the type, so such names get the suffix <c>Entity</c>.
    /// </summary>
    internal static readonly IReadOnlySet<string> ReservedTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // BCL
        "Task", "List", "IReadOnlyList", "IReadOnlyDictionary", "IEnumerable", "CancellationToken", "HttpMethod", "OrderedDictionary",
        "Guid", "DateTime", "DateTimeOffset", "TimeOnly", "Exception", "Enumerable", "String", "Object", "Boolean", "Byte", "SByte",
        "Int16", "Int32", "Int64", "Single", "Double", "Decimal", "Nullable", "Array", "Type", "IDisposable", "Stream",
        // SDK runtime
        "VdmEntity", "VdmMediaEntity", "VdmComplex", "VdmObject", "VdmTypeMetadata", "EntityField", "EntityFieldAll", "EntityLink",
        "IEntitySelectable", "IOneToOneLink", "ExpressionFluentHelper", "FluentHelperRead", "FluentHelperByKey", "FluentHelperCreate",
        "FluentHelperUpdate", "FluentHelperDelete", "SingleValuedFluentHelperFunction", "CollectionValuedFluentHelperFunction",
        "VoidResult", "ODataExecutionTarget", "IFieldValueConverter", "FieldValueConverter", "ITypeConverter", "IBatchService",
        "IFluentHelperServiceBatch", "IFluentHelperBatchChangeSet", "IFluentHelperBatchEndChangeSet", "BatchFluentHelperBasic",
        "BatchChangeSetFluentHelperBasic", "ODataKey", "ODataField",
        // names of generated members and namespaces
        "Fields", "Builder", "Batch",
    };

    /// <summary>
    /// Names the members of a generated entity or complex type may not take: the public and protected members of the runtime base
    /// classes, <see cref="object"/>, and members the generator emits itself.
    /// </summary>
    internal static readonly IReadOnlySet<string> ReservedMemberNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // object
        "Equals", "GetHashCode", "ToString", "GetType", "MemberwiseClone", "Finalize", "ReferenceEquals",
        // VdmObject
        "TypeMetadata", "CustomFieldNames", "CustomFields", "SetCustomField", "HasCustomField", "GetCustomField", "ToMapOfFields",
        "GetSetOfFields", "ToMapOfNavigationProperties", "GetSetOfNavigationProperties", "ToMapOfCustomFields", "GetSetOfCustomFields",
        "ToMap", "GetKey", "GetChangedFields", "RememberChangedField", "ResetChangedFields",
        // VdmEntity / VdmMediaEntity
        "VersionIdentifier", "EntityCollection", "DefaultServicePath", "ServicePathForFetch", "DestinationForFetch", "FetchTarget",
        "AttachToService", "FetchFieldAsListAsync", "FetchFieldAsSingleAsync", "FetchMediaStreamAsync",
        // emitted by the generator
        "EntityMetadata", "ComplexMetadata", "Fields", "Builder", "Build", "All", "Custom", "FieldValueConverter", "ITypeConverter",
        "_entity", "_values", "_key",
    };

    private static readonly Dictionary<EdmSimpleKind, string> SimpleTypeNames = new()
    {
        [EdmSimpleKind.Binary] = "byte[]",
        [EdmSimpleKind.Boolean] = "bool",
        [EdmSimpleKind.Byte] = "byte",
        [EdmSimpleKind.DateTime] = "DateTime",
        [EdmSimpleKind.DateTimeOffset] = "DateTimeOffset",
        [EdmSimpleKind.Decimal] = "decimal",
        [EdmSimpleKind.Double] = "double",
        [EdmSimpleKind.Guid] = "Guid",
        [EdmSimpleKind.Int16] = "short",
        [EdmSimpleKind.Int32] = "int",
        [EdmSimpleKind.Int64] = "long",
        [EdmSimpleKind.SByte] = "sbyte",
        [EdmSimpleKind.Single] = "float",
        [EdmSimpleKind.String] = "string",
        [EdmSimpleKind.Time] = "TimeOnly",
    };

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>The C# type of a primitive EDM type, without nullability.</summary>
    internal static string TypeName(EdmSimpleKind kind) => SimpleTypeNames[kind];

    /// <summary>Whether the C# type of the primitive is a reference type (<c>string</c>, <c>byte[]</c>).</summary>
    internal static bool IsReferenceType(EdmSimpleKind kind) => kind is EdmSimpleKind.String or EdmSimpleKind.Binary;

    /// <summary>The identifier as it must be written in code: keywords get a verbatim <c>@</c> prefix.</summary>
    internal static string Identifier(string name) => NamingUtils.IsKeyword(name) ? "@" + name : name;

    /// <summary>A string literal.</summary>
    internal static string Literal(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:x4}");
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
        return builder.Append('"').ToString();
    }

    /// <summary>Escapes text for XML documentation and collapses whitespace to single spaces.</summary>
    internal static string Xml(string text) => Whitespace()
        .Replace(text, " ")
        .Trim()
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>Escapes a value for an XML attribute in documentation.</summary>
    internal static string XmlAttribute(string text) => Xml(text).Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>Writes a one-line summary.</summary>
    internal static void Summary(CodeWriter writer, string text) => writer.Line($"/// <summary>{text}</summary>");

    /// <summary>Writes a summary of several lines (or one line, if <paramref name="lines"/> has one entry).</summary>
    internal static void Summary(CodeWriter writer, IReadOnlyList<string> lines)
    {
        if (lines.Count == 1)
        {
            Summary(writer, lines[0]);
            return;
        }
        SummaryBlock(writer, lines);
    }

    /// <summary>Writes a summary that always spans several lines.</summary>
    internal static void SummaryBlock(CodeWriter writer, IReadOnlyList<string> lines)
    {
        writer.Line("/// <summary>");
        foreach (var line in lines)
        {
            writer.Line("/// " + line);
        }
        writer.Line("/// </summary>");
    }

    /// <summary>A <c>using</c> list, sorted as given.</summary>
    internal static void Usings(CodeWriter writer, IEnumerable<string> namespaces)
    {
        foreach (var ns in namespaces)
        {
            writer.Line($"using {ns};");
        }
    }

    /// <summary>Joins namespace segments, skipping empty ones.</summary>
    internal static string Namespace(params string[] segments) => string.Join('.', segments.Where(s => s.Length > 0));
}
