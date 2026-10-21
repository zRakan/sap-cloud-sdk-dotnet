using System.Text.RegularExpressions;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Emit;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Generation;

internal sealed record EmitSettings(
    bool PojosOnly,
    string FileHeader,
    string? VersionReference,
    bool EmitGeneratedCodeAttribute,
    bool EmitSystemUsings,
    bool GenerateLinksToApiBusinessHub);

/// <summary>Emits the C# source of the generation model. The output shape is that of the sample in the ODataV4.Core tests.</summary>
internal sealed partial class SourceEmitter(EmitSettings settings)
{
    private const string GeneratorName = "Sap.Cloud.Sdk.DataModel.ODataV4.Generator";

    private static string Id(string name) => NamingUtils.EscapeIdentifier(name);

    // ---------------------------------------------------------------------------------------------------------------
    // entities and complex types

    public string Entity(GenService service, GenEntity entity)
    {
        var w = new CodeWriter();
        var usings = TypeUsings(service);
        WriteHeader(w, usings, service.TypesNamespace, pragmaObsolete: service.IsDeprecated && !settings.PojosOnly);

        var name = Id(entity.CsName);
        var lines = new List<string> { $"Original entity name from the Odata EDM: <b>{XmlDoc.Escape(entity.EdmName)}</b>" };
        AddParagraphs(lines, entity.Descriptions);
        XmlDoc.SummaryLines(w, lines);
        WriteTypeAttributes(w, $"[JsonConverter(typeof(VdmObjectJsonConverter<{name}>))]");

        var marker = entity.IsEntitySet ? ", IVdmEntitySet" : string.Empty;
        w.Block($"public partial class {name} : VdmEntity<{name}>{marker}", () =>
        {
            WriteTypeInfo(w, entity, name);
            WriteFields(w, entity, name, includeAll: true);

            w.Blank();
            w.Line("protected override VdmTypeInfo TypeInfo => s_typeInfo;");
            w.Blank();
            w.Line($"protected override string EntityCollection => {XmlDoc.Literal(entity.EntityCollection)};");
            if (!settings.PojosOnly)
            {
                w.Blank();
                w.Line($"protected override string? DefaultServicePath => {Id(service.InterfaceName)}.DefaultServicePath;");
            }

            WriteProperties(w, entity);

            if (!settings.PojosOnly && entity.Keys.Count > 0)
            {
                w.Blank();
                w.Block("protected override ODataEntityKey GetKey()", () =>
                {
                    w.Line("var entityKey = base.GetKey();");
                    // in declaration order of the properties, as in Java
                    foreach (var key in entity.Properties.Where(p => p.IsKey))
                    {
                        w.Line($"entityKey.AddKeyProperty({XmlDoc.Literal(key.EdmName)}, {Id(key.CsName)});");
                    }

                    w.Line("return entityKey;");
                });
            }

            foreach (var navigation in entity.Navigations)
            {
                WriteNavigationAccessors(w, navigation);
            }

            if (!settings.PojosOnly)
            {
                foreach (var operation in entity.BoundOperations)
                {
                    WriteBoundOperation(w, operation);
                }
            }

            WriteBuilder(w, entity, name, entity.Navigations);
        });

        return w.ToString();
    }

    public string Complex(GenService service, GenComplex complex)
    {
        var w = new CodeWriter();
        WriteHeader(w, TypeUsings(service), service.TypesNamespace, pragmaObsolete: false);

        var name = Id(complex.CsName);
        var lines = new List<string> { $"Original complex type name from the Odata EDM: <b>{XmlDoc.Escape(complex.EdmName)}</b>" };
        AddParagraphs(lines, complex.Descriptions);
        XmlDoc.SummaryLines(w, lines);
        WriteTypeAttributes(w, $"[JsonConverter(typeof(VdmObjectJsonConverter<{name}>))]");

        w.Block($"public partial class {name} : VdmComplex<{name}>", () =>
        {
            WriteTypeInfo(w, complex, name);
            WriteFields(w, complex, name, includeAll: false);

            w.Blank();
            w.Line("protected override VdmTypeInfo TypeInfo => s_typeInfo;");

            WriteProperties(w, complex);
            WriteBuilder(w, complex, name, []);
        });

        return w.ToString();
    }

    public string Enum(GenService service, GenEnum genEnum)
    {
        var w = new CodeWriter();
        var usings = new List<string> { "System.Text.Json.Serialization", "Sap.Cloud.Sdk.DataModel.ODataV4.Core" };
        WriteHeader(w, usings, service.TypesNamespace, pragmaObsolete: false);

        var name = Id(genEnum.CsName);
        var lines = new List<string> { $"Original enum name from the Odata EDM: <b>{XmlDoc.Escape(genEnum.EdmName)}</b>" };
        AddParagraphs(lines, genEnum.Descriptions);
        XmlDoc.SummaryLines(w, lines);
        WriteTypeAttributes(w, $"[JsonConverter(typeof(VdmEnumJsonConverter<{name}>))]");

        w.Block($"public sealed class {name} : VdmEnum, IVdmEnum<{name}>", () =>
        {
            foreach (var member in genEnum.Members)
            {
                w.Blank();
                XmlDoc.Summary(w, $"Member <b>{XmlDoc.Escape(member.EdmName)}</b> (value {member.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}).");
                w.Line($"public static readonly {name} {Id(member.CsName)} = new({XmlDoc.Literal(member.EdmName)}, {member.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
            }

            w.Blank();
            w.Line($"private static readonly IReadOnlyList<{name}> s_values = [{string.Join(", ", genEnum.Members.Select(m => Id(m.CsName)))}];");
            w.Blank();
            w.Line($"private {name}(string name, long value)");
            w.Indented(() => w.Line(": base(name, value)"));
            w.Line("{");
            w.Line("}");
            w.Blank();
            XmlDoc.Summary(w, "All members in declaration order.");
            w.Line($"public static IReadOnlyList<{name}> Values => s_values;");
        });

        return w.ToString();
    }

    private List<string> TypeUsings(GenService service)
    {
        var usings = new List<string>
        {
            "System.Text.Json.Serialization",
            "Sap.Cloud.Sdk.DataModel.OData.Client.Request",
            "Sap.Cloud.Sdk.DataModel.ODataV4.Core",
            "Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter",
        };
        if (!settings.PojosOnly)
        {
            usings.Add(service.ServicesNamespace);
        }

        return usings;
    }

    private static void WriteTypeInfo(CodeWriter w, GenStructured type, string name)
    {
        w.Line($"private static readonly VdmTypeInfo<{name}> s_typeInfo = new VdmTypeInfoBuilder<{name}>({XmlDoc.Literal(type.FullEdmName)})");
        w.Indented(() =>
        {
            foreach (var property in type.Properties)
            {
                var member = Id(property.CsName);
                w.Line($".Field({XmlDoc.Literal(property.EdmName)}, e => e.{member}, (e, v) => e.{member} = v)");
            }

            if (type is GenEntity entity)
            {
                foreach (var navigation in entity.Navigations)
                {
                    var field = navigation.FieldName;
                    w.Line($".Navigation({XmlDoc.Literal(navigation.EdmName)}, e => e.{field}, (e, v) => e.{field} = v)");
                }
            }

            w.Line(".Build();");
        });
    }

    private void WriteFields(CodeWriter w, GenStructured type, string name, bool includeAll)
    {
        if (settings.PojosOnly)
        {
            return;
        }

        w.Blank();
        XmlDoc.Summary(w, "Static descriptors of the properties; use them with <c>Select</c>, <c>Filter</c> and <c>OrderBy</c>.");
        w.Block("public static class Fields", () =>
        {
            if (includeAll)
            {
                w.Blank();
                XmlDoc.Summary(w, "Use with available request builders to apply the <c>$select</c> query to all fields.");
                w.Line($"public static readonly ISimpleProperty<{name}> All = SimpleProperty.CreateAll<{name}>();");
            }

            foreach (var property in type.Properties)
            {
                w.Blank();
                XmlDoc.Summary(w, $"Original property name from the Odata EDM: <b>{XmlDoc.Escape(property.EdmName)}</b>");
                w.Line($"public static readonly {property.DescriptorType(name)} {Id(property.CsName)} = {property.DescriptorInitializer(name)};");
            }

            if (type is GenEntity entity)
            {
                foreach (var navigation in entity.Navigations)
                {
                    var kind = navigation.IsCollection ? "Collection" : "Single";
                    var target = Id(navigation.Target!.CsName);
                    var descriptor = $"NavigationProperty.{kind}<{name}, {target}>";
                    w.Blank();
                    XmlDoc.Summary(w, $"Navigation property <b>{XmlDoc.Escape(navigation.EdmName)}</b> to <c>{XmlDoc.Escape(navigation.Target.CsName)}</c>.");
                    w.Line($"public static readonly {descriptor} {Id(navigation.CsName)} = {descriptor}.Of({XmlDoc.Literal(navigation.EdmName)});");
                }
            }
        });
    }

    private static void WriteProperties(CodeWriter w, GenStructured type)
    {
        foreach (var property in type.Properties)
        {
            var member = Id(property.CsName);
            w.Blank();
            w.Line($"private {property.PropertyType} {property.FieldName};");
            w.Blank();
            var lines = new List<string>
            {
                (property.IsKey ? "(Key Field) " : string.Empty) + $"Original property name from the Odata EDM: <b>{XmlDoc.Escape(property.EdmName)}</b>",
            };
            AddParagraph(lines, property.BasicDescription);
            AddParagraph(lines, property.DetailedDescription);
            AddParagraph(lines, property.Constraints);
            XmlDoc.SummaryLines(w, lines);
            w.Block($"public {property.PropertyType} {member}", () =>
            {
                w.Line($"get => {property.FieldName};");
                w.Block("set", () =>
                {
                    w.Line($"RememberChangedField({XmlDoc.Literal(property.EdmName)}, {property.FieldName});");
                    w.Line($"{property.FieldName} = value;");
                });
            });
        }
    }

    private static void WriteNavigationAccessors(CodeWriter w, GenNavigation navigation)
    {
        var target = Id(navigation.Target!.CsName);
        var name = XmlDoc.Escape(navigation.CsName);
        w.Blank();
        if (navigation.IsCollection)
        {
            w.Line($"private List<{target}>? {navigation.FieldName};");
            w.Blank();
            XmlDoc.Summary(w, $"Gets the {name} if they were loaded or set; <see langword=\"null\"/> otherwise.");
            w.Line($"public IReadOnlyList<{target}>? {Id(navigation.IfPresentName)}() => {navigation.FieldName};");
            w.Blank();
            XmlDoc.Summary(w, $"Replaces the {name}.");
            w.Block($"public void {Id(navigation.SetName)}(IEnumerable<{target}> value)", () =>
            {
                w.Line($"{navigation.FieldName} ??= [];");
                w.Line($"{navigation.FieldName}.Clear();");
                w.Line($"{navigation.FieldName}.AddRange(value);");
            });
            w.Blank();
            XmlDoc.Summary(w, $"Adds {name}.");
            w.Block($"public void {Id(navigation.AddName!)}(params {target}[] entity)", () =>
            {
                w.Line($"{navigation.FieldName} ??= [];");
                w.Line($"{navigation.FieldName}.AddRange(entity);");
            });
        }
        else
        {
            w.Line($"private {target}? {navigation.FieldName};");
            w.Blank();
            XmlDoc.Summary(w, $"Gets the {name} if it was loaded or set; <see langword=\"null\"/> otherwise.");
            w.Line($"public {target}? {Id(navigation.IfPresentName)}() => {navigation.FieldName};");
            w.Blank();
            XmlDoc.Summary(w, $"Sets the {name}.");
            w.Line($"public void {Id(navigation.SetName)}({target}? value) => {navigation.FieldName} = value;");
        }
    }

    private static void WriteBoundOperation(CodeWriter w, GenBoundOperation operation)
    {
        w.Blank();
        var kind = operation.IsFunction ? "function" : "action";
        var paragraphs = new List<string>();
        paragraphs.AddRange(operation.Descriptions.Select(XmlDoc.Escape));
        XmlDoc.SummaryWithParagraphs(w, $"Bound {kind} <b>{XmlDoc.Escape(operation.QualifiedName)}</b>.", paragraphs);
        WriteParameterDocs(w, operation.Parameters);
        w.Block($"public static {operation.ClassText} {Id(operation.CsName)}({ParameterList(operation.Parameters)})", () =>
        {
            var arguments = XmlDoc.Literal(operation.QualifiedName);
            if (operation.Parameters.Count > 0)
            {
                WriteParameterDictionary(w, operation.Parameters);
                arguments += ", parameters";
            }

            w.Line($"return new {operation.ClassText}({arguments});");
        });
    }

    private static void WriteBuilder(CodeWriter w, GenStructured type, string name, IReadOnlyList<GenNavigation> navigations)
    {
        var builder = Id(type.BuilderName);
        w.Blank();
        XmlDoc.Summary(w, "Creates a builder that sets the properties without recording changes.");
        w.Line($"public static {builder} Builder() => new();");
        w.Blank();
        XmlDoc.Summary(w, $"Fluent builder of <see cref=\"{XmlDoc.Escape(type.CsName)}\"/>.");
        w.Block($"public sealed class {builder}", () =>
        {
            w.Line($"private readonly {name} _entity = new();");
            foreach (var navigation in navigations)
            {
                var target = Id(navigation.Target!.CsName);
                w.Line(navigation.IsCollection
                    ? $"private readonly List<{target}> {navigation.FieldName} = [];"
                    : $"private {target}? {navigation.FieldName};");
            }

            foreach (var property in type.Properties)
            {
                w.Blank();
                XmlDoc.Summary(w, $"Sets <c>{XmlDoc.Escape(property.CsName)}</c>.");
                w.Block($"public {builder} {Id(property.CsName)}({property.PropertyType} value)", () =>
                {
                    w.Line($"_entity.{property.FieldName} = value;");
                    w.Line("return this;");
                });
            }

            foreach (var navigation in navigations)
            {
                var target = Id(navigation.Target!.CsName);
                w.Blank();
                if (navigation.IsCollection)
                {
                    XmlDoc.Summary(w, $"Adds <c>{XmlDoc.Escape(navigation.CsName)}</c>.");
                    w.Block($"public {builder} {Id(navigation.CsName)}(params {target}[] value)", () =>
                    {
                        w.Line($"{navigation.FieldName}.AddRange(value);");
                        w.Line("return this;");
                    });
                }
                else
                {
                    XmlDoc.Summary(w, $"Sets <c>{XmlDoc.Escape(navigation.CsName)}</c>.");
                    w.Block($"public {builder} {Id(navigation.CsName)}({target}? value)", () =>
                    {
                        w.Line($"{navigation.FieldName} = value;");
                        w.Line("return this;");
                    });
                }
            }

            w.Blank();
            XmlDoc.Summary(w, $"Creates the {XmlDoc.Escape(type.CsName)}.");
            w.Block($"public {name} Build()", () =>
            {
                foreach (var navigation in navigations)
                {
                    w.Line(navigation.IsCollection
                        ? $"_entity.{navigation.FieldName} = [.. {navigation.FieldName}];"
                        : $"_entity.{navigation.FieldName} = {navigation.FieldName};");
                }

                w.Line("return _entity;");
            });
        });
    }

    // ---------------------------------------------------------------------------------------------------------------
    // services

    public string ServiceInterface(GenService service)
    {
        var w = new CodeWriter();
        WriteHeader(w, ServiceUsings(service), service.ServicesNamespace, pragmaObsolete: false);
        WriteServiceDocumentation(w, service);
        WriteTypeAttributes(w, service.IsDeprecated ? $"[Obsolete({XmlDoc.Literal(service.DeprecationMessage ?? string.Empty)})]" : null);

        var name = Id(service.InterfaceName);
        w.Block($"public interface {name} : IServiceWithNavigableEntities", () =>
        {
            XmlDoc.Summary(w, "The default service path.");
            w.Line($"const string DefaultServicePath = {XmlDoc.Literal(service.Details.ServiceUrl)};");
            w.Blank();
            XmlDoc.Summary(w, "Creates a service that uses another service path.");
            w.Line("/// <param name=\"servicePath\">The service path.</param>");
            w.Line("/// <returns>The service.</returns>");
            w.Line($"{name} WithServicePath(string servicePath);");
            w.Blank();
            XmlDoc.Summary(w, "Creates a batch request.");
            w.Line("BatchRequestBuilder Batch();");

            foreach (var method in service.Methods)
            {
                w.Blank();
                WriteMethodDocumentation(w, method);
                w.Line($"{ReturnType(method)} {Id(method.Name)}({MethodParameters(method)});");
            }
        });

        return w.ToString();
    }

    public string ServiceImplementation(GenService service)
    {
        var w = new CodeWriter();
        WriteHeader(w, ServiceUsings(service), service.ServicesNamespace, pragmaObsolete: false);
        WriteServiceDocumentation(w, service);
        WriteTypeAttributes(w, service.IsDeprecated ? $"[Obsolete({XmlDoc.Literal(service.DeprecationMessage ?? string.Empty)})]" : null);

        var interfaceName = Id(service.InterfaceName);
        var name = Id(service.ImplementationName);
        w.Block($"public class {name} : ServiceWithNavigableEntities, {interfaceName}", () =>
        {
            XmlDoc.Summary(w, $"Initializes a new instance of the <see cref=\"{XmlDoc.Escape(service.ImplementationName)}\"/> class with the default service path.");
            w.Line($"public {name}()");
            w.Indented(() => w.Line($": base({interfaceName}.DefaultServicePath)"));
            w.Line("{");
            w.Line("}");
            w.Blank();
            w.Line($"private {name}(string servicePath)");
            w.Indented(() => w.Line(": base(servicePath)"));
            w.Line("{");
            w.Line("}");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"public {interfaceName} WithServicePath(string servicePath) => new {name}(servicePath);");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line("public BatchRequestBuilder Batch() => new(ServicePath);");

            foreach (var method in service.Methods)
            {
                w.Blank();
                w.Line("/// <inheritdoc />");
                WriteMethodImplementation(w, method);
            }
        });

        return w.ToString();
    }

    private static List<string> ServiceUsings(GenService service)
    {
        var usings = new List<string> { "Sap.Cloud.Sdk.DataModel.ODataV4.Core" };
        if (service.Entities.Count + service.Complexes.Count + service.Enums.Count > 0)
        {
            usings.Add(service.TypesNamespace);
        }

        return usings;
    }

    private void WriteServiceDocumentation(CodeWriter w, GenService service)
    {
        var summary = service.Description is { Length: > 0 } description
            ? XmlDoc.Escape(description)
            : $"OData service <b>{XmlDoc.Escape(service.Name)}</b>.";
        var details = service.Details;
        var remarks = new List<string>();
        if (details.ExternalDocs?.Url is { Length: > 0 } url)
        {
            var text = string.IsNullOrEmpty(details.ExternalDocs.Description) ? url : details.ExternalDocs.Description;
            remarks.Add($"<see href=\"{Attribute(url)}\">{XmlDoc.Escape(text)}</see>");
        }

        if (settings.GenerateLinksToApiBusinessHub)
        {
            remarks.Add($"Reference: <see href=\"{Attribute("https://api.sap.com/shell/discover/contentpackage/SAPS4HANACloud/api/" + service.Name + "?section=OVERVIEW")}\">SAP Business Accelerator Hub</see>");
        }

        var hasDetails = !string.IsNullOrEmpty(details.Info?.Version)
            || !string.IsNullOrEmpty(details.MinErpVersion)
            || details.ExtOverview is { Count: > 0 };
        if (hasDetails)
        {
            remarks.Add($"OData service: {XmlDoc.Escape(service.Name)}");
            if (!string.IsNullOrEmpty(details.Info?.Version))
            {
                remarks.Add($"API version: {XmlDoc.Escape(details.Info.Version)}");
            }

            if (!string.IsNullOrEmpty(details.MinErpVersion))
            {
                remarks.Add($"Minimum ERP version: {XmlDoc.Escape(details.MinErpVersion)}");
            }

            foreach (var entry in details.ExtOverview ?? [])
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    remarks.Add($"{XmlDoc.Escape(entry.Name)}: {string.Join(", ", entry.Values.Select(OverviewValue))}");
                }
            }
        }

        if (remarks.Count == 0)
        {
            XmlDoc.Summary(w, summary);
            return;
        }

        XmlDoc.Summary(w, summary);
        w.Line("/// <remarks>");
        foreach (var remark in remarks)
        {
            w.Line($"/// <para>{remark}</para>");
        }

        w.Line("/// </remarks>");
    }

    private static string OverviewValue(string value)
    {
        var match = AnchorRegex().Match(value);
        return match.Success && match.Value.Length == value.Length
            ? $"<see href=\"{Attribute(match.Groups["href"].Value)}\">{XmlDoc.Escape(match.Groups["text"].Value)}</see>"
            : XmlDoc.Escape(value);
    }

    private static string Attribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal);

    private static string ReturnType(ServiceMethod method) => method.Kind switch
    {
        ServiceMethodKind.GetAll => $"GetAllRequestBuilder<{Id(method.Entity!.CsName)}>",
        ServiceMethodKind.Count => $"CountRequestBuilder<{Id(method.Entity!.CsName)}>",
        ServiceMethodKind.GetByKey => $"GetByKeyRequestBuilder<{Id(method.Entity!.CsName)}>",
        ServiceMethodKind.Create => $"CreateRequestBuilder<{Id(method.Entity!.CsName)}>",
        ServiceMethodKind.Update => $"UpdateRequestBuilder<{Id(method.Entity!.CsName)}>",
        ServiceMethodKind.Delete => $"DeleteRequestBuilder<{Id(method.Entity!.CsName)}>",
        ServiceMethodKind.UnboundFunction =>
            $"{(method.ReturnsCollection ? "Collection" : "Single")}ValueFunctionRequestBuilder<{method.ResultType}>",
        _ => $"{(method.ReturnsCollection ? "Collection" : "Single")}ValueActionRequestBuilder<{method.ResultType}>",
    };

    private static string MethodParameters(ServiceMethod method) => method.Kind switch
    {
        ServiceMethodKind.Create or ServiceMethodKind.Update or ServiceMethodKind.Delete =>
            $"{Id(method.Entity!.CsName)} {Id(EntityParameterName(method.Entity))}",
        ServiceMethodKind.GetAll or ServiceMethodKind.Count => string.Empty,
        _ => ParameterList(method.Parameters),
    };

    private static string EntityParameterName(GenEntity entity) => NamingUtils.ToCamelCase(entity.CsName);

    private static string ParameterList(IReadOnlyList<GenParameter> parameters) =>
        string.Join(", ", parameters.Select(p => $"{p.TypeText} {Id(p.CsName)}"));

    private static void WriteMethodDocumentation(CodeWriter w, ServiceMethod method)
    {
        switch (method.Kind)
        {
            case ServiceMethodKind.UnboundFunction:
            case ServiceMethodKind.UnboundAction:
                var paragraphs = new List<string>();
                paragraphs.AddRange(method.Descriptions.Select(XmlDoc.Escape));
                var kind = method.Kind == ServiceMethodKind.UnboundFunction ? "Function" : "Action";
                XmlDoc.SummaryWithParagraphs(w, $"{kind} import <b>{XmlDoc.Escape(method.ImportName!)}</b>.", paragraphs);
                WriteParameterDocs(w, method.Parameters);
                break;
            case ServiceMethodKind.GetByKey:
                XmlDoc.Summary(w, XmlDoc.Escape(method.Name) + ".");
                WriteParameterDocs(w, method.Parameters);
                break;
            default:
                XmlDoc.Summary(w, XmlDoc.Escape(method.Name) + ".");
                break;
        }
    }

    private static void WriteParameterDocs(CodeWriter w, IReadOnlyList<GenParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                w.Line($"/// <param name=\"{XmlDoc.Escape(parameter.CsName)}\">{XmlDoc.Escape(parameter.Description)}</param>");
            }
        }
    }

    private static void WriteParameterDictionary(CodeWriter w, IReadOnlyList<GenParameter> parameters)
    {
        w.BlockWithSemicolon("var parameters = new Dictionary<string, object?>", () =>
        {
            foreach (var parameter in parameters)
            {
                w.Line($"[{XmlDoc.Literal(parameter.EdmName)}] = {Id(parameter.CsName)},");
            }
        });
    }

    private static void WriteMethodImplementation(CodeWriter w, ServiceMethod method)
    {
        var returnType = ReturnType(method);
        var signature = $"public {returnType} {Id(method.Name)}({MethodParameters(method)})";
        switch (method.Kind)
        {
            case ServiceMethodKind.GetAll:
            case ServiceMethodKind.Count:
                w.Line($"{signature} => new(ServicePath, {XmlDoc.Literal(method.EntitySetName!)});");
                break;
            case ServiceMethodKind.GetByKey:
                var keys = string.Join(", ", method.Parameters.Select(p => $"[{XmlDoc.Literal(p.EdmName)}] = {Id(p.CsName)}"));
                w.Line($"{signature} => new(ServicePath, new Dictionary<string, object?> {{ {keys} }}, {XmlDoc.Literal(method.EntitySetName!)});");
                break;
            case ServiceMethodKind.Create:
            case ServiceMethodKind.Update:
            case ServiceMethodKind.Delete:
                w.Line($"{signature} => new(ServicePath, {Id(EntityParameterName(method.Entity!))}, {XmlDoc.Literal(method.EntitySetName!)});");
                break;
            default:
                var name = XmlDoc.Literal(method.ImportName!);
                if (method.Parameters.Count == 0)
                {
                    w.Line($"{signature} => new(ServicePath, {name});");
                    break;
                }

                w.Block(signature, () =>
                {
                    WriteParameterDictionary(w, method.Parameters);
                    w.Line($"return new(ServicePath, {name}, parameters);");
                });
                break;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // shared

    private void WriteHeader(CodeWriter w, IEnumerable<string> usings, string ns, bool pragmaObsolete)
    {
        foreach (var line in HeaderLines(settings.FileHeader))
        {
            w.Line(line);
        }

        if (settings.VersionReference is not null)
        {
            w.Line($"// Generated by the OData v4 code generator of the SAP Cloud SDK for .NET, version {settings.VersionReference}");
        }

        w.Line("// <auto-generated/>");
        w.Blank();
        w.Line("#nullable enable");
        if (pragmaObsolete)
        {
            w.Line("#pragma warning disable CS0618 // The service is deprecated.");
        }

        w.Blank();

        var all = new List<string>(usings);
        if (settings.EmitSystemUsings)
        {
            all.Add("System");
            all.Add("System.Collections.Generic");
        }

        foreach (var u in all.Distinct(StringComparer.Ordinal).OrderBy(u => IsSystem(u) ? 0 : 1).ThenBy(u => u, StringComparer.Ordinal))
        {
            w.Line($"using {u};");
        }

        w.Blank();
        w.Line($"namespace {ns};");
        w.Blank();
    }

    private static bool IsSystem(string ns) => ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal);

    private static IEnumerable<string> HeaderLines(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            yield break;
        }

        foreach (var raw in header.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            var trimmed = line.TrimStart();
            yield return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal) || trimmed.StartsWith('*')
                ? line
                : "// " + line;
        }
    }

    private void WriteTypeAttributes(CodeWriter w, string? attribute)
    {
        if (settings.EmitGeneratedCodeAttribute)
        {
            w.Line($"[global::System.CodeDom.Compiler.GeneratedCode({XmlDoc.Literal(GeneratorName)}, {XmlDoc.Literal(typeof(SourceEmitter).Assembly.GetName().Version?.ToString(3) ?? "1.0.0")})]");
        }

        if (attribute is not null)
        {
            w.Line(attribute);
        }
    }

    private static void AddParagraphs(List<string> lines, IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            AddParagraph(lines, text);
        }
    }

    private static void AddParagraph(List<string> lines, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            lines.Add($"<para>{XmlDoc.Escape(text)}</para>");
        }
    }

    [GeneratedRegex("""^<a href='(?<href>[^']*)'>(?<text>.*)</a>$""", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex AnchorRegex();
}
