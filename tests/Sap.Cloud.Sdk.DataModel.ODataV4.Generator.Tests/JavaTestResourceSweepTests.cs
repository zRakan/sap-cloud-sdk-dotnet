using Microsoft.CodeAnalysis;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>
/// Runs the generator over every EDMX file in the test resources of Java's OData v4 generator and compiles the output.
/// </summary>
public sealed class JavaTestResourceSweepTests
{
    /// <summary>Inputs that Java does not generate code for either, with the reason.</summary>
    private static readonly Dictionary<string, string> s_expectedSkips = new(StringComparer.Ordinal)
    {
        // Not OData v4 (Edmx Version 1.0, V2 namespaces): Java skips them as "incompatible OData version".
        ["DatamodelMetadataGenerationTest/input/odata-v2-spec.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/Baseline.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/DoubleAtomLink.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/EmptyNamespace.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/LongS4StyleAtomLink.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/MalformedUrl.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/MultipleNamespace.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/MultipleSchemas.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/NoNamespace.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/ShortS4StyleAtomLink.edmx"] = "not OData v4",
        ["ServiceDetailsResolverTest/WithoutAtomLink.edmx"] = "not OData v4",

        ["AllowedFunctionsResolverTest/PartlyNoAnnotations.edmx"] = "not OData v4",

        // The JSON next to the file is picked up as its swagger file but is none (it is a test fixture of Java's
        // AllowedFunctionsResolver); Java rejects the service with the same reason.
        ["AllowedFunctionsResolverTest/Baseline.edmx"] = "sibling Baseline.json is not a swagger file",

        // Malformed on purpose.
        ["DatamodelMetadataGenerationTest/input/semantically-inconsistent-metadata.edmx"] = "unknown property type Edm.NotExistingType",
        ["DatamodelMetadataGenerationTest/input/syntactically-inconsistent-metadata.edmx"] = "not well-formed XML",
        ["oDataGeneratorIntegrationTest/failOnWarning/input/noContentOnPurpose.xml"] = "not an EDMX document",
    };

    public static TheoryData<string> AllEdmxFiles()
    {
        var root = TestDataDirectory();
        var data = new TheoryData<string>();
        foreach (var file in Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".edmx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal))
        {
            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllEdmxFiles))]
    public async Task Generated_code_compiles_without_errors(string relativePath)
    {
        var file = Path.Combine(TestDataDirectory(), relativePath);
        var mappings = Path.Combine(Path.GetDirectoryName(file)!, "serviceNameMappings.properties");
        var options = new ODataV4GeneratorOptions
        {
            InputFiles = [file],
            OutputDirectory = Path.Combine(Path.GetTempPath(), "odata-v4-sweep-unused"),
            RootNamespace = "Sweep.Generated",
            DefaultBasePath = "/",
            ServiceNameMappingsFile = File.Exists(mappings) ? mappings : null,
            DryRun = true,
        };

        var result = await ODataV4Generator.GenerateAsync(options);
        var summary = Assert.Single(result.Services);

        if (s_expectedSkips.TryGetValue(relativePath, out var reason))
        {
            Assert.False(summary.Generated, $"{relativePath} is expected to be skipped ({reason}) but was generated.");
            Assert.Empty(result.Files);
            Assert.NotNull(summary.FailureReason);
            return;
        }

        Assert.True(summary.Generated, $"{relativePath} was skipped: {summary.FailureReason}\n{string.Join("\n", result.Warnings.Select(w => w.Text))}");
        Assert.NotEmpty(result.Files);

        var diagnostics = InMemoryCompiler.Compile(result.Files);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.True(
            warnings.Count == 0,
            $"{relativePath}: {warnings.Count} compile warnings:\n" + string.Join("\n", warnings.Take(25).Select(e => e.ToString())));
        Assert.True(
            errors.Count == 0,
            $"{relativePath}: {errors.Count} compile errors:\n" + string.Join("\n", errors.Take(25).Select(e => e.ToString())));
    }

    [Fact]
    public void Sweep_covers_the_expected_number_of_inputs()
    {
        var count = Directory.EnumerateFiles(TestDataDirectory(), "*", SearchOption.AllDirectories).Count(f => f.EndsWith(".edmx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        Assert.True(count >= 30, $"Only {count} EDMX files found in TestData.");
    }

    private static string TestDataDirectory() => Path.Combine(AppContext.BaseDirectory, "TestData");
}
