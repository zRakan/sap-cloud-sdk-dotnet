namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>Writes the entity and complex type classes.</summary>
internal static class EntityEmitter
{
    private const string ExceptionDoc =
        "/// <exception cref=\"Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions.ODataException\">The entity is unmanaged, i.e. it has not been retrieved using the OData VDM's services.</exception>";

    internal static string EmitEntity(EmitContext context, GenEntity entity)
    {
        var service = context.Service;
        var pojos = context.Options.GeneratePojosOnly;
        var usings = new List<string>(EmitContext.EntityUsings);
        if (!pojos)
        {
            usings.Add("Sap.Cloud.Sdk.TypeConverter");
            usings.Add(service.ServiceNamespace);
        }

        var w = context.StartFile(usings, service.EntityNamespace);
        var baseClass = entity.HasStream ? "VdmMediaEntity" : "VdmEntity";
        var name = entity.ClassName;
        var original = $"Original entity name from the OData EDM: <b>{CSharp.Xml(entity.Type.Name)}</b>";
        CSharp.SummaryBlock(w, Descriptions.SummaryLines("", entity.BasicDescription, entity.DetailedDescription, "", original));
        using (w.Block($"public class {name} : {baseClass}<{name}>"))
        {
            w.Line("/// <summary>The static description of the properties. It drives (de)serialization, change tracking, the entity key and equality.</summary>");
            WriteMetadata(w, name, "EntityMetadata", entity.Properties, entity.ActiveNavigationProperties);
            w.Blank();

            if (!pojos)
            {
                WriteFields(w, entity);
            }

            foreach (var property in entity.Properties)
            {
                WriteProperty(w, property, "Original property name from the OData EDM");
            }
            foreach (var navigation in entity.ActiveNavigationProperties)
            {
                WriteNavigation(w, navigation);
            }

            w.Line("/// <inheritdoc />");
            w.Line("protected override VdmTypeMetadata TypeMetadata => EntityMetadata;");
            w.Blank();
            w.Line("/// <inheritdoc />");
            w.Line($"protected override string? EntityCollection => {CSharp.Literal(entity.EntitySetName)};");
            if (!pojos)
            {
                w.Blank();
                w.Line("/// <inheritdoc />");
                w.Line($"protected override string? DefaultServicePath => {service.InterfaceName}.DefaultServicePath;");
            }
            w.Blank();
            WriteBuilder(w, name, entity.BuilderName, entity.Properties, entity.ActiveNavigationProperties);
        }
        return w.ToString();
    }

    internal static string EmitComplexType(EmitContext context, GenComplexType complex)
    {
        var service = context.Service;
        var w = context.StartFile(["System", .. EmitContext.EntityUsings.Where(u => u.StartsWith("Sap.", StringComparison.Ordinal))], service.EntityNamespace);
        var name = complex.ClassName;
        var original = $"Original complex type name from the OData EDM: <b>{CSharp.Xml(complex.Type.Name)}</b>";
        CSharp.SummaryBlock(w, Descriptions.SummaryLines("", complex.BasicDescription, complex.DetailedDescription, "", original));
        using (w.Block($"public class {name} : VdmComplex<{name}>"))
        {
            w.Line("/// <summary>The static description of the properties. It drives (de)serialization, change tracking and equality.</summary>");
            WriteMetadata(w, name, "ComplexMetadata", complex.Properties, []);
            w.Blank();
            foreach (var property in complex.Properties)
            {
                WriteProperty(w, property, "Original property name from the OData EDM");
            }
            w.Line("/// <inheritdoc />");
            w.Line("protected override VdmTypeMetadata TypeMetadata => ComplexMetadata;");
            w.Blank();
            WriteBuilder(w, name, name + "Builder", complex.Properties, []);
        }
        return w.ToString();
    }

    private static void WriteMetadata(
        CodeWriter w,
        string className,
        string fieldName,
        IReadOnlyList<GenProperty> properties,
        IEnumerable<GenNavigationProperty> navigations)
    {
        w.Line($"protected static readonly VdmTypeMetadata {fieldName} = VdmTypeMetadata.For<{className}>()");
        using (w.Indented())
        {
            foreach (var property in properties)
            {
                var key = property.IsKey ? ", key: true" : "";
                w.Line($".Property({CSharp.Literal(property.EdmName)}, static e => e.{property.FieldName}, static (e, v) => e.{property.FieldName} = v{key})");
            }
            foreach (var navigation in navigations)
            {
                var method = navigation.IsMany ? "NavigationList" : "Navigation";
                w.Line($".{method}({CSharp.Literal(navigation.EdmName)}, static e => e.{navigation.FieldName}, static (e, v) => e.{navigation.FieldName} = v)");
            }
            w.Line(".Build();");
        }
    }

    private static void WriteFields(CodeWriter w, GenEntity entity)
    {
        var name = entity.ClassName;
        w.Line("/// <summary>");
        w.Line($"/// The fields and navigation properties of {CSharp.Xml(name)} to use with the available fluent helpers.");
        w.Line("/// </summary>");
        using (w.Block("public static class Fields"))
        {
            w.Line("/// <summary>Selects all fields of the entity (<c>$select=*</c>).</summary>");
            w.Line($"public static readonly {entity.SelectableName} All = new {entity.AllFieldsName}();");
            foreach (var property in entity.Properties.Where(p => p.IsSimple))
            {
                w.Blank();
                w.Line($"/// <summary>Use with available fluent helpers to apply the <b>{CSharp.Xml(property.EdmName)}</b> field to query operations.</summary>");
                w.Line($"public static readonly {entity.FieldClassName}<{property.TypeName}> {property.Name} = new({CSharp.Literal(property.EdmName)});");
            }
            foreach (var navigation in entity.ActiveNavigationProperties)
            {
                w.Blank();
                w.Line($"/// <summary>Use with available fluent helpers to apply the <b>{CSharp.Xml(navigation.EdmName)}</b> navigation property to query operations.</summary>");
                var target = navigation.Target!.ClassName;
                var linkClass = navigation.IsMany ? entity.LinkClassName : entity.OneToOneLinkClassName;
                w.Line($"public static readonly {linkClass}<{target}> {navigation.Name} = new({CSharp.Literal(navigation.EdmName)});");
            }
            w.Blank();
            w.Line("/// <summary>Use with available fluent helpers to apply an extension field to query operations.</summary>");
            w.Line("/// <param name=\"fieldName\">The name of the extension field as returned by the OData service.</param>");
            w.Line("/// <typeparam name=\"T\">The type of the extension field when performing value comparisons.</typeparam>");
            w.Line($"public static {entity.FieldClassName}<T> Custom<T>(string fieldName) => new(fieldName);");
            w.Blank();
            w.Line("/// <summary>Use with available fluent helpers to apply an extension field to query operations.</summary>");
            w.Line("/// <param name=\"fieldName\">The name of the extension field as returned by the OData service.</param>");
            w.Line("/// <param name=\"typeConverter\">Converts the value of the extension field as returned by the OData service.</param>");
            w.Line("/// <typeparam name=\"T\">The type of the extension field when performing value comparisons.</typeparam>");
            w.Line("/// <typeparam name=\"TDomain\">The type of the extension field as returned by the OData service.</typeparam>");
            w.Line($"public static {entity.FieldClassName}<T> Custom<T, TDomain>(string fieldName, ITypeConverter<T, TDomain> typeConverter) =>");
            using (w.Indented())
            {
                w.Line("new(fieldName, FieldValueConverter.From(typeConverter));");
            }
        }
        w.Blank();
    }

    private static void WriteProperty(CodeWriter w, GenProperty property, string originalPrefix)
    {
        w.Line($"private {property.TypeName} {property.FieldName};");
        w.Blank();
        var original = $"{originalPrefix}: <b>{CSharp.Xml(property.EdmName)}</b>";
        CSharp.SummaryBlock(
            w,
            Descriptions.SummaryLines(
                property.IsKey ? "(Key Field) " : "",
                property.BasicDescription,
                property.DetailedDescription,
                property.Constraints,
                original));
        if (property.IsKey)
        {
            w.Line("[ODataKey]");
        }
        w.Line($"[ODataField({CSharp.Literal(property.EdmName)})]");
        using (w.Block($"public {property.TypeName} {property.Name}"))
        {
            w.Line($"get => {property.FieldName};");
            using (w.Block("set"))
            {
                w.Line($"RememberChangedField({CSharp.Literal(property.EdmName)}, {property.FieldName});");
                w.Line($"{property.FieldName} = value;");
            }
        }
        w.Blank();
    }

    private static void WriteNavigation(CodeWriter w, GenNavigationProperty navigation)
    {
        var nav = navigation.Name;
        var edm = CSharp.Xml(navigation.EdmName);
        var target = navigation.Target!.ClassName;
        var field = navigation.FieldName;
        var literal = CSharp.Literal(navigation.EdmName);

        if (!navigation.IsMany)
        {
            w.Line($"private {target}? {field};");
            w.Blank();
            w.Line("/// <summary>");
            w.Line($"/// Fetches the <b>{edm}</b> entity (one to one) associated with this entity. This corresponds to the OData navigation property <b>{edm}</b>.");
            DescriptionPara(w, navigation);
            w.Line("/// Please note: This method will not cache or persist the query results.");
            w.Line("/// </summary>");
            w.Line(ExceptionDoc);
            w.Line($"public async Task<{target}?> Fetch{nav}Async(CancellationToken cancellationToken = default) =>");
            using (w.Indented())
            {
                w.Line($"await FetchFieldAsSingleAsync<{target}>({literal}, cancellationToken).ConfigureAwait(false);");
            }
            w.Blank();
            w.Line($"/// <summary>Retrieval of the associated <b>{edm}</b> entity (one to one); it is fetched if not loaded yet.</summary>");
            using (w.Block($"public async Task<{target}?> Get{nav}OrFetchAsync(CancellationToken cancellationToken = default)"))
            {
                w.Line($"{field} ??= await Fetch{nav}Async(cancellationToken).ConfigureAwait(false);");
                w.Line($"return {field};");
            }
            w.Blank();
            w.Line($"/// <summary>Retrieval of the associated <b>{edm}</b> entity if it is loaded already; <see langword=\"null\"/> otherwise. Nothing is queried.</summary>");
            w.Line($"public {target}? Get{nav}IfPresent() => {field};");
            w.Blank();
            w.Line($"/// <summary>Overwrites the associated <b>{CSharp.Xml(target)}</b> entity for the loaded navigation property <b>{edm}</b>.</summary>");
            w.Line($"public void Set{nav}({target}? value) => {field} = value;");
            w.Blank();
            return;
        }

        w.Line($"private List<{target}>? {field};");
        w.Blank();
        w.Line("/// <summary>");
        w.Line($"/// Fetches the <b>{edm}</b> entities (one to many) associated with this entity. This corresponds to the OData navigation property <b>{edm}</b>.");
        DescriptionPara(w, navigation);
        w.Line("/// Please note: This method will not cache or persist the query results.");
        w.Line("/// </summary>");
        w.Line(ExceptionDoc);
        w.Line($"public async Task<IReadOnlyList<{target}>> Fetch{nav}Async(CancellationToken cancellationToken = default) =>");
        using (w.Indented())
        {
            w.Line($"await FetchFieldAsListAsync<{target}>({literal}, cancellationToken).ConfigureAwait(false);");
        }
        w.Blank();
        w.Line($"/// <summary>Retrieval of the associated <b>{edm}</b> entities (one to many); they are fetched if not loaded yet.</summary>");
        using (w.Block($"public async Task<IReadOnlyList<{target}>> Get{nav}OrFetchAsync(CancellationToken cancellationToken = default)"))
        {
            w.Line($"{field} ??= [.. await Fetch{nav}Async(cancellationToken).ConfigureAwait(false)];");
            w.Line($"return {field};");
        }
        w.Blank();
        w.Line($"/// <summary>Retrieval of the associated <b>{edm}</b> entities if they are loaded already; <see langword=\"null\"/> otherwise. Nothing is queried.</summary>");
        w.Line($"public IReadOnlyList<{target}>? Get{nav}IfPresent() => {field};");
        w.Blank();
        w.Line($"/// <summary>Overwrites the list of associated <b>{CSharp.Xml(target)}</b> entities for the loaded navigation property <b>{edm}</b>.</summary>");
        using (w.Block($"public void Set{nav}(IEnumerable<{target}> value)"))
        {
            w.Line("var values = value.ToList();");
            w.Line($"{field} ??= [];");
            w.Line($"{field}.Clear();");
            w.Line($"{field}.AddRange(values);");
        }
        w.Blank();
        w.Line($"/// <summary>Adds elements to the list of associated <b>{CSharp.Xml(target)}</b> entities for the navigation property <b>{edm}</b>.</summary>");
        using (w.Block($"public void Add{nav}(params {target}[] entity)"))
        {
            w.Line($"{field} ??= [];");
            w.Line($"{field}.AddRange(entity);");
        }
        w.Blank();
    }

    private static void DescriptionPara(CodeWriter w, GenNavigationProperty navigation)
    {
        if (navigation.Description.Length > 0)
        {
            w.Line($"/// <para>{CSharp.Xml(navigation.Description)}</para>");
        }
    }

    private static void WriteBuilder(
        CodeWriter w,
        string className,
        string builderName,
        IReadOnlyList<GenProperty> properties,
        IEnumerable<GenNavigationProperty> navigations)
    {
        var navigationList = navigations.ToList();
        var xmlName = CSharp.Xml(className);
        w.Line($"/// <summary>Starts a builder for {xmlName} instances. Values set through the builder are not tracked as changes.</summary>");
        w.Line($"public static {builderName} Builder() => new();");
        w.Blank();
        w.Line($"/// <summary>Helper class to allow for fluent creation of {xmlName} instances.</summary>");
        using (w.Block($"public sealed class {builderName}"))
        {
            w.Line($"private readonly {className} _entity = new();");
            w.Blank();
            using (w.Block($"internal {builderName}()"))
            {
                foreach (var navigation in navigationList.Where(n => n.IsMany))
                {
                    w.Line($"_entity.{navigation.FieldName} = [];");
                }
            }
            foreach (var property in properties)
            {
                w.Blank();
                w.Line($"/// <summary>Sets <b>{CSharp.Xml(property.EdmName)}</b>.</summary>");
                using (w.Block($"public {builderName} {property.Name}({property.TypeName} value)"))
                {
                    w.Line($"_entity.{property.FieldName} = value;");
                    w.Line("return this;");
                }
            }
            foreach (var navigation in navigationList)
            {
                w.Blank();
                var target = navigation.Target!.ClassName;
                if (navigation.IsMany)
                {
                    w.Line($"/// <summary>Navigation property <b>{CSharp.Xml(navigation.EdmName)}</b> for <b>{xmlName}</b> to multiple <b>{CSharp.Xml(target)}</b>.</summary>");
                    using (w.Block($"public {builderName} {navigation.Name}(params {target}[] value)"))
                    {
                        w.Line($"_entity.{navigation.FieldName}!.AddRange(value);");
                        w.Line("return this;");
                    }
                }
                else
                {
                    w.Line($"/// <summary>Navigation property <b>{CSharp.Xml(navigation.EdmName)}</b> for <b>{xmlName}</b> to single <b>{CSharp.Xml(target)}</b>.</summary>");
                    using (w.Block($"public {builderName} {navigation.Name}({target}? value)"))
                    {
                        w.Line($"_entity.{navigation.FieldName} = value;");
                        w.Line("return this;");
                    }
                }
            }
            w.Blank();
            w.Line($"/// <summary>Creates the {xmlName}; the builder must not be used afterwards.</summary>");
            w.Line($"public {className} Build() => _entity;");
        }
    }
}
