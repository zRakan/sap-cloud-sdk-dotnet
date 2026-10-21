using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>The options of the generator: filters, output control, input handling, deprecation, headers and the result.</summary>
public class GeneratorBehaviorTests
{
    private static readonly string TestService = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testService", "input", "test_service.edmx");

    private static readonly string TestServiceMappings = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testService", "input", "serviceNameMappings.properties");

    private static string[] RelativeFiles(string output) =>
        Directory.EnumerateFiles(output, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(output, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    // ----- Filters --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task IncludedEntitySets_RestrictEntitiesAndSkipNavigationsToOthers()
    {
        var (result, output) = await Harness.GenerateAsync(
            TestService,
            options =>
            {
                options.NameSource = NameSource.Name;
                options.IncludedEntitySets = ["A_TestEntity", "A_OtherTestEntity", "DoesNotExist"];
            },
            TestServiceMappings);

        var files = RelativeFiles(output);
        Assert.Contains("Namespaces/Test/TestEntityV2.cs", files);
        Assert.DoesNotContain("Namespaces/Test/TestEntityMultiLink.cs", files);
        Assert.Contains(result.Warnings, w => w.Contains("Entity set 'DoesNotExist' was not found", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Associated entity type A_TestEntityMultiLinkType is either not found or its entity set has been filtered out", StringComparison.Ordinal));
        // complex types of the included entities are generated, function imports that return filtered entities are skipped
        Assert.Contains("Namespaces/Test/TestComplex.cs", files);
        Assert.Contains(result.Warnings, w => w.Contains("TestFunctionImportUnprecedentedEntityReturnType", StringComparison.Ordinal));
        var entity = File.ReadAllText(Path.Combine(output, "Namespaces", "Test", "TestEntityV2.cs"));
        Assert.DoesNotContain("Link", entity, StringComparison.Ordinal);
        Assert.True(Harness.Compile(output).Count == 0);
    }

    [Fact]
    public async Task IncludedFunctionImports_RestrictFunctionImports()
    {
        var (result, output) = await Harness.GenerateAsync(
            TestService,
            options =>
            {
                options.NameSource = NameSource.Name;
                options.IncludedFunctionImports = ["TestFunctionImportNoReturnType", "Missing"];
            },
            TestServiceMappings);

        var files = RelativeFiles(output);
        Assert.Contains("Namespaces/Test/TestFunctionImportNoReturnFluentHelper.cs", files);
        Assert.DoesNotContain("Namespaces/Test/ContinueFluentHelper.cs", files);
        Assert.Equal(1, Assert.Single(result.Services).FunctionImportCount);
        Assert.Contains(result.Warnings, w => w.Contains("Function import 'Missing' was not found", StringComparison.Ordinal));
        Assert.True(Harness.Compile(output).Count == 0);
    }

    [Fact]
    public async Task EmptyFilters_MeanEverything()
    {
        var (_, output) = await Harness.GenerateAsync(
            TestService,
            options =>
            {
                options.IncludedEntitySets = [];
                options.IncludedFunctionImports = [];
            },
            TestServiceMappings);

        Assert.Contains("Namespaces/Test/ContinueFluentHelper.cs", RelativeFiles(output));
    }

    [Fact]
    public async Task PojosOnly_GeneratesNoServiceBatchOrFluentHelpers()
    {
        var (_, output) = await Harness.GenerateAsync(TestService, options => options.GeneratePojosOnly = true, TestServiceMappings);

        var files = RelativeFiles(output);
        Assert.All(files, f => Assert.StartsWith("Namespaces/Test/", f, StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.Contains("FluentHelper", StringComparison.Ordinal) || f.Contains("Batch", StringComparison.Ordinal));
        var entity = File.ReadAllText(Path.Combine(output, "Namespaces", "Test", "TestEntityV2.cs"));
        Assert.DoesNotContain("class Fields", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultServicePath", entity, StringComparison.Ordinal);
        Assert.Contains("FetchMultiLinkAsync", entity, StringComparison.Ordinal);
    }

    // ----- Result ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Result_SummarizesTheService()
    {
        var (result, output) = await Harness.GenerateAsync(TestService, options => options.NameSource = NameSource.Name, TestServiceMappings);

        var summary = Assert.Single(result.Services);
        Assert.Equal(ServiceGenerationStatus.Generated, summary.Status);
        Assert.Equal("test_service", summary.Name);
        Assert.Equal(TestService, summary.SourceFile);
        Assert.Equal("Test", summary.ClassName);
        Assert.Equal("Generated.Namespaces.Test", summary.Namespace);
        Assert.Equal("/sap/opu/odata/sap/API_TEST_SRV", summary.ServiceUrl);
        Assert.Equal(8, summary.EntityCount);
        Assert.Equal(4, summary.ComplexTypeCount);
        Assert.Equal(summary.Files.Order(), result.FilesWritten.Order());
        Assert.Equal(result.FilesWritten.Count, RelativeFiles(output).Length);
        Assert.All(result.FilesWritten, f => Assert.True(Path.IsPathRooted(f) && File.Exists(f)));
        Assert.Equal(result.FilesWritten.Order(StringComparer.Ordinal), result.FilesWritten);
    }

    // ----- Output control -------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExistingFiles_AreNotOverwrittenByDefault()
    {
        var (_, output) = await Harness.GenerateAsync(TestService, null, TestServiceMappings);
        var options = new ODataGeneratorOptions { OutputDirectory = output, RootNamespace = "Generated" };
        options.InputPaths.Add(TestService);

        var exception = await Assert.ThrowsAsync<ODataGeneratorWriteException>(() => ODataGenerator.GenerateAsync(options));
        Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);

        options.OverwriteFiles = true;
        var result = await ODataGenerator.GenerateAsync(options);
        Assert.NotEmpty(result.FilesWritten);
    }

    [Fact]
    public async Task DeleteTargetDirectory_RemovesStaleFiles()
    {
        var (_, output) = await Harness.GenerateAsync(TestService, null, TestServiceMappings);
        var stale = Path.Combine(output, "Namespaces", "Stale", "Old.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "// old");
        var options = new ODataGeneratorOptions { OutputDirectory = output, RootNamespace = "Generated", DeleteTargetDirectory = true };
        options.InputPaths.Add(TestService);

        await ODataGenerator.GenerateAsync(options);

        Assert.False(File.Exists(stale));
        Assert.False(Directory.Exists(Path.GetDirectoryName(stale)));
    }

    [Fact]
    public async Task DeleteTargetDirectory_RefusesToDeleteInput()
    {
        var directory = Harness.NewTemporaryDirectory();
        File.Copy(TestService, Path.Combine(directory, "test_service.edmx"));
        var options = new ODataGeneratorOptions { OutputDirectory = directory, DeleteTargetDirectory = true };
        options.InputPaths.Add(Path.Combine(directory, "test_service.edmx"));

        await Assert.ThrowsAsync<ODataGeneratorWriteException>(() => ODataGenerator.GenerateAsync(options));
        Assert.True(File.Exists(Path.Combine(directory, "test_service.edmx")));
    }

    [Fact]
    public async Task OutputFollowsTheRootNamespace()
    {
        var (_, output) = await Harness.GenerateAsync(TestService, options => options.RootNamespace = "My.Company.Api", TestServiceMappings);

        Assert.Contains("namespace My.Company.Api.Namespaces.Test;", File.ReadAllText(Path.Combine(output, "Namespaces", "Test", "TestEntityV2.cs")), StringComparison.Ordinal);
        Assert.Contains("namespace My.Company.Api.Services;", File.ReadAllText(Path.Combine(output, "Services", "ITestService.cs")), StringComparison.Ordinal);
        Assert.Contains("namespace My.Company.Api.Namespaces.Test.Batch;", File.ReadAllText(Path.Combine(output, "Namespaces", "Test", "Batch", "ITestServiceBatch.cs")), StringComparison.Ordinal);

        var (_, noRoot) = await Harness.GenerateAsync(TestService, options => options.RootNamespace = "", TestServiceMappings);
        Assert.Contains("namespace Namespaces.Test;", File.ReadAllText(Path.Combine(noRoot, "Namespaces", "Test", "TestEntityV2.cs")), StringComparison.Ordinal);
        Assert.True(Harness.Compile(noRoot).Count == 0);
    }

    [Fact]
    public async Task FileHeaderAndVersionReference_AreWrittenToEveryFile()
    {
        var (_, output) = await Harness.GenerateAsync(
            TestService,
            options =>
            {
                options.FileHeader = "Copyright (c) Example\n\nAll rights reserved.";
                options.GenerateVersionReference = true;
            },
            TestServiceMappings);

        foreach (var file in Directory.EnumerateFiles(output, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.StartsWith(
                "// <auto-generated/>\n// Copyright (c) Example\n//\n// All rights reserved.\n// Generated by the SAP Cloud SDK for .NET OData V2 generator.\n#nullable enable\n",
                text,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task BlockCommentFileHeader_IsKeptAsIs()
    {
        var (_, output) = await Harness.GenerateAsync(TestService, options => options.FileHeader = "/*\n * Copyright (c) Example\n */\n", TestServiceMappings);

        Assert.StartsWith("// <auto-generated/>\n/*\n * Copyright (c) Example\n */\n#nullable enable\n", File.ReadAllText(Path.Combine(output, "Services", "ITestService.cs")), StringComparison.Ordinal);
        Assert.True(Harness.Compile(output).Count == 0);
    }

    // ----- Input ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Directories_AreSearchedRecursively_AndNonV2FilesAreSkipped()
    {
        var input = Harness.NewTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(input, "sub"));
        File.Copy(TestService, Path.Combine(input, "sub", "test_service.edmx"));
        File.Copy(Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "invalidService", "v4_service.edmx"), Path.Combine(input, "v4_service.edmx"));
        File.WriteAllText(Path.Combine(input, "readme.txt"), "ignored");
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory(), RootNamespace = "G" };
        options.InputPaths.Add(input);

        var result = await ODataGenerator.GenerateAsync(options);

        Assert.Equal(2, result.Services.Count);
        Assert.Equal(ServiceGenerationStatus.Skipped, result.Services.Single(s => s.Name == "v4_service").Status);
        Assert.Equal(ServiceGenerationStatus.Generated, result.Services.Single(s => s.Name == "test_service").Status);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("No OData service", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExcludePattern_SkipsMatchingFiles()
    {
        var input = Harness.NewTemporaryDirectory();
        File.Copy(TestService, Path.Combine(input, "test_service.edmx"));
        File.Copy(TestService, Path.Combine(input, "other_service.edmx"));
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory(), ExcludeFilePattern = "foo*, other_*.edmx" };
        options.InputPaths.Add(input);

        var result = await ODataGenerator.GenerateAsync(options);

        Assert.Equal(ServiceGenerationStatus.Skipped, result.Services.Single(s => s.Name == "other_service").Status);
        Assert.Equal(ServiceGenerationStatus.Generated, result.Services.Single(s => s.Name == "test_service").Status);
    }

    [Fact]
    public async Task MissingInputPath_Throws()
    {
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory() };
        options.InputPaths.Add(Path.Combine(Harness.NewTemporaryDirectory(), "missing.edmx"));

        await Assert.ThrowsAsync<ODataGeneratorReadException>(() => ODataGenerator.GenerateAsync(options));
    }

    [Fact]
    public async Task NoServices_WarnsAndFailOnWarningThrows()
    {
        var input = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "invalidService");
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory() };
        options.InputPaths.Add(input);

        var result = await ODataGenerator.GenerateAsync(options);
        Assert.Contains(result.Warnings, w => w.Contains("No OData service", StringComparison.Ordinal));
        Assert.Empty(result.FilesWritten);

        options.FailOnWarning = true;
        await Assert.ThrowsAsync<ODataGeneratorException>(() => ODataGenerator.GenerateAsync(options));
    }

    [Fact]
    public async Task FailingFile_DoesNotStopOtherFiles()
    {
        var input = Harness.NewTemporaryDirectory();
        File.Copy(TestService, Path.Combine(input, "test_service.edmx"));
        File.Copy(Path.Combine(Harness.JavaData, "DatamodelMetadataGenerationTest", "input", "semantically-incorrect-metadata.edmx"), Path.Combine(input, "broken.edmx"));
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory(), NameSource = NameSource.Name };
        options.InputPaths.Add(input);

        var result = await ODataGenerator.GenerateAsync(options);

        Assert.Equal(ServiceGenerationStatus.Failed, result.Services.Single(s => s.Name == "broken").Status);
        Assert.Equal(ServiceGenerationStatus.Generated, result.Services.Single(s => s.Name == "test_service").Status);
        Assert.Contains(result.Warnings, w => w.Contains("broken.edmx", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingHttpMethod_FailsTheService()
    {
        var file = Harness.WriteEdmx(Harness.Edmx(
            """
            <EntityType Name="Foo"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/></EntityType>
            <EntityContainer Name="C"><EntitySet Name="Foos" EntityType="Test.Foo"/><FunctionImport Name="F" ReturnType="Edm.String"/></EntityContainer>
            """));

        var (result, output) = await Harness.GenerateAsync(file, options => options.DefaultBasePath = "/");

        var summary = Assert.Single(result.Services);
        Assert.Equal(ServiceGenerationStatus.Failed, summary.Status);
        Assert.Contains("m:HttpMethod", summary.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task NonSimpleFunctionParameter_SkipsTheFunctionImport()
    {
        var file = Harness.WriteEdmx(Harness.Edmx(
            """
            <ComplexType Name="Complex"><Property Name="A" Type="Edm.String"/></ComplexType>
            <EntityType Name="Foo"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/></EntityType>
            <EntityContainer Name="C"><EntitySet Name="Foos" EntityType="Test.Foo"/>
              <FunctionImport Name="F" ReturnType="Edm.String" m:HttpMethod="GET"><Parameter Name="P" Type="Test.Complex" Mode="In"/></FunctionImport>
              <FunctionImport Name="G" ReturnType="Edm.String" m:HttpMethod="GET"/>
            </EntityContainer>
            """));

        var (result, output) = await Harness.GenerateAsync(file, options => options.DefaultBasePath = "/");

        Assert.Equal(1, Assert.Single(result.Services).FunctionImportCount);
        Assert.Contains(result.Warnings, w => w.Contains("non-simple parameter P", StringComparison.Ordinal));
        Assert.True(Harness.Compile(output).Count == 0);
    }

    // ----- Service paths and mappings -------------------------------------------------------------------------------

    [Fact]
    public async Task ServiceNameMappingsFile_IsCreatedAndExtended_AndSuppliesNames()
    {
        var directory = Harness.NewTemporaryDirectory();
        var mappings = Path.Combine(directory, "sub", "mappings.properties");
        var (result, _) = await Harness.GenerateAsync(TestService, options => options.ServiceNameMappingsFile = mappings);

        Assert.Equal(ServiceGenerationStatus.Generated, Assert.Single(result.Services).Status);
        Assert.Equal("\n# test service\ntest_service.className = Test\ntest_service.namespaceName = Test\n", File.ReadAllText(mappings));

        // edit the mapping: the names follow
        File.WriteAllText(mappings, "test_service.className = Renamed\ntest_service.namespaceName = My.Space\n");
        var (renamed, output) = await Harness.GenerateAsync(TestService, options => options.ServiceNameMappingsFile = mappings);
        Assert.Equal("Generated.Namespaces.My.Space", Assert.Single(renamed.Services).Namespace);
        Assert.True(File.Exists(Path.Combine(output, "Services", "IRenamedService.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Namespaces", "My", "Space", "Batch", "DefaultRenamedServiceBatch.cs")));
        Assert.True(Harness.Compile(output).Count == 0);
    }

    [Fact]
    public async Task InvalidMappingNames_FailTheService()
    {
        var directory = Harness.NewTemporaryDirectory();
        var mappings = Path.Combine(directory, "mappings.properties");
        File.WriteAllText(mappings, "test_service.className = Not Valid\n");

        var (result, _) = await Harness.GenerateAsync(TestService, options => options.ServiceNameMappingsFile = mappings);

        var summary = Assert.Single(result.Services);
        Assert.Equal(ServiceGenerationStatus.Failed, summary.Status);
        Assert.Contains("not a valid C# identifier", summary.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoServicesWithTheSameName_AreRejectedUnlessMapped()
    {
        var input = Harness.NewTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(input, "a"));
        Directory.CreateDirectory(Path.Combine(input, "b"));
        File.Copy(TestService, Path.Combine(input, "a", "test_service.edmx"));
        File.Copy(TestService, Path.Combine(input, "b", "test_service.edmx"));
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory(), RootNamespace = "G" };
        options.InputPaths.Add(input);

        var result = await ODataGenerator.GenerateAsync(options);

        Assert.Equal(1, result.Services.Count(s => s.Status == ServiceGenerationStatus.Generated));
        Assert.Equal(1, result.Services.Count(s => s.Status == ServiceGenerationStatus.Failed));
        Assert.Contains(result.Warnings, w => w.Contains("already used by the service", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TwoServices_WithDifferentNames_CompileTogether()
    {
        var input = Harness.NewTemporaryDirectory();
        File.Copy(TestService, Path.Combine(input, "first.edmx"));
        File.Copy(Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "groceryStore", "input", "SDK_Grocery_Store.edmx"), Path.Combine(input, "SDK_Grocery_Store.edmx"));
        var options = new ODataGeneratorOptions { OutputDirectory = Harness.NewTemporaryDirectory(), RootNamespace = "G", NameSource = NameSource.Name, DefaultBasePath = "/" };
        options.InputPaths.Add(input);

        var result = await ODataGenerator.GenerateAsync(options);

        Assert.All(result.Services, s => Assert.Equal(ServiceGenerationStatus.Generated, s.Status));
        var diagnostics = Harness.Compile(options.OutputDirectory);
        Assert.True(diagnostics.Count == 0, Harness.Describe(diagnostics));
    }

    [Fact]
    public async Task DefaultBasePath_AndSwaggerFile_DecideTheDefaultServicePath()
    {
        var (withBase, _) = await Harness.GenerateAsync(TestService, options => options.DefaultBasePath = "/base/");
        Assert.Equal("/base/API_TEST_SRV", Assert.Single(withBase.Services).ServiceUrl);

        var (atomLink, _) = await Harness.GenerateAsync(TestService);
        Assert.Equal("/sap/opu/odata/sap/API_TEST_SRV", Assert.Single(atomLink.Services).ServiceUrl);

        var deprecationInput = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testServiceDeprecation", "input", "test_service.edmx");
        var (swagger, output) = await Harness.GenerateAsync(deprecationInput, options => options.DefaultBasePath = "/ignored/");
        Assert.Equal("/test/service", Assert.Single(swagger.Services).ServiceUrl);
        Assert.Contains("const string DefaultServicePath = \"/test/service\";", File.ReadAllText(Path.Combine(output, "Services", "ITestService.cs")), StringComparison.Ordinal);
    }

    // ----- Deprecation and documentation ----------------------------------------------------------------------------

    [Fact]
    public async Task DeprecatedSwaggerState_MarksTheServiceObsolete()
    {
        var input = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testServiceDeprecation", "input", "test_service.edmx");
        var (result, output) = await Harness.GenerateAsync(input, options => options.NameSource = NameSource.Name);

        Assert.True(Assert.Single(result.Services).IsDeprecated);
        var service = File.ReadAllText(Path.Combine(output, "Services", "ITestService.cs"));
        Assert.Contains(
            "[Obsolete(\"The service and all its related classes are deprecated as of release 9000 (AUG-1999). Please use the successor API (https://foo.bar/test/service_reloaded) instead.\")]",
            service,
            StringComparison.Ordinal);
        Assert.Contains("[Obsolete(", File.ReadAllText(Path.Combine(output, "Services", "DefaultTestService.cs")), StringComparison.Ordinal);
        Assert.Contains("#pragma warning disable CS0618", File.ReadAllText(Path.Combine(output, "Namespaces", "Test", "TestEntityV2.cs")), StringComparison.Ordinal);
        Assert.Contains("This service enables you to test OData specifications.", service, StringComparison.Ordinal);
        Assert.Contains("<see href=\"https://foo.bar/help.html\">Demo service</see>", service, StringComparison.Ordinal);
        Assert.Contains("API Version: 1 ", service.Replace("API Version: 1<", "API Version: 1 <"), StringComparison.Ordinal);
        Assert.Contains("Minimum ERP Version: 1805", service, StringComparison.Ordinal);
        Assert.Contains("Foo: Fizz, Buzz", service, StringComparison.Ordinal);
        Assert.True(Harness.Compile(output).Count == 0);
    }

    [Fact]
    public async Task DeprecationNoticeOption_MarksAnyServiceObsoleteWithTheNotice()
    {
        var (result, output) = await Harness.GenerateAsync(TestService, options => options.DeprecationNotice = "Use the other API.", TestServiceMappings);

        Assert.True(Assert.Single(result.Services).IsDeprecated);
        Assert.Contains(
            "[Obsolete(\"The service and all its related classes are deprecated. Use the other API.\")]",
            File.ReadAllText(Path.Combine(output, "Services", "ITestService.cs")),
            StringComparison.Ordinal);

        var (empty, emptyOutput) = await Harness.GenerateAsync(TestService, options => options.DeprecationNotice = "", TestServiceMappings);
        Assert.True(Assert.Single(empty.Services).IsDeprecated);
        Assert.Contains("Please use the OData generator to generate the service.", File.ReadAllText(Path.Combine(emptyOutput, "Services", "ITestService.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiBusinessHubLink_IsOptional()
    {
        var (_, without) = await Harness.GenerateAsync(TestService, null, TestServiceMappings);
        var (_, with) = await Harness.GenerateAsync(TestService, options => options.GenerateLinksToApiBusinessHub = true, TestServiceMappings);

        Assert.DoesNotContain("api.sap.com", File.ReadAllText(Path.Combine(without, "Services", "ITestService.cs")), StringComparison.Ordinal);
        Assert.Contains(
            "https://api.sap.com/shell/discover/contentpackage/SAPS4HANACloud/api/test_service?section=OVERVIEW",
            File.ReadAllText(Path.Combine(with, "Services", "ITestService.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LabelsQuickInfoAndDocumentation_BecomeXmlDocumentation()
    {
        var file = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "minimalTestWithExplicitDeprecation", "input", "minimal_metadata.edmx");
        var (_, output) = await Harness.GenerateAsync(file, options => options.NameSource = NameSource.Name);

        var entity = File.ReadAllText(Path.Combine(output, "Namespaces", "MinimalMetadata", "SimplePerson.cs"));
        Assert.Contains("/// Simple Person Test Data", entity, StringComparison.Ordinal);
        Assert.Contains("/// (Key Field) Person Summary.", entity, StringComparison.Ordinal);
        Assert.Contains("/// <para>Some long description for the Person property.</para>", entity, StringComparison.Ordinal);
        Assert.Contains("/// <para>Constraints: Not nullable, Maximum length: 10</para>", entity, StringComparison.Ordinal);
        Assert.Contains("/// <para>Original property name from the OData EDM: <b>Person</b></para>", entity, StringComparison.Ordinal);
        Assert.Contains("/// Email Address Summary.", entity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NameSourceLabel_NamesEntitiesAndPropertiesAfterTheirLabels()
    {
        var file = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "keyNamedField", "input", "EntityWithKeyNamedField.edmx");
        var (_, output) = await Harness.GenerateAsync(file, options => options.NameSource = NameSource.Label);

        Assert.True(File.Exists(Path.Combine(output, "Namespaces", "SOMESMALLSRV", "SomeTypeLabel.cs")) || Directory.GetFiles(output, "SomeTypeLabel.cs", SearchOption.AllDirectories).Length == 1);
        var entity = File.ReadAllText(Directory.GetFiles(output, "SomeTypeLabel.cs", SearchOption.AllDirectories)[0]);
        Assert.Contains("public Guid? Key", entity, StringComparison.Ordinal);
        Assert.Contains("[ODataField(\"KeyFieldWithKeyLabel\")]", entity, StringComparison.Ordinal);
    }

    [Fact]
    public void AntMatch_FollowsAntRules()
    {
        Assert.True(ODataGenerator.AntMatch("*.edmx", "a.edmx"));
        Assert.True(ODataGenerator.AntMatch("a?c.edmx", "abc.edmx"));
        Assert.False(ODataGenerator.AntMatch("a?c.edmx", "ac.edmx"));
        Assert.True(ODataGenerator.AntMatch("**/a.edmx", "x/y/a.edmx"));
        Assert.False(ODataGenerator.AntMatch("*.edmx", "x/a.edmx"));
        Assert.True(ODataGenerator.AntMatch("SDK_*", "SDK_Grocery_Store.edmx"));
        Assert.False(ODataGenerator.AntMatch("sdk_*", "SDK_Grocery_Store.edmx"));
        Assert.True(ODataGenerator.AntMatch("a.b", "a.b"));
        Assert.False(ODataGenerator.AntMatch("a.b", "aXb"));
    }
}
