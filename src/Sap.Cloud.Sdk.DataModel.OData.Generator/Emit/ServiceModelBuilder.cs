using System.Globalization;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// Turns the EDM model of a service into the generation model: decides which entity sets, complex types and function imports are
/// generated, assigns the C# names (resolving clashes) and determines the service methods.
/// </summary>
/// <remarks>
/// Port of <c>EntitySetProcessor</c> and the processing half of <c>NamespaceClassGenerator</c>. The order of the steps is the
/// order of Java, because the order decides which of two clashing names gets the suffix.
/// </remarks>
internal sealed class ServiceModelBuilder
{
    private static readonly string[] ReservedServiceMembers =
        ["Batch", "WithServicePath", "DefaultServicePath", "Equals", "GetHashCode", "ToString", "GetType", "MemberwiseClone", "Finalize"];

    private readonly EdmModel _model;
    private readonly INamingStrategy _naming;
    private readonly ODataGeneratorOptions _options;
    private readonly ICollection<string> _warnings;
    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<ApiFunction>> _allowedFunctions;
    private readonly GenService _service;

    private readonly NamingContext _typeNames = new(NameEquality.CaseInsensitive);
    private readonly NamingContext _fileNames = new(NameEquality.CaseInsensitive);
    private readonly NamingContext _serviceMembers = new();
    private readonly Dictionary<string, GenEntity> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenComplexType> _complexTypes = new(StringComparer.Ordinal);

    internal ServiceModelBuilder(
        EdmModel model,
        GenService service,
        INamingStrategy naming,
        ODataGeneratorOptions options,
        IReadOnlyDictionary<string, IReadOnlyCollection<ApiFunction>> allowedFunctions,
        ICollection<string> warnings)
    {
        _model = model;
        _service = service;
        _naming = naming;
        _options = options;
        _allowedFunctions = allowedFunctions;
        _warnings = warnings;
        _typeNames.Reserve(CSharp.ReservedTypeNames);
        _serviceMembers.Reserve(ReservedServiceMembers);
    }

    internal void Build()
    {
        var entitySets = SelectEntitySets();
        var newEntities = new List<GenEntity>();
        foreach (var entitySet in entitySets)
        {
            var entityType = entitySet.EntityType!;
            var created = false;
            if (!_entities.TryGetValue(entityType.QualifiedName, out var entity))
            {
                entity = BuildEntity(entityType, entitySet);
                _entities[entityType.QualifiedName] = entity;
                _service.Entities.Add(entity);
                newEntities.Add(entity);
                created = true;
            }

            if (_options.GeneratePojosOnly || (!created && !_options.ServiceMethodsPerEntitySet))
            {
                continue;
            }
            AddServiceMethods(entity, entitySet);
        }

        // second pass: the navigation targets are known now
        foreach (var entity in newEntities)
        {
            foreach (var navigation in entity.NavigationProperties)
            {
                if (_entities.TryGetValue(navigation.TargetType.QualifiedName, out var target))
                {
                    navigation.Target = target;
                }
                else
                {
                    _warnings.Add(
                        $"Unable to generate code for navigation property {navigation.EdmName} of entity {entity.ClassName}: Associated entity type {navigation.TargetType.Name} is either not found or its entity set has been filtered out.");
                }
            }
        }

        if (!_options.GeneratePojosOnly)
        {
            BuildFunctionImports();
        }
    }

    // ----- Entity sets ----------------------------------------------------------------------------------------------

    private List<EdmEntitySet> SelectEntitySets()
    {
        var all = _model.EntitySets;
        if (_options.IncludedEntitySets is not { Count: > 0 } included)
        {
            return [.. all];
        }
        foreach (var name in included.Where(n => all.All(s => s.Name != n)))
        {
            _warnings.Add($"Entity set '{name}' was not found in the EDMX for service '{_service.Name}', skipping.");
        }
        return all.Where(s => included.Contains(s.Name)).ToList();
    }

    private GenEntity BuildEntity(EdmEntityType entityType, EdmEntitySet entitySet)
    {
        var className = UniqueTypeName(_naming.GenerateClassName(entityType.Name, entityType.Label));
        var entity = new GenEntity
        {
            Type = entityType,
            ClassName = className,
            FileName = _fileNames.EnsureUniqueName(className),
            HelpersFileName = _fileNames.EnsureUniqueName(className + "FluentHelpers"),
            SelectableName = _typeNames.EnsureUniqueName($"I{className}Selectable"),
            AllFieldsName = _typeNames.EnsureUniqueName($"{className}AllFields"),
            FieldClassName = _typeNames.EnsureUniqueName($"{className}Field"),
            LinkClassName = _typeNames.EnsureUniqueName($"{className}Link"),
            OneToOneLinkClassName = _typeNames.EnsureUniqueName($"{className}OneToOneLink"),
            BuilderName = $"{className}Builder",
            EntitySetName = entitySet.Name,
            BasicDescription = Descriptions.Basic(entityType),
            DetailedDescription = Descriptions.Detailed(entityType),
        };

        var members = NewMemberContext(className, entity.BuilderName);
        foreach (var property in entityType.Properties)
        {
            entity.Properties.Add(BuildProperty(property, members));
        }
        foreach (var key in entityType.Keys)
        {
            var property = entity.Properties.First(p => p.EdmName == key);
            property.IsKey = true;
            entity.KeyProperties.Add(property);
        }

        foreach (var navigation in entityType.NavigationProperties)
        {
            entity.NavigationProperties.Add(BuildNavigationProperty(navigation, members));
        }

        var parameters = new NamingContext();
        parameters.Reserve(["servicePath", "entityCollection"]);
        foreach (var key in entity.KeyProperties)
        {
            entity.KeyParameterNames.Add(parameters.EnsureUniqueName(NamingUtils.ToParameterName(key.Name)));
        }
        return entity;
    }

    private static NamingContext NewMemberContext(string className, string builderName)
    {
        var members = new NamingContext(NameEquality.CaseInsensitive);
        members.Reserve(CSharp.ReservedMemberNames);
        members.Reserve([className, builderName]);
        return members;
    }

    private GenProperty BuildProperty(EdmProperty property, NamingContext members)
    {
        var name = members.EnsureUniqueName(_naming.GeneratePropertyName(property.Name, property.Label));
        var fieldName = members.EnsureUniqueName("_" + NamingUtils.UncapitalizeFirst(name));
        var type = property.Type!;
        var constraints = Descriptions.Constraints(property.Facets);
        if (type is EdmSimpleType simple)
        {
            return new GenProperty
            {
                EdmName = property.Name,
                Name = name,
                FieldName = fieldName,
                TypeName = CSharp.TypeName(simple.SimpleKind) + "?",
                SimpleKind = simple.SimpleKind,
                BasicDescription = Descriptions.Basic(property),
                DetailedDescription = Descriptions.Detailed(property),
                Constraints = constraints,
            };
        }

        var complex = GetOrBuildComplexType((EdmComplexType)type);
        return new GenProperty
        {
            EdmName = property.Name,
            Name = name,
            FieldName = fieldName,
            TypeName = complex.ClassName + "?",
            BasicDescription = Descriptions.Basic(property),
            DetailedDescription = Descriptions.Detailed(property),
            Constraints = constraints,
        };
    }

    private GenNavigationProperty BuildNavigationProperty(EdmNavigationProperty navigation, NamingContext members)
    {
        // The accessors derived from the name must not clash with other members either.
        var baseName = _naming.GenerateNavigationPropertyName(navigation.Name);
        string name;
        var attempt = 1;
        while (true)
        {
            var candidate = attempt == 1 ? baseName : string.Create(CultureInfo.InvariantCulture, $"{baseName}_{attempt}");
            var derived = new[] { $"Fetch{candidate}Async", $"Get{candidate}OrFetchAsync", $"Get{candidate}IfPresent", $"Set{candidate}", $"Add{candidate}" };
            if (!members.AlreadyUses(candidate) && derived.All(d => !members.AlreadyUses(d)))
            {
                name = candidate;
                break;
            }
            attempt++;
        }
        members.EnsureUniqueName(name);
        members.Reserve([$"Fetch{name}Async", $"Get{name}OrFetchAsync", $"Get{name}IfPresent", $"Set{name}", $"Add{name}"]);
        var fieldName = members.EnsureUniqueName("_to" + name);
        return new GenNavigationProperty
        {
            EdmName = navigation.Name,
            Name = name,
            FieldName = fieldName,
            TargetType = navigation.TargetType!,
            IsMany = navigation.Multiplicity == EdmMultiplicity.Many,
            Description = JoinDescription(Descriptions.Basic(navigation), Descriptions.Detailed(navigation)),
        };
    }

    private GenComplexType GetOrBuildComplexType(EdmComplexType type)
    {
        if (_complexTypes.TryGetValue(type.QualifiedName, out var existing))
        {
            return existing;
        }

        var className = UniqueTypeName(_naming.GenerateClassName(type.Name, type.Label));
        var complex = new GenComplexType
        {
            Type = type,
            ClassName = className,
            FileName = _fileNames.EnsureUniqueName(className),
            BasicDescription = Descriptions.Basic(type),
            DetailedDescription = Descriptions.Detailed(type),
        };
        // registered before the properties are processed: complex types may be recursive
        _complexTypes[type.QualifiedName] = complex;
        _service.ComplexTypes.Add(complex);

        var members = NewMemberContext(className, className + "Builder");
        foreach (var property in type.Properties)
        {
            complex.Properties.Add(BuildProperty(property, members));
        }
        return complex;
    }

    private string UniqueTypeName(string proposed)
    {
        var name = proposed;
        if (CSharp.ReservedTypeNames.Contains(name) || _naming.IsReservedKeyword(name.ToLowerInvariant()))
        {
            name += "Entity";
        }
        return _typeNames.EnsureUniqueName(name);
    }

    private static string JoinDescription(string basic, string detailed) =>
        basic.Length == 0 ? detailed : detailed.Length == 0 ? basic : basic + " " + detailed;

    // ----- Service methods ------------------------------------------------------------------------------------------

    private void AddServiceMethods(GenEntity entity, EdmEntitySet entitySet)
    {
        if (!_allowedFunctions.TryGetValue(entitySet.Name, out var allowed) || allowed.Count == 0)
        {
            return;
        }

        var suffix = _options.ServiceMethodsPerEntitySet ? MethodNameSuffixFromEntitySet(entitySet.Name) : entity.ClassName;
        var parameterName = NamingUtils.ToParameterName(entity.ClassName);
        foreach (var function in allowed.Order())
        {
            var helperName = HelperName(entity, function);
            var prefix = function switch
            {
                ApiFunction.Read => "GetAll",
                ApiFunction.ReadByKey => "Get",
                ApiFunction.Create => "Create",
                ApiFunction.Update => "Update",
                _ => "Delete",
            };
            var methodName = prefix + NamingUtils.CapitalizeFirst(suffix) + (function == ApiFunction.ReadByKey ? "ByKey" : "");
            _service.EntityMethods.Add(new GenServiceMethod
            {
                Function = function,
                MethodName = _serviceMembers.EnsureUniqueName(methodName),
                Entity = entity,
                EntitySetName = entitySet.Name,
                ParameterName = parameterName,
                HelperName = helperName,
            });
        }
    }

    private string HelperName(GenEntity entity, ApiFunction function)
    {
        if (entity.HelperNames.TryGetValue(function, out var existing))
        {
            return existing;
        }
        var suffix = function switch
        {
            ApiFunction.Read => "FluentHelper",
            ApiFunction.ReadByKey => "ByKeyFluentHelper",
            ApiFunction.Create => "CreateFluentHelper",
            ApiFunction.Update => "UpdateFluentHelper",
            _ => "DeleteFluentHelper",
        };
        var name = _typeNames.EnsureUniqueName(entity.ClassName + suffix);
        entity.HelperNames[function] = name;
        return name;
    }

    private static string MethodNameSuffixFromEntitySet(string entitySetName)
    {
        var name = NamingUtils.RemoveStartIgnoreCase(entitySetName, "A_");
        name = NamingUtils.ReplaceInvalidIdentifierCharacters(name, "_");
        return name.Length == 0 ? "EntitySet" : name;
    }

    // ----- Function imports -----------------------------------------------------------------------------------------

    private void BuildFunctionImports()
    {
        var functionImports = _model.FunctionImports;
        if (_options.IncludedFunctionImports is { Count: > 0 } included)
        {
            foreach (var name in included.Where(n => functionImports.All(f => f.Name != n)))
            {
                _warnings.Add($"Function import '{name}' was not found in the EDMX for service '{_service.Name}', skipping.");
            }
            functionImports = functionImports.Where(f => included.Contains(f.Name)).ToList();
        }

        foreach (var function in functionImports)
        {
            if (function.HttpMethod is null)
            {
                throw new ODataGeneratorReadException(
                    $"There was not HTTP method given for function import {function.Name}. You need to provide one via the 'm:HttpMethod' property.");
            }
            BuildFunctionImport(function);
        }
    }

    private void BuildFunctionImport(EdmFunctionImport function)
    {
        string valueType;
        string resultType;
        var isCollection = function.ReturnMultiplicity == EdmMultiplicity.Many;
        switch (function.ReturnType)
        {
            case null:
                valueType = "VoidResult";
                resultType = "VoidResult?";
                break;
            case EdmSimpleType simple:
                valueType = CSharp.TypeName(simple.SimpleKind);
                resultType = CSharp.IsReferenceType(simple.SimpleKind) ? valueType + "?" : valueType;
                break;
            case EdmComplexType complexType:
                valueType = GetOrBuildComplexType(complexType).ClassName;
                resultType = valueType + "?";
                break;
            case EdmEntityType entityType:
                if (!_entities.TryGetValue(entityType.QualifiedName, out var entity))
                {
                    _warnings.Add(
                        $"Unable to generate code for function import '{function.Name}': Return type is an entity ({entityType.Name}), but it is either not found or its entity set has been filtered out.");
                    return;
                }
                valueType = entity.ClassName;
                resultType = valueType + "?";
                break;
            default:
                _warnings.Add($"Unsupported EDM return type found for function import {function.Name}");
                return;
        }
        if (isCollection)
        {
            resultType = $"IReadOnlyList<{valueType}>";
        }

        var parameterNames = new NamingContext();
        parameterNames.Reserve(["servicePath", "_servicePath"]);
        var parameters = new List<GenParameter>();
        foreach (var parameter in function.Parameters)
        {
            if (parameter.Type is not EdmSimpleType simpleParameter)
            {
                _warnings.Add(
                    $"Skipping generation of function import {function.Name}: The function has a non-simple parameter {parameter.Name}. This feature is not yet supported by the OData V2 generator.");
                return;
            }
            var nullable = parameter.Facets.Nullable == true;
            var typeName = CSharp.TypeName(simpleParameter.SimpleKind) + (nullable ? "?" : "");
            parameters.Add(new GenParameter
            {
                EdmName = parameter.Name,
                Name = parameterNames.EnsureUniqueName(_naming.GenerateParameterName(parameter.Name, parameter.Label)),
                TypeName = typeName,
                IsNullable = nullable,
                Description = ParameterDescription(parameter),
            });
        }

        var helperName = UniqueTypeName(_naming.GenerateFluentHelperClassName(function.Name, function.Label));
        var generated = new GenFunctionImport
        {
            EdmName = function.Name,
            HelperClassName = helperName,
            FileName = _fileNames.EnsureUniqueName(helperName),
            MethodName = _serviceMembers.EnsureUniqueName(_naming.GenerateOperationMethodName(function.Name, function.Label)),
            ValueTypeName = valueType,
            ResultTypeName = resultType,
            IsCollection = isCollection,
            HttpMethodExpression = HttpMethodExpression(function.HttpMethod!),
            Description = JoinDescription(Descriptions.Basic(function), Descriptions.Detailed(function)),
        };
        generated.Parameters.AddRange(parameters);
        _service.FunctionImports.Add(generated);
    }

    private static string ParameterDescription(EdmParameter parameter)
    {
        var parts = new List<string>();
        var description = JoinDescription(Descriptions.Basic(parameter), Descriptions.Detailed(parameter));
        if (description.Length > 0)
        {
            parts.Add(description);
        }
        var constraints = Descriptions.Constraints(parameter.Facets);
        if (constraints.Length > 0)
        {
            parts.Add("Constraints: " + constraints + ".");
        }
        parts.Add($"Original parameter name from the OData EDM: {parameter.Name}.");
        return string.Join(' ', parts);
    }

    internal static string HttpMethodExpression(string httpMethod) => httpMethod.ToUpperInvariant() switch
    {
        "GET" => "HttpMethod.Get",
        "POST" => "HttpMethod.Post",
        "PUT" => "HttpMethod.Put",
        "DELETE" => "HttpMethod.Delete",
        "PATCH" => "HttpMethod.Patch",
        "HEAD" => "HttpMethod.Head",
        "OPTIONS" => "HttpMethod.Options",
        "TRACE" => "HttpMethod.Trace",
        var other => $"new HttpMethod({CSharp.Literal(other)})",
    };
}
