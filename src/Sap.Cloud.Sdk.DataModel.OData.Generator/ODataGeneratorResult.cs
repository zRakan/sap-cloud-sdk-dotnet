namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>What happened to one input file.</summary>
public enum ServiceGenerationStatus
{
    /// <summary>The service was generated.</summary>
    Generated,

    /// <summary>The file was skipped because it is excluded or not an OData V2 service.</summary>
    Skipped,

    /// <summary>The file could not be read or processed; see <see cref="ServiceGenerationSummary.Error"/>.</summary>
    Failed,
}

/// <summary>The outcome for one EDMX file.</summary>
public sealed class ServiceGenerationSummary
{
    /// <summary>The service name: the file name without extension.</summary>
    public required string Name { get; init; }

    /// <summary>The EDMX file.</summary>
    public required string SourceFile { get; init; }

    /// <summary>What happened to the file.</summary>
    public required ServiceGenerationStatus Status { get; init; }

    /// <summary>The reason if the file was skipped or failed.</summary>
    public string? Error { get; init; }

    /// <summary>The service title.</summary>
    public string? Title { get; init; }

    /// <summary>The base name of the service classes, for example <c>SdkGroceryStore</c>.</summary>
    public string? ClassName { get; init; }

    /// <summary>The namespace of the entities of the service.</summary>
    public string? Namespace { get; init; }

    /// <summary>The default service path.</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>Whether the service is deprecated.</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>The number of generated entity classes.</summary>
    public int EntityCount { get; init; }

    /// <summary>The number of generated complex type classes.</summary>
    public int ComplexTypeCount { get; init; }

    /// <summary>The number of generated function imports.</summary>
    public int FunctionImportCount { get; init; }

    /// <summary>The files written for this service (full paths).</summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}

/// <summary>The result of <see cref="ODataGenerator.GenerateAsync"/>.</summary>
public sealed class ODataGeneratorResult
{
    /// <summary>All files written, as full paths in a stable order.</summary>
    public required IReadOnlyList<string> FilesWritten { get; init; }

    /// <summary>The warnings of the generation: skipped constructs and files that failed.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>One summary per input file, in the order of processing.</summary>
    public required IReadOnlyList<ServiceGenerationSummary> Services { get; init; }
}
