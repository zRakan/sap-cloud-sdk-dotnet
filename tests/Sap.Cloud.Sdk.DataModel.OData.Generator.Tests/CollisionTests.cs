using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>
/// Name collisions: C# keywords, names the generated code uses itself, clashes between members and types, and clashes caused
/// by the naming strategy. Every case generates code and compiles it.
/// </summary>
public class CollisionTests
{
    private const string KeyId = "<Key><PropertyRef Name=\"Id\"/></Key><Property Name=\"Id\" Type=\"Edm.Int32\" Nullable=\"false\"/>";

    private static async Task<(ODataGeneratorResult Result, string Output)> GenerateAsync(string schema, Action<ODataGeneratorOptions>? configure = null)
    {
        var file = Harness.WriteEdmx(Harness.Edmx(schema));
        return await Harness.GenerateAsync(
            file,
            options =>
            {
                options.DefaultBasePath = "/";
                options.NameSource = NameSource.Name;
                configure?.Invoke(options);
            });
    }

    private static void AssertGeneratedAndCompiles(ODataGeneratorResult result, string output)
    {
        var summary = Assert.Single(result.Services);
        Assert.True(summary.Status == ServiceGenerationStatus.Generated, summary.Error);
        var diagnostics = Harness.Compile(output);
        Assert.True(diagnostics.Count == 0, Harness.Describe(diagnostics));
    }

    private static string Read(string output, string relative) => File.ReadAllText(Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static bool Exists(string output, string relative) => File.Exists(Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public async Task EntityNamesThatAreKeywordsOrNamesOfGeneratedCode_GetSuffix()
    {
        string[] names = ["class", "Event", "Object", "Task", "List", "Guid", "DateTime", "Fields", "Builder", "Batch", "VdmEntity", "Stream", "Default"];
        var types = string.Concat(names.Select(n => $"<EntityType Name=\"{n}\">{KeyId}</EntityType>"));
        var sets = string.Concat(names.Select(n => $"<EntitySet Name=\"{n}s\" EntityType=\"Test.{n}\"/>"));
        var (result, output) = await GenerateAsync(types + $"<EntityContainer Name=\"C\">{sets}</EntityContainer>");

        AssertGeneratedAndCompiles(result, output);
        // the S/4HANA strategy appends "Entity" if the lower-cased name is a keyword; the generator does the same for the names it uses itself
        foreach (var reserved in new[] { "Class", "Event", "Default", "Object", "Task", "List", "Guid", "DateTime", "Fields", "Builder", "Batch", "VdmEntity", "Stream" })
        {
            Assert.True(Exists(output, $"Namespaces/Sample/{reserved}Entity.cs"), reserved);
        }
    }

    [Fact]
    public async Task S4Strategy_AppendsEntityToKeywords_AndKeepsNamesUnique()
    {
        var types = $"<EntityType Name=\"A_Class\">{KeyId}</EntityType><EntityType Name=\"Class\">{KeyId}</EntityType><EntityType Name=\"C_Class\">{KeyId}</EntityType>";
        var sets = "<EntitySet Name=\"A\" EntityType=\"Test.A_Class\"/><EntitySet Name=\"B\" EntityType=\"Test.Class\"/><EntitySet Name=\"C\" EntityType=\"Test.C_Class\"/>";
        var (result, output) = await GenerateAsync(types + $"<EntityContainer Name=\"C\">{sets}</EntityContainer>");

        AssertGeneratedAndCompiles(result, output);
        Assert.True(Exists(output, "Namespaces/Sample/ClassEntity.cs"));
        Assert.True(Exists(output, "Namespaces/Sample/ClassEntity_2.cs"));
        Assert.True(Exists(output, "Namespaces/Sample/ClassEntity_3.cs"));
    }

    [Fact]
    public async Task PropertiesNamedLikeGeneratedOrInheritedMembers_AreRenamed()
    {
        string[] clashing =
        [
            "Foo", "Fields", "All", "Custom", "Builder", "Build", "FooBuilder", "EntityMetadata", "ToString", "GetHashCode", "Equals", "GetType",
            "EntityCollection", "DefaultServicePath", "VersionIdentifier", "CustomFields", "GetCustomField", "RememberChangedField", "TypeMetadata",
            "ID", "_id", "FieldValueConverter", "ITypeConverter", "FetchBarAsync", "SetBar", "Bar", "GetChangedFields", "ServicePathForFetch",
        ];
        var properties = string.Concat(clashing.Select(n => $"<Property Name=\"{n}\" Type=\"Edm.String\"/>"));
        var schema =
            $"""
            <EntityType Name="Foo">{KeyId}{properties}
              <NavigationProperty Name="to_Bar" Relationship="Test.FooBar" FromRole="F" ToRole="B"/>
            </EntityType>
            <EntityType Name="Bar">{KeyId}</EntityType>
            <Association Name="FooBar">
              <End Type="Test.Foo" Multiplicity="1" Role="F"/>
              <End Type="Test.Bar" Multiplicity="*" Role="B"/>
            </Association>
            <EntityContainer Name="C">
              <EntitySet Name="Foos" EntityType="Test.Foo"/>
              <EntitySet Name="Bars" EntityType="Test.Bar"/>
            </EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var foo = Read(output, "Namespaces/Sample/Foo.cs");
        foreach (var renamed in new[] { "Foo_2", "Fields_2", "All_2", "Custom_2", "Builder_2", "Build_2", "ToString_2", "Equals_2", "EntityCollection_2", "ID_2" })
        {
            Assert.Contains($" {renamed}\n", foo, StringComparison.Ordinal);
        }
        // the OData names are kept in the attributes and in the metadata table
        Assert.Contains("[ODataField(\"Fields\")]", foo, StringComparison.Ordinal);
        Assert.Contains(".Property(\"ToString\"", foo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NavigationProperty_ClashingWithProperty_IsRenamed()
    {
        var schema =
            $"""
            <EntityType Name="Foo">{KeyId}
              <Property Name="Bar" Type="Edm.String"/>
              <Property Name="ToBar" Type="Edm.String"/>
              <NavigationProperty Name="Bar2" Relationship="Test.FooBar" FromRole="F" ToRole="B"/>
              <NavigationProperty Name="to_Bar" Relationship="Test.FooBar" FromRole="F" ToRole="B"/>
              <NavigationProperty Name="Bar" Relationship="Test.FooBar2" FromRole="F" ToRole="B"/>
            </EntityType>
            <EntityType Name="Bar">{KeyId}</EntityType>
            <Association Name="FooBar"><End Type="Test.Foo" Multiplicity="1" Role="F"/><End Type="Test.Bar" Multiplicity="*" Role="B"/></Association>
            <Association Name="FooBar2"><End Type="Test.Foo" Multiplicity="1" Role="F"/><End Type="Test.Bar" Multiplicity="0..1" Role="B"/></Association>
            <EntityContainer Name="C">
              <EntitySet Name="Foos" EntityType="Test.Foo"/>
              <EntitySet Name="Bars" EntityType="Test.Bar"/>
            </EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var foo = Read(output, "Namespaces/Sample/Foo.cs");
        Assert.Contains("FetchBar_2Async", foo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TypeNamesDerivedFromOtherTypes_AreKeptUnique()
    {
        // FooField would clash with the field class of Foo, FooFluentHelper with its read helper, FooBuilder is a nested type.
        string[] names = ["Foo", "FooField", "FooLink", "FooFluentHelper", "IFooSelectable", "FooAllFields", "FooByKeyFluentHelper", "FooBuilder"];
        var types = string.Concat(names.Select(n => $"<EntityType Name=\"{n}\">{KeyId}</EntityType>"));
        var sets = string.Concat(names.Select(n => $"<EntitySet Name=\"{n}Set\" EntityType=\"Test.{n}\"/>"));
        var (result, output) = await GenerateAsync(types + $"<EntityContainer Name=\"C\">{sets}</EntityContainer>");

        AssertGeneratedAndCompiles(result, output);
        Assert.True(Exists(output, "Namespaces/Sample/FooField_2.cs"));
        Assert.True(Exists(output, "Namespaces/Sample/FooFluentHelper_2.cs"));
    }

    [Fact]
    public async Task FunctionImports_ClashingWithServiceMethodsAndEachOther_AreRenamed()
    {
        var schema =
            $"""
            <EntityType Name="Foo">{KeyId}</EntityType>
            <ComplexType Name="Res"><Property Name="Value" Type="Edm.String"/></ComplexType>
            <EntityContainer Name="C">
              <EntitySet Name="Foos" EntityType="Test.Foo"/>
              <FunctionImport Name="GetAllFoo" ReturnType="Edm.String" m:HttpMethod="GET"/>
              <FunctionImport Name="getAllFoo" ReturnType="Edm.String" m:HttpMethod="GET"/>
              <FunctionImport Name="Batch" ReturnType="Edm.String" m:HttpMethod="GET"/>
              <FunctionImport Name="WithServicePath" m:HttpMethod="POST"/>
              <FunctionImport Name="ToString" ReturnType="Collection(Edm.Int32)" m:HttpMethod="GET"/>
              <FunctionImport Name="Foo" ReturnType="Test.Foo" m:HttpMethod="GET"/>
              <FunctionImport Name="Res" ReturnType="Test.Res" m:HttpMethod="GET">
                <Parameter Name="servicePath" Type="Edm.String" Mode="In"/>
                <Parameter Name="_servicePath" Type="Edm.String" Mode="In"/>
                <Parameter Name="class" Type="Edm.String" Mode="In"/>
                <Parameter Name="Class" Type="Edm.String" Mode="In"/>
                <Parameter Name="event" Type="Edm.DateTime" Mode="In" Nullable="true"/>
                <Parameter Name="value" Type="Edm.Int32" Mode="In" Nullable="true"/>
                <Parameter Name="data" Type="Edm.Binary" Mode="In" Nullable="true"/>
              </FunctionImport>
            </EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var service = Read(output, "Services/ISampleService.cs");
        Assert.Contains(" GetAllFoo_2(", service, StringComparison.Ordinal) ;
        Assert.Contains(" Batch_2(", service, StringComparison.Ordinal);
        Assert.Contains("classParameter", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimpleStrategy_FailsServiceWithKeywordParameter_LikeJava()
    {
        var schema =
            $"""
            <EntityType Name="Foo">{KeyId}</EntityType>
            <EntityContainer Name="C">
              <EntitySet Name="Foos" EntityType="Test.Foo"/>
              <FunctionImport Name="F" ReturnType="Edm.String" m:HttpMethod="GET"><Parameter Name="class" Type="Edm.String" Mode="In"/></FunctionImport>
            </EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema, options => options.NamingStrategy = new SimpleNamingStrategy());

        var summary = Assert.Single(result.Services);
        Assert.Equal(ServiceGenerationStatus.Failed, summary.Status);
        Assert.Contains("reserved keyword", summary.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task KeysNamedLikeConstructorParameters_GetUniqueParameterNames()
    {
        var schema =
            """
            <EntityType Name="Foo">
              <Key><PropertyRef Name="ServicePath"/><PropertyRef Name="EntityCollection"/><PropertyRef Name="class"/><PropertyRef Name="Class"/><PropertyRef Name="entity"/></Key>
              <Property Name="ServicePath" Type="Edm.String" Nullable="false"/>
              <Property Name="EntityCollection" Type="Edm.String" Nullable="false"/>
              <Property Name="class" Type="Edm.Int32" Nullable="false"/>
              <Property Name="Class" Type="Edm.Int32" Nullable="false"/>
              <Property Name="entity" Type="Edm.Int32" Nullable="false"/>
            </EntityType>
            <EntityContainer Name="C"><EntitySet Name="Foos" EntityType="Test.Foo"/></EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
    }

    [Fact]
    public async Task EntityWithoutKey_AndCompositeKey_Compile()
    {
        var schema =
            """
            <EntityType Name="NoKey"><Property Name="A" Type="Edm.String"/></EntityType>
            <EntityType Name="Composite">
              <Key><PropertyRef Name="A"/><PropertyRef Name="B"/></Key>
              <Property Name="A" Type="Edm.Guid" Nullable="false"/><Property Name="B" Type="Edm.DateTime" Nullable="false"/>
            </EntityType>
            <EntityContainer Name="C"><EntitySet Name="NoKeys" EntityType="Test.NoKey"/><EntitySet Name="Composites" EntityType="Test.Composite"/></EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var service = Read(output, "Services/ISampleService.cs");
        Assert.Contains("GetNoKeyByKey();", service, StringComparison.Ordinal);
        Assert.Contains("GetCompositeByKey(Guid? a, DateTime? b);", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComplexTypes_RecursiveSharedAndClashingWithEntities_Compile()
    {
        var schema =
            $"""
            <ComplexType Name="Node"><Property Name="Value" Type="Edm.String"/><Property Name="Child" Type="Test.Node"/><Property Name="Other" Type="Test.A_Leaf"/></ComplexType>
            <ComplexType Name="A_Leaf"><Property Name="Name" Type="Edm.String"/></ComplexType>
            <EntityType Name="Leaf">{KeyId}<Property Name="Tree" Type="Test.Node"/><Property Name="Leaf" Type="Test.A_Leaf"/></EntityType>
            <EntityType Name="Other">{KeyId}<Property Name="Tree" Type="Test.Node"/></EntityType>
            <EntityContainer Name="C">
              <EntitySet Name="Leaves" EntityType="Test.Leaf"/>
              <EntitySet Name="Others" EntityType="Test.Other"/>
              <FunctionImport Name="GetNodes" ReturnType="Collection(Test.Node)" m:HttpMethod="GET"/>
            </EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        Assert.Equal(2, Assert.Single(result.Services).ComplexTypeCount);
        // the entity Leaf is generated first, so the complex type A_Leaf (also "Leaf" for the S/4 strategy) gets the suffix
        Assert.True(Exists(output, "Namespaces/Sample/Leaf.cs"));
        Assert.True(Exists(output, "Namespaces/Sample/Leaf_2.cs"));
    }

    [Fact]
    public async Task InheritedEntityTypes_ContainBaseTypeProperties()
    {
        var schema =
            """
            <EntityType Name="Base"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/><Property Name="Name" Type="Edm.String"/></EntityType>
            <EntityType Name="Derived" BaseType="Test.Base"><Property Name="Extra" Type="Edm.String"/></EntityType>
            <EntityContainer Name="C"><EntitySet Name="Deriveds" EntityType="Test.Derived"/></EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var derived = Read(output, "Namespaces/Sample/Derived.cs");
        Assert.Contains(".Property(\"Id\"", derived, StringComparison.Ordinal);
        Assert.Contains(".Property(\"Name\"", derived, StringComparison.Ordinal);
        Assert.Contains(".Property(\"Extra\"", derived, StringComparison.Ordinal);
        Assert.Contains("GetDerivedByKey(int? id)", Read(output, "Services/ISampleService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameEntityTypeInSeveralEntitySets_GeneratesServiceMethodsPerSetOnRequest()
    {
        var schema =
            $"""
            <EntityType Name="A_Person">{KeyId}</EntityType>
            <EntityContainer Name="C">
              <EntitySet Name="A_Firsts" EntityType="Test.A_Person"/>
              <EntitySet Name="A_Seconds" EntityType="Test.A_Person"/>
              <EntitySet Name="Person" EntityType="Test.A_Person"/>
            </EntityContainer>
            """;

        var (single, singleOutput) = await GenerateAsync(schema);
        AssertGeneratedAndCompiles(single, singleOutput);
        var singleService = Read(singleOutput, "Services/ISampleService.cs");
        Assert.Contains("GetAllPerson()", singleService, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllFirsts", singleService, StringComparison.Ordinal);

        var (perSet, perSetOutput) = await GenerateAsync(schema, options => options.ServiceMethodsPerEntitySet = true);
        AssertGeneratedAndCompiles(perSet, perSetOutput);
        var perSetService = Read(perSetOutput, "Services/ISampleService.cs");
        Assert.Contains("GetAllFirsts()", perSetService, StringComparison.Ordinal);
        Assert.Contains("GetAllSeconds()", perSetService, StringComparison.Ordinal);
        Assert.Contains("GetAllPerson()", perSetService, StringComparison.Ordinal);
        Assert.Contains("\"A_Seconds\"", Read(perSetOutput, "Services/DefaultSampleService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropertyNamesDifferingOnlyInCase_AreKeptApart()
    {
        var schema = $"<EntityType Name=\"Foo\">{KeyId}<Property Name=\"Name\" Type=\"Edm.String\"/><Property Name=\"NAME\" Type=\"Edm.String\"/><Property Name=\"name\" Type=\"Edm.String\"/></EntityType><EntityContainer Name=\"C\"><EntitySet Name=\"Foos\" EntityType=\"Test.Foo\"/></EntityContainer>";
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var foo = Read(output, "Namespaces/Sample/Foo.cs");
        Assert.Contains(" NAME_2\n", foo, StringComparison.Ordinal);
        Assert.Contains(" Name_3\n", foo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_WithInvalidCharactersAndLabels_AreConverted()
    {
        var schema =
            """
            <EntityType Name="Sales-Order.Item" sap:label="Sales order  item!">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.Int32" Nullable="false" sap:label="The ID"/>
              <Property Name="Net-Value" Type="Edm.Decimal" sap:label="Net value (EUR)"/>
              <Property Name="Größe" Type="Edm.String"/>
              <Property Name="Quote&quot;Slash\" Type="Edm.String"/>
            </EntityType>
            <EntityContainer Name="C"><EntitySet Name="Items" EntityType="Test.Sales-Order.Item"/></EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema, options => options.NameSource = NameSource.Label);

        AssertGeneratedAndCompiles(result, output);
        var entity = Read(output, "Namespaces/Sample/SalesOrderItem.cs");
        Assert.Contains("public int? TheID", entity, StringComparison.Ordinal);
        Assert.Contains("public decimal? NetValueEUR", entity, StringComparison.Ordinal);
        Assert.Contains("public string? Größe", entity, StringComparison.Ordinal);
        Assert.Contains("[ODataField(\"Net-Value\")]", entity, StringComparison.Ordinal);
        Assert.Contains("[ODataField(\"Quote\\\"Slash\\\\\")]", entity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentationText_IsEscapedForXml()
    {
        var schema =
            """
            <EntityType Name="Foo" sap:label="Foo &lt;b&gt; &amp; more">
              <Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.Int32" Nullable="false" sap:quickinfo="Id with &lt;tags&gt; &amp; line&#10;breaks">
                <Documentation><Summary>Summary &lt;x/&gt;</Summary><LongDescription>Long &amp; "quoted" description</LongDescription></Documentation>
              </Property>
            </EntityType>
            <EntityContainer Name="C"><EntitySet Name="Foos" EntityType="Test.Foo"/></EntityContainer>
            """;
        var (result, output) = await GenerateAsync(schema);

        AssertGeneratedAndCompiles(result, output);
        var foo = Read(output, "Namespaces/Sample/Foo.cs");
        Assert.Contains("Summary &lt;x/&gt;", foo, StringComparison.Ordinal);
        Assert.Contains("Long &amp; \"quoted\" description", foo, StringComparison.Ordinal);
    }
}
