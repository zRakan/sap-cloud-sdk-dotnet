namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>Writes the per-entity helper types (selectable, field and link types, read/create/update/delete helpers) and the function import helpers.</summary>
internal static class FluentHelperEmitter
{
    internal static string EmitEntityHelpers(EmitContext context, GenEntity entity)
    {
        var w = context.StartFile(
            [
                "System",
                "System.Collections.Generic",
                "Sap.Cloud.Sdk.DataModel.OData.Client.Expression",
                "Sap.Cloud.Sdk.DataModel.OData.Helper",
                "Sap.Cloud.Sdk.TypeConverter",
            ],
            context.Service.EntityNamespace);
        var name = entity.ClassName;

        CSharp.Summary(w, $"Selectable fields and navigation properties of <see cref=\"{name}\"/>.");
        using (w.Block($"public interface {entity.SelectableName} : IEntitySelectable<{name}>"))
        {
        }
        w.Blank();

        CSharp.Summary(w, $"Selects all fields of <see cref=\"{name}\"/>.");
        using (w.Block($"public sealed class {entity.AllFieldsName} : EntityFieldAll<{name}>, {entity.SelectableName}"))
        {
        }
        w.Blank();

        var field = entity.FieldClassName;
        CSharp.Summary(w, $"A field of <see cref=\"{name}\"/>.");
        w.Line("/// <typeparam name=\"TField\">The type of the field.</typeparam>");
        using (w.Block($"public class {field}<TField> : EntityField<{name}, TField>, {entity.SelectableName}"))
        {
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{field}{{TField}}\"/> class.</summary>");
            w.Line("/// <param name=\"fieldName\">The name of the field in the OData service.</param>");
            w.Line($"public {field}(string fieldName)");
            using (w.Indented())
            {
                w.Line(": base(fieldName)");
            }
            using (w.Braces())
            {
            }
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{field}{{TField}}\"/> class with a type converter.</summary>");
            w.Line("/// <param name=\"fieldName\">The name of the field in the OData service.</param>");
            w.Line("/// <param name=\"typeConverter\">Converts the value of the field.</param>");
            w.Line($"public {field}(string fieldName, IFieldValueConverter<TField>? typeConverter)");
            using (w.Indented())
            {
                w.Line(": base(fieldName, typeConverter)");
            }
            using (w.Braces())
            {
            }
        }
        w.Blank();

        var link = entity.LinkClassName;
        CSharp.Summary(w, $"A navigation property of <see cref=\"{name}\"/>.");
        w.Line("/// <typeparam name=\"TObject\">The type of the related entity.</typeparam>");
        w.Line($"public class {link}<TObject> : EntityLink<{link}<TObject>, {name}, TObject>, {entity.SelectableName}");
        using (w.Indented())
        {
            w.Line("where TObject : VdmObject");
        }
        using (w.Braces())
        {
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{link}{{TObject}}\"/> class.</summary>");
            w.Line("/// <param name=\"fieldName\">The name of the navigation property in the OData service.</param>");
            w.Line($"public {link}(string fieldName)");
            using (w.Indented())
            {
                w.Line(": base(fieldName)");
            }
            using (w.Braces())
            {
            }
            w.Blank();
            w.Line($"private {link}(EntityLink<{link}<TObject>, {name}, TObject> toClone)");
            using (w.Indented())
            {
                w.Line(": base(toClone)");
            }
            using (w.Braces())
            {
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override {link}<TObject> TranslateLinkType(EntityLink<{link}<TObject>, {name}, TObject> link) => new(link);");
        }
        w.Blank();

        var oneToOne = entity.OneToOneLinkClassName;
        CSharp.Summary(w, $"A to-one navigation property of <see cref=\"{name}\"/>, which can also be used to filter.");
        w.Line("/// <typeparam name=\"TObject\">The type of the related entity.</typeparam>");
        w.Line($"public class {oneToOne}<TObject> : {link}<TObject>, IOneToOneLink<{name}, TObject>");
        using (w.Indented())
        {
            w.Line("where TObject : VdmObject");
        }
        using (w.Braces())
        {
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{oneToOne}{{TObject}}\"/> class.</summary>");
            w.Line("/// <param name=\"fieldName\">The name of the navigation property in the OData service.</param>");
            w.Line($"public {oneToOne}(string fieldName)");
            using (w.Indented())
            {
                w.Line(": base(fieldName)");
            }
            using (w.Braces())
            {
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"public ExpressionFluentHelper<{name}> Filter(ExpressionFluentHelper<TObject> filterExpression) => FilterOnOneToOneLink(filterExpression);");
        }

        foreach (var function in Enum.GetValues<ApiFunction>())
        {
            if (!entity.HelperNames.TryGetValue(function, out var helperName))
            {
                continue;
            }
            w.Blank();
            switch (function)
            {
                case ApiFunction.Read:
                    WriteReadHelper(w, entity, helperName);
                    break;
                case ApiFunction.ReadByKey:
                    WriteByKeyHelper(w, entity, helperName);
                    break;
                default:
                    WriteModificationHelper(w, entity, function, helperName);
                    break;
            }
        }
        return w.ToString();
    }

    private static void WriteReadHelper(CodeWriter w, GenEntity entity, string helper)
    {
        var name = entity.ClassName;
        CSharp.Summary(w, $"Reads <see cref=\"{name}\"/> entities.");
        using (w.Block($"public class {helper} : FluentHelperRead<{helper}, {name}, {entity.SelectableName}>"))
        {
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{helper}\"/> class.</summary>");
            w.Line("/// <param name=\"servicePath\">The path of the OData service.</param>");
            w.Line("/// <param name=\"entityCollection\">The entity collection.</param>");
            w.Line($"public {helper}(string servicePath, string entityCollection)");
            using (w.Indented())
            {
                w.Line(": base(servicePath, entityCollection)");
            }
            using (w.Braces())
            {
            }
        }
    }

    private static void WriteByKeyHelper(CodeWriter w, GenEntity entity, string helper)
    {
        var name = entity.ClassName;
        CSharp.Summary(w, $"Reads one <see cref=\"{name}\"/> by its key.");
        using (w.Block($"public class {helper} : FluentHelperByKey<{helper}, {name}, {entity.SelectableName}>"))
        {
            w.Line("private readonly OrderedDictionary<string, object?> _key = [];");
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{helper}\"/> class.</summary>");
            w.Line("/// <param name=\"servicePath\">The path of the OData service.</param>");
            w.Line("/// <param name=\"entityCollection\">The entity collection.</param>");
            for (var i = 0; i < entity.KeyProperties.Count; i++)
            {
                w.Line($"/// <param name=\"{entity.KeyParameterNames[i]}\">{KeyParameterDoc(entity.KeyProperties[i])}</param>");
            }
            var parameters = string.Concat(entity.KeyProperties.Select((k, i) => $", {k.TypeName} {CSharp.Identifier(entity.KeyParameterNames[i])}"));
            w.Line($"public {helper}(string servicePath, string entityCollection{parameters})");
            using (w.Indented())
            {
                w.Line(": base(servicePath, entityCollection)");
            }
            using (w.Braces())
            {
                for (var i = 0; i < entity.KeyProperties.Count; i++)
                {
                    w.Line($"_key[{CSharp.Literal(entity.KeyProperties[i].EdmName)}] = {CSharp.Identifier(entity.KeyParameterNames[i])};");
                }
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line("protected override IReadOnlyDictionary<string, object?> Key => _key;");
        }
    }

    internal static string KeyParameterDoc(GenProperty key) =>
        key.BasicDescription.Length > 0
            ? CSharp.Xml(key.BasicDescription)
            : $"The {CSharp.Xml(key.EdmName)} of the entity.";

    private static void WriteModificationHelper(CodeWriter w, GenEntity entity, ApiFunction function, string helper)
    {
        var name = entity.ClassName;
        var (baseClass, summary) = function switch
        {
            ApiFunction.Create => ("FluentHelperCreate", "Creates"),
            ApiFunction.Update => ("FluentHelperUpdate", "Updates"),
            _ => ("FluentHelperDelete", "Deletes"),
        };
        CSharp.Summary(w, $"{summary} a <see cref=\"{name}\"/>.");
        using (w.Block($"public class {helper} : {baseClass}<{helper}, {name}>"))
        {
            w.Line($"private readonly {name} _entity;");
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{helper}\"/> class.</summary>");
            w.Line("/// <param name=\"servicePath\">The path of the OData service.</param>");
            w.Line("/// <param name=\"entity\">The entity.</param>");
            w.Line("/// <param name=\"entityCollection\">The entity collection.</param>");
            w.Line($"public {helper}(string servicePath, {name} entity, string entityCollection)");
            using (w.Indented())
            {
                w.Line(": base(servicePath, entityCollection)");
            }
            using (w.Braces())
            {
                w.Line("_entity = entity;");
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override {name} Entity => _entity;");
        }
    }

    internal static string EmitFunctionImport(EmitContext context, GenFunctionImport function)
    {
        var w = context.StartFile(
            [
                "System",
                "System.Collections.Generic",
                "System.Net.Http",
                "System.Threading",
                "System.Threading.Tasks",
                "Sap.Cloud.Sdk.DataModel.OData.Helper",
            ],
            context.Service.EntityNamespace);
        var helper = function.HelperClassName;
        var baseClass = function.IsCollection ? "CollectionValuedFluentHelperFunction" : "SingleValuedFluentHelperFunction";
        var summary = new List<string>();
        if (function.Description.Length > 0)
        {
            summary.Add(CSharp.Xml(function.Description));
        }
        summary.Add($"Calls the function import <b>{CSharp.Xml(function.EdmName)}</b>.");
        CSharp.Summary(w, summary.Count == 1 ? summary : ParagraphLines(summary));
        using (w.Block($"public class {helper} : {baseClass}<{helper}, {function.ValueTypeName}, {function.ResultTypeName}>"))
        {
            w.Line("private readonly OrderedDictionary<string, object?> _values = [];");
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{helper}\"/> class.</summary>");
            w.Line("/// <param name=\"servicePath\">The path of the OData service.</param>");
            foreach (var parameter in function.Parameters)
            {
                w.Line($"/// <param name=\"{parameter.Name}\">{ParameterDoc(parameter)}</param>");
            }
            var parameters = string.Concat(function.Parameters.Select(p => $", {p.TypeName} {CSharp.Identifier(p.Name)}"));
            w.Line($"public {helper}(string servicePath{parameters})");
            using (w.Indented())
            {
                w.Line(": base(servicePath)");
            }
            using (w.Braces())
            {
                foreach (var parameter in function.Parameters)
                {
                    var assignment = $"_values[{CSharp.Literal(parameter.EdmName)}] = {CSharp.Identifier(parameter.Name)};";
                    if (parameter.IsNullable)
                    {
                        using (w.Block($"if ({CSharp.Identifier(parameter.Name)} is not null)"))
                        {
                            w.Line(assignment);
                        }
                    }
                    else
                    {
                        w.Line(assignment);
                    }
                }
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override string FunctionName => {CSharp.Literal(function.EdmName)};");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line("protected override IReadOnlyDictionary<string, object?> Parameters => _values;");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override HttpMethod RequestMethod => {function.HttpMethodExpression};");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override Task<{function.ResultTypeName}> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>");
            using (w.Indented())
            {
                w.Line($"{(function.IsCollection ? "ExecuteMultipleAsync" : "ExecuteSingleAsync")}(target, cancellationToken);");
            }
        }
        return w.ToString();
    }

    internal static string ParameterDoc(GenParameter parameter) =>
        parameter.Description.Length > 0 ? CSharp.Xml(parameter.Description) : "A parameter of the function import.";

    private static List<string> ParagraphLines(List<string> paragraphs) => paragraphs.Select(p => $"<para>{p}</para>").ToList();
}
