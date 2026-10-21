using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sap.Cloud.Sdk.DataModel.OData.Generator;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

/// <summary>Locations of test data and helpers to run the generator and compile its output.</summary>
internal static class Harness
{
    internal const string SampleRootNamespace = "Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    /// <summary>The directory with the files copied from the Java generator's test resources.</summary>
    internal static string JavaData => Path.Combine(AppContext.BaseDirectory, "TestData", "Java");

    internal static string TestData => Path.Combine(AppContext.BaseDirectory, "TestData");

    /// <summary>The repository root: the directory with the solution file.</summary>
    internal static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sap.Cloud.Sdk.slnx")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("The repository root cannot be found.");
        }
    }

    /// <summary>The checked-in sample of the runtime library tests, generated from <c>SDK_Grocery_Store.edmx</c>.</summary>
    internal static string SampleDirectory => Path.Combine(RepositoryRoot, "tests", "Sap.Cloud.Sdk.DataModel.OData.Core.Tests", "Sample");

    internal static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "sap-odata-generator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Runs the generator over one EDMX file.</summary>
    internal static async Task<(ODataGeneratorResult Result, string OutputDirectory)> GenerateAsync(
        string edmxFile,
        Action<ODataGeneratorOptions>? configure = null,
        string? mappings = null)
    {
        var output = NewTemporaryDirectory();
        var options = new ODataGeneratorOptions
        {
            OutputDirectory = output,
            RootNamespace = "Generated",
            OverwriteFiles = true,
        };
        options.InputPaths.Add(edmxFile);
        if (mappings is not null)
        {
            var copy = Path.Combine(NewTemporaryDirectory(), "serviceNameMappings.properties");
            File.Copy(mappings, copy);
            options.ServiceNameMappingsFile = copy;
        }
        configure?.Invoke(options);
        var result = await ODataGenerator.GenerateAsync(options);
        return (result, output);
    }

    /// <summary>Writes an EDMX document to a temporary file and returns its path.</summary>
    internal static string WriteEdmx(string content, string name = "Sample")
    {
        var path = Path.Combine(NewTemporaryDirectory(), name + ".edmx");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A minimal OData V2 EDMX document with the given content inside the schema.</summary>
    internal static string Edmx(string schemaContent, string ns = "Test") =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <edmx:Edmx Version="1.0" xmlns:edmx="http://schemas.microsoft.com/ado/2007/06/edmx"
                   xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
                   xmlns:sap="http://www.sap.com/Protocols/SAPData">
          <edmx:DataServices m:DataServiceVersion="2.0">
            <Schema Namespace="{ns}" xmlns="http://schemas.microsoft.com/ado/2008/09/edm">
        {schemaContent}
            </Schema>
          </edmx:DataServices>
        </edmx:Edmx>
        """;

    /// <summary>Compiles all C# files below <paramref name="directory"/> in memory against the runtime library.</summary>
    /// <returns>The error and warning diagnostics.</returns>
    internal static IReadOnlyList<Diagnostic> Compile(string directory)
    {
        var compilation = CreateCompilation(directory);
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        return emit.Diagnostics.Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToList();
    }

    /// <summary>Creates the compilation of all C# files below <paramref name="directory"/>.</summary>
    internal static CSharpCompilation CreateCompilation(string directory)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithDocumentationMode(DocumentationMode.Diagnose);
        var trees = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parseOptions, f))
            .ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
            .WithAllowUnsafe(false);
        return CSharpCompilation.Create("GeneratedCode_" + Guid.NewGuid().ToString("N"), trees, References.Value, options);
    }

    private static List<MetadataReference> LoadReferences()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var references = trusted
            .Split(Path.PathSeparator)
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        // The runtime library is referenced by the test project, but be explicit about it.
        var core = typeof(Sap.Cloud.Sdk.DataModel.OData.Helper.VdmEntity).Assembly.Location;
        if (!trusted.Contains(core, StringComparison.Ordinal))
        {
            references.Add(MetadataReference.CreateFromFile(core));
        }
        return references;
    }

    internal static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join('\n', diagnostics.Take(25).Select(d => d.ToString()));
}
