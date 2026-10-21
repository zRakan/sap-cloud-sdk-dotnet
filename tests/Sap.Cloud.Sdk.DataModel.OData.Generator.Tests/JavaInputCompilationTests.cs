using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>
/// Runs the generator over every EDMX file in the test resources of the Java generator and compiles the output in memory against the
/// runtime library. Files that the Java generator rejects on purpose are expected to be rejected with the same reason.
/// </summary>
public class JavaInputCompilationTests
{
    /// <summary>Expected outcome of inputs that are not generated, by path relative to <c>TestData/Java</c>.</summary>
    private static readonly Dictionary<string, (ServiceGenerationStatus Status, string Reason)> ExpectedRejections = new(StringComparer.Ordinal)
    {
        // Java: EdmxValidator rejects it ("incompatible OData version"), OData V4
        ["DatamodelMetadataGenerationTest/input/odata-v4-spec.edmx"] = (ServiceGenerationStatus.Skipped, "OData V2"),
        ["oDataGeneratorIntegrationTest/invalidService/v4_service.edmx"] = (ServiceGenerationStatus.Skipped, "OData V2"),

        // Java: DataServiceVersion 42.0 is neither 1.x nor 2.x
        ["DatamodelMetadataGenerationTest/input/v2-structured-with-unexpected-version.edmx"] = (ServiceGenerationStatus.Skipped, "OData V2"),

        // Java: no DataServices element at all
        ["oDataGeneratorIntegrationTest/failOnWarning/input/noContentOnPurpose.xml"] = (ServiceGenerationStatus.Skipped, "OData V2"),

        // Java: Olingo fails on the intentionally broken tag ("Parsing of the metadata file failed")
        ["DatamodelMetadataGenerationTest/input/syntactically-incorrect-metadata.edmx"] = (ServiceGenerationStatus.Failed, "Please check that the metadata file is valid"),

        // Java: "THIS ENTITY SET REFERS TO A NON-EXISTING ENTITY TYPE"
        ["DatamodelMetadataGenerationTest/input/semantically-incorrect-metadata.edmx"] = (ServiceGenerationStatus.Failed, "cannot be resolved"),

        // Java: ServiceDetailsResolverTest.testWithoutAnyBasePath / testWithMalformedUrl
        ["ServiceDetailsResolverTest/WithoutAtomLink.edmx"] = (ServiceGenerationStatus.Failed, "service path"),
        ["ServiceDetailsResolverTest/MalformedUrl.edmx"] = (ServiceGenerationStatus.Failed, "atom:link"),

        // These files only serve the resolver tests of Java (service path, allowed functions); their entity sets refer to types of a
        // namespace the schema does not declare, which Olingo cannot resolve either ("unable to generate all VDM classes").
        ["ServiceDetailsResolverTest/Baseline.edmx"] = (ServiceGenerationStatus.Failed, "cannot be resolved"),
        ["ServiceDetailsResolverTest/EmptyNamespace.edmx"] = (ServiceGenerationStatus.Failed, "cannot be resolved"),
        ["ServiceDetailsResolverTest/NoNamespace.edmx"] = (ServiceGenerationStatus.Failed, "cannot be resolved"),
        ["ServiceDetailsResolverTest/MultipleNamespace.edmx"] = (ServiceGenerationStatus.Failed, "cannot be resolved"),
        ["AllowedFunctionsResolverTest/Baseline.edmx"] = (ServiceGenerationStatus.Failed, "cannot be resolved"),

        // no atom:link and no default base path: Java cannot determine a service path either
        ["AllowedFunctionsResolverTest/PartlyNoAnnotations.edmx"] = (ServiceGenerationStatus.Failed, "service path"),

        // Java: AllowedFunctionsResolverTest.testWithInvalidAttributeText
        ["AllowedFunctionsResolverTest/InvalidAttributeValue.edmx"] = (ServiceGenerationStatus.Failed, "Could not parse attribute"),
    };

    public static IEnumerable<object[]> Inputs() =>
        Directory
            .EnumerateFiles(Harness.JavaData, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".edmx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(Harness.JavaData, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .Select(f => new object[] { f });

    [Fact]
    public void AllJavaInputsAreCovered()
    {
        var inputs = Inputs().Select(i => (string)i[0]).ToList();
        Assert.True(inputs.Count >= 35, $"Only {inputs.Count} inputs found.");
        Assert.All(ExpectedRejections.Keys, key => Assert.Contains(key, inputs));
    }

    /// <summary>The default configuration: S/4HANA naming strategy, names from labels.</summary>
    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task DefaultOptions_OutputCompiles(string relativePath) =>
        await GenerateAndCompileAsync(relativePath, static _ => { });

    /// <summary>A different configuration: simple naming from names, service methods per entity set, links to the API hub.</summary>
    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task SimpleNamingAndEntitySetMethods_OutputCompiles(string relativePath) =>
        await GenerateAndCompileAsync(
            relativePath,
            static options =>
            {
                options.NamingStrategy = new SimpleNamingStrategy();
                options.NameSource = NameSource.Name;
                options.ServiceMethodsPerEntitySet = true;
                options.GenerateLinksToApiBusinessHub = true;
                options.DeprecationNotice = "Use something else.";
                options.FileHeader = "Copyright (c) Test\nAll rights reserved.";
                options.GenerateVersionReference = true;
            });

    /// <summary>Only the entity and complex type classes.</summary>
    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task PojosOnly_OutputCompiles(string relativePath) =>
        await GenerateAndCompileAsync(relativePath, static options => options.GeneratePojosOnly = true);

    private static async Task GenerateAndCompileAsync(string relativePath, Action<ODataGeneratorOptions> configure)
    {
        var file = Path.Combine(Harness.JavaData, relativePath);
        var mappings = Path.Combine(Path.GetDirectoryName(file)!, "serviceNameMappings.properties");
        var (result, output) = await Harness.GenerateAsync(file, configure, File.Exists(mappings) ? mappings : null);

        var summary = Assert.Single(result.Services);
        if (ExpectedRejections.TryGetValue(relativePath, out var expected))
        {
            Assert.Equal(expected.Status, summary.Status);
            Assert.Contains(expected.Reason, summary.Error, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(output, "*.cs", SearchOption.AllDirectories));
            return;
        }

        Assert.True(summary.Status == ServiceGenerationStatus.Generated, $"{relativePath}: {summary.Status} {summary.Error}");
        var diagnostics = Harness.Compile(output);
        Assert.True(diagnostics.Count == 0, $"{relativePath}:\n{Harness.Describe(diagnostics)}");
    }
}
