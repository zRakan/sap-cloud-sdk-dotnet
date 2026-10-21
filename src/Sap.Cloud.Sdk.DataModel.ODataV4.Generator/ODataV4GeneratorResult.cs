namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>The severity of a message of the generator.</summary>
public enum GeneratorMessageLevel
{
    /// <summary>Progress and skipped constructs that are expected.</summary>
    Info,

    /// <summary>Something was skipped or renamed that the caller might want to know about.</summary>
    Warning,
}

/// <summary>A message that the generator produced while reading and generating.</summary>
/// <param name="Level">The severity.</param>
/// <param name="Text">The message.</param>
/// <param name="Source">The input file the message belongs to, if any.</param>
public sealed record GeneratorMessage(GeneratorMessageLevel Level, string Text, string? Source);

/// <summary>A generated source file.</summary>
/// <param name="RelativePath">The path relative to the output directory, with <c>/</c> separators.</param>
/// <param name="FullPath">The full path of the file in the output directory.</param>
/// <param name="Content">The source text (UTF-8 without byte order mark, <c>\n</c> line breaks).</param>
public sealed record GeneratedFile(string RelativePath, string FullPath, string Content);

/// <summary>What the generator did for one input file.</summary>
/// <param name="ServiceName">The service name: the file name without extension.</param>
/// <param name="SourceFile">The input file.</param>
/// <param name="Title">The title of the service, if the file could be read.</param>
/// <param name="ClassName">The base name of the service classes, if known.</param>
/// <param name="Namespace">The namespace of the entity classes, if known.</param>
/// <param name="ServicePath">The default service path, if known.</param>
/// <param name="EntityCount">The number of generated entity classes.</param>
/// <param name="ComplexTypeCount">The number of generated complex type classes.</param>
/// <param name="EnumTypeCount">The number of generated enumerations.</param>
/// <param name="OperationCount">The number of generated function and action methods (bound and unbound overloads counted once).</param>
/// <param name="IsDeprecated">Whether the generated service is marked as obsolete.</param>
/// <param name="Generated">Whether code was generated for the service.</param>
/// <param name="FailureReason">Why the file was skipped, if it was.</param>
public sealed record GeneratedServiceSummary(
    string ServiceName,
    string SourceFile,
    string? Title,
    string? ClassName,
    string? Namespace,
    string? ServicePath,
    int EntityCount,
    int ComplexTypeCount,
    int EnumTypeCount,
    int OperationCount,
    bool IsDeprecated,
    bool Generated,
    string? FailureReason);

/// <summary>The outcome of <see cref="ODataV4Generator.GenerateAsync"/>.</summary>
public sealed class ODataV4GeneratorResult
{
    internal ODataV4GeneratorResult(
        IReadOnlyList<GeneratedFile> files,
        IReadOnlyList<GeneratedServiceSummary> services,
        IReadOnlyList<GeneratorMessage> messages,
        bool failureDueToWarningsNecessary)
    {
        Files = files;
        Services = services;
        Messages = messages;
        FailureDueToWarningsNecessary = failureDueToWarningsNecessary;
    }

    /// <summary>The generated files in a stable order (services in file order, then types by name, then the service files).</summary>
    public IReadOnlyList<GeneratedFile> Files { get; }

    /// <summary>One summary per input file that was considered, in the order of the files.</summary>
    public IReadOnlyList<GeneratedServiceSummary> Services { get; }

    /// <summary>All messages in the order in which they occurred.</summary>
    public IReadOnlyList<GeneratorMessage> Messages { get; }

    /// <summary>The messages with <see cref="GeneratorMessageLevel.Warning"/>.</summary>
    public IEnumerable<GeneratorMessage> Warnings => Messages.Where(m => m.Level == GeneratorMessageLevel.Warning);

    /// <summary>
    /// <see langword="true"/> if <see cref="ODataV4GeneratorOptions.FailOnWarning"/> is set and there were warnings; a
    /// build tool should fail then (Java: <c>failureDueToWarningsNecessary()</c>).
    /// </summary>
    public bool FailureDueToWarningsNecessary { get; }
}
