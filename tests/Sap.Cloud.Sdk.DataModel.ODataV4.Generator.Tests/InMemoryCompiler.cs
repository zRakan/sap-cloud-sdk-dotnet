using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>Compiles generated code in memory against the runtime library and the platform assemblies.</summary>
internal static class InMemoryCompiler
{
    // The global usings that <ImplicitUsings>enable</ImplicitUsings> adds in Microsoft.NET.Sdk projects.
    private const string ImplicitUsings =
        """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    private static readonly Lazy<IReadOnlyList<MetadataReference>> s_references = new(LoadReferences);

    /// <summary>Compiles the sources and returns the diagnostics of severity error and warning.</summary>
    /// <param name="files">The generated files.</param>
    /// <returns>The diagnostics.</returns>
    public static IReadOnlyList<Diagnostic> Compile(IEnumerable<GeneratedFile> files)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = files
            .Select(f => CSharpSyntaxTree.ParseText(f.Content, options, path: f.RelativePath))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, options, path: "ImplicitUsings.cs"))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratedCode",
            trees,
            s_references.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        return [.. compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning)];
    }

    private static List<MetadataReference> LoadReferences()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is not available.");
        return [.. trusted.Split(Path.PathSeparator).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }
}
