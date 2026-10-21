using Microsoft.CodeAnalysis;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>Builds small EDMX documents and runs the generator on them.</summary>
internal static class TestEdmx
{
    public const string RootNamespace = "Test.Gen";

    /// <summary>Wraps schema content (in the EDM namespace) in an OData v4 EDMX document.</summary>
    public static string Document(string schemas, string references = "") =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
          <edmx:Reference Uri="https://example.org/Org.OData.Core.V1.xml">
            <edmx:Include Alias="Core" Namespace="Org.OData.Core.V1"/>
          </edmx:Reference>
          <edmx:Reference Uri="https://example.org/Org.OData.Capabilities.V1.xml">
            <edmx:Include Alias="Capabilities" Namespace="Org.OData.Capabilities.V1"/>
          </edmx:Reference>
          <edmx:Reference Uri="https://example.org/com.sap.vocabularies.Common.v1.xml">
            <edmx:Include Alias="Common" Namespace="com.sap.vocabularies.Common.v1"/>
          </edmx:Reference>
          {references}
          <edmx:DataServices>
        {schemas}
          </edmx:DataServices>
        </edmx:Edmx>
        """;

    public static string Schema(string ns, string content) =>
        $"<Schema Namespace=\"{ns}\" xmlns=\"http://docs.oasis-open.org/odata/ns/edm\">\n{content}\n</Schema>";

    /// <summary>Generates in memory from an EDMX document with the given file name (the service name).</summary>
    public static async Task<(ODataV4GeneratorResult Result, string Directory)> GenerateAsync(
        string edmx,
        string fileName = "Test.edmx",
        Func<ODataV4GeneratorOptions, ODataV4GeneratorOptions>? configure = null,
        Action<string>? prepare = null)
    {
        var directory = NewDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "in"));
        await File.WriteAllTextAsync(Path.Combine(directory, "in", fileName), edmx);
        prepare?.Invoke(directory);
        var options = new ODataV4GeneratorOptions
        {
            InputDirectory = Path.Combine(directory, "in"),
            OutputDirectory = Path.Combine(directory, "out"),
            RootNamespace = RootNamespace,
            DefaultBasePath = "/service/",
            DryRun = true,
        };
        return (await ODataV4Generator.GenerateAsync(configure?.Invoke(options) ?? options), directory);
    }

    public static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "odata-v4-generator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string Content(ODataV4GeneratorResult result, string relativePath) =>
        result.Files.Single(f => f.RelativePath == relativePath).Content;

    /// <summary>Asserts that the generated files compile without errors and warnings.</summary>
    public static void AssertCompiles(ODataV4GeneratorResult result)
    {
        Assert.NotEmpty(result.Files);
        var diagnostics = InMemoryCompiler.Compile(result.Files);
        Assert.True(
            diagnostics.Count == 0,
            "Compile diagnostics:\n" + string.Join("\n", diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Take(30).Select(d => d.ToString())));
    }
}
