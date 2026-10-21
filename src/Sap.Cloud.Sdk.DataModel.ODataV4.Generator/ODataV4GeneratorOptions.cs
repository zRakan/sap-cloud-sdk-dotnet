namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>The configuration of <see cref="ODataV4Generator"/>.</summary>
/// <remarks>
/// Mirrors the configuration of Java's <c>DataModelGenerator</c>. Not ported: the annotation strategy (there are no
/// Java annotations to emit) and the swagger-less <c>sap:</c> attribute handling of v2.
/// </remarks>
public sealed class ODataV4GeneratorOptions
{
    /// <summary>The default root namespace.</summary>
    public const string DefaultRootNamespace = "Sap.Cloud.Sdk.S4Hana.DataModel.ODataV4";

    /// <summary>
    /// A directory that is searched recursively for <c>.edmx</c> and <c>.xml</c> files (Java: input directory). A
    /// <c>&lt;name&gt;.json</c> next to <c>&lt;name&gt;.edmx</c> is read as swagger/OpenAPI file of that service.
    /// </summary>
    public string? InputDirectory { get; init; }

    /// <summary>Individual EDMX files, in addition to <see cref="InputDirectory"/>.</summary>
    public IReadOnlyList<string> InputFiles { get; init; } = [];

    /// <summary>
    /// The directory that receives the generated files: <c>Namespaces/&lt;Service&gt;/*.cs</c> and <c>Services/*.cs</c>.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// The root namespace of the generated code (Java: package name). Entities go to
    /// <c>&lt;RootNamespace&gt;.Namespaces.&lt;Service&gt;</c>, services to <c>&lt;RootNamespace&gt;.Services</c>.
    /// </summary>
    public string RootNamespace { get; init; } = DefaultRootNamespace;

    /// <summary>
    /// The service name mappings file (see <see cref="ServiceNameMappings"/>). If set, it is read and, after the
    /// services were loaded, written back with the names that were used. <see langword="null"/> uses no file.
    /// </summary>
    public string? ServiceNameMappingsFile { get; init; }

    /// <summary>The naming strategy; <see langword="null"/> selects <see cref="DefaultNamingStrategy"/>.</summary>
    public INamingStrategy? NamingStrategy { get; init; }

    /// <summary>The source of the names; assigned to the naming strategy. Java default: <see cref="Generator.NameSource.Label"/>.</summary>
    public NameSource NameSource { get; init; } = NameSource.Label;

    /// <summary>Whether the output directory is emptied first.</summary>
    public bool DeleteTargetDirectory { get; init; }

    /// <summary>Whether existing files in the output directory may be overwritten; otherwise generation fails for them.</summary>
    public bool OverwriteFiles { get; init; }

    /// <summary>
    /// Whether the signatures of the service methods and bound operations that exist in the generated files of the
    /// output directory are kept when the order of key or operation parameters changed (Java default: <see langword="true"/>).
    /// </summary>
    public bool KeepExistingSignatures { get; init; } = true;

    /// <summary>Whether only the data classes are generated (no service, no field descriptors, no operations).</summary>
    public bool GeneratePojosOnly { get; init; }

    /// <summary>
    /// Whether the service methods are generated per entity set (named after the set) instead of once per entity type
    /// (named after the class). Java default: <see langword="false"/>.
    /// </summary>
    public bool ServiceMethodsPerEntitySet { get; init; }

    /// <summary>The entity sets to generate; <see langword="null"/> or empty means all.</summary>
    public IReadOnlySet<string>? IncludedEntitySets { get; init; }

    /// <summary>The function imports to generate; <see langword="null"/> or empty means all.</summary>
    public IReadOnlySet<string>? IncludedFunctionImports { get; init; }

    /// <summary>The action imports to generate; <see langword="null"/> or empty means all.</summary>
    public IReadOnlySet<string>? IncludedActionImports { get; init; }

    /// <summary>A comma separated list of ant-style patterns for file names that are not generated.</summary>
    public string ExcludeFilePattern { get; init; } = string.Empty;

    /// <summary>A notice that marks the generated service as obsolete; the text is used in the <c>[Obsolete]</c> message.</summary>
    public string? DeprecationNotice { get; init; }

    /// <summary>
    /// The base path that is prepended to the namespace of the EDMX to form the default service path if there is no
    /// swagger file, for example <c>/sap/opu/odata4/</c>.
    /// </summary>
    public string? DefaultBasePath { get; init; }

    /// <summary>Whether the documentation of the service links to the SAP Business Accelerator Hub.</summary>
    public bool GenerateLinksToApiBusinessHub { get; init; }

    /// <summary>
    /// Whether the files carry a comment that names the generator version. Java default is <see langword="true"/>; here
    /// it is off so that the output does not change with the version of the generator.
    /// </summary>
    public bool GenerateVersionReference { get; init; }

    /// <summary>
    /// Text that is written at the top of every file, for example a copyright notice. Lines that do not start with a
    /// comment marker get a <c>// </c> prefix.
    /// </summary>
    public string FileHeader { get; init; } = string.Empty;

    /// <summary>Whether the generated types carry <c>[GeneratedCode]</c>.</summary>
    public bool EmitGeneratedCodeAttribute { get; init; }

    /// <summary>
    /// Whether the files import <c>System</c> and <c>System.Collections.Generic</c>. Off by default because the sample
    /// shape relies on implicit usings (<c>&lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt;</c>).
    /// </summary>
    public bool EmitSystemUsings { get; init; }

    /// <summary>Whether warnings make <see cref="ODataV4GeneratorResult.FailureDueToWarningsNecessary"/> true.</summary>
    public bool FailOnWarning { get; init; }

    /// <summary>Generate in memory only; nothing is written or deleted.</summary>
    public bool DryRun { get; init; }
}
