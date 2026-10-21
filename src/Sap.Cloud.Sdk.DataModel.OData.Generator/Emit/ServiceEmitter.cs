using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>Writes the service interface and its default implementation, and the batch interfaces and their implementations.</summary>
internal static partial class ServiceEmitter
{
    private const string HubLinkPrefix = "https://api.sap.com/shell/discover/contentpackage/SAPS4HANACloud/api/";

    [GeneratedRegex("^<a href='(.*)'>(.*)</a>$", RegexOptions.CultureInvariant)]
    private static partial Regex AnchorPattern();

    private static string[] ServiceUsings(GenService service) =>
    [
        "System",
        "Sap.Cloud.Sdk.DataModel.OData.Helper.Batch",
        service.EntityNamespace,
        service.BatchNamespace,
    ];

    internal static string EmitInterface(EmitContext context)
    {
        var service = context.Service;
        var w = context.StartFile(ServiceUsings(service), service.ServiceNamespace);
        WriteClassDocumentation(w, service);
        WriteDeprecation(w, service);
        using (w.Block($"public interface {service.InterfaceName} : IBatchService<{service.BatchInterfaceName}>"))
        {
            w.Line("/// <summary>The default path of the service.</summary>");
            w.Line($"const string DefaultServicePath = {CSharp.Literal(service.Details.ServiceUrl!)};");
            w.Blank();
            w.Line("/// <summary>Creates a service that uses another service path.</summary>");
            w.Line("/// <param name=\"servicePath\">The service path.</param>");
            w.Line("/// <returns>The service.</returns>");
            w.Line($"{service.InterfaceName} WithServicePath(string servicePath);");
            foreach (var method in service.EntityMethods)
            {
                w.Blank();
                WriteMethodDocumentation(w, method);
                w.Line($"{method.HelperName} {method.MethodName}({MethodParameters(method)});");
            }
            foreach (var function in service.FunctionImports)
            {
                w.Blank();
                WriteFunctionDocumentation(w, function);
                w.Line($"{function.HelperClassName} {function.MethodName}({FunctionParameters(function)});");
            }
        }
        return w.ToString();
    }

    internal static string EmitImplementation(EmitContext context)
    {
        var service = context.Service;
        var w = context.StartFile(ServiceUsings(service), service.ServiceNamespace);
        var name = service.ImplementationName;
        w.Line($"/// <summary>The default implementation of <see cref=\"{service.InterfaceName}\"/>.</summary>");
        WriteDeprecation(w, service);
        using (w.Block($"public class {name} : {service.InterfaceName}"))
        {
            w.Line("private readonly string _servicePath;");
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{name}\"/> class with the default service path.</summary>");
            using (w.Block($"public {name}()"))
            {
                w.Line($"_servicePath = {service.InterfaceName}.DefaultServicePath;");
            }
            w.Blank();
            using (w.Block($"private {name}(string servicePath)"))
            {
                w.Line("_servicePath = servicePath;");
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"public {service.InterfaceName} WithServicePath(string servicePath) => new {name}(servicePath);");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"public {service.BatchInterfaceName} Batch() => new {service.BatchImplementationName}(this, _servicePath);");
            foreach (var method in service.EntityMethods)
            {
                w.Blank();
                w.Line("/// <inheritdoc />");
                var collection = CSharp.Literal(method.EntitySetName);
                var expression = method.Function switch
                {
                    ApiFunction.Read => $"new {method.HelperName}(_servicePath, {collection})",
                    ApiFunction.ReadByKey => $"new {method.HelperName}(_servicePath, {collection}{KeyArguments(method.Entity)})",
                    _ => $"new {method.HelperName}(_servicePath, {CSharp.Identifier(method.ParameterName)}, {collection})",
                };
                w.Line($"public {method.HelperName} {method.MethodName}({MethodParameters(method)}) => {expression};");
            }
            foreach (var function in service.FunctionImports)
            {
                w.Blank();
                w.Line("/// <inheritdoc />");
                var arguments = string.Concat(function.Parameters.Select(p => ", " + CSharp.Identifier(p.Name)));
                w.Line($"public {function.HelperClassName} {function.MethodName}({FunctionParameters(function)}) => new {function.HelperClassName}(_servicePath{arguments});");
            }
        }
        return w.ToString();
    }

    internal static string EmitBatchInterfaces(EmitContext context)
    {
        var service = context.Service;
        var w = context.StartFile(BatchUsings(service), service.BatchNamespace);
        var batch = service.BatchInterfaceName;
        var changeSet = service.ChangeSetInterfaceName;
        CSharp.Summary(w, "Batch requests of the service.");
        using (w.Block($"public interface {batch} : IFluentHelperServiceBatch<{batch}, {changeSet}>"))
        {
        }
        w.Blank();
        CSharp.Summary(w, "A change set of a batch request of the service.");
        w.Line($"public interface {changeSet} :");
        using (w.Indented())
        {
            w.Line($"IFluentHelperBatchChangeSet<{changeSet}>,");
            w.Line($"IFluentHelperBatchEndChangeSet<{batch}>");
        }
        using (w.Braces())
        {
            var first = true;
            foreach (var method in ModifyingMethods(service))
            {
                if (!first)
                {
                    w.Blank();
                }
                first = false;
                var entity = CSharp.Xml(method.Entity.ClassName);
                w.Line($"/// <summary>{OperationVerb(method.Function)} a {entity} in the change set.</summary>");
                w.Line($"/// <param name=\"{method.ParameterName}\">The {entity}.</param>");
                w.Line($"{changeSet} {method.MethodName}({method.Entity.ClassName} {CSharp.Identifier(method.ParameterName)});");
            }
        }
        return w.ToString();
    }

    internal static string EmitBatchImplementation(EmitContext context)
    {
        var service = context.Service;
        var w = context.StartFile(BatchUsings(service), service.BatchNamespace);
        var name = service.BatchImplementationName;
        var batch = service.BatchInterfaceName;
        var changeSet = service.ChangeSetInterfaceName;
        CSharp.Summary(w, $"The default implementation of <see cref=\"{batch}\"/>.");
        w.Line($"public class {name} :");
        using (w.Indented())
        {
            w.Line($"BatchFluentHelperBasic<{batch}, {changeSet}>,");
            w.Line(batch);
        }
        using (w.Braces())
        {
            w.Line($"private readonly {service.InterfaceName} _service;");
            w.Line("private readonly string _servicePath;");
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{name}\"/> class.</summary>");
            w.Line("/// <param name=\"service\">The service.</param>");
            w.Line($"public {name}({service.InterfaceName} service)");
            using (w.Indented())
            {
                w.Line($": this(service, {service.InterfaceName}.DefaultServicePath)");
            }
            using (w.Braces())
            {
            }
            w.Blank();
            w.Line($"/// <summary>Initializes a new instance of the <see cref=\"{name}\"/> class.</summary>");
            w.Line("/// <param name=\"service\">The service.</param>");
            w.Line("/// <param name=\"servicePath\">The service path.</param>");
            using (w.Block($"public {name}({service.InterfaceName} service, string servicePath)"))
            {
                w.Line("_service = service;");
                w.Line("_servicePath = servicePath;");
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line("protected override string ServicePathForBatchRequest => _servicePath;");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override {batch} This => this;");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"public override {changeSet} BeginChangeSet() => new {service.ChangeSetImplementationName}(this, _service);");
        }
        return w.ToString();
    }

    internal static string EmitBatchChangeSetImplementation(EmitContext context)
    {
        var service = context.Service;
        var w = context.StartFile(BatchUsings(service), service.BatchNamespace);
        var name = service.ChangeSetImplementationName;
        var batch = service.BatchInterfaceName;
        var changeSet = service.ChangeSetInterfaceName;
        CSharp.Summary(w, $"The default implementation of <see cref=\"{changeSet}\"/>.");
        w.Line($"public class {name} :");
        using (w.Indented())
        {
            w.Line($"BatchChangeSetFluentHelperBasic<{batch}, {changeSet}>,");
            w.Line(changeSet);
        }
        using (w.Braces())
        {
            w.Line($"private readonly {service.InterfaceName} _service;");
            w.Blank();
            w.Line($"internal {name}({service.BatchImplementationName} batchFluentHelper, {service.InterfaceName} service)");
            using (w.Indented())
            {
                w.Line(": base(batchFluentHelper, batchFluentHelper)");
            }
            using (w.Braces())
            {
                w.Line("_service = service;");
            }
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override {changeSet} This => this;");
            foreach (var method in ModifyingMethods(service))
            {
                w.Blank();
                w.Line("/// <inheritdoc />");
                var adder = method.Function switch
                {
                    ApiFunction.Create => "AddRequestCreate",
                    ApiFunction.Update => "AddRequestUpdate",
                    _ => "AddRequestDelete",
                };
                var parameter = CSharp.Identifier(method.ParameterName);
                w.Line($"public {changeSet} {method.MethodName}({method.Entity.ClassName} {parameter}) => {adder}(_service.{method.MethodName}, {parameter});");
            }
        }
        return w.ToString();
    }

    private static string[] BatchUsings(GenService service) =>
    [
        "Sap.Cloud.Sdk.DataModel.OData.Helper.Batch",
        service.EntityNamespace,
        service.ServiceNamespace,
    ];

    private static IEnumerable<GenServiceMethod> ModifyingMethods(GenService service) =>
        service.EntityMethods.Where(m => m.Function is ApiFunction.Create or ApiFunction.Update or ApiFunction.Delete);

    private static string OperationVerb(ApiFunction function) => function switch
    {
        ApiFunction.Create => "Creates",
        ApiFunction.Update => "Updates",
        _ => "Deletes",
    };

    private static string KeyArguments(GenEntity entity) =>
        string.Concat(entity.KeyParameterNames.Select(n => ", " + CSharp.Identifier(n)));

    private static string MethodParameters(GenServiceMethod method)
    {
        var entity = method.Entity;
        return method.Function switch
        {
            ApiFunction.Read => "",
            ApiFunction.ReadByKey => string.Join(", ", entity.KeyProperties.Select((k, i) => $"{k.TypeName} {CSharp.Identifier(entity.KeyParameterNames[i])}")),
            _ => $"{entity.ClassName} {CSharp.Identifier(method.ParameterName)}",
        };
    }

    private static string FunctionParameters(GenFunctionImport function) =>
        string.Join(", ", function.Parameters.Select(p => $"{p.TypeName} {CSharp.Identifier(p.Name)}"));

    private static void WriteMethodDocumentation(CodeWriter w, GenServiceMethod method)
    {
        var entity = CSharp.Xml(method.Entity.ClassName);
        switch (method.Function)
        {
            case ApiFunction.Read:
                w.Line($"/// <summary>Fetches {entity} entities.</summary>");
                break;
            case ApiFunction.ReadByKey:
                w.Line($"/// <summary>Fetches a {entity} by key.</summary>");
                for (var i = 0; i < method.Entity.KeyProperties.Count; i++)
                {
                    w.Line($"/// <param name=\"{method.Entity.KeyParameterNames[i]}\">{FluentHelperEmitter.KeyParameterDoc(method.Entity.KeyProperties[i])}</param>");
                }
                break;
            default:
                var verb = method.Function switch
                {
                    ApiFunction.Create => "Creates",
                    ApiFunction.Update => "Updates",
                    _ => "Deletes",
                };
                var direction = method.Function switch
                {
                    ApiFunction.Create => "to create",
                    ApiFunction.Update => "to update",
                    _ => "to delete",
                };
                w.Line($"/// <summary>{verb} a {entity}.</summary>");
                w.Line($"/// <param name=\"{method.ParameterName}\">The {entity} {direction}.</param>");
                break;
        }
    }

    private static void WriteFunctionDocumentation(CodeWriter w, GenFunctionImport function)
    {
        w.Line($"/// <summary>Calls the function import {CSharp.Xml(function.EdmName)}.</summary>");
        foreach (var parameter in function.Parameters)
        {
            w.Line($"/// <param name=\"{parameter.Name}\">{FluentHelperEmitter.ParameterDoc(parameter)}</param>");
        }
    }

    private static void WriteClassDocumentation(CodeWriter w, GenService service)
    {
        var details = service.Details;
        var lines = new List<string> { $"Service {CSharp.Xml(service.Name)} (namespace {CSharp.Xml(service.EdmNamespace)})." };
        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            var description = CSharp.Xml(details.Description);
            if (!Regex.IsMatch(description, @"[.?!]\s*$", RegexOptions.CultureInvariant))
            {
                description += ".";
            }
            lines.Add($"<para>{description}</para>");
        }
        if (!string.IsNullOrWhiteSpace(details.ExternalDocsUrl))
        {
            var text = string.IsNullOrWhiteSpace(details.ExternalDocsDescription) ? details.ExternalDocsUrl : details.ExternalDocsDescription;
            lines.Add($"<para><see href=\"{CSharp.XmlAttribute(details.ExternalDocsUrl)}\">{CSharp.Xml(text)}</see></para>");
        }
        if (service.LinkToApiBusinessHub)
        {
            var url = HubLinkPrefix + service.Name + "?section=OVERVIEW";
            lines.Add($"<para>Reference: <see href=\"{CSharp.XmlAttribute(url)}\">SAP Business Accelerator Hub</see></para>");
        }
        var table = new List<string>();
        if (!string.IsNullOrWhiteSpace(details.Version))
        {
            table.Add($"API Version: {CSharp.Xml(details.Version)}");
        }
        if (!string.IsNullOrWhiteSpace(details.MinErpVersion))
        {
            table.Add($"Minimum ERP Version: {CSharp.Xml(details.MinErpVersion)}");
        }
        foreach (var entry in details.ExtOverview ?? [])
        {
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                table.Add($"{CSharp.Xml(entry.Name)}: {string.Join(", ", entry.Values.Select(OverviewValue))}");
            }
        }
        if (table.Count > 0)
        {
            lines.Add("<para>Details:</para>");
            lines.Add("<list type=\"bullet\">");
            lines.AddRange(table.Select(t => $"<item><description>{t}</description></item>"));
            lines.Add("</list>");
        }
        if (service.IsDeprecated)
        {
            lines.Add($"<para>Deprecated: {CSharp.Xml(DeprecationMessage(service))}</para>");
        }
        CSharp.Summary(w, lines);
    }

    private static string OverviewValue(string value)
    {
        var anchor = AnchorPattern().Match(value);
        return anchor.Success
            ? $"<see href=\"{CSharp.XmlAttribute(anchor.Groups[1].Value)}\">{CSharp.Xml(anchor.Groups[2].Value)}</see>"
            : CSharp.Xml(value);
    }

    private static void WriteDeprecation(CodeWriter w, GenService service)
    {
        if (service.IsDeprecated)
        {
            w.Line($"[Obsolete({CSharp.Literal(DeprecationMessage(service))})]");
        }
    }

    internal static string DeprecationMessage(GenService service)
    {
        var state = service.Details.StateInfo;
        var release = state?.DeprecationRelease is { Length: > 0 } r ? " as of release " + r : "";
        var date = state?.DeprecationDate is { Length: > 0 } d ? " (" + d + ")" : "";
        string suffix;
        if (state?.SuccessorApi is { Length: > 0 } successor)
        {
            suffix = $" Please use the successor API ({successor}) instead.";
        }
        else
        {
            suffix = " " + (string.IsNullOrWhiteSpace(service.DeprecationNotice) ? "Please use the OData generator to generate the service." : service.DeprecationNotice.Trim());
        }
        return $"The service and all its related classes are deprecated{release}{date}.{suffix}";
    }
}
