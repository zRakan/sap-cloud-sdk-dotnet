namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>The configuration of <see cref="ODataGenerator"/>.</summary>
/// <remarks>
/// <para>
/// Mirrors the configuration of Java's <c>DataModelGenerator</c>. Differences: the Java package name is a
/// <see cref="RootNamespace"/>; the annotation strategy, the metadata JSON files and <c>keepExistingSignatures</c> are not
/// ported (the generated classes carry <c>ODataField</c> / <c>ODataKey</c> attributes and no reflection-based
/// signatures to preserve); input can be given as files or directories.
/// </para>
/// </remarks>
public sealed class ODataGeneratorOptions
{
    /// <summary>The default root namespace, the counterpart of Java's default package <c>com.sap.cloud.sdk.s4hana.datamodel.odata</c>.</summary>
    public const string DefaultRootNamespace = "Sap.Cloud.Sdk.S4Hana.DataModel.OData";

    /// <summary>The EDMX files (<c>.edmx</c> or <c>.xml</c>) or directories to read. Directories are searched recursively.</summary>
    /// <remarks>
    /// A swagger or OpenAPI file next to an EDMX file with the same base name (<c>name.json</c>) provides the service path,
    /// the title, the deprecation state and the allowed operations.
    /// </remarks>
    public IList<string> InputPaths { get; } = [];

    /// <summary>The directory the generated files are written to. The directory structure follows the namespaces below <see cref="RootNamespace"/>.</summary>
    public string OutputDirectory { get; set; } = "target";

    /// <summary>The root namespace of the generated code; <c>.Namespaces.{Service}</c> and <c>.Services</c> are appended.</summary>
    public string RootNamespace { get; set; } = DefaultRootNamespace;

    /// <summary>
    /// A properties file that maps service names (the EDMX file names without extension) to class and namespace names
    /// (<c>Name.className</c>, <c>Name.namespaceName</c>). Missing entries are added; <see langword="null"/> keeps the mappings in memory.
    /// </summary>
    public string? ServiceNameMappingsFile { get; set; }

    /// <summary>Converts OData names to C# names. Defaults to <see cref="S4HanaNamingStrategy"/>, as in Java.</summary>
    public INamingStrategy NamingStrategy { get; set; } = new S4HanaNamingStrategy();

    /// <summary>Whether names are derived from the OData name or from <c>sap:label</c>. Applied to <see cref="NamingStrategy"/>. Defaults to <see cref="Generator.NameSource.Label"/>, as in Java.</summary>
    public NameSource NameSource { get; set; } = NameSource.Label;

    /// <summary>Deletes the output directory contents before generating.</summary>
    public bool DeleteTargetDirectory { get; set; }

    /// <summary>Allows overwriting existing files. If <see langword="false"/>, an existing file makes the generator fail.</summary>
    public bool OverwriteFiles { get; set; }

    /// <summary>Generates only the entity and complex type classes, without fluent helpers, services and batch support.</summary>
    public bool GeneratePojosOnly { get; set; }

    /// <summary>
    /// Generates service methods for every entity set. If <see langword="false"/>, only the first entity set of an entity type
    /// gets service methods.
    /// </summary>
    public bool ServiceMethodsPerEntitySet { get; set; }

    /// <summary>Adds a link to the SAP Business Accelerator Hub to the documentation of the services.</summary>
    public bool GenerateLinksToApiBusinessHub { get; set; }

    /// <summary>Adds a comment naming the generator to the generated files. Off by default to keep the output free of version noise.</summary>
    public bool GenerateVersionReference { get; set; }

    /// <summary>An ant-style, comma separated pattern of EDMX file names that are not generated.</summary>
    public string? ExcludeFilePattern { get; set; }

    /// <summary>
    /// The prefix of the service path if no swagger file gives one: <c>DefaultBasePath + namespace</c>. Without it, the
    /// <c>atom:link</c> of the metadata gives the path.
    /// </summary>
    public string? DefaultBasePath { get; set; }

    /// <summary>A comment placed at the top of every generated file, for example a copyright notice. Every line is prefixed with <c>//</c> unless it is a comment already.</summary>
    public string? FileHeader { get; set; }

    /// <summary>Restricts the generated entities to these entity sets (plus dependent complex types). <see langword="null"/> or empty means all.</summary>
    public IReadOnlyCollection<string>? IncludedEntitySets { get; set; }

    /// <summary>Restricts the generated function imports to these names. <see langword="null"/> or empty means all.</summary>
    public IReadOnlyCollection<string>? IncludedFunctionImports { get; set; }

    /// <summary>Marks every generated service as deprecated and uses this text as the notice (an empty text uses the default notice).</summary>
    public string? DeprecationNotice { get; set; }

    /// <summary>Makes <see cref="ODataGenerator.GenerateAsync"/> throw if the generation produced warnings.</summary>
    public bool FailOnWarning { get; set; }
}
