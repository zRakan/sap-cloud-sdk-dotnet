using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>
/// Payload of update requests: PATCH sends only changed fields, PUT sends everything. Ports
/// <c>UpdateRequestHelperPatchTest</c> and <c>UpdateRequestHelperPutTest</c>.
/// </summary>
public class UpdatePayloadTests
{
    private static string Patch(PatchEntity entity, params IFieldReference[] includedFields) =>
        new UpdateRequestBuilder<PatchEntity>("/service", entity, "EntityParentCollection")
            .IncludingFields(includedFields)
            .ToRequest()
            .SerializedEntity;

    private static string Put(PatchEntity entity, params IFieldReference[] excludedFields) =>
        new UpdateRequestBuilder<PatchEntity>("/service", entity, "EntityParentCollection")
            .ReplacingEntity()
            .ExcludingFields(excludedFields)
            .ToRequest()
            .SerializedEntity;

    [Fact]
    public void Patch_BigDecimal()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.Value = 0.00000000001m;

        Asserts.JsonLenient("{\"Value\":0.00000000001,\"@odata.type\":\"#TestEntity\"}", Patch(entity));
        Assert.Contains("\"Value\":0.00000000001", Patch(entity), StringComparison.Ordinal);
    }

    [Fact]
    public void Patch_Double()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.EffortValue = 42d;

        Asserts.JsonLenient("{\"EffortValue\":42,\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_Integer()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.ShoeSize = 46;

        Asserts.JsonLenient("{\"ShoeSize\":46,\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_String()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.Name = "Bar";

        Asserts.JsonLenient("{\"Name\":\"Bar\",\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_Date()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.BirthDate = new DateOnly(1999, 3, 14);

        Asserts.JsonLenient("{\"BirthDate\":\"1999-03-14\",\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_Character()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.FavouriteCharacter = 'A';

        Asserts.JsonLenient("{\"FavouriteCharacter\":\"A\",\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_Boolean()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.IsRetired = false;

        Asserts.JsonLenient("{\"IsRetired\":false,\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_SimpleCollection()
    {
        var entity = PatchEntity.Create(name: "Foo");
        entity.Emails = ["foo@sap.com"];

        Asserts.JsonLenient("{\"Emails\":[\"foo@sap.com\"],\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_NothingChanged_SendsOnlyTheType()
    {
        Assert.Equal("{\"@odata.type\":\"#TestEntity\"}", Patch(PatchEntity.Create(name: "Foo")));
    }

    [Fact]
    public void Patch_IncludedFieldsAreSentEvenIfUnchanged()
    {
        var entity = PatchEntity.Create(name: "Foo", value: 1m);

        Asserts.JsonLenient(
            "{\"Name\":\"Foo\",\"Value\":1,\"@odata.type\":\"#TestEntity\"}",
            Patch(entity, PatchEntity.Fields.Name, PatchEntity.Fields.Value));
    }

    [Fact]
    public void Patch_ExistingNeighborIsReferencedById()
    {
        var foo = PatchEntity.Create(name: "Foo", value: 0m);
        var bar = PatchEntity.Create(name: "Bar", value: 1m);
        foo.Neighbor = bar;

        Asserts.JsonLenient(
            "{\"Neighbor\":{\"@id\":\"EntityParentCollection('Bar')\"},\"@odata.type\":\"#TestEntity\"}",
            Patch(foo));
    }

    [Fact]
    public void Patch_NewNeighborIsSentWithItsChanges()
    {
        var foo = PatchEntity.Create(name: "Foo", value: 0m);
        var bar = new PatchEntity { Name = "Bar", Value = 1m };
        foo.Neighbor = bar;

        Asserts.JsonLenient(
            "{\"Neighbor\":{\"Value\":1,\"Name\":\"Bar\"},\"@odata.type\":\"#TestEntity\"}",
            Patch(foo));
    }

    [Fact]
    public void Patch_ChangedNeighborIsSentWithChangesAndId()
    {
        var foo = PatchEntity.Create(name: "Foo", value: 0m);
        var bar = PatchEntity.Create(name: "Bar");
        bar.Value = 1m;
        foo.Neighbor = bar;

        Asserts.JsonLenient(
            "{\"Neighbor\":{\"Value\":1,\"@id\":\"EntityParentCollection('Bar')\"},\"@odata.type\":\"#TestEntity\"}",
            Patch(foo));
    }

    [Fact]
    public void Patch_CyclicNeighborsTerminate()
    {
        var bar = new PatchEntity { Name = "Bar" };
        var foo = new PatchEntity { Name = "Foo" };
        foo.Neighbor = bar;
        bar.Neighbor = foo;

        Asserts.JsonLenient(
            "{\"Neighbor\":{\"Neighbor\":{\"@id\":\"EntityParentCollection('Foo')\"},\"Name\":\"Bar\"},\"Name\":\"Foo\",\"@odata.type\":\"#TestEntity\"}",
            Patch(foo));
    }

    [Fact]
    public void Patch_SameEntityReferencedTwiceIsSerializedTwice()
    {
        var shared = new PatchEntity { Name = "Shared" };
        var foo = new PatchEntity { Name = "Foo" };
        foo.Neighbor = shared;
        foo.ComplexProperty = PatchComplex.Create("x");
        var json = Patch(foo, PatchEntity.Fields.Name);

        Assert.Contains("\"Neighbor\":{\"Name\":\"Shared\"}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Patch_NewComplexMemberIncludesAllFieldsAndCustomFields()
    {
        var complex = PatchComplex.Create("Foo");
        complex.SetCustomField("customField", "customValue");
        var entity = new PatchEntity { ComplexProperty = complex };

        Asserts.JsonLenient(
            "{\"ComplexProperty\":{\"StringProperty\":\"Foo\",\"customField\":\"customValue\"},\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
        Asserts.JsonEqual(
            "{\"ComplexProperty\":{\"StringProperty\":\"Foo\",\"ComplexProperty\":null,\"customField\":\"customValue\"},\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
    }

    [Fact]
    public void Patch_RemovedComplexMember()
    {
        var entity = PatchEntity.Create(complexProperty: PatchComplex.Create("Foo"));
        entity.ComplexProperty = null;

        Asserts.JsonEqual("{\"ComplexProperty\":null,\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_UpdatedFieldOfComplexMemberSendsTheWholeComplexValue()
    {
        var entity = PatchEntity.Create(complexProperty: PatchComplex.Create());
        entity.ComplexProperty!.StringProperty = "Foo";

        Asserts.JsonLenient("{\"ComplexProperty\":{\"StringProperty\":\"Foo\"},\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_RemovedFieldsOfComplexMember()
    {
        var complex = PatchComplex.Create("Foo");
        complex.SetCustomField("customField", "customValue");
        complex.ResetChangedFields();
        var entity = PatchEntity.Create(complexProperty: complex);
        entity.ComplexProperty!.StringProperty = null;
        entity.ComplexProperty.SetCustomField("customField", null);

        Asserts.JsonLenient(
            "{\"ComplexProperty\":{\"StringProperty\":null,\"customField\":null},\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
    }

    [Fact]
    public void Patch_UpdatedFieldOfNestedComplexMember()
    {
        var child = new PatchComplex();
        var parent = PatchComplex.Create("Parent", child);
        var entity = PatchEntity.Create(complexProperty: parent);
        child.StringProperty = "Hello, World!";

        Asserts.JsonLenient(
            "{\"ComplexProperty\":{\"StringProperty\":\"Parent\",\"ComplexProperty\":{\"StringProperty\":\"Hello, World!\"}},\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
    }

    [Fact]
    public void Patch_RemovedFieldOfNestedComplexMember()
    {
        var child = PatchComplex.Create("Child");
        var parent = PatchComplex.Create("Parent", child);
        var entity = PatchEntity.Create(complexProperty: parent);
        entity.ComplexProperty!.ComplexProperty!.StringProperty = null;

        Asserts.JsonLenient(
            "{\"ComplexProperty\":{\"StringProperty\":\"Parent\",\"ComplexProperty\":{\"StringProperty\":null}},\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
    }

    [Fact]
    public void Patch_NullableCollectionSetToEmpty()
    {
        var entity = new PatchEntity();
        Assert.Null(entity.ComplexProperties);

        entity.ComplexProperties = [];

        Asserts.JsonEqual("{\"ComplexProperties\":[],\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_EmptyCollectionReplacedByEmptyCollectionIsNoChange()
    {
        var entity = PatchEntity.Create(complexProperties: []);

        entity.ComplexProperties = [];

        Asserts.JsonEqual("{\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_RemovedCollection()
    {
        var entity = PatchEntity.Create(complexProperties: []);

        entity.ComplexProperties = null;

        Asserts.JsonEqual("{\"ComplexProperties\":null,\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Patch_NewCollectionOfComplexValues()
    {
        var complex = PatchComplex.Create("Foo");
        complex.SetCustomField("customField", "customValue");
        var entity = PatchEntity.Create(complexProperties: []);

        entity.ComplexProperties = [complex];

        Asserts.JsonEqual(
            "{\"ComplexProperties\":[{\"StringProperty\":\"Foo\",\"ComplexProperty\":null,\"customField\":\"customValue\"}],\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
    }

    [Fact]
    public void Patch_UpdatedComplexMemberInCollection()
    {
        var member = new PatchComplex();
        var entity = PatchEntity.Create(complexProperties: [member]);

        member.StringProperty = "Foo";
        member.SetCustomField("customField", "customValue");

        Asserts.JsonLenient(
            "{\"ComplexProperties\":[{\"StringProperty\":\"Foo\",\"customField\":\"customValue\"}],\"@odata.type\":\"#TestEntity\"}",
            Patch(entity));
    }

    [Fact]
    public void Patch_RemovedFieldOfComplexMemberInCollection()
    {
        var member = PatchComplex.Create("Foo");
        var entity = PatchEntity.Create(complexProperties: [member]);

        member.StringProperty = null;

        Asserts.JsonLenient("{\"ComplexProperties\":[{\"StringProperty\":null}],\"@odata.type\":\"#TestEntity\"}", Patch(entity));
    }

    [Fact]
    public void Put_SendsAllFields()
    {
        var entity = PatchEntity.Create(name: "Foo", isRetired: true);
        entity.Value = 0.00000000001m;
        entity.Emails = ["sampleEmail@sap.com"];

        Asserts.JsonEqual(
            "{\"@odata.type\":\"#TestEntity\",\"Value\":0.00000000001,\"Emails\":[\"sampleEmail@sap.com\"],\"Name\":\"Foo\",\"IsRetired\":true}",
            Put(entity));
    }

    [Fact]
    public void Put_ExcludedFieldsAreLeftOut()
    {
        var entity = PatchEntity.Create(name: "Foo", isRetired: true);
        entity.Value = 0.00000000001m;

        Asserts.JsonEqual(
            "{\"@odata.type\":\"#TestEntity\",\"Value\":0.00000000001}",
            Put(entity, PatchEntity.Fields.IsRetired, PatchEntity.Fields.Name, PatchEntity.Fields.Emails));
    }

    [Fact]
    public void Put_OmitsNullFields()
    {
        var entity = PatchEntity.Create(name: "Foo", isRetired: true);
        entity.Value = 0.00000000001m;

        Asserts.JsonEqual("{\"@odata.type\":\"#TestEntity\",\"Value\":0.00000000001,\"Name\":\"Foo\",\"IsRetired\":true}", Put(entity));
    }

    [Fact]
    public void Patch_OfSampleEntity_SendsOnlyChangedFieldsWithTheKeyInTheUrl()
    {
        var person = VdmJson.Deserialize<Person>("{\"UserName\":\"u\",\"FirstName\":\"f\",\"LastName\":\"l\"}")!;
        person.LastName = "new";

        var request = new UpdateRequestBuilder<Person>("/service", person, "People").ToRequest();

        Assert.Equal("{\"LastName\":\"new\",\"@odata.type\":\"#Trippin.Person\"}", request.SerializedEntity);
        Assert.Equal("/service/People('u')", request.GetRelativeUri().OriginalString);
    }
}
