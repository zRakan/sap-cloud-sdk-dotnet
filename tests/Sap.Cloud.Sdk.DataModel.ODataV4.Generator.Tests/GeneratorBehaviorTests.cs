namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>What the generator generates for which EDMX construct, and what it skips.</summary>
public sealed class GeneratorBehaviorTests
{
    private const string Types = "Namespaces/Test/";

    private static readonly string s_threeSets = TestEdmx.Document(TestEdmx.Schema("N", """
        <EntityType Name="Alpha"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
        <EntityType Name="Beta"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
        <EntityType Name="Gamma"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
        <Function Name="F1"><ReturnType Type="Edm.String"/></Function>
        <Function Name="F2"><ReturnType Type="Edm.String"/></Function>
        <Action Name="A1"/>
        <Action Name="A2"/>
        <EntityContainer Name="Container">
          <EntitySet Name="Alphas" EntityType="N.Alpha"/>
          <EntitySet Name="Betas" EntityType="N.Beta"/>
          <EntitySet Name="Gammas" EntityType="N.Gamma"/>
          <FunctionImport Name="F1" Function="N.F1"/>
          <FunctionImport Name="F2" Function="N.F2"/>
          <ActionImport Name="A1" Action="N.A1"/>
          <ActionImport Name="A2" Action="N.A2"/>
        </EntityContainer>
        """));

    private static ODataV4GeneratorOptions With(
        ODataV4GeneratorOptions o,
        IReadOnlySet<string>? entitySets = null,
        IReadOnlySet<string>? functions = null,
        IReadOnlySet<string>? actions = null,
        bool pojosOnly = false,
        bool perSet = false,
        string? deprecation = null,
        bool failOnWarning = false,
        string header = "",
        bool versionReference = false,
        bool generatedCode = false,
        bool systemUsings = false,
        bool links = false,
        NameSource nameSource = NameSource.Label,
        string excludes = "") => new()
        {
            InputDirectory = o.InputDirectory,
            OutputDirectory = o.OutputDirectory,
            RootNamespace = o.RootNamespace,
            DefaultBasePath = o.DefaultBasePath,
            DryRun = true,
            IncludedEntitySets = entitySets,
            IncludedFunctionImports = functions,
            IncludedActionImports = actions,
            GeneratePojosOnly = pojosOnly,
            ServiceMethodsPerEntitySet = perSet,
            DeprecationNotice = deprecation,
            FailOnWarning = failOnWarning,
            FileHeader = header,
            GenerateVersionReference = versionReference,
            EmitGeneratedCodeAttribute = generatedCode,
            EmitSystemUsings = systemUsings,
            GenerateLinksToApiBusinessHub = links,
            NameSource = nameSource,
            ExcludeFilePattern = excludes,
        };

    // ---------------------------------------------------------------------------------------------------------------
    // filters

    [Fact]
    public async Task Everything_is_generated_without_filters()
    {
        var (result, _) = await TestEdmx.GenerateAsync(s_threeSets);

        TestEdmx.AssertCompiles(result);
        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        foreach (var name in new[] { "GetAllAlpha", "GetAllBeta", "GetAllGamma", " F1(", " F2(", " A1(", " A2(" })
        {
            Assert.Contains(name, service, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Entity_set_filter_restricts_the_generated_entities()
    {
        var (result, _) = await TestEdmx.GenerateAsync(s_threeSets, configure: o => With(o, entitySets: new HashSet<string> { "Alphas", "Gammas", "Missing" }));

        TestEdmx.AssertCompiles(result);
        Assert.Contains(result.Files, f => f.RelativePath == Types + "Alpha.cs");
        Assert.Contains(result.Files, f => f.RelativePath == Types + "Gamma.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == Types + "Beta.cs");
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("'Missing'", warning.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Function_and_action_import_filters_restrict_the_generated_operations()
    {
        var (result, _) = await TestEdmx.GenerateAsync(
            s_threeSets,
            configure: o => With(o, functions: new HashSet<string> { "F2" }, actions: new HashSet<string> { "A1" }));

        TestEdmx.AssertCompiles(result);
        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        Assert.DoesNotContain(" F1(", service, StringComparison.Ordinal);
        Assert.Contains(" F2(", service, StringComparison.Ordinal);
        Assert.Contains(" A1(", service, StringComparison.Ordinal);
        Assert.DoesNotContain(" A2(", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_methods_are_per_entity_type_by_default_and_per_entity_set_on_request()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="A_Alpha"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <EntityContainer Name="Container">
              <EntitySet Name="A_Firsts" EntityType="N.A_Alpha"/>
              <EntitySet Name="Seconds" EntityType="N.A_Alpha"/>
            </EntityContainer>
            """));

        var (perType, _) = await TestEdmx.GenerateAsync(edmx);
        var (perSet, _) = await TestEdmx.GenerateAsync(edmx, configure: o => With(o, perSet: true));

        var typeService = TestEdmx.Content(perType, "Services/ITestService.cs");
        Assert.Contains("GetAllRequestBuilder<Alpha> GetAllAlpha()", typeService, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllFirsts", typeService, StringComparison.Ordinal);
        Assert.Contains("new(ServicePath, \"A_Firsts\")", TestEdmx.Content(perType, "Services/DefaultTestService.cs"), StringComparison.Ordinal);

        var setService = TestEdmx.Content(perSet, "Services/ITestService.cs");
        Assert.Contains("GetAllFirsts()", setService, StringComparison.Ordinal);
        Assert.Contains("GetAllSeconds()", setService, StringComparison.Ordinal);
        TestEdmx.AssertCompiles(perType);
        TestEdmx.AssertCompiles(perSet);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // switches

    [Fact]
    public async Task Pojos_only_generates_data_classes_without_service_or_descriptors()
    {
        var (result, _) = await TestEdmx.GenerateAsync(s_threeSets, configure: o => With(o, pojosOnly: true));

        TestEdmx.AssertCompiles(result);
        Assert.DoesNotContain(result.Files, f => f.RelativePath.StartsWith("Services/", StringComparison.Ordinal));
        var alpha = TestEdmx.Content(result, Types + "Alpha.cs");
        Assert.DoesNotContain("class Fields", alpha, StringComparison.Ordinal);
        Assert.DoesNotContain("GetKey", alpha, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultServicePath", alpha, StringComparison.Ordinal);
        Assert.DoesNotContain("IVdmEntitySet", alpha, StringComparison.Ordinal);
        Assert.DoesNotContain(".Services;", alpha, StringComparison.Ordinal);
        Assert.Contains("RememberChangedField", alpha, StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_header_version_reference_generated_code_attribute_and_system_usings_are_optional()
    {
        var (plain, _) = await TestEdmx.GenerateAsync(s_threeSets);
        var (custom, _) = await TestEdmx.GenerateAsync(
            s_threeSets,
            configure: o => With(o, header: "Copyright (c) Test\n\n/* license */", versionReference: true, generatedCode: true, systemUsings: true));

        var plainFile = TestEdmx.Content(plain, Types + "Alpha.cs");
        Assert.StartsWith("// <auto-generated/>\n\n#nullable enable\n\nusing System.Text.Json.Serialization;", plainFile, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedCode", plainFile, StringComparison.Ordinal);

        foreach (var file in custom.Files)
        {
            Assert.StartsWith("// Copyright (c) Test\n/* license */\n// Generated by the OData v4 code generator", file.Content, StringComparison.Ordinal);
            Assert.Contains("// <auto-generated/>", file.Content, StringComparison.Ordinal);
            Assert.Contains("using System;\nusing System.Collections.Generic;\n", file.Content, StringComparison.Ordinal);
        }

        Assert.Contains("[global::System.CodeDom.Compiler.GeneratedCode(\"Sap.Cloud.Sdk.DataModel.ODataV4.Generator\"", TestEdmx.Content(custom, Types + "Alpha.cs"), StringComparison.Ordinal);
        TestEdmx.AssertCompiles(custom);
    }

    [Fact]
    public async Task Business_hub_link_is_added_on_request()
    {
        var (result, _) = await TestEdmx.GenerateAsync(s_threeSets, "Test_Service.edmx", o => With(o, links: true));

        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        Assert.Contains("https://api.sap.com/shell/discover/contentpackage/SAPS4HANACloud/api/Test_Service?section=OVERVIEW", service, StringComparison.Ordinal);
        TestEdmx.AssertCompiles(result);
    }

    [Fact]
    public async Task Name_source_selects_between_names_and_labels()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing">
              <Annotation Term="Common.Label" String="Fancy Thing"/>
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"><Annotation Term="Common.Label" String="Identifier of the thing"/></Property>
            </EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (labels, _) = await TestEdmx.GenerateAsync(edmx, configure: o => With(o, nameSource: NameSource.Label));
        var (names, _) = await TestEdmx.GenerateAsync(edmx, configure: o => With(o, nameSource: NameSource.Name));

        Assert.Contains(labels.Files, f => f.RelativePath == Types + "FancyThing.cs");
        Assert.Contains("public string? IdentifierOfTheThing\n", TestEdmx.Content(labels, Types + "FancyThing.cs"), StringComparison.Ordinal);
        Assert.Contains(names.Files, f => f.RelativePath == Types + "Thing.cs");
        Assert.Contains("public string? Id\n", TestEdmx.Content(names, Types + "Thing.cs"), StringComparison.Ordinal);
        TestEdmx.AssertCompiles(labels);
        TestEdmx.AssertCompiles(names);
    }

    [Fact]
    public async Task Exclude_pattern_skips_matching_files()
    {
        var (result, _) = await TestEdmx.GenerateAsync(s_threeSets, "Skip_me.edmx", o => With(o, excludes: "Other*,Skip_?e.edmx,*.foo"));

        var summary = Assert.Single(result.Services);
        Assert.False(summary.Generated);
        Assert.Contains("exclusion", summary.FailureReason, StringComparison.Ordinal);
        Assert.Empty(result.Files);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // deprecation and warnings

    [Fact]
    public async Task Deprecation_notice_marks_the_service_as_obsolete()
    {
        var (result, _) = await TestEdmx.GenerateAsync(s_threeSets, configure: o => With(o, deprecation: "This is a custom deprecation message."));

        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        Assert.Contains("[Obsolete(\"The service and all its related classes are deprecated. This is a custom deprecation message.\")]", service, StringComparison.Ordinal);
        Assert.Contains("[Obsolete(", TestEdmx.Content(result, "Services/DefaultTestService.cs"), StringComparison.Ordinal);
        Assert.Contains("#pragma warning disable CS0618", TestEdmx.Content(result, Types + "Alpha.cs"), StringComparison.Ordinal);
        Assert.True(result.Services.Single().IsDeprecated);
        TestEdmx.AssertCompiles(result);
    }

    [Fact]
    public async Task Swagger_state_info_marks_the_service_as_obsolete_with_a_successor()
    {
        var (result, _) = await TestEdmx.GenerateAsync(
            s_threeSets,
            prepare: directory => File.Copy(
                Path.Combine(AppContext.BaseDirectory, "TestData", "ServiceDetailsResolverTest", "v3DeprecatedService.json"),
                Path.Combine(directory, "in", "Test.json")));

        var service = result.Files.Single(f => f.RelativePath.StartsWith("Services/I", StringComparison.Ordinal)).Content;
        Assert.Contains("deprecated as of release 1908 (AUG-2019). Please use the successor API (https://api.sap.com/api/SOME_OTHER_API) instead.", service, StringComparison.Ordinal);
        TestEdmx.AssertCompiles(result);
    }

    [Fact]
    public async Task Swagger_provides_service_path_documentation_and_the_allowed_operations()
    {
        var input = Path.Combine(AppContext.BaseDirectory, "TestData", "oDataGeneratorIntegrationTest", "minimalTest", "input");
        var result = await ODataV4Generator.GenerateAsync(new ODataV4GeneratorOptions
        {
            InputDirectory = input,
            OutputDirectory = Path.Combine(TestEdmx.NewDirectory(), "out"),
            RootNamespace = TestEdmx.RootNamespace,
            ServiceNameMappingsFile = Path.Combine(input, "serviceNameMappings.properties"),
            DryRun = true,
        });

        var summary = Assert.Single(result.Services);
        Assert.Equal("/", summary.ServicePath);
        Assert.Equal("minimal metadata", summary.Title);
        var service = TestEdmx.Content(result, "Services/IMinimalMetadataService.cs");
        Assert.Contains("You can use this inbound synchronous service to create, read and update.", service, StringComparison.Ordinal);
        Assert.Contains("API version: 1", service, StringComparison.Ordinal);

        // the paths allow GET (collection and by key), POST and PATCH, but no DELETE
        Assert.Contains("GetAllSimplePerson()", service, StringComparison.Ordinal);
        Assert.Contains("GetSimplePersonByKey(string person)", service, StringComparison.Ordinal);
        Assert.Contains("CreateSimplePerson(", service, StringComparison.Ordinal);
        Assert.Contains("UpdateSimplePerson(", service, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteSimplePerson", service, StringComparison.Ordinal);
        TestEdmx.AssertCompiles(result);
    }

    [Fact]
    public async Task Capabilities_annotations_remove_service_methods()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <EntityContainer Name="Container">
              <EntitySet Name="Things" EntityType="N.Thing">
                <Annotation Term="Capabilities.InsertRestrictions"><Record><PropertyValue Property="Insertable" Bool="false"/></Record></Annotation>
                <Annotation Term="Capabilities.DeleteRestrictions"><Record><PropertyValue Property="Deletable" Bool="false"/></Record></Annotation>
                <Annotation Term="Capabilities.ReadRestrictions">
                  <Record>
                    <PropertyValue Property="ReadByKeyRestrictions"><Record><PropertyValue Property="Readable" Bool="false"/></Record></PropertyValue>
                  </Record>
                </Annotation>
              </EntitySet>
            </EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        Assert.Contains("GetAllThing()", service, StringComparison.Ordinal);
        Assert.Contains("UpdateThing(", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateThing", service, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteThing", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ByKey", service, StringComparison.Ordinal);
        Assert.Empty(result.Warnings);
        TestEdmx.AssertCompiles(result);
    }

    [Fact]
    public async Task Fail_on_warning_is_reported_in_the_result()
    {
        var withWarning = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/><Property Name="Where" Type="Edm.GeographyPoint"/></EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (failing, _) = await TestEdmx.GenerateAsync(withWarning, configure: o => With(o, failOnWarning: true));
        var (tolerant, _) = await TestEdmx.GenerateAsync(withWarning);
        var (clean, _) = await TestEdmx.GenerateAsync(s_threeSets, configure: o => With(o, failOnWarning: true));

        Assert.True(failing.FailureDueToWarningsNecessary);
        Assert.False(tolerant.FailureDueToWarningsNecessary);
        Assert.False(clean.FailureDueToWarningsNecessary);
        Assert.Single(failing.Warnings);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // what is generated for which construct

    [Fact]
    public async Task Descriptions_and_constraints_are_documented()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing">
              <Annotation Term="Core.Description" String="A thing &amp; more &lt;b&gt;."/>
              <Annotation Term="Core.LongDescription" String="Long text."/>
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String" Nullable="false" MaxLength="10"><Annotation Term="Common.QuickInfo" String="Quick info of the id"/></Property>
              <Property Name="Price" Type="Edm.Decimal" Precision="5" Scale="2" DefaultValue="1.5"/>
              <Property Name="Plain" Type="Edm.String"/>
            </EntityType>
            <Function Name="Compute" IsBound="true">
              <Annotation Term="Core.Description" String="Computes things."/>
              <Parameter Name="thing" Type="N.Thing"/>
              <Parameter Name="factor" Type="Edm.Int32" Nullable="false"><Annotation Term="Core.Description" String="The factor."/></Parameter>
              <ReturnType Type="Edm.Int32"/>
            </Function>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.Contains("/// <para>A thing &amp; more &lt;b&gt;.</para>", thing, StringComparison.Ordinal);
        Assert.Contains("/// <para>Long text.</para>", thing, StringComparison.Ordinal);
        Assert.Contains("/// <para>Quick info of the id</para>\n    /// <para>Constraints: Not nullable, Maximum length: 10</para>", thing, StringComparison.Ordinal);
        Assert.Contains("/// <para>Constraints: Nullable, Default value: 1.5, Precision: 5, Scale: 2</para>", thing, StringComparison.Ordinal);
        Assert.Contains("/// <para>Computes things.</para>", thing, StringComparison.Ordinal);
        Assert.Contains("/// <param name=\"factor\">The factor.</param>", thing, StringComparison.Ordinal);

        // properties without anything to say keep the one-line documentation
        Assert.Contains("    /// <summary>\n    /// Original property name from the Odata EDM: <b>Plain</b>\n    /// </summary>", thing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inherited_properties_are_flattened_and_abstract_types_without_set_are_not_generated()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Base" Abstract="true">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"/>
              <NavigationProperty Name="Owner" Type="N.Owner"/>
            </EntityType>
            <EntityType Name="Derived" BaseType="N.Base"><Property Name="Extra" Type="Edm.String"/></EntityType>
            <EntityType Name="Owner"><Key><PropertyRef Name="Key"/></Key><Property Name="Key" Type="Edm.String"/></EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Deriveds" EntityType="N.Derived"/></EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        Assert.DoesNotContain(result.Files, f => f.RelativePath == Types + "Base.cs");
        var derived = TestEdmx.Content(result, Types + "Derived.cs");
        Assert.Contains("public string? Id\n", derived, StringComparison.Ordinal);
        Assert.Contains("public string? Extra\n", derived, StringComparison.Ordinal);
        Assert.Contains("entityKey.AddKeyProperty(\"Id\", Id);", derived, StringComparison.Ordinal);
        Assert.Contains("GetOwnerIfPresent()", derived, StringComparison.Ordinal);
        Assert.Contains(result.Files, f => f.RelativePath == Types + "Owner.cs");
    }

    [Fact]
    public async Task Unsupported_constructs_are_skipped_with_a_message()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EnumType Name="Color"><Member Name="Red"/></EnumType>
            <ComplexType Name="Money"><Property Name="Amount" Type="Edm.Decimal"/></ComplexType>
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"/>
              <Property Name="Picture" Type="Edm.Stream"/>
              <Property Name="Where" Type="Edm.GeographyPoint"/>
              <Property Name="Shape" Type="Edm.GeometryPolygon"/>
              <Property Name="Anything" Type="Edm.Untyped"/>
            </EntityType>
            <EntityType Name="Unreachable"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <EntityType Name="Orphan"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <Function Name="ColorOf" IsBound="true"><Parameter Name="thing" Type="N.Thing"/><ReturnType Type="N.Color"/></Function>
            <Function Name="BoundToOrphan" IsBound="true"><Parameter Name="orphan" Type="N.Orphan"/><ReturnType Type="Edm.String"/></Function>
            <Function Name="BoundToComplex" IsBound="true"><Parameter Name="money" Type="N.Money"/><ReturnType Type="Edm.String"/></Function>
            <Function Name="TakesGeo" IsBound="true"><Parameter Name="thing" Type="N.Thing"/><Parameter Name="where" Type="Edm.GeographyPoint"/><ReturnType Type="Edm.String"/></Function>
            <Function Name="GetColor"><ReturnType Type="N.Color"/></Function>
            <Function Name="TakesOrphan"><Parameter Name="orphan" Type="N.Orphan"/><ReturnType Type="Edm.String"/></Function>
            <Function Name="GetMoney"><ReturnType Type="Collection(N.Money)"/></Function>
            <EntityContainer Name="Container">
              <EntitySet Name="Things" EntityType="N.Thing"/>
              <Singleton Name="Me" Type="N.Thing"/>
              <FunctionImport Name="GetColor" Function="N.GetColor"/>
              <FunctionImport Name="TakesOrphan" Function="N.TakesOrphan"/>
              <FunctionImport Name="GetMoney" Function="N.GetMoney"/>
            </EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var texts = result.Messages.Select(m => m.Text).ToList();
        Assert.Contains(texts, t => t.Contains("Singletons are not supported", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("property type Stream", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("property type GeographyPoint", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("property type GeometryPolygon", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("property type Untyped", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Operation BoundToOrphan bound to type N.Orphan will be skipped", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Operation BoundToComplex bound to type N.Money will be skipped", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("TakesGeo", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("TakesOrphan generation as it contains entity parameter orphan", StringComparison.Ordinal));
        Assert.Equal(2, texts.Count(t => t.Contains("Unsupported EDM return type", StringComparison.Ordinal)));

        Assert.DoesNotContain(result.Files, f => f.RelativePath == Types + "Unreachable.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == Types + "Orphan.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == Types + "Color.cs");
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.DoesNotContain("Picture", thing, StringComparison.Ordinal);
        Assert.DoesNotContain("BoundTo", thing, StringComparison.Ordinal);

        // complex types that are only used by an operation are generated
        Assert.Contains(result.Files, f => f.RelativePath == Types + "Money.cs");
        Assert.Contains("CollectionValueFunctionRequestBuilder<Money> GetMoney()", TestEdmx.Content(result, "Services/ITestService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entities_that_are_only_returned_by_operations_are_generated_with_the_operation_as_collection()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <EntityType Name="Report"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
            <Function Name="CreateReport" IsBound="true" EntitySetPath="thing"><Parameter Name="thing" Type="N.Thing"/><ReturnType Type="N.Report"/></Function>
            <Function Name="TopReport"><ReturnType Type="N.Report"/></Function>
            <EntityContainer Name="Container">
              <EntitySet Name="Things" EntityType="N.Thing"/>
              <FunctionImport Name="TopReport" Function="N.TopReport"/>
            </EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        // the unbound import is processed first and names the collection
        Assert.Contains("EntityCollection => \"TopReport\"", TestEdmx.Content(result, Types + "Report.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("IVdmEntitySet", TestEdmx.Content(result, Types + "Report.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Type_definitions_are_resolved_to_their_underlying_type()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <TypeDefinition Name="Code" UnderlyingType="Edm.String" MaxLength="4"/>
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="N.Code"/>
              <Property Name="Codes" Type="Collection(N.Code)"/>
            </EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.Contains("public string? Id\n", thing, StringComparison.Ordinal);
        Assert.Contains("public List<string>? Codes\n", thing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task External_annotations_with_targets_and_aliases_are_applied()
    {
        var edmx = TestEdmx.Document(
            TestEdmx.Schema("N", """
                <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/><Property Name="Name" Type="Edm.String"/></EntityType>
                <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
                <Annotations Target="N.Thing"><Annotation Term="Core.Description" String="From outside."/></Annotations>
                <Annotations Target="N.Thing/Name"><Annotation Term="Common.Label" String="The Name"/></Annotations>
                <Annotations Target="N.Container/Things">
                  <Annotation Term="Capabilities.DeleteRestrictions"><Record><PropertyValue Property="Deletable" Bool="false"/></Record></Annotation>
                </Annotations>
                """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.Contains("<para>From outside.</para>", thing, StringComparison.Ordinal);
        Assert.Contains("public string? TheName\n", thing, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteThing", TestEdmx.Content(result, "Services/ITestService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alias_qualified_names_are_resolved_to_the_namespace()
    {
        var edmx = TestEdmx.Document("""
            <Schema Namespace="Org.Example.Model" Alias="self" xmlns="http://docs.oasis-open.org/odata/ns/edm">
              <EnumType Name="Kind"><Member Name="A"/></EnumType>
              <EntityType Name="Thing">
                <Key><PropertyRef Name="Id"/></Key>
                <Property Name="Id" Type="Edm.String"/>
                <Property Name="Kind" Type="self.Kind"/>
                <NavigationProperty Name="Friends" Type="Collection(self.Thing)"/>
              </EntityType>
              <Function Name="Count" IsBound="true"><Parameter Name="thing" Type="self.Thing"/><ReturnType Type="Edm.Int32"/></Function>
              <Function Name="Top"><ReturnType Type="self.Thing"/></Function>
              <EntityContainer Name="Container">
                <EntitySet Name="Things" EntityType="self.Thing"/>
                <FunctionImport Name="Top" Function="self.Top"/>
              </EntityContainer>
            </Schema>
            """);

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.Contains("VdmTypeInfoBuilder<Thing>(\"Org.Example.Model.Thing\")", thing, StringComparison.Ordinal);
        Assert.Contains("Of(\"Kind\", \"Org.Example.Model.Kind\")", thing, StringComparison.Ordinal);
        Assert.Contains("Count()", thing, StringComparison.Ordinal);
        Assert.Contains("\"Org.Example.Model.Count\"", thing, StringComparison.Ordinal);
        Assert.Contains("SingleValueFunctionRequestBuilder<Thing> Top()", TestEdmx.Content(result, "Services/ITestService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cyclic_complex_types_and_navigation_cycles_are_generated()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <ComplexType Name="Node"><Property Name="Next" Type="N.Node"/><Property Name="Children" Type="Collection(N.Node)"/></ComplexType>
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"/>
              <Property Name="Tree" Type="N.Node"/>
              <NavigationProperty Name="Self" Type="N.Thing"/>
              <NavigationProperty Name="Other" Type="N.Other"/>
            </EntityType>
            <EntityType Name="Other"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/><NavigationProperty Name="Back" Type="N.Thing"/></EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // inputs

    [Fact]
    public async Task Files_that_are_not_odata_v4_or_not_readable_are_skipped_and_the_others_are_generated()
    {
        var directory = TestEdmx.NewDirectory();
        var input = Path.Combine(directory, "in");
        Directory.CreateDirectory(input);
        await File.WriteAllTextAsync(Path.Combine(input, "Good.edmx"), s_threeSets);
        await File.WriteAllTextAsync(Path.Combine(input, "Broken.xml"), "<edmx:Edmx");
        await File.WriteAllTextAsync(Path.Combine(input, "V2.edmx"), "<edmx:Edmx Version=\"1.0\" xmlns:edmx=\"http://schemas.microsoft.com/ado/2007/06/edmx\"/>");
        await File.WriteAllTextAsync(Path.Combine(input, "Unknown.edmx"), TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="N.Missing"/></EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """)));
        await File.WriteAllTextAsync(Path.Combine(input, "Notes.txt"), "ignored");

        var result = await ODataV4Generator.GenerateAsync(new ODataV4GeneratorOptions
        {
            InputDirectory = input,
            OutputDirectory = Path.Combine(directory, "out"),
            RootNamespace = TestEdmx.RootNamespace,
            DefaultBasePath = "/",
            DryRun = true,
        });

        Assert.Equal(["Broken", "Good", "Unknown", "V2"], result.Services.Select(s => s.ServiceName).Order(StringComparer.Ordinal));
        Assert.Equal(["Good"], result.Services.Where(s => s.Generated).Select(s => s.ServiceName));
        Assert.All(result.Services.Where(s => !s.Generated), s => Assert.False(string.IsNullOrEmpty(s.FailureReason)));
        Assert.NotEmpty(result.Files);
        Assert.Contains(result.Warnings, w => w.Text.Contains("Unknown type 'N.Missing'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_inputs_fail_with_a_read_exception()
    {
        var output = Path.Combine(TestEdmx.NewDirectory(), "out");

        await Assert.ThrowsAsync<ODataGeneratorReadException>(() => ODataV4Generator.GenerateAsync(
            new ODataV4GeneratorOptions { InputDirectory = Path.Combine(output, "nope"), OutputDirectory = output, DryRun = true }));
        await Assert.ThrowsAsync<ODataGeneratorReadException>(() => ODataV4Generator.GenerateAsync(
            new ODataV4GeneratorOptions { InputFiles = [Path.Combine(output, "nope.edmx")], OutputDirectory = output, DryRun = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => ODataV4Generator.GenerateAsync(
            new ODataV4GeneratorOptions { OutputDirectory = output, DryRun = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => ODataV4Generator.GenerateAsync(
            new ODataV4GeneratorOptions { InputFiles = ["x.edmx"], OutputDirectory = output, RootNamespace = "Not valid", DryRun = true }));
    }

    [Fact]
    public async Task An_empty_input_directory_generates_nothing_and_says_so()
    {
        var directory = TestEdmx.NewDirectory();
        var result = await ODataV4Generator.GenerateAsync(new ODataV4GeneratorOptions
        {
            InputDirectory = directory,
            OutputDirectory = Path.Combine(directory, "out"),
            DryRun = true,
        });

        Assert.Empty(result.Files);
        Assert.Contains(result.Warnings, w => w.Text.Contains("No OData service definitions found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancellation_is_honored_when_writing()
    {
        var (_, directory) = await TestEdmx.GenerateAsync(s_threeSets);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ODataV4Generator.GenerateAsync(
            new ODataV4GeneratorOptions
            {
                InputDirectory = Path.Combine(directory, "in"),
                OutputDirectory = Path.Combine(directory, "out"),
                DefaultBasePath = "/",
            },
            cancellation.Token));
    }
}
