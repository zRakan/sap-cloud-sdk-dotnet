namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>Name clashes: C# keywords, members that clash with generated members, types that clash with each other.</summary>
public sealed class CollisionTests
{
    private const string Types = "Namespaces/Test/";

    [Fact]
    public async Task Properties_that_clash_with_generated_members_are_renamed()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"/>
              <Property Name="Thing" Type="Edm.String"/>
              <Property Name="Fields" Type="Edm.String"/>
              <Property Name="All" Type="Edm.String"/>
              <Property Name="Builder" Type="Edm.String"/>
              <Property Name="ThingBuilder" Type="Edm.String"/>
              <Property Name="Build" Type="Edm.String"/>
              <Property Name="Equals" Type="Edm.String"/>
              <Property Name="ToString" Type="Edm.String"/>
              <Property Name="TypeInfo" Type="Edm.String"/>
              <Property Name="EntityCollection" Type="Edm.String"/>
              <Property Name="ODataType" Type="Edm.String"/>
              <Property Name="VersionIdentifier" Type="Edm.String"/>
              <Property Name="SimpleProperty" Type="Edm.String"/>
              <Property Name="GetKey" Type="Edm.String"/>
              <Property Name="Class" Type="Edm.String"/>
              <Property Name="default" Type="Edm.String"/>
              <Property Name="name" Type="Edm.String"/>
              <Property Name="Name" Type="Edm.String"/>
              <Property Name="_Underscore" Type="Edm.String"/>
              <NavigationProperty Name="Others" Type="Collection(N.Thing)"/>
              <Property Name="SetOthers" Type="Edm.String"/>
              <Property Name="GetOthersIfPresent" Type="Edm.String"/>
              <Property Name="AddOthers" Type="Edm.String"/>
              <NavigationProperty Name="Parent" Type="N.Thing"/>
              <Property Name="Parent" Type="Edm.String"/>
            </EntityType>
            <EntityContainer Name="Container">
              <EntitySet Name="Things" EntityType="N.Thing"/>
            </EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.Contains("public string? Thing_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Fields_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? All_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Builder_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? ThingBuilder_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Build_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Equals_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? ToString_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? TypeInfo_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? EntityCollection_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? ODataType_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? VersionIdentifier_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? SimpleProperty_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? GetKey_2", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Class\n", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Default\n", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Name\n", thing, StringComparison.Ordinal);
        Assert.Contains("public string? Name_2\n", thing, StringComparison.Ordinal);
        Assert.Contains("public string? _Underscore\n", thing, StringComparison.Ordinal);

        // the EDM names are untouched: they are what goes over the wire
        Assert.Contains(".Field(\"Fields\", e => e.Fields_2, (e, v) => e.Fields_2 = v)", thing, StringComparison.Ordinal);
        Assert.Contains("Fields_2 = SimpleProperty.String<Thing>.Of(\"Fields\")", thing, StringComparison.Ordinal);

        // the navigation property keeps its name, the clashing structural property and the accessor methods move
        Assert.Contains("public string? SetOthers\n", thing, StringComparison.Ordinal);
        Assert.Contains("GetOthers_2IfPresent()", thing, StringComparison.Ordinal);
        Assert.Contains("SetOthers_2(IEnumerable<Thing> value)", thing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Types_that_clash_with_each_other_or_with_names_used_by_the_generated_code_are_renamed()
    {
        var edmx = TestEdmx.Document(
            TestEdmx.Schema("A", """
                <EnumType Name="Dictionary"><Member Name="One" Value="1"/></EnumType>
                <ComplexType Name="Guid"><Property Name="Value" Type="Edm.String"/></ComplexType>
                <EntityType Name="List"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Guid"/><Property Name="Kind" Type="A.Dictionary"/><Property Name="Where" Type="A.Guid"/></EntityType>
                <EntityType Name="foo"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
                <EntityType Name="Foo"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
                <EntityType Name="String"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
                <EntityType Name="IClashService"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
                <EntityType Name="Fields"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
                <EntityContainer Name="Container">
                  <EntitySet Name="Lists" EntityType="A.List"/>
                  <EntitySet Name="Foos" EntityType="A.foo"/>
                  <EntitySet Name="Foos2" EntityType="A.Foo"/>
                  <EntitySet Name="Strings" EntityType="A.String"/>
                  <EntitySet Name="Services" EntityType="A.IClashService"/>
                  <EntitySet Name="FieldsSet" EntityType="A.Fields"/>
                </EntityContainer>
                """),
            references: string.Empty);

        var (result, _) = await TestEdmx.GenerateAsync(edmx, "Clash.edmx");

        TestEdmx.AssertCompiles(result);
        var names = result.Files.Select(f => Path.GetFileNameWithoutExtension(f.RelativePath)).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("List_2", names);
        Assert.Contains("Dictionary_2", names);
        Assert.Contains("Guid_2", names);
        Assert.Contains("String_2", names);
        Assert.Contains("IClashService_2", names);
        Assert.Contains("Foo", names);
        Assert.Contains("Foo_2", names);
        Assert.Contains("Fields_2", names);

        // no two files may differ only by case
        Assert.Equal(result.Files.Count, result.Files.Select(f => f.RelativePath.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Types_with_the_same_name_in_different_schemas_get_different_classes()
    {
        var edmx = TestEdmx.Document(
            TestEdmx.Schema("A", """
                <EntityType Name="Person"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.String"/></EntityType>
                <ComplexType Name="Address"><Property Name="Street" Type="Edm.String"/></ComplexType>
                """)
            + TestEdmx.Schema("B", """
                <EntityType Name="Person"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32"/><Property Name="Home" Type="A.Address"/></EntityType>
                <ComplexType Name="Address"><Property Name="City" Type="Edm.String"/></ComplexType>
                <EntityContainer Name="Container">
                  <EntitySet Name="PeopleA" EntityType="A.Person"/>
                  <EntitySet Name="PeopleB" EntityType="B.Person"/>
                </EntityContainer>
                """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        Assert.Contains("VdmTypeInfoBuilder<Person>(\"A.Person\")", TestEdmx.Content(result, Types + "Person.cs"), StringComparison.Ordinal);
        Assert.Contains("VdmTypeInfoBuilder<Person_2>(\"B.Person\")", TestEdmx.Content(result, Types + "Person_2.cs"), StringComparison.Ordinal);
        Assert.Contains("A.Address", TestEdmx.Content(result, Types + "Address.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operations_and_parameters_that_clash_are_renamed()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"/>
              <Property Name="Compute" Type="Edm.String"/>
            </EntityType>
            <Function Name="Compute" IsBound="true">
              <Parameter Name="thing" Type="N.Thing"/>
              <Parameter Name="class" Type="Edm.String" Nullable="false"/>
              <Parameter Name="object" Type="Edm.Int32" Nullable="false"/>
              <Parameter Name="parameters" Type="Edm.String"/>
              <Parameter Name="Parameters" Type="Edm.String"/>
              <ReturnType Type="Edm.String"/>
            </Function>
            <Function Name="GetKey" IsBound="true">
              <Parameter Name="thing" Type="N.Thing"/>
              <ReturnType Type="Edm.String"/>
            </Function>
            <Function Name="Batch"><ReturnType Type="Edm.String"/></Function>
            <Function Name="WithServicePath"><ReturnType Type="Edm.String"/></Function>
            <Function Name="GetAllThings"><ReturnType Type="Edm.String"/></Function>
            <Function Name="ServicePath"><ReturnType Type="Edm.String"/></Function>
            <Function Name="Same"><ReturnType Type="Edm.String"/></Function>
            <Action Name="Same"/>
            <Action Name="ForEntity"><Parameter Name="value" Type="Edm.String"/></Action>
            <EntityContainer Name="Container">
              <EntitySet Name="Things" EntityType="N.Thing"/>
              <FunctionImport Name="Batch" Function="N.Batch"/>
              <FunctionImport Name="WithServicePath" Function="N.WithServicePath"/>
              <FunctionImport Name="GetAllThings" Function="N.GetAllThings"/>
              <FunctionImport Name="ServicePath" Function="N.ServicePath"/>
              <FunctionImport Name="Same" Function="N.Same"/>
              <ActionImport Name="Same" Action="N.Same"/>
              <ActionImport Name="ForEntity" Action="N.ForEntity"/>
            </EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx, configure: o => new ODataV4GeneratorOptions
        {
            InputDirectory = o.InputDirectory,
            OutputDirectory = o.OutputDirectory,
            RootNamespace = o.RootNamespace,
            DefaultBasePath = o.DefaultBasePath,
            DryRun = true,
            ServiceMethodsPerEntitySet = true,
        });

        TestEdmx.AssertCompiles(result);
        var thing = TestEdmx.Content(result, Types + "Thing.cs");
        Assert.Contains("Compute_2(string @class, int @object, string? parameters_2, string? parameters_3)", thing.Replace("classParameter", "@class", StringComparison.Ordinal).Replace("objectParameter", "@object", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("GetKey_2()", thing, StringComparison.Ordinal);

        var service = TestEdmx.Content(result, "Services/ITestService.cs");
        foreach (var name in new[] { "Batch_2", "WithServicePath_2", "ServicePath_2", "ForEntity_2", "GetAllThings_2" })
        {
            Assert.Contains($" {name}(", service, StringComparison.Ordinal);
        }

        Assert.Contains(" Same(", service, StringComparison.Ordinal);
        Assert.Contains(" Same_2(", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enum_members_that_clash_with_the_enum_or_its_base_class_are_renamed()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EnumType Name="Mode">
              <Member Name="Mode"/>
              <Member Name="Name"/>
              <Member Name="Value"/>
              <Member Name="Values"/>
              <Member Name="ToString"/>
              <Member Name="Equals"/>
              <Member Name="draft"/>
              <Member Name="Draft"/>
              <Member Name="class"/>
            </EnumType>
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.String"/>
              <Property Name="Mode" Type="N.Mode"/>
            </EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var mode = TestEdmx.Content(result, Types + "Mode.cs");
        foreach (var member in new[] { "Mode_2", "Name_2", "Value_2", "Values_2", "ToString_2", "Equals_2", "Draft", "Draft_2", "Class" })
        {
            Assert.Contains($" {member} = new(", mode, StringComparison.Ordinal);
        }

        // the EDM names are what is written to and read from the payload
        Assert.Contains("new(\"Name\", 1)", mode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_with_unusual_characters_and_labels_produce_valid_identifiers()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <EntityType Name="A_SalesOrderType">
              <Annotation Term="Common.Label" String="Sales Order"/>
              <Key><PropertyRef Name="Order-Id"/></Key>
              <Property Name="Order-Id" Type="Edm.String"><Annotation Term="Common.Label" String="Order ID"/></Property>
              <Property Name="123abc" Type="Edm.String"/>
              <Property Name="Weißwurst" Type="Edm.String"/>
            </EntityType>
            <EntityContainer Name="Container"><EntitySet Name="A_SalesOrders" EntityType="N.A_SalesOrderType"/></EntityContainer>
            """).Replace("\\u00df", "ß", StringComparison.Ordinal));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
        var order = TestEdmx.Content(result, Types + "SalesOrder.cs");
        Assert.Contains("public string? OrderID\n", order, StringComparison.Ordinal);
        Assert.Contains(".Field(\"Order-Id\", e => e.OrderID", order, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Property_that_has_the_name_of_an_imported_type_still_compiles()
    {
        var edmx = TestEdmx.Document(TestEdmx.Schema("N", """
            <ComplexType Name="Money"><Property Name="Amount" Type="Edm.Decimal"/></ComplexType>
            <EntityType Name="Thing">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.Guid"/>
              <Property Name="Guid" Type="Edm.Guid"/>
              <Property Name="List" Type="Collection(Edm.String)"/>
              <Property Name="Dictionary" Type="Edm.String"/>
              <Property Name="DateTimeOffset" Type="Edm.DateTimeOffset"/>
              <Property Name="TimeSpan" Type="Edm.Duration"/>
              <Property Name="Money" Type="N.Money"/>
              <Property Name="String" Type="Edm.String"/>
              <Property Name="Object" Type="Edm.String"/>
              <Property Name="JsonConverter" Type="Edm.String"/>
              <Property Name="VdmEntity" Type="Edm.String"/>
              <Property Name="ITestService" Type="Edm.String"/>
              <Property Name="BoundFunction" Type="Edm.String"/>
              <Property Name="NavigationProperty" Type="Edm.String"/>
              <Property Name="ComplexProperty" Type="Edm.String"/>
            </EntityType>
            <EntityContainer Name="Container"><EntitySet Name="Things" EntityType="N.Thing"/></EntityContainer>
            """));

        var (result, _) = await TestEdmx.GenerateAsync(edmx);

        TestEdmx.AssertCompiles(result);
    }
}
