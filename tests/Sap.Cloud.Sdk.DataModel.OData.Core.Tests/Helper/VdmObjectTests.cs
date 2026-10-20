using System.Reflection;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Annotation;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Helper;
using Sap.Cloud.Sdk.TypeConverter.Exceptions;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Change tracking, custom fields, equality and attachment of entities (Java: ODataChangedFieldsTest, VdmEntityTest, CustomFieldTypeConverterTest).</summary>
public class VdmObjectTests
{
    private sealed class AttachableEntity : VdmEntity<AttachableEntity>
    {
        private static readonly VdmTypeMetadata Metadata = VdmTypeMetadata.For<AttachableEntity>().Build();

        protected override VdmTypeMetadata TypeMetadata => Metadata;

        protected override string? EntityCollection => null;

        protected override string? DefaultServicePath => "/odata/default";
    }

    private static readonly DefaultHttpDestination Destination = DefaultHttpDestination.CreateBuilder("https://example.org").Name("UnitTestDestination").Build();

    [Fact]
    public void NewEntity_HasNoChangedFields() => Assert.Empty(new TestVdmEntity().GetChangedFields());

    [Fact]
    public void Builder_DoesNotTrackChanges() => Assert.Empty(TestVdmEntity.Built(e => e.StringValue = "Foo").GetChangedFields());

    [Fact]
    public void SetterTracksChange()
    {
        var entity = new TestVdmEntity { StringValue = "Foo" };

        Assert.Equal("Foo", Assert.Single(entity.GetChangedFields(), f => f.Key == "StringValue").Value);
    }

    [Fact]
    public void SettingNullOnNullField_IsNotAChange()
    {
        var entity = new TestVdmEntity { DecimalValue = null };

        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void CustomFieldChangeIsTracked()
    {
        var entity = new TestVdmEntity();

        entity.SetCustomField("BarValue", "Foo");

        Assert.Equal("Foo", entity.GetChangedFields()["BarValue"]);
    }

    [Fact]
    public void SettingCustomFieldToNull_IsNotAChange()
    {
        var entity = new TestVdmEntity();

        entity.SetCustomField("BarValue", null);

        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void RevertingValue_IsNotReportedAsChange()
    {
        var entity = new TestVdmEntity { IntegerValue = 9000 };
        Assert.Equal(9000, entity.GetChangedFields()["IntegerValue"]);

        entity.IntegerValue = null;

        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void ComplexValueChangeIsTrackedAsWhole()
    {
        var entity = new TestVdmEntity();
        var complex1 = TestVdmComplex.Built(c => c.SomeValue = "Foo");
        var complex2 = new TestVdmComplex { SomeValue = "Bar" };

        entity.ComplexValue = complex1;
        Assert.Same(complex1, Assert.Single(entity.GetChangedFields()).Value);

        entity.ComplexValue = complex2;
        Assert.Same(complex2, Assert.Single(entity.GetChangedFields()).Value);
    }

    [Fact]
    public void NavigationPropertiesAreNotTrackedAsChanges()
    {
        var entity = new TestVdmEntity();

        entity.SetToParent(TestVdmEntity.Built(e => e.StringValue = "Foo"));
        entity.AddToChildren(TestVdmEntity.Built(e => e.StringValue = "Bar"));

        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void ChangedNonCustomFields_AreResetByResetChangedFields()
    {
        var entity = TestVdmEntity.Built(e => e.StringValue = "old");
        entity.StringValue = "old";
        Assert.Empty(entity.GetChangedFields());

        entity.StringValue = "new";
        Assert.Equal(["StringValue"], entity.GetChangedFields().Keys);

        entity.ResetChangedFields();
        Assert.Empty(entity.GetChangedFields());
    }

    [Fact]
    public void ChangedCustomFields()
    {
        var entity = TestVdmEntity.Built();
        entity.SetCustomField("foo", "bar");
        Assert.Equal(["foo"], entity.GetChangedFields().Keys);

        entity.ResetChangedFields();
        entity.SetCustomField("foo", "bar");
        Assert.Empty(entity.GetChangedFields());

        entity.SetCustomField("foo", "baz");
        Assert.Equal(["foo"], entity.GetChangedFields().Keys);
    }

    [Fact]
    public void GetCustomField_ForMissingField_Throws()
    {
        var entity = new TestVdmEntity();

        Assert.Throws<NoSuchEntityFieldException>(() => entity.GetCustomField<string>("missing"));
        Assert.False(entity.HasCustomField("missing"));
    }

    [Fact]
    public void Equality_IsByValue_AndIncludesTheVersionIdentifierButNotTheAttachment()
    {
        var foo1 = TestVdmEntity.Built(e => e.StringValue = "foo");
        var foo2 = TestVdmEntity.Built(e => e.StringValue = "foo");
        Assert.Equal(foo1, foo2);
        Assert.Equal(foo1.GetHashCode(), foo2.GetHashCode());

        foo1.VersionIdentifier = "1";
        foo2.VersionIdentifier = "1";
        Assert.Equal(foo1, foo2);

        foo1.AttachToService("bar", Destination);
        foo2.AttachToService("baz", (IHttpDestinationProperties?)null);
        Assert.Equal(foo1, foo2);

        foo2.VersionIdentifier = "2";
        Assert.NotEqual(foo1, foo2);
    }

    [Fact]
    public void Equality_ConsidersCustomFieldsAndNavigationProperties()
    {
        var left = TestVdmEntity.Built();
        var right = TestVdmEntity.Built();
        left.SetCustomField("x", 1);
        Assert.NotEqual(left, right);

        right.SetCustomField("x", 1);
        Assert.Equal(left, right);

        left.AddToChildren(TestVdmEntity.Built(e => e.StringValue = "child"));
        Assert.NotEqual(left, right);
        right.AddToChildren(TestVdmEntity.Built(e => e.StringValue = "child"));
        Assert.Equal(left, right);
    }

    [Fact]
    public void ToString_ListsTheProperties()
    {
        var text = TestVdmEntity.Built(e => e.StringValue = "foo").ToString();

        Assert.Contains("TestVdmEntity(", text, StringComparison.Ordinal);
        Assert.Contains("StringValue=foo", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachToService_WithDefaults_UsesDefaultServicePath()
    {
        var sut = new AttachableEntity();

        sut.AttachToService(null, (IHttpDestinationProperties?)null);

        Assert.Equal("/odata/default", sut.ServicePathForFetch);
        Assert.Null(sut.DestinationForFetch);
    }

    [Fact]
    public void AttachToService_WithDestination_RemembersIt()
    {
        var sut = new AttachableEntity();

        sut.AttachToService(null, Destination);

        Assert.Equal("/odata/default", sut.ServicePathForFetch);
        Assert.Same(Destination, sut.DestinationForFetch);
    }

    [Fact]
    public void AttachToService_WithCustomServicePath_UsesIt()
    {
        var sut = new AttachableEntity();

        sut.AttachToService("/sap/opu/odata", Destination);

        Assert.Equal("/sap/opu/odata", sut.ServicePathForFetch);
    }

    [Fact]
    public void AttachToService_AlsoAttachesLoadedNavigationProperties()
    {
        var parent = TestVdmEntity.Built();
        var child = TestVdmEntity.Built();
        var sut = TestVdmEntity.Built();
        sut.SetToParent(parent);
        sut.AddToChildren(child);

        sut.AttachToService("/service", Destination);

        Assert.Equal("/service", parent.ServicePathForFetch);
        Assert.Same(Destination, parent.DestinationForFetch);
        Assert.Same(Destination, child.DestinationForFetch);
    }

    [Fact]
    public async Task Fetch_OnEntityWithoutDestination_Throws()
    {
        var entity = TestVdmEntity.Built(e => e.IntegerValue = 1);

        var exception = await Assert.ThrowsAsync<Client.Exceptions.ODataRequestException>(() => entity.FetchToParentAsync());

        Assert.Contains("created locally without an assigned HttpDestination", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedCustomField_ConvertsOnSet_AndGet()
    {
        var entity = new TestVdmEntity();
        var field = new EntityField<TestVdmEntity, DateTime>("MyCustomField", FieldValueConverter.From(new DateTimeCalendarConverter()));

        entity.SetCustomField(field, new DateTime(2017, 10, 4, 0, 0, 0));

        var stored = Assert.IsType<DateTimeOffset>(entity.GetCustomField<object>("MyCustomField"));
        Assert.Equal(new DateTimeOffset(2017, 10, 4, 0, 0, 0, TimeSpan.Zero), stored);
        Assert.Equal(new DateTime(2017, 10, 4), entity.GetCustomField(field));
    }

    [Fact]
    public void TypedCustomField_WithNull_StaysNull()
    {
        var entity = new TestVdmEntity();
        var field = new EntityField<TestVdmEntity, DateTime?>("MyCustomField", null);

        entity.SetCustomField(field, null);

        Assert.Null(entity.GetCustomField<object>("MyCustomField"));
        Assert.Null(entity.GetCustomField(field));
    }

    [Fact]
    public void TypedCustomField_WithoutConverter_PassesValueThrough()
    {
        var entity = new TestVdmEntity();
        var field = new EntityField<TestVdmEntity, string>("MyCustomField");

        entity.SetCustomField(field, "value");

        Assert.Equal("value", entity.GetCustomField(field));
        Assert.Null(field.TypeConverter);
    }

    private sealed class FailingConverter : Sap.Cloud.Sdk.TypeConverter.AbstractTypeConverter<DateTime, bool>
    {
        public override Sap.Cloud.Sdk.TypeConverter.ConvertedObject<bool> ToDomainNonNull(DateTime obj) => throw new InvalidOperationException("fails");

        public override Sap.Cloud.Sdk.TypeConverter.ConvertedObject<DateTime> FromDomainNonNull(bool domainObject) => throw new InvalidOperationException("fails");
    }

    [Fact]
    public void TypedCustomField_ConversionFailure_ThrowsNotConvertible()
    {
        var entity = new TestVdmEntity();
        entity.SetCustomField("MyCustomField", true);
        var field = new EntityField<TestVdmEntity, DateTime>("MyCustomField", FieldValueConverter.From(new FailingConverter()));

        Assert.Throws<ObjectNotConvertibleException>(() => entity.GetCustomField(field));
        Assert.Throws<ObjectNotConvertibleException>(() => entity.SetCustomField(field, DateTime.UnixEpoch));
    }

    [Fact]
    public void SampleEntities_HaveMetadataConsistentWithTheirAttributes()
    {
        var entityTypes = typeof(Customer).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(Customer).Namespace && typeof(VdmObject).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();
        Assert.True(entityTypes.Count >= 9);

        foreach (var type in entityTypes)
        {
            var metadata = ((VdmObject)Activator.CreateInstance(type)!).GetMetadata();
            var annotated = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.GetCustomAttribute<ODataFieldAttribute>() is not null)
                .ToList();
            var expectedFields = metadata.FieldProperties.Where(p => p.Name != "versionIdentifier").Select(p => p.Name).ToList();
            Assert.Equal(expectedFields, annotated.Select(p => p.GetCustomAttribute<ODataFieldAttribute>()!.ODataName).ToList());
            Assert.Equal(
                metadata.KeyProperties.Select(p => p.Name).ToList(),
                annotated.Where(p => p.GetCustomAttribute<ODataKeyAttribute>() is not null).Select(p => p.GetCustomAttribute<ODataFieldAttribute>()!.ODataName).ToList());
        }
    }
}
