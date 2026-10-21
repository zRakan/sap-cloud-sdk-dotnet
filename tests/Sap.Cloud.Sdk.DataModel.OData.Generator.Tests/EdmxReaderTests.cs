using System.Text;
using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>The reader that replaces Olingo's EDMX provider.</summary>
public class EdmxReaderTests
{
    private static EdmModel Read(string xml) => EdmxReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private const string Shop =
        """
        <EntityType Name="Order" sap:label="Order label">
          <Key><PropertyRef Name="Id"/></Key>
          <Property Name="Id" Type="Edm.Int32" Nullable="false"/>
          <Property Name="Total" Type="Edm.Decimal" Precision="10" Scale="2" DefaultValue="0"/>
          <Property Name="Address" Type="Test.Address"/>
          <NavigationProperty Name="Items" Relationship="Test.OrderItems" FromRole="Order" ToRole="Item"/>
        </EntityType>
        <EntityType Name="Item" m:HasStream="true"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/></EntityType>
        <ComplexType Name="Address"><Property Name="Street" Type="Edm.String" MaxLength="Max"/></ComplexType>
        <Association Name="OrderItems"><End Type="Test.Order" Multiplicity="1" Role="Order"/><End Type="Test.Item" Multiplicity="*" Role="Item"/></Association>
        <EntityContainer Name="C" m:IsDefaultEntityContainer="true">
          <EntitySet Name="Orders" EntityType="Test.Order" sap:creatable="false"/>
          <EntitySet Name="Items" EntityType="Test.Item"/>
          <FunctionImport Name="Many" ReturnType="Collection(Test.Item)" EntitySet="Items" m:HttpMethod="GET"><Parameter Name="P" Type="Edm.Int32" Mode="In" Nullable="false"/></FunctionImport>
          <FunctionImport Name="One" ReturnType="Edm.String" m:HttpMethod="Post"/>
          <FunctionImport Name="None" m:HttpMethod="POST"/>
        </EntityContainer>
        """;

    [Fact]
    public void Reads_TypesAssociationsAndContainers()
    {
        var model = Read(Harness.Edmx(Shop));

        Assert.Equal("2.0", model.DataServiceVersion);
        var schema = Assert.Single(model.Schemas);
        Assert.Equal("Test", schema.Namespace);
        Assert.Equal(["Order", "Item"], schema.EntityTypes.Select(t => t.Name));

        var order = schema.EntityTypes[0];
        Assert.Equal("Order label", order.Label);
        Assert.Equal(["Id"], order.Keys);
        Assert.False(order.HasStream);
        Assert.True(schema.EntityTypes[1].HasStream);
        var total = order.Properties.Single(p => p.Name == "Total");
        Assert.Equal(new EdmFacets(null, "0", null, 10, 2), total.Facets);
        Assert.Equal(EdmSimpleKind.Decimal, ((EdmSimpleType)total.Type!).SimpleKind);
        Assert.Equal(EdmTypeKind.Complex, order.Properties.Single(p => p.Name == "Address").Type!.Kind);

        var items = Assert.Single(order.NavigationProperties);
        Assert.Equal(EdmMultiplicity.Many, items.Multiplicity);
        Assert.Same(schema.EntityTypes[1], items.TargetType);

        Assert.Equal(["Orders", "Items"], model.EntitySets.Select(s => s.Name));
        Assert.Equal("false", model.EntitySets[0].SapAttributes["creatable"]);
        var many = model.FunctionImports.Single(f => f.Name == "Many");
        Assert.Equal(EdmMultiplicity.Many, many.ReturnMultiplicity);
        Assert.Same(schema.EntityTypes[1], many.ReturnType);
        Assert.Equal("GET", many.HttpMethod);
        Assert.Equal(false, many.Parameters.Single().Facets.Nullable);
        Assert.Null(model.FunctionImports.Single(f => f.Name == "None").ReturnType);
        Assert.Equal("Post", model.FunctionImports.Single(f => f.Name == "One").HttpMethod);
    }

    [Fact]
    public void NavigationMultiplicity_FollowsTheTargetEnd()
    {
        var model = Read(Harness.Edmx(
            """
            <EntityType Name="A"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/>
              <NavigationProperty Name="ToB" Relationship="Test.AB" FromRole="A" ToRole="B"/>
              <NavigationProperty Name="ToC" Relationship="Test.AB" FromRole="B" ToRole="A"/>
            </EntityType>
            <EntityType Name="B"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/></EntityType>
            <Association Name="AB"><End Type="Test.A" Multiplicity="0..1" Role="A"/><End Type="Test.B" Multiplicity="1" Role="B"/></Association>
            """));

        var a = model.Schemas[0].EntityTypes[0];
        Assert.Equal(EdmMultiplicity.One, a.NavigationProperties[0].Multiplicity);
        Assert.Equal("B", a.NavigationProperties[0].TargetType!.Name);
        Assert.Equal(EdmMultiplicity.ZeroToOne, a.NavigationProperties[1].Multiplicity);
        Assert.Equal("A", a.NavigationProperties[1].TargetType!.Name);
    }

    [Fact]
    public void Aliases_AndSeveralSchemas_AreResolved()
    {
        var model = Read(
            """
            <edmx:Edmx Version="1.0" xmlns:edmx="http://schemas.microsoft.com/ado/2007/06/edmx" xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata">
              <edmx:DataServices m:DataServiceVersion="1.0">
                <Schema Namespace="Model.Types" Alias="Types" xmlns="http://schemas.microsoft.com/ado/2006/04/edm">
                  <ComplexType Name="Money"><Property Name="Amount" Type="Edm.Decimal"/></ComplexType>
                  <EntityType Name="Product"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32"/><Property Name="Price" Type="Types.Money"/></EntityType>
                </Schema>
                <Schema Namespace="Model.Service" xmlns="http://schemas.microsoft.com/ado/2008/09/edm">
                  <EntityContainer Name="C"><EntitySet Name="Products" EntityType="Model.Types.Product"/></EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """);

        Assert.Equal(2, model.Schemas.Count);
        Assert.Equal("Model.Types", model.FirstNamespace);
        var product = model.EntitySets.Single().EntityType!;
        Assert.Equal("Model.Types.Money", product.Properties[1].Type!.QualifiedName);
    }

    [Fact]
    public void BaseType_InheritsPropertiesKeysAndNavigations()
    {
        var model = Read(Harness.Edmx(
            """
            <EntityType Name="Base"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32"/><Property Name="Name" Type="Edm.String"/>
              <NavigationProperty Name="Peers" Relationship="Test.R" FromRole="A" ToRole="B"/></EntityType>
            <EntityType Name="Derived" BaseType="Test.Base"><Property Name="Name" Type="Edm.Int64"/><Property Name="Extra" Type="Edm.String"/></EntityType>
            <Association Name="R"><End Type="Test.Base" Multiplicity="1" Role="A"/><End Type="Test.Base" Multiplicity="*" Role="B"/></Association>
            """));

        var derived = model.Schemas[0].EntityTypes[1];
        Assert.Equal(["Id", "Name", "Extra"], derived.Properties.Select(p => p.Name));
        Assert.Equal(EdmSimpleKind.Int64, ((EdmSimpleType)derived.Properties[1].Type!).SimpleKind);
        Assert.Equal(["Id"], derived.Keys);
        Assert.Equal(["Peers"], derived.NavigationProperties.Select(n => n.Name));
    }

    [Fact]
    public void Documentation_IsRead()
    {
        var model = Read(Harness.Edmx(
            """
            <EntityType Name="E" sap:quickinfo="Quick"><Key><PropertyRef Name="Id"/></Key>
              <Property Name="Id" Type="Edm.Int32"><Documentation><Summary> Sum </Summary><LongDescription>Long</LongDescription></Documentation></Property>
            </EntityType>
            """));

        var id = model.Schemas[0].EntityTypes[0].Properties[0];
        Assert.Equal(new EdmDocumentation(" Sum ", "Long"), id.Documentation);
        Assert.Equal("Quick", model.Schemas[0].EntityTypes[0].QuickInfo);
    }

    [Fact]
    public void ByteOrderMark_IsTolerated()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Harness.Edmx(Shop))).ToArray();
        Assert.NotNull(EdmxReader.Read(new MemoryStream(bytes)));
    }

    public static TheoryData<string, string> InvalidModels() => new()
    {
        { "<foo/>", "not an EDMX" },
        { "<edmx:Edmx xmlns:edmx=\"http://schemas.microsoft.com/ado/2007/06/edmx\"/>", "DataServices" },
        { "<edmx:Edmx xmlns:edmx=\"http://schemas.microsoft.com/ado/2007/06/edmx\"><edmx:DataServices/></edmx:Edmx>", "not an OData V2" },
        { "<edmx:Edmx Version=\"4.0\" xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\"><edmx:DataServices><Schema Namespace=\"A\"/></edmx:DataServices></edmx:Edmx>", "not an OData V2" },
        { Harness.Edmx("<EntityType Name=\"E\"><Property Name=\"P\" Type=\"Test.Missing\"/></EntityType>"), "cannot be resolved" },
        { Harness.Edmx("<EntityType Name=\"E\"><Property Name=\"P\" Type=\"Edm.Unknown\"/></EntityType>"), "not a supported primitive" },
        { Harness.Edmx("<EntityType Name=\"E\"><Key><PropertyRef Name=\"Nope\"/></Key><Property Name=\"P\" Type=\"Edm.String\"/></EntityType>"), "key property 'Nope'" },
        { Harness.Edmx("<EntityType Name=\"E\"><Property Type=\"Edm.String\"/></EntityType>"), "attribute 'Name'" },
        { Harness.Edmx("<EntityType Name=\"E\"><NavigationProperty Name=\"N\" Relationship=\"Test.Nope\" FromRole=\"A\" ToRole=\"B\"/></EntityType>"), "association 'Test.Nope'" },
        { Harness.Edmx("<EntityType Name=\"E\"><Property Name=\"P\" Type=\"Edm.String\"/></EntityType><EntityType Name=\"F\"><Property Name=\"Q\" Type=\"Test.E\"/></EntityType>"), "has the entity type" },
        { Harness.Edmx("<Association Name=\"A\"><End Type=\"Test.E\" Multiplicity=\"2\" Role=\"R\"/></Association>"), "Unknown multiplicity" },
        { Harness.Edmx("<EntityContainer Name=\"C\"><EntitySet Name=\"S\" EntityType=\"Test.Missing\"/></EntityContainer>"), "cannot be resolved" },
        { "<edmx:Edmx xmlns:edmx=\"http://schemas.microsoft.com/ado/2007/06/edmx\"><edmx:DataServices><Schema>", "Please check" },
    };

    [Theory]
    [MemberData(nameof(InvalidModels))]
    public void InvalidDocuments_AreRejected(string xml, string reason)
    {
        var exception = Assert.Throws<ODataGeneratorReadException>(() => Read(xml));
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DoctypeDeclarations_AreProhibited()
    {
        var xml = "<!DOCTYPE x [<!ENTITY a \"b\">]>" + Harness.Edmx("");
        Assert.Throws<ODataGeneratorReadException>(() => Read(xml));
    }

    [Fact]
    public void ReadFile_ReportsFileProblems()
    {
        var missing = Path.Combine(Harness.NewTemporaryDirectory(), "missing.edmx");
        Assert.Throws<ODataGeneratorReadException>(() => EdmxReader.ReadFile(missing));
    }
}
