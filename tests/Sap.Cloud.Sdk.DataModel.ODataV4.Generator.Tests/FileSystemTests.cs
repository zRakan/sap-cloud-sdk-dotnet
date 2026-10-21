namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>Writing, overwriting and deleting, the service name mappings file and the "keep existing signatures" feature.</summary>
public sealed class FileSystemTests
{
    private static readonly string s_keepSignatures = Path.Combine(AppContext.BaseDirectory, "TestData", "KeepExistingSignaturesTest");

    private static ODataV4GeneratorOptions Options(string input, string output, Action<ODataV4GeneratorOptionsBuilder>? configure = null)
    {
        var builder = new ODataV4GeneratorOptionsBuilder { Input = input, Output = output };
        configure?.Invoke(builder);
        return builder.Build();
    }

    private sealed class ODataV4GeneratorOptionsBuilder
    {
        public string Input { get; set; } = string.Empty;

        public string Output { get; set; } = string.Empty;

        public bool Overwrite { get; set; }

        public bool Delete { get; set; }

        public bool KeepSignatures { get; set; } = true;

        public string? Mappings { get; set; }

        public ODataV4GeneratorOptions Build() => new()
        {
            InputDirectory = Input,
            OutputDirectory = Output,
            RootNamespace = TestEdmx.RootNamespace,
            DefaultBasePath = "/",
            OverwriteFiles = Overwrite,
            DeleteTargetDirectory = Delete,
            KeepExistingSignatures = KeepSignatures,
            ServiceNameMappingsFile = Mappings,
        };
    }

    private static async Task<string> WriteInputAsync(string name, string edmx)
    {
        var directory = TestEdmx.NewDirectory();
        var input = Path.Combine(directory, "in");
        Directory.CreateDirectory(input);
        await File.WriteAllTextAsync(Path.Combine(input, name), edmx);
        return directory;
    }

    private static readonly string s_simple = TestEdmx.Document(TestEdmx.Schema("N", """
        <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
        <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
        """));

    [Fact]
    public async Task Files_are_written_to_the_output_directory_without_byte_order_mark_and_with_lf()
    {
        var directory = await WriteInputAsync("Test.edmx", s_simple);
        var output = Path.Combine(directory, "out");

        var result = await ODataV4Generator.GenerateAsync(Options(Path.Combine(directory, "in"), output));

        Assert.NotEmpty(result.Files);
        foreach (var file in result.Files)
        {
            Assert.Equal(Path.Combine(output, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)), file.FullPath);
            var bytes = await File.ReadAllBytesAsync(file.FullPath);
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.Equal(file.Content, System.Text.Encoding.UTF8.GetString(bytes));
            Assert.DoesNotContain((byte)'\r', bytes);
        }

        Assert.True(File.Exists(Path.Combine(output, "Namespaces", "Test", "Thing.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Services", "ITestService.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Services", "DefaultTestService.cs")));
    }

    [Fact]
    public async Task Existing_files_are_only_overwritten_on_request()
    {
        var directory = await WriteInputAsync("Test.edmx", s_simple);
        var input = Path.Combine(directory, "in");
        var output = Path.Combine(directory, "out");
        await ODataV4Generator.GenerateAsync(Options(input, output));
        var thing = Path.Combine(output, "Namespaces", "Test", "Thing.cs");
        await File.WriteAllTextAsync(thing, "changed");

        var exception = await Assert.ThrowsAsync<ODataGeneratorWriteException>(() => ODataV4Generator.GenerateAsync(Options(input, output)));
        Assert.Contains("Cannot override existing file", exception.Message, StringComparison.Ordinal);
        Assert.Equal("changed", await File.ReadAllTextAsync(thing));

        await ODataV4Generator.GenerateAsync(Options(input, output, o => o.Overwrite = true));
        Assert.NotEqual("changed", await File.ReadAllTextAsync(thing));
    }

    [Fact]
    public async Task Delete_target_directory_removes_stale_files_but_keeps_the_directory()
    {
        var directory = await WriteInputAsync("Test.edmx", s_simple);
        var input = Path.Combine(directory, "in");
        var output = Path.Combine(directory, "out");
        Directory.CreateDirectory(Path.Combine(output, "Old"));
        await File.WriteAllTextAsync(Path.Combine(output, "Old", "Stale.cs"), "stale");
        await File.WriteAllTextAsync(Path.Combine(output, "Stale2.cs"), "stale");

        await ODataV4Generator.GenerateAsync(Options(input, output, o => o.Delete = true));

        Assert.False(Directory.Exists(Path.Combine(output, "Old")));
        Assert.False(File.Exists(Path.Combine(output, "Stale2.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Namespaces", "Test", "Thing.cs")));
    }

    [Fact]
    public async Task Dry_run_writes_and_deletes_nothing()
    {
        var directory = await WriteInputAsync("Test.edmx", s_simple);
        var output = Path.Combine(directory, "out");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "Keep.cs"), "keep");

        var result = await ODataV4Generator.GenerateAsync(new ODataV4GeneratorOptions
        {
            InputDirectory = Path.Combine(directory, "in"),
            OutputDirectory = output,
            DefaultBasePath = "/",
            DeleteTargetDirectory = true,
            DryRun = true,
        });

        Assert.NotEmpty(result.Files);
        Assert.Equal(["Keep.cs"], Directory.EnumerateFileSystemEntries(output).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Two_services_that_generate_the_same_files_are_rejected()
    {
        var directory = await WriteInputAsync("A.edmx", s_simple);
        var input = Path.Combine(directory, "in");
        await File.WriteAllTextAsync(Path.Combine(input, "B.edmx"), s_simple);
        var mappings = Path.Combine(directory, "mappings.properties");
        await File.WriteAllTextAsync(mappings, "A.className = Same\nA.packageName = Same\nB.className = Same\nB.packageName = Same\n");

        var exception = await Assert.ThrowsAsync<ODataGeneratorException>(() =>
            ODataV4Generator.GenerateAsync(Options(input, Path.Combine(directory, "out"), o => o.Mappings = mappings)));

        Assert.Contains("More than one service generates the file", exception.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // service name mappings

    [Fact]
    public async Task Service_name_mappings_are_created_with_the_names_that_were_used()
    {
        var directory = await WriteInputAsync("Product_Api_Service.edmx", s_simple);
        var mappings = Path.Combine(directory, "serviceNameMappings.properties");

        var result = await ODataV4Generator.GenerateAsync(Options(Path.Combine(directory, "in"), Path.Combine(directory, "out"), o => o.Mappings = mappings));

        Assert.Contains(result.Files, f => f.RelativePath == "Namespaces/Product/Thing.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Services/IProductService.cs");
        Assert.Equal(
            "\n# Product Api Service\nProduct_Api_Service.className = Product\nProduct_Api_Service.packageName = Product\n",
            await File.ReadAllTextAsync(mappings));
    }

    [Fact]
    public async Task Existing_service_name_mappings_decide_the_names_and_unrelated_entries_are_kept()
    {
        var directory = await WriteInputAsync("Product_Api_Service.edmx", s_simple);
        var mappings = Path.Combine(directory, "serviceNameMappings.properties");
        await File.WriteAllTextAsync(
            mappings,
            "# my own comment\nOther.className = Other\n\n# Product Api Service\nProduct_Api_Service.className = Goods\nProduct_Api_Service.packageName = goods.items\n");

        var result = await ODataV4Generator.GenerateAsync(Options(Path.Combine(directory, "in"), Path.Combine(directory, "out"), o => o.Mappings = mappings));

        Assert.Contains(result.Files, f => f.RelativePath == "Namespaces/goods/items/Thing.cs");
        Assert.Contains("namespace Test.Gen.Namespaces.goods.items;", TestEdmx.Content(result, "Namespaces/goods/items/Thing.cs"), StringComparison.Ordinal);
        Assert.Contains(result.Files, f => f.RelativePath == "Services/IGoodsService.cs");
        var text = await File.ReadAllTextAsync(mappings);
        Assert.Contains("Other.className = Other", text, StringComparison.Ordinal);
        Assert.Contains("# my own comment", text, StringComparison.Ordinal);
        Assert.Contains("Product_Api_Service.className = Goods", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_mapped_names_skip_the_service()
    {
        var directory = await WriteInputAsync("Test.edmx", s_simple);
        var mappings = Path.Combine(directory, "serviceNameMappings.properties");
        await File.WriteAllTextAsync(mappings, "Test.className = class\n");

        var result = await ODataV4Generator.GenerateAsync(Options(Path.Combine(directory, "in"), Path.Combine(directory, "out"), o => o.Mappings = mappings));

        var summary = Assert.Single(result.Services);
        Assert.False(summary.Generated);
        Assert.Contains("not a valid class name", summary.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_name_mappings_round_trip()
    {
        var file = Path.Combine(TestEdmx.NewDirectory(), "mappings.properties");
        var mappings = await ServiceNameMappings.LoadAsync(file);
        Assert.Null(mappings.GetString("a.className"));

        mappings.PutString("a.className", "A", "First", "service");
        mappings.PutString("a.packageName", "a");
        await mappings.SaveAsync();

        var text = await File.ReadAllTextAsync(file);
        Assert.Equal("\n# First service\na.className = A\na.packageName = a\n", text);
        var loaded = await ServiceNameMappings.LoadAsync(file);
        Assert.Equal("A", loaded.GetString("a.className"));
        Assert.Equal("a", loaded.GetString("a.packageName"));
        Assert.Equal(text, loaded.Render());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // keep existing signatures (Java: KeepExistingSignaturesTest)

    private static async Task<Dictionary<string, string>> ReadOutputAsync(string output)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(output, "*.cs", SearchOption.AllDirectories))
        {
            result[Path.GetRelativePath(output, file).Replace('\\', '/')] =await File.ReadAllTextAsync(file);
        }

        return result;
    }

    [Fact]
    public async Task Existing_signatures_survive_a_change_of_the_parameter_order()
    {
        var output = Path.Combine(TestEdmx.NewDirectory(), "out");
        await ODataV4Generator.GenerateAsync(Options(Path.Combine(s_keepSignatures, "FirstNameLastName"), output));
        var before = await ReadOutputAsync(output);
        var service = before["Services/IMetadataService.cs"];
        Assert.Contains("GetSimplePersonByKey(string firstName, string lastName)", service, StringComparison.Ordinal);
        Assert.Contains("Greet(string firstName, string lastName)", service, StringComparison.Ordinal);
        Assert.Contains("BookTicket(string firstName, string lastName, string from, string to)", service, StringComparison.Ordinal);
        Assert.Contains("Greet(string firstName, string lastName)", before["Namespaces/Metadata/SimplePerson.cs"], StringComparison.Ordinal);

        await ODataV4Generator.GenerateAsync(Options(Path.Combine(s_keepSignatures, "LastNameFirstName"), output, o => o.Overwrite = true));

        var after = await ReadOutputAsync(output);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var (name, content) in before)
        {
            var expected = content.Split('\n');
            var actual = after[name].Split('\n');
            var line = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            Assert.True(line == -1 && expected.Length == actual.Length, $"{name} differs at line {line + 1}:\n{(line >= 0 ? expected[line] : string.Empty)}\n{(line >= 0 ? actual[line] : string.Empty)}");
        }
    }

    [Fact]
    public async Task Without_keeping_signatures_the_new_order_is_generated()
    {
        var output = Path.Combine(TestEdmx.NewDirectory(), "out");
        await ODataV4Generator.GenerateAsync(Options(Path.Combine(s_keepSignatures, "FirstNameLastName"), output));
        var before = await ReadOutputAsync(output);

        await ODataV4Generator.GenerateAsync(Options(Path.Combine(s_keepSignatures, "LastNameFirstName"), output, o =>
        {
            o.Overwrite = true;
            o.KeepSignatures = false;
        }));

        var after = await ReadOutputAsync(output);
        Assert.NotEqual(before["Services/IMetadataService.cs"], after["Services/IMetadataService.cs"]);
        Assert.Contains("GetSimplePersonByKey(string lastName, string firstName)", after["Services/IMetadataService.cs"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_parameter_adds_an_overload_that_keeps_the_old_one()
    {
        var output = Path.Combine(TestEdmx.NewDirectory(), "out");
        var firstRun = await WriteInputAsync("Test.edmx", OperationWith("<Parameter Name=\"A\" Type=\"Edm.String\"/>"));
        await ODataV4Generator.GenerateAsync(Options(Path.Combine(firstRun, "in"), output));

        var secondRun = await WriteInputAsync("Test.edmx", OperationWith("<Parameter Name=\"B\" Type=\"Edm.Int32\" Nullable=\"false\"/><Parameter Name=\"A\" Type=\"Edm.String\"/>"));
        var result = await ODataV4Generator.GenerateAsync(Options(Path.Combine(secondRun, "in"), output, o => o.Overwrite = true));

        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        Assert.Contains("Compute(string? a);", service, StringComparison.Ordinal);
        Assert.Contains("Compute(string? a, int b);", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Compute(int b, string? a)", service, StringComparison.Ordinal);
        TestEdmx.AssertCompiles(result);

        static string OperationWith(string parameters) => TestEdmx.Document(TestEdmx.Schema("N", $"""
            <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <Function Name="Compute">{parameters}<ReturnType Type="Edm.String"/></Function>
            <EntityContainer Name="Container">
              <EntitySet Name="Things" EntityType="N.Thing"/>
              <FunctionImport Name="Compute" Function="N.Compute"/>
            </EntityContainer>
            """));
    }

    [Fact]
    public async Task A_removed_or_changed_key_breaks_the_existing_signature()
    {
        var output = Path.Combine(TestEdmx.NewDirectory(), "out");
        await ODataV4Generator.GenerateAsync(Options(Path.Combine(s_keepSignatures, "FirstNameLastName"), output));

        await Assert.ThrowsAsync<ODataGeneratorWriteException>(() =>
            ODataV4Generator.GenerateAsync(Options(Path.Combine(s_keepSignatures, "FirstNameLastNameFoo"), output, o => o.Overwrite = true)));
    }

    [Fact]
    public async Task Overloaded_operations_do_not_confuse_the_signature_scan()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <Function Name="Check" IsBound="true"><Parameter Name="thing" Type="N.Thing"/><ReturnType Type="Edm.Boolean"/></Function>
            <Function Name="Check" IsBound="true"><Parameter Name="thing" Type="N.Thing"/><Parameter Name="really" Type="Edm.Boolean"/><ReturnType Type="Edm.Boolean"/></Function>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));
        var directory = await WriteInputAsync("Test.edmx", edmx);
        var output = Path.Combine(directory, "out");

        var first = await ODataV4Generator.GenerateAsync(Options(Path.Combine(directory, "in"), output));
        var second = await ODataV4Generator.GenerateAsync(Options(Path.Combine(directory, "in"), output, o => o.Overwrite = true));

        Assert.Equal(first.Files.Select(f => f.Content), second.Files.Select(f => f.Content));
        Assert.Empty(second.Warnings);
    }
}
