namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>A simple or complex property of a generated entity or complex type.</summary>
internal sealed class GenProperty
{
    internal required string EdmName { get; init; }

    /// <summary>The C# property name.</summary>
    internal required string Name { get; init; }

    /// <summary>The backing field, for example <c>_id</c>.</summary>
    internal required string FieldName { get; init; }

    /// <summary>The C# type including nullability, for example <c>int?</c> or <c>ProductCount?</c>.</summary>
    internal required string TypeName { get; init; }

    /// <summary>The primitive kind; <see langword="null"/> for complex properties.</summary>
    internal EdmSimpleKind? SimpleKind { get; init; }

    internal bool IsSimple => SimpleKind is not null;

    internal bool IsKey { get; set; }

    internal string BasicDescription { get; init; } = "";

    internal string DetailedDescription { get; init; } = "";

    internal string Constraints { get; init; } = "";
}

/// <summary>A navigation property of a generated entity.</summary>
internal sealed class GenNavigationProperty
{
    internal required string EdmName { get; init; }

    /// <summary>The C# name, also used in <c>Fetch{Name}Async</c> and the builder method.</summary>
    internal required string Name { get; init; }

    internal required string FieldName { get; init; }

    internal required EdmEntityType TargetType { get; init; }

    internal bool IsMany { get; init; }

    internal string Description { get; init; } = "";

    /// <summary>The generated target, if its entity set is generated. Navigation properties to other entities are skipped.</summary>
    internal GenEntity? Target { get; set; }
}

/// <summary>A generated complex type.</summary>
internal sealed class GenComplexType
{
    internal required EdmComplexType Type { get; init; }

    internal required string ClassName { get; init; }

    internal required string FileName { get; init; }

    internal List<GenProperty> Properties { get; } = [];

    internal string BasicDescription { get; init; } = "";

    internal string DetailedDescription { get; init; } = "";
}

/// <summary>A service method for one entity set.</summary>
internal sealed class GenServiceMethod
{
    internal required ApiFunction Function { get; init; }

    internal required string MethodName { get; init; }

    internal required GenEntity Entity { get; init; }

    internal required string EntitySetName { get; init; }

    /// <summary>The name of the entity parameter of create, update and delete.</summary>
    internal string ParameterName { get; init; } = "";

    internal string HelperName { get; init; } = "";
}

/// <summary>A generated entity class with its helper types.</summary>
internal sealed class GenEntity
{
    internal required EdmEntityType Type { get; init; }

    internal required string ClassName { get; init; }

    internal required string FileName { get; init; }

    internal required string HelpersFileName { get; init; }

    internal required string SelectableName { get; init; }

    internal required string AllFieldsName { get; init; }

    internal required string FieldClassName { get; init; }

    internal required string LinkClassName { get; init; }

    internal required string OneToOneLinkClassName { get; init; }

    internal required string BuilderName { get; init; }

    /// <summary>The entity set the collection name comes from: the first set of the type.</summary>
    internal required string EntitySetName { get; init; }

    internal string BasicDescription { get; init; } = "";

    internal string DetailedDescription { get; init; } = "";

    internal List<GenProperty> Properties { get; } = [];

    internal List<GenNavigationProperty> NavigationProperties { get; } = [];

    /// <summary>The navigation properties whose target is generated.</summary>
    internal IEnumerable<GenNavigationProperty> ActiveNavigationProperties => NavigationProperties.Where(n => n.Target is not null);

    internal List<GenProperty> KeyProperties { get; } = [];

    internal bool HasStream => Type.HasStream;

    /// <summary>The names of the helper classes by operation; created when the first service method needs them.</summary>
    internal Dictionary<ApiFunction, string> HelperNames { get; } = [];

    /// <summary>The parameter names of the key in the by-key helper and the service method.</summary>
    internal List<string> KeyParameterNames { get; } = [];
}

/// <summary>A parameter of a function import.</summary>
internal sealed class GenParameter
{
    internal required string EdmName { get; init; }

    internal required string Name { get; init; }

    internal required string TypeName { get; init; }

    internal bool IsNullable { get; init; }

    internal string Description { get; init; } = "";
}

/// <summary>A generated function import.</summary>
internal sealed class GenFunctionImport
{
    internal required string EdmName { get; init; }

    internal required string HelperClassName { get; init; }

    internal required string FileName { get; init; }

    internal required string MethodName { get; init; }

    /// <summary>The value type argument of the helper base class (<c>TObject</c>).</summary>
    internal required string ValueTypeName { get; init; }

    /// <summary>The result type argument of the helper base class (<c>TResult</c>).</summary>
    internal required string ResultTypeName { get; init; }

    internal bool IsCollection { get; init; }

    internal required string HttpMethodExpression { get; init; }

    internal List<GenParameter> Parameters { get; } = [];

    internal string Description { get; init; } = "";
}

/// <summary>Everything the emitters need to write the code of one service.</summary>
internal sealed class GenService
{
    internal required string Name { get; init; }

    internal required string Title { get; init; }

    /// <summary>The base name of the service classes, for example <c>SdkGroceryStore</c>.</summary>
    internal required string ClassName { get; init; }

    internal required string NamespaceName { get; init; }

    internal required ServiceDetails Details { get; init; }

    internal required string RootNamespace { get; init; }

    internal required string EdmNamespace { get; init; }

    internal bool IsDeprecated { get; init; }

    internal string? DeprecationNotice { get; init; }

    internal bool LinkToApiBusinessHub { get; init; }

    internal bool PojosOnly { get; init; }

    internal List<GenEntity> Entities { get; } = [];

    internal List<GenComplexType> ComplexTypes { get; } = [];

    /// <summary>The entity service methods in the order of generation.</summary>
    internal List<GenServiceMethod> EntityMethods { get; } = [];

    internal List<GenFunctionImport> FunctionImports { get; } = [];

    internal string InterfaceName => $"I{ClassName}Service";

    internal string ImplementationName => $"Default{ClassName}Service";

    internal string BatchInterfaceName => $"I{ClassName}ServiceBatch";

    internal string ChangeSetInterfaceName => $"I{ClassName}ServiceBatchChangeSet";

    internal string BatchImplementationName => $"Default{ClassName}ServiceBatch";

    internal string ChangeSetImplementationName => $"Default{ClassName}ServiceBatchChangeSet";

    internal string EntityNamespace => CSharp.Namespace(RootNamespace, "Namespaces", NamespaceName);

    internal string BatchNamespace => EntityNamespace + ".Batch";

    internal string ServiceNamespace => CSharp.Namespace(RootNamespace, "Services");
}
