using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Request payloads for create and the update strategies (Java: ODataEntitySerializerTest).</summary>
public class EntitySerializerTests
{
    private static TestVdmEntity Entity(string text = "string", bool flag = false)
    {
        var entity = TestVdmEntity.Built(e =>
        {
            e.StringValue = text;
            e.BooleanValue = flag;
        });
        entity.IntegerValue = 42;
        return entity;
    }

    [Fact]
    public void Create_OmitsNulls_InDeclarationOrder() =>
        Assert.Equal(
            "{\"IntegerValue\":42,\"StringValue\":\"string\",\"BooleanValue\":false}",
            ODataEntitySerializer.SerializeEntityForCreate(Entity()));

    [Fact]
    public void Create_RemovesEmptyListsRecursively()
    {
        var entity = TestVdmEntity.Built(e => e.StringValue = "a");
        entity.AddToChildren();
        var child = TestVdmEntity.Built(c => c.IntegerValue = 1);
        child.AddToChildren();
        entity.AddToChildren(child);

        Assert.Equal(
            "{\"StringValue\":\"a\",\"to_Children\":[{\"IntegerValue\":1}]}",
            ODataEntitySerializer.SerializeEntityForCreate(entity));
    }

    [Fact]
    public void Create_SerializesDeepInsert()
    {
        var entity = TestVdmEntity.Built(e => e.StringValue = "root");
        entity.SetToParent(TestVdmEntity.Built(p => p.StringValue = "parent"));
        entity.AddToChildren(TestVdmEntity.Built(c => c.StringValue = "child1"), TestVdmEntity.Built(c => c.StringValue = "child2"));

        JsonAssert.Equal(
            """{"StringValue":"root","to_Parent":{"StringValue":"parent"},"to_Children":[{"StringValue":"child1"},{"StringValue":"child2"}]}""",
            ODataEntitySerializer.SerializeEntityForCreate(entity));
    }

    [Fact]
    public void Put_WritesAllFields_AndNoVersionIdentifier()
    {
        var entity = Entity();
        entity.VersionIdentifier = "W/\"1\"";

        Assert.Equal(
            "{\"IntegerValue\":42,\"GuidValue\":null,\"StringValue\":\"string\",\"OffsetDateTimeValue\":null,\"to_Parent\":null,\"to_Children\":null,\"DecimalValue\":null,\"DoubleValue\":null,\"LocalTimeValue\":null,\"LocalDateTimeValue\":null,\"BooleanValue\":false,\"ComplexValue\":null}",
            ODataEntitySerializer.SerializeEntityForUpdatePut(entity, null));
    }

    [Fact]
    public void Put_WithExcludedFields()
    {
        var entity = Entity("NewString", true);
        entity.IntegerValue = 45;

        Assert.Equal(
            "{\"IntegerValue\":45,\"GuidValue\":null,\"StringValue\":\"NewString\",\"OffsetDateTimeValue\":null,\"to_Parent\":null,\"to_Children\":null,\"DecimalValue\":null,\"LocalTimeValue\":null,\"LocalDateTimeValue\":null,\"BooleanValue\":true}",
            ODataEntitySerializer.SerializeEntityForUpdatePut(entity, ["DoubleValue", "ComplexValue"]));
    }

    [Fact]
    public void PatchShallow_SendsChangedAndIncludedFields() =>
        JsonAssert.Equal(
            "{\"a\":null,\"b\":null,\"IntegerValue\":42}",
            ODataEntitySerializer.SerializeEntityForUpdatePatchShallow(Entity(), ["a", "b"]));

    [Fact]
    public void PatchShallow_SendsChangedCustomFields()
    {
        var entity = TestVdmEntity.Built();
        entity.SetCustomField("Custom", "value");

        JsonAssert.Equal("{\"Custom\":\"value\"}", ODataEntitySerializer.SerializeEntityForUpdatePatchShallow(entity, []));
    }

    [Fact]
    public void PatchShallow_ExplicitNullIsSent()
    {
        var entity = TestVdmEntity.Built(e => e.StringValue = "x");
        entity.StringValue = null;

        JsonAssert.Equal("{\"StringValue\":null}", ODataEntitySerializer.SerializeEntityForUpdatePatchShallow(entity, []));
    }

    [Fact]
    public void PatchRecursive_FullAndDelta()
    {
        var grandchild = TestVdmComplex.Built(c => c.SomeValue = "initialGrandchildValue");
        var child = TestVdmComplex.Built(c =>
        {
            c.SomeValue = "initialChildValue";
            c.ComplexValue = grandchild;
        });
        var root = TestVdmEntity.Built(e =>
        {
            e.StringValue = "initialRootValue";
            e.BooleanValue = false;
            e.ComplexValue = child;
        });
        root.StringValue = "newRootValue";
        grandchild.SomeValue = "newGrandchildValue";

        JsonAssert.Equal(
            """{"StringValue":"newRootValue","customField":null,"ComplexValue":{"SomeValue":"initialChildValue","OtherValue":null,"ComplexValue":{"SomeValue":"newGrandchildValue","OtherValue":null,"ComplexValue":null}}}""",
            ODataEntitySerializer.SerializeEntityForUpdatePatchRecursiveFull(root, ["customField"]));
        JsonAssert.Equal(
            """{"customField":null,"ComplexValue":{"ComplexValue":{"SomeValue":"newGrandchildValue"}},"StringValue":"newRootValue"}""",
            ODataEntitySerializer.SerializeEntityForUpdatePatchRecursiveDelta(root, ["customField"]));

        var sibling = TestVdmComplex.Built(c => c.SomeValue = "newSiblingGrandchildValue");
        child.ComplexValue = sibling;

        JsonAssert.Equal(
            """{"customField":null,"ComplexValue":{"ComplexValue":{"SomeValue":"newSiblingGrandchildValue","OtherValue":null,"ComplexValue":null}},"StringValue":"newRootValue"}""",
            ODataEntitySerializer.SerializeEntityForUpdatePatchRecursiveDelta(root, ["customField"]));
    }
}
