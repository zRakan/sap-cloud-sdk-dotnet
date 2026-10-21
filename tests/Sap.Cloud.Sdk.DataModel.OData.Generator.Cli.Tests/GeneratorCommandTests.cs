namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Cli.Tests;

public sealed class GeneratorCommandTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("sap-odata-cli-").FullName;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
        _out.Dispose();
        _err.Dispose();
    }

    private static string TestData(string file) => Path.Combine(AppContext.BaseDirectory, "TestData", file);

    private Task<int> Run(params string[] args) => GeneratorCommand.RunAsync(args, _out, _err, CancellationToken.None);

    [Fact]
    public async Task Help_PrintsUsage()
    {
        Assert.Equal(GeneratorCommand.Success, await Run("--help"));
        Assert.Contains("sap-odata-generator <v2|v4>", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("v3", "--input", "x", "--output", "y")]
    [InlineData("v2", "--output", "y")]
    [InlineData("v2", "--input", "x")]
    [InlineData("v2", "--input", "x", "--output", "y", "--unknown")]
    [InlineData("v2", "--input", "x", "--output", "y", "--include-action-imports", "a")]
    [InlineData("v4", "--input", "x", "--output", "y", "--name-source", "other")]
    [InlineData("v4", "--input")]
    public async Task InvalidArguments_AreUsageErrors(params string[] args)
    {
        Assert.Equal(GeneratorCommand.UsageError, await Run(args));
        Assert.Contains("Usage:", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsAllOptions()
    {
        var c = CommandLine.Parse([
            "v4", "-i", "a.edmx", "--input", "b.edmx", "-o", "out", "-n", "My.Ns", "--name-source", "name",
            "--naming-strategy", "simple", "--include-entity-sets", "A, B", "--include-action-imports", "X",
            "--pojos-only", "--per-entity-set", "--overwrite", "--delete-target", "--fail-on-warning"]);

        Assert.Equal("v4", c.Version);
        Assert.Equal(["a.edmx", "b.edmx"], c.Inputs);
        Assert.Equal(("out", "My.Ns"), (c.Output, c.Namespace));
        Assert.True(c.NameSourceIsName && c.SimpleNaming && c.PojosOnly && c.PerEntitySet && c.Overwrite && c.DeleteTarget && c.FailOnWarning);
        Assert.Equal(new HashSet<string> { "A", "B" }, c.EntitySets);
        Assert.Equal(new HashSet<string> { "X" }, c.ActionImports);
    }

    [Fact]
    public async Task V2_GeneratesTheGroceryStoreService()
    {
        var exitCode = await Run("v2", "--input", TestData("v2.edmx"), "--output", _output, "--namespace", "Generated.Grocery",
            "--name-source", "name", "--default-base-path", "/");

        Assert.True(exitCode == GeneratorCommand.Success, _out.ToString() + _err);
        var files = Directory.GetFiles(_output, "*.cs", SearchOption.AllDirectories);
        Assert.Contains(files, f => Path.GetFileName(f) == "Product.cs");
        Assert.Contains("namespace Generated.Grocery", File.ReadAllText(files.First(f => Path.GetFileName(f) == "Product.cs")), StringComparison.Ordinal);
        Assert.Contains("files written", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task V4_GeneratesTheTrippinService_AndFailOnWarningFails()
    {
        // Trippin.edmx has no atom:link, so a default base path is required (as in the Java generator).
        Assert.Equal(GeneratorCommand.GenerationFailed, await Run("v4", "--input", TestData("v4.edmx"), "--output", _output, "--name-source", "name"));
        Assert.Contains("Could not determine a valid default service path", _out.ToString() + _err, StringComparison.Ordinal);

        Assert.Equal(GeneratorCommand.Success, await Run("v4", "--input", TestData("v4.edmx"), "--output", _output, "--name-source", "name",
            "--default-base-path", "/odata/"));
        Assert.Contains(Directory.GetFiles(_output, "*.cs", SearchOption.AllDirectories), f => Path.GetFileName(f) == "Person.cs");

        // The sample contains one property type that is skipped with a warning.
        Assert.Equal(GeneratorCommand.GenerationFailed, await Run("v4", "--input", TestData("v4.edmx"), "--output", _output,
            "--name-source", "name", "--default-base-path", "/odata/", "--overwrite", "--fail-on-warning"));
    }

    [Fact]
    public async Task MissingInput_FailsGeneration()
    {
        Assert.NotEqual(GeneratorCommand.Success, await Run("v2", "--input", Path.Combine(_output, "missing.edmx"), "--output", _output));
    }
}
