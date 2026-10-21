using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Details;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Generation;

internal sealed record ProcessorSettings(
    bool PojosOnly,
    bool ServiceMethodsPerEntitySet,
    IReadOnlySet<string>? IncludedEntitySets,
    IReadOnlySet<string>? IncludedFunctionImports,
    IReadOnlySet<string>? IncludedActionImports);

/// <summary>
/// Turns the EDM model of one service into the generation model: it decides which entity sets, types and operations
/// are generated and what their C# names are.
/// </summary>
/// <remarks>
/// Port of <c>EntitySetProcessor</c>, <c>NamespaceClassGenerator</c> (the model part) and the model part of
/// <c>ServiceClassGenerator</c>. Types are generated on demand, in the order in which Java generates them: the
/// entity sets with their property types, then the entities that navigation properties lead to, then function
/// imports, action imports, bound functions and bound actions.
/// </remarks>
internal sealed class ServiceProcessor
{
    private static readonly string[] s_objectMembers =
        ["Equals", "GetHashCode", "GetType", "ToString", "MemberwiseClone", "Finalize", "ReferenceEquals"];

    private static readonly string[] s_vdmObjectMembers =
    [
        "ODataType", "TypeInfo", "GetCustomFieldNames", "GetCustomFields", "SetCustomField", "HasCustomField",
        "GetCustomField", "GetAnnotationProperties", "GetKey", "GetChangedFields", "RememberChangedField",
        "ResetChangedFields", "ODataTypeAnnotations", "ODataVersionAnnotations",
    ];

    private static readonly string[] s_entityMembers = ["VersionIdentifier", "EntityCollection", "DefaultServicePath", "All"];

    // Names that the emitted code declares itself, or that it refers to in an expression (where a member of the
    // same name would hide the type).
    private static readonly string[] s_generatedMembers =
    [
        "Fields", "Builder", "Build", "s_typeInfo", "SimpleProperty", "ComplexProperty", "NavigationProperty",
        "BoundFunction", "BoundAction", "VdmTypeInfoBuilder", "ODataEntityKey",
    ];

    private static readonly string[] s_reservedTypeNames =
    [
        "List", "Dictionary", "Guid", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan", "String", "Object", "Boolean",
        "Byte", "SByte", "Int16", "Int32", "Int64", "Single", "Double", "Decimal", "IEnumerable", "IReadOnlyList",
        "Task", "Action", "Func", "Nullable", "Exception", "Type", "Attribute", "Obsolete", "ObsoleteAttribute", "Array",
        "VdmEntity", "VdmComplex", "VdmObject", "VdmEnum", "IVdmEnum", "IVdmEntitySet", "VdmVoid", "SimpleProperty",
        "ComplexProperty", "NavigationProperty", "ISimpleProperty", "BoundFunction", "BoundAction", "VdmTypeInfo",
        "VdmTypeInfoBuilder", "VdmObjectJsonConverter", "VdmEnumJsonConverter", "JsonConverter", "JsonConverterAttribute",
        "ODataEntityKey", "GetAllRequestBuilder", "CountRequestBuilder", "GetByKeyRequestBuilder", "CreateRequestBuilder",
        "UpdateRequestBuilder", "DeleteRequestBuilder", "BatchRequestBuilder", "SingleValueFunctionRequestBuilder",
        "CollectionValueFunctionRequestBuilder", "SingleValueActionRequestBuilder", "CollectionValueActionRequestBuilder",
        "ServiceWithNavigableEntities", "IServiceWithNavigableEntities", "GeneratedCode", "GeneratedCodeAttribute",

        // members that every entity or complex type declares: a class cannot have a member named like itself
        "Fields", "Builder", "TypeInfo", "EntityCollection", "DefaultServicePath", "GetKey",
    ];

    private static readonly string[] s_serviceMembers =
    [
        "WithServicePath", "Batch", "ServicePath", "ForEntity", "WithFunction", "ApplyAction", "ApplyFunction",
        "DefaultServicePath", "Dispose",
    ];

    private static readonly string[] s_enumMembers =
        ["Name", "Value", "Values", "GetConstant", "s_values", "Equals", "GetHashCode", "GetType", "ToString", "MemberwiseClone", "Finalize", "ReferenceEquals"];

    private readonly EdmModel _model;
    private readonly GenService _service;
    private readonly INamingStrategy _naming;
    private readonly ProcessorSettings _settings;
    private readonly IReadOnlyDictionary<string, SortedSet<ApiFunction>> _allowed;
    private readonly LegacySignatureScanner _legacy;
    private readonly Action<string> _warn;
    private readonly Action<string> _info;

    private readonly Dictionary<string, GenEntity> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenComplex> _complexes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GenEnum> _enums = new(StringComparer.Ordinal);
    private readonly NamingContext _typeNames;
    private readonly NamingContext _serviceMethodNames = new();
    private readonly Dictionary<string, string> _importMethodNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unboundSignatures = new(StringComparer.Ordinal);

    public ServiceProcessor(
        EdmModel model,
        GenService service,
        INamingStrategy naming,
        ProcessorSettings settings,
        IReadOnlyDictionary<string, SortedSet<ApiFunction>> allowed,
        LegacySignatureScanner legacy,
        Action<string> warn,
        Action<string> info)
    {
        _model = model;
        _service = service;
        _naming = naming;
        _settings = settings;
        _allowed = allowed;
        _legacy = legacy;
        _warn = warn;
        _info = info;
        _typeNames = new NamingContext(NameEquality.CaseInsensitive, info);
        _typeNames.Reserve(s_reservedTypeNames);
        _typeNames.Reserve([service.InterfaceName, service.ImplementationName, service.ClassName]);
        _serviceMethodNames.Reserve(s_serviceMembers);
        _serviceMethodNames.Reserve(s_objectMembers);
        _serviceMethodNames.Reserve([service.InterfaceName, service.ImplementationName]);
    }

    private string InterfaceFile => "Services/" + _service.InterfaceName + ".cs";

    public void Process()
    {
        var container = _model.Container;
        if (container is null)
        {
            _warn("The EDMX does not contain an entity container; only types that are referenced by operations can be generated.");
        }

        var created = ProcessEntitySets(container);
        foreach (var entity in created)
        {
            LinkNavigations(entity);
        }

        if (!_settings.PojosOnly)
        {
            ProcessFunctionImports(container);
            ProcessActionImports(container);

            // Bound operations come last so that the types they are bound to have already been generated.
            var bound = _model.Operations.Where(o => o.IsBound).ToList();
            foreach (var operation in bound.Where(o => o.IsFunction).Concat(bound.Where(o => !o.IsFunction)))
            {
                ProcessBoundOperation(operation);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // entity sets

    private List<GenEntity> ProcessEntitySets(EdmEntityContainer? container)
    {
        var created = new List<GenEntity>();
        if (container is null)
        {
            return created;
        }

        if (container.Singletons.Count > 0)
        {
            _info($"Singletons are not supported and skipped: {string.Join(", ", container.Singletons)}.");
        }

        var include = _settings.IncludedEntitySets;
        var sets = container.EntitySets.AsEnumerable();
        if (include is { Count: > 0 })
        {
            sets = sets.Where(s => include.Contains(s.Name)).ToList();
            foreach (var missing in include.Where(n => container.EntitySets.All(s => s.Name != n)).OrderBy(n => n, StringComparer.Ordinal))
            {
                _warn($"Entity set '{missing}' was not found in the EDMX for service '{_service.Name}', skipping.");
            }
        }

        foreach (var set in sets)
        {
            var type = _model.FindType(set.EntityTypeName) as EdmEntityType
                ?? throw new ODataGeneratorReadException($"The entity type '{set.EntityTypeName}' of entity set '{set.Name}' could not be found.");
            _info($"  Found entity type {type.Name} from set {set.Name}");

            var first = !_entities.TryGetValue(type.FullName, out var entity);
            if (first)
            {
                entity = ProcessEntity(type, set.Name);
                created.Add(entity);
            }

            if (_settings.PojosOnly || (!first && !_settings.ServiceMethodsPerEntitySet))
            {
                continue;
            }

            if (_allowed.TryGetValue(set.Name, out var functions))
            {
                foreach (var function in functions)
                {
                    AddEntitySetFunction(function, entity!, set);
                }
            }
        }

        return created;
    }

    private void AddEntitySetFunction(ApiFunction function, GenEntity entity, EdmEntitySet set)
    {
        var suffix = _settings.ServiceMethodsPerEntitySet ? RemoveStartIgnoreCase(set.Name, "A_") : entity.CsName;
        switch (function)
        {
            case ApiFunction.Read:
                AddEntityMethod(ServiceMethodKind.GetAll, NamingUtils.DeriveGetAllMethodName(suffix), entity, set);
                AddEntityMethod(ServiceMethodKind.Count, NamingUtils.DeriveCountMethodName(suffix), entity, set);
                break;
            case ApiFunction.ReadByKey:
                AddGetByKey(NamingUtils.DeriveGetByKeyMethodName(suffix), entity, set);
                break;
            case ApiFunction.Create:
                AddEntityMethod(ServiceMethodKind.Create, NamingUtils.DeriveCreateMethodName(suffix), entity, set);
                break;
            case ApiFunction.Update:
                AddEntityMethod(ServiceMethodKind.Update, NamingUtils.DeriveUpdateMethodName(suffix), entity, set);
                break;
            case ApiFunction.Delete:
                AddEntityMethod(ServiceMethodKind.Delete, NamingUtils.DeriveDeleteMethodName(suffix), entity, set);
                break;
            case ApiFunction.Navigate:
                entity.IsEntitySet = true;
                break;
            default:
                break;
        }
    }

    private void AddEntityMethod(ServiceMethodKind kind, string proposedName, GenEntity entity, EdmEntitySet set)
    {
        _service.Methods.Add(new ServiceMethod
        {
            Kind = kind,
            Name = _serviceMethodNames.EnsureUniqueName(proposedName),
            Entity = entity,
            EntitySetName = set.Name,
        });
    }

    private void AddGetByKey(string proposedName, GenEntity entity, EdmEntitySet set)
    {
        if (entity.Keys.Count == 0)
        {
            _warn($"Entity type {entity.EdmName} of entity set {set.Name} has no key; no {proposedName} method is generated.");
            return;
        }

        var name = _serviceMethodNames.EnsureUniqueName(proposedName);
        var parameterNames = new NamingContext();
        var parameters = entity.Keys.Select(key => new GenParameter
        {
            EdmName = key.EdmName,
            CsName = parameterNames.EnsureUniqueName(NamingUtils.ToCamelCase(key.CsName)),
            TypeText = key.KeyParameterType,
            Description = key.BasicDescription,
        }).ToList();

        var sets = KeepSignatures(InterfaceFile, name, parameters, overloads: 1);
        if (sets.Count != 1)
        {
            throw new ODataGeneratorWriteException(
                $"Entity in class {_service.InterfaceName} has different key parameters than last time the code generator run: {name} has {sets.Count} parameter groups.");
        }

        _service.Methods.Add(new ServiceMethod
        {
            Kind = ServiceMethodKind.GetByKey,
            Name = name,
            Entity = entity,
            EntitySetName = set.Name,
            Parameters = sets[0],
        });
    }

    // ---------------------------------------------------------------------------------------------------------------
    // entities, complex types, enums

    private GenEntity ProcessEntity(EdmEntityType type, string entityCollection)
    {
        var csName = _typeNames.EnsureUniqueName(_naming.GenerateClassName(type.Name, type.Annotations.Label));
        var builderName = _typeNames.EnsureUniqueName(csName + "Builder");
        var entity = new GenEntity(csName, type.Name, type.FullName)
        {
            EntityCollection = entityCollection,
            EdmType = type,
            BuilderName = builderName,
            Members = NewMembers(csName, builderName, includeEntityMembers: true),
            Descriptions = Descriptions(type.Annotations),
        };
        _entities[type.FullName] = entity;
        _service.Entities.Add(entity);

        ProcessProperties(entity, type);

        foreach (var keyRef in type.Key)
        {
            var key = entity.Properties.FirstOrDefault(p => string.Equals(p.EdmName, keyRef.EffectiveName, StringComparison.Ordinal));
            if (key is null)
            {
                _warn($"Key property {keyRef.Name} of entity type {type.FullName} was not found in the set of processed properties.");
                continue;
            }

            key.IsKey = true;
            entity.Keys.Add(key);
        }

        foreach (var navigation in type.AllNavigationProperties())
        {
            var target = _model.ResolveType(navigation.RawType);
            if (target.Type is not EdmEntityType targetType)
            {
                throw new ODataGeneratorReadException($"The navigation property {navigation.Name} of {type.FullName} does not lead to an entity type.");
            }

            var csNavigation = AllocateNavigationName(entity.Members, _naming.GenerateNavigationPropertyName(navigation.Name), target.IsCollection);
            entity.Navigations.Add(new GenNavigation
            {
                EdmName = navigation.Name,
                CsName = csNavigation.Name,
                FieldName = entity.Members.EnsureUniqueName("_to" + csNavigation.Name),
                IsCollection = target.IsCollection,
                TargetEdmType = targetType,
                IfPresentName = csNavigation.IfPresent,
                SetName = csNavigation.Set,
                AddName = csNavigation.Add,
            });
        }

        return entity;
    }

    private static (string Name, string IfPresent, string Set, string? Add) AllocateNavigationName(NamingContext members, string proposed, bool isCollection)
    {
        var candidate = proposed;
        var counter = 1;
        while (true)
        {
            var ifPresent = "Get" + candidate + "IfPresent";
            var set = "Set" + candidate;
            var add = isCollection ? "Add" + candidate : null;
            if (!members.AlreadyUses(candidate) && !members.AlreadyUses(ifPresent) && !members.AlreadyUses(set) && (add is null || !members.AlreadyUses(add)))
            {
                members.Reserve(candidate);
                members.Reserve(ifPresent);
                members.Reserve(set);
                if (add is not null)
                {
                    members.Reserve(add);
                }

                return (candidate, ifPresent, set, add);
            }

            counter++;
            candidate = proposed + "_" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void LinkNavigations(GenEntity entity)
    {
        if (entity.NavigationsLinked)
        {
            return;
        }

        entity.NavigationsLinked = true;
        foreach (var navigation in entity.Navigations)
        {
            if (!_entities.TryGetValue(navigation.TargetEdmType.FullName, out var target))
            {
                target = ProcessEntity(navigation.TargetEdmType, navigation.EdmName);
                LinkNavigations(target);
            }

            navigation.Target = target;
        }
    }

    private GenComplex ProcessComplex(EdmComplexType type)
    {
        if (_complexes.TryGetValue(type.FullName, out var existing))
        {
            return existing;
        }

        _info($"  Found complex type {type.Name}");
        var csName = _typeNames.EnsureUniqueName(type.Name);
        var builderName = _typeNames.EnsureUniqueName(csName + "Builder");
        var complex = new GenComplex(csName, type.Name, type.FullName)
        {
            BuilderName = builderName,
            Members = NewMembers(csName, builderName, includeEntityMembers: false),
            Descriptions = Descriptions(type.Annotations),
        };

        // registered before the properties are processed: complex types may refer to each other
        _complexes[type.FullName] = complex;
        _service.Complexes.Add(complex);
        ProcessProperties(complex, type);
        return complex;
    }

    private GenEnum ProcessEnum(EdmEnumType type)
    {
        if (_enums.TryGetValue(type.FullName, out var existing))
        {
            return existing;
        }

        _info($"  Found enum type {type.Name}");
        var csName = _typeNames.EnsureUniqueName(type.Name);
        var genEnum = new GenEnum(csName, type.Name, type.FullName) { Descriptions = Descriptions(type.Annotations) };
        var members = new NamingContext();
        members.Reserve(s_enumMembers);
        members.Reserve(csName);
        long next = 0;
        foreach (var member in type.Members)
        {
            var value = long.TryParse(member.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : next;
            next = value + 1;
            genEnum.Members.Add(new GenEnumMember(members.EnsureUniqueName(_naming.GenerateEnumMemberName(member.Name)), member.Name, value));
        }

        _enums[type.FullName] = genEnum;
        _service.Enums.Add(genEnum);
        return genEnum;
    }

    private NamingContext NewMembers(string csName, string builderName, bool includeEntityMembers)
    {
        var members = new NamingContext();
        members.Reserve(s_objectMembers);
        members.Reserve(s_vdmObjectMembers);
        members.Reserve(s_generatedMembers);
        members.Reserve(csName);
        members.Reserve(builderName);
        members.Reserve(_service.InterfaceName);
        if (includeEntityMembers)
        {
            members.Reserve(s_entityMembers);
        }

        return members;
    }

    private void ProcessProperties(GenStructured owner, EdmStructuredType type)
    {
        foreach (var property in type.AllProperties())
        {
            var reference = _model.ResolveType(property.RawType);
            PrimitiveMapping? primitive = null;
            GenEnum? genEnum = null;
            GenComplex? genComplex = null;
            GenPropertyKind kind;
            switch (reference.Type)
            {
                case EdmPrimitiveType primitiveType:
                    primitive = PrimitiveMappings.Get(primitiveType.Kind);
                    if (primitive is null)
                    {
                        _warn($"Encountered unsupported property type {primitiveType.Name} of property {property.Name} in {type.FullName}; the property is skipped.");
                        continue;
                    }

                    kind = GenPropertyKind.Simple;
                    break;
                case EdmEnumType enumType:
                    genEnum = ProcessEnum(enumType);
                    kind = GenPropertyKind.Enum;
                    break;
                case EdmComplexType complexType:
                    genComplex = ProcessComplex(complexType);
                    kind = GenPropertyKind.Complex;
                    break;
                default:
                    throw new ODataGeneratorReadException($"The property {property.Name} of {type.FullName} has the unsupported type {property.RawType}.");
            }

            var csName = owner.Members.EnsureUniqueName(_naming.GeneratePropertyName(property.Name, property.Annotations.Label));
            owner.Properties.Add(new GenProperty
            {
                EdmName = property.Name,
                CsName = csName,
                FieldName = owner.Members.EnsureUniqueName("_" + NamingUtils.ToCamelCase(csName)),
                Kind = kind,
                IsCollection = reference.IsCollection,
                Primitive = primitive,
                EnumType = genEnum,
                ComplexType = genComplex,
                BasicDescription = BasicDescription(property.Annotations),
                DetailedDescription = property.Annotations.LongDescription,
                Constraints = Constraints(property.Facets),
            });
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // operations

    private void ProcessFunctionImports(EdmEntityContainer? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var import in Filter(container.FunctionImports, i => i.Name, _settings.IncludedFunctionImports, "Function import"))
        {
            var functionName = _model.ResolveQualifiedName(import.FunctionName);
            var overloads = _model.Operations.Where(o => o.IsFunction && !o.IsBound && o.QualifiedName == functionName).ToList();
            if (overloads.Count == 0)
            {
                _warn($"The function '{import.FunctionName}' of function import '{import.Name}' was not found; the import is skipped.");
            }

            foreach (var function in overloads)
            {
                ProcessUnboundOperation(import.Name, function, isFunction: true);
            }
        }
    }

    private void ProcessActionImports(EdmEntityContainer? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var import in Filter(container.ActionImports, i => i.Name, _settings.IncludedActionImports, "Action import"))
        {
            var actionName = _model.ResolveQualifiedName(import.ActionName);
            var action = _model.Operations.FirstOrDefault(o => !o.IsFunction && !o.IsBound && o.QualifiedName == actionName);
            if (action is null)
            {
                _warn($"The action '{import.ActionName}' of action import '{import.Name}' was not found; the import is skipped.");
                continue;
            }

            ProcessUnboundOperation(import.Name, action, isFunction: false);
        }
    }

    private List<T> Filter<T>(List<T> all, Func<T, string> nameOf, IReadOnlySet<string>? include, string kind)
    {
        if (include is not { Count: > 0 })
        {
            return all;
        }

        foreach (var missing in include.Where(n => all.All(i => nameOf(i) != n)).OrderBy(n => n, StringComparer.Ordinal))
        {
            _warn($"{kind} '{missing}' was not found in the EDMX for service '{_service.Name}', skipping.");
        }

        return all.Where(i => include.Contains(nameOf(i))).ToList();
    }

    private void ProcessUnboundOperation(string importName, EdmOperation operation, bool isFunction)
    {
        var parameters = ProcessParameters(operation, importName);
        if (parameters is null)
        {
            return;
        }

        var result = ResolveResult(operation, importName, entitySetPath: null);
        if (result is null)
        {
            return;
        }

        var key = (isFunction ? "F:" : "A:") + importName;
        if (!_importMethodNames.TryGetValue(key, out var methodName))
        {
            methodName = _serviceMethodNames.EnsureUniqueName(_naming.GenerateOperationMethodName(importName, operation.Annotations.Label));
            _importMethodNames[key] = methodName;
        }

        var overloads = _model.Operations.Count(o => !o.IsBound && o.IsFunction == isFunction && o.QualifiedName == operation.QualifiedName);
        var sets = KeepSignatures(InterfaceFile, methodName, parameters, overloads);
        foreach (var set in sets)
        {
            var signature = key + "|" + methodName + "(" + string.Join(", ", set.Select(p => p.TypeText)) + ")";
            if (!_unboundSignatures.Add(signature))
            {
                _warn($"The overload {methodName}({string.Join(", ", set.Select(p => p.TypeText))}) of '{importName}' has the same C# signature as another overload and is skipped.");
                continue;
            }

            _service.Methods.Add(new ServiceMethod
            {
                Kind = isFunction ? ServiceMethodKind.UnboundFunction : ServiceMethodKind.UnboundAction,
                Name = methodName,
                ImportName = importName,
                Parameters = set,
                ResultType = result.TypeText,
                ReturnsCollection = result.IsCollection,
                Descriptions = Descriptions(operation.Annotations),
            });
        }
    }

    private void ProcessBoundOperation(EdmOperation operation)
    {
        if (operation.Parameters.Count == 0)
        {
            _warn($"The bound operation {operation.QualifiedName} has no binding parameter and is skipped.");
            return;
        }

        var binding = _model.ResolveType(operation.Parameters[0].RawType);
        if (binding.Type is not EdmEntityType bindingType || !_entities.TryGetValue(bindingType.FullName, out var entity))
        {
            _warn($"Operation {operation.Name} bound to type {binding.Type.FullName} will be skipped: Type is not an entity or entity is not part of an entity set.");
            return;
        }

        var parameters = ProcessParameters(operation, operation.Name);
        if (parameters is null)
        {
            return;
        }

        var result = ResolveResult(operation, operation.Name, operation.EntitySetPath);
        if (result is null)
        {
            return;
        }

        if (!entity.OperationNames.TryGetValue(operation.Name, out var methodName))
        {
            methodName = entity.Members.EnsureUniqueName(_naming.GenerateOperationMethodName(operation.Name, operation.Annotations.Label));
            entity.OperationNames[operation.Name] = methodName;
        }

        var classText = BoundClass(operation, binding.IsCollection, result, entity.CsName);
        var overloads = _model.Operations.Count(o =>
            o.IsBound
            && o.Name == operation.Name
            && o.Parameters.Count > 0
            && _model.ResolveType(o.Parameters[0].RawType).Type == binding.Type);
        var sets = KeepSignatures(_service.TypesFolder + "/" + entity.CsName + ".cs", methodName, parameters, overloads);
        foreach (var set in sets)
        {
            var signature = methodName + "(" + string.Join(", ", set.Select(p => p.TypeText)) + ")";
            if (!entity.BoundSignatures.Add(signature))
            {
                _warn($"The bound operation {operation.QualifiedName} has the same C# signature {signature} as another operation of {entity.CsName} and is skipped.");
                continue;
            }

            entity.BoundOperations.Add(new GenBoundOperation
            {
                IsFunction = operation.IsFunction,
                CsName = methodName,
                QualifiedName = operation.QualifiedName,
                ClassText = classText,
                Parameters = set,
                Descriptions = Descriptions(operation.Annotations),
            });
        }
    }

    // Overloaded operations are not merged with the signatures of the previous run: the old file contains the
    // parameter lists of all overloads, which cannot be attributed to one of them.
    private IReadOnlyList<IReadOnlyList<GenParameter>> KeepSignatures(string file, string methodName, List<GenParameter> parameters, int overloads) =>
        overloads > 1 ? [parameters] : _legacy.DetermineArguments(file, methodName, parameters, p => p.CsName);

    private static string BoundClass(EdmOperation operation, bool boundToCollection, ResultInfo result, string bindingType)
    {
        var generics = $"<{bindingType}, {result.TypeText}>";
        var returnsCollection = result.IsCollection;
        var cardinality = (boundToCollection ? "Collection" : "Single") + "To" + (returnsCollection ? "Collection" : "Single");
        if (!operation.IsFunction)
        {
            return $"BoundAction.{cardinality}{generics}";
        }

        var composableEntity = operation.IsComposable && result.Kind == ResultKind.Entity;
        return $"BoundFunction.{cardinality}{(composableEntity ? "EntityComposable" : string.Empty)}{generics}";
    }

    private List<GenParameter>? ProcessParameters(EdmOperation operation, string operationName)
    {
        var names = new NamingContext();
        names.Reserve("parameters");
        var result = new List<GenParameter>();
        foreach (var parameter in operation.OperationParameters)
        {
            var reference = _model.ResolveType(parameter.RawType);
            string item;
            switch (reference.Type)
            {
                case EdmPrimitiveType primitiveType:
                    var primitive = PrimitiveMappings.Get(primitiveType.Kind);
                    if (primitive is null)
                    {
                        _warn($"Skipping operation {operationName} generation as its parameter {parameter.Name} has the unsupported type {primitiveType.Name}.");
                        return null;
                    }

                    item = primitive.ClrType;
                    break;
                case EdmEnumType enumType:
                    item = ProcessEnum(enumType).CsName;
                    break;
                case EdmComplexType complexType:
                    item = ProcessComplex(complexType).CsName;
                    break;
                case EdmEntityType entityType:
                    if (!_entities.TryGetValue(entityType.FullName, out var entity))
                    {
                        _warn($"Skipping operation {operationName} generation as it contains entity parameter {parameter.Name} unassociated with an entity set.");
                        return null;
                    }

                    item = entity.CsName;
                    break;
                default:
                    return null;
            }

            var typeText = (reference.IsCollection ? $"List<{item}>" : item) + (parameter.Facets.Nullable ? "?" : string.Empty);
            result.Add(new GenParameter
            {
                EdmName = parameter.Name,
                CsName = names.EnsureUniqueName(_naming.GenerateParameterName(parameter.Name, parameter.Annotations.Label)),
                TypeText = typeText,
                Description = JoinedDescription(parameter.Annotations),
            });
        }

        return result;
    }

    private ResultInfo? ResolveResult(EdmOperation operation, string operationName, string? entitySetPath)
    {
        if (operation.ReturnType is null)
        {
            return new ResultInfo("VdmVoid", false, ResultKind.Void);
        }

        var reference = _model.ResolveType(operation.ReturnType.RawType);
        switch (reference.Type)
        {
            case EdmPrimitiveType primitiveType:
                var primitive = PrimitiveMappings.Get(primitiveType.Kind);
                if (primitive is null)
                {
                    _warn($"Unsupported EDM return type {primitiveType.Name} found for operation {operationName}. Skipping the operation.");
                    return null;
                }

                return new ResultInfo(primitive.ClrType, reference.IsCollection, ResultKind.Primitive);
            case EdmComplexType complexType:
                return new ResultInfo(ProcessComplex(complexType).CsName, reference.IsCollection, ResultKind.Complex);
            case EdmEntityType entityType:
                if (!_entities.TryGetValue(entityType.FullName, out var entity))
                {
                    entity = ProcessEntity(entityType, entitySetPath ?? operationName);
                    LinkNavigations(entity);
                }

                return new ResultInfo(entity.CsName, reference.IsCollection, ResultKind.Entity);
            default:
                _warn($"Unsupported EDM return type {reference.Type.Name} found for operation {operationName}. Skipping the operation.");
                return null;
        }
    }

    private sealed record ResultInfo(string TypeText, bool IsCollection, ResultKind Kind);

    private enum ResultKind
    {
        Void,
        Primitive,
        Complex,
        Entity,
    }

    // ---------------------------------------------------------------------------------------------------------------
    // descriptions

    private static string RemoveStartIgnoreCase(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    internal static string BasicDescription(EdmAnnotations annotations)
    {
        if (!string.IsNullOrEmpty(annotations.Description))
        {
            return annotations.Description;
        }

        var text = annotations.QuickInfo ?? annotations.Label;
        return text is null ? string.Empty : text.Trim();
    }

    internal static List<string> Descriptions(EdmAnnotations annotations)
    {
        var result = new List<string>();
        var basic = BasicDescription(annotations).Trim();
        if (basic.Length > 0)
        {
            result.Add(basic);
        }

        var detailed = annotations.LongDescription?.Trim();
        if (!string.IsNullOrEmpty(detailed))
        {
            result.Add(detailed);
        }

        return result;
    }

    private static string? JoinedDescription(EdmAnnotations annotations)
    {
        var descriptions = Descriptions(annotations);
        return descriptions.Count == 0 ? null : string.Join(' ', descriptions);
    }

    private static string? Constraints(EdmFacets facets)
    {
        if (facets.Nullable && string.IsNullOrEmpty(facets.DefaultValue) && facets.MaxLength is null && facets.Precision is null && facets.Scale is null)
        {
            return null;
        }

        var parts = new List<string> { facets.Nullable ? "Nullable" : "Not nullable" };
        if (!string.IsNullOrEmpty(facets.DefaultValue))
        {
            parts.Add($"Default value: {facets.DefaultValue}");
        }

        if (facets.MaxLength is { } maxLength)
        {
            parts.Add($"Maximum length: {maxLength}");
        }

        if (facets.Precision is { } precision)
        {
            parts.Add($"Precision: {precision}");
        }

        if (facets.Scale is { } scale)
        {
            parts.Add($"Scale: {scale}");
        }

        return "Constraints: " + string.Join(", ", parts);
    }
}
