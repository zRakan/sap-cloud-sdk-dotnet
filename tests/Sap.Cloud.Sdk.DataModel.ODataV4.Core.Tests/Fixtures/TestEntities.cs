#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;

/// <summary>The simplest entity: one string key <c>id</c> (<c>TestEntity</c> of the Java tests).</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<TestEntity>))]
public class TestEntity : VdmEntity<TestEntity>
{
    private static readonly VdmTypeInfo<TestEntity> s_typeInfo = new VdmTypeInfoBuilder<TestEntity>("TestEntity")
        .Field("id", e => e.Id, (e, v) => e.Id = v)
        .Build();

    public static class Fields
    {
        public static readonly SimpleProperty.String<TestEntity> Id = SimpleProperty.String<TestEntity>.Of("id");
    }

    private string? _id;

    public string? Id
    {
        get => _id;
        set
        {
            RememberChangedField("id", _id);
            _id = value;
        }
    }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "EntityCollection";

    protected override string? DefaultServicePath => "/odata/default";

    protected override ODataEntityKey GetKey()
    {
        var key = base.GetKey();
        key.AddKeyProperty("id", Id);
        return key;
    }

    /// <summary>Creates an entity without recording changes (Lombok's <c>@Builder</c> in the Java tests).</summary>
    public static TestEntity WithId(string id) => new() { _id = id };
}

/// <summary>Service of <see cref="TestEntity"/> with functions and actions (<c>TestEntityService</c> of the Java tests).</summary>
public static class TestEntityService
{
    public const string ServicePath = "/";

    public static GetAllRequestBuilder<TestEntity> GetTestEntities() => new(ServicePath, "EntityCollection");

    public static CountRequestBuilder<TestEntity> CountTestEntities() => new(ServicePath, "EntityCollection");

    public static GetByKeyRequestBuilder<TestEntity> GetTestEntitiesByKey(string key) =>
        new(ServicePath, new Dictionary<string, object?> { ["key"] = key }, "EntityCollection");

    public static CreateRequestBuilder<TestEntity> CreateTestEntity(TestEntity testEntity) => new(ServicePath, testEntity, "EntityCollection");

    public static DeleteRequestBuilder<TestEntity> DeleteTestEntity(TestEntity testEntity) => new(ServicePath, testEntity, "EntityCollection");

    public static UpdateRequestBuilder<TestEntity> UpdateTestEntity(TestEntity testEntity) => new(ServicePath, testEntity, "EntityCollection");

    public static SingleValueActionRequestBuilder<int> ActionSingleResult() =>
        new(ServicePath, "action-single", new Dictionary<string, object?> { ["secret"] = "pass" });

    public static CollectionValueActionRequestBuilder<string> ActionMultipleResult() =>
        new(ServicePath, "action-multiple", new Dictionary<string, object?> { ["secret"] = "pass" });

    public static SingleValueFunctionRequestBuilder<int> FunctionSingleResult() =>
        new(ServicePath, "function-single", new Dictionary<string, object?> { ["secret"] = "pass" });

    public static CollectionValueFunctionRequestBuilder<string> FunctionMultipleResult() =>
        new(ServicePath, "function-multiple", new Dictionary<string, object?> { ["secret"] = "pass" });

    /// <summary>A batch request with predictable boundaries: <c>00000000-0000-0000-0000-000000000001</c>, ...</summary>
    public static BatchRequestBuilder Batch()
    {
        var counter = 0;
        return new BatchRequestBuilder(ServicePath, () => new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)++counter));
    }
}

/// <summary>Complex type of the PATCH/PUT tests.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<PatchComplex>))]
public class PatchComplex : VdmComplex<PatchComplex>
{
    private static readonly VdmTypeInfo<PatchComplex> s_typeInfo = new VdmTypeInfoBuilder<PatchComplex>("TestComplex")
        .Field("StringProperty", e => e.StringProperty, (e, v) => e.StringProperty = v)
        .Field("ComplexProperty", e => e.ComplexProperty, (e, v) => e.ComplexProperty = v)
        .Build();

    private string? _stringProperty;
    private PatchComplex? _complexProperty;

    public string? StringProperty
    {
        get => _stringProperty;
        set
        {
            RememberChangedField("StringProperty", _stringProperty);
            _stringProperty = value;
        }
    }

    public PatchComplex? ComplexProperty
    {
        get => _complexProperty;
        set
        {
            RememberChangedField("ComplexProperty", _complexProperty);
            _complexProperty = value;
        }
    }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    public static PatchComplex Create(string? stringProperty = null, PatchComplex? complexProperty = null) =>
        new() { _stringProperty = stringProperty, _complexProperty = complexProperty };
}

/// <summary>Entity of the PATCH/PUT tests: every kind of value, a self reference and complex values.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<PatchEntity>))]
public class PatchEntity : VdmEntity<PatchEntity>
{
    private static readonly VdmTypeInfo<PatchEntity> s_typeInfo = new VdmTypeInfoBuilder<PatchEntity>("TestEntity")
        .Field("Neighbor", e => e.Neighbor, (e, v) => e.Neighbor = v)
        .Field("Value", e => e.Value, (e, v) => e.Value = v)
        .Field("Emails", e => e.Emails, (e, v) => e.Emails = v)
        .Field("Name", e => e.Name, (e, v) => e.Name = v)
        .Field("EffortValue", e => e.EffortValue, (e, v) => e.EffortValue = v)
        .Field("ShoeSize", e => e.ShoeSize, (e, v) => e.ShoeSize = v)
        .Field("IsRetired", e => e.IsRetired, (e, v) => e.IsRetired = v)
        .Field("BirthDate", e => e.BirthDate, (e, v) => e.BirthDate = v)
        .Field("FavouriteCharacter", e => e.FavouriteCharacter, (e, v) => e.FavouriteCharacter = v)
        .Field("ComplexProperty", e => e.ComplexProperty, (e, v) => e.ComplexProperty = v)
        .Field("ComplexProperties", e => e.ComplexProperties, (e, v) => e.ComplexProperties = v)
        .Build();

    public static class Fields
    {
        public static readonly SimpleProperty.NumericDecimal<PatchEntity> Value = SimpleProperty.NumericDecimal<PatchEntity>.Of("Value");
        public static readonly SimpleProperty.Collection<PatchEntity, string> Emails = SimpleProperty.Collection<PatchEntity, string>.Of("Emails");
        public static readonly SimpleProperty.String<PatchEntity> Name = SimpleProperty.String<PatchEntity>.Of("Name");
        public static readonly SimpleProperty.Boolean<PatchEntity> IsRetired = SimpleProperty.Boolean<PatchEntity>.Of("IsRetired");
    }

    private PatchEntity? _neighbor;
    private decimal? _value;
    private List<string>? _emails;
    private string? _name;
    private double? _effortValue;
    private int? _shoeSize;
    private bool? _isRetired;
    private DateOnly? _birthDate;
    private char? _favouriteCharacter;
    private PatchComplex? _complexProperty;
    private List<PatchComplex>? _complexProperties;

    public PatchEntity? Neighbor
    {
        get => _neighbor;
        set
        {
            RememberChangedField("Neighbor", _neighbor);
            _neighbor = value;
        }
    }

    public decimal? Value
    {
        get => _value;
        set
        {
            RememberChangedField("Value", _value);
            _value = value;
        }
    }

    public List<string>? Emails
    {
        get => _emails;
        set
        {
            RememberChangedField("Emails", _emails);
            _emails = value;
        }
    }

    public string? Name
    {
        get => _name;
        set
        {
            RememberChangedField("Name", _name);
            _name = value;
        }
    }

    public double? EffortValue
    {
        get => _effortValue;
        set
        {
            RememberChangedField("EffortValue", _effortValue);
            _effortValue = value;
        }
    }

    public int? ShoeSize
    {
        get => _shoeSize;
        set
        {
            RememberChangedField("ShoeSize", _shoeSize);
            _shoeSize = value;
        }
    }

    public bool? IsRetired
    {
        get => _isRetired;
        set
        {
            RememberChangedField("IsRetired", _isRetired);
            _isRetired = value;
        }
    }

    public DateOnly? BirthDate
    {
        get => _birthDate;
        set
        {
            RememberChangedField("BirthDate", _birthDate);
            _birthDate = value;
        }
    }

    public char? FavouriteCharacter
    {
        get => _favouriteCharacter;
        set
        {
            RememberChangedField("FavouriteCharacter", _favouriteCharacter);
            _favouriteCharacter = value;
        }
    }

    public PatchComplex? ComplexProperty
    {
        get => _complexProperty;
        set
        {
            RememberChangedField("ComplexProperty", _complexProperty);
            _complexProperty = value;
        }
    }

    public List<PatchComplex>? ComplexProperties
    {
        get => _complexProperties;
        set
        {
            RememberChangedField("ComplexProperties", _complexProperties);
            _complexProperties = value;
        }
    }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "EntityParentCollection";

    protected override ODataEntityKey GetKey()
    {
        var key = new ODataEntityKey(OData.Client.ODataProtocol.V4);
        key.AddKeyProperty(nameof(Name), Name);
        return key;
    }

    /// <summary>Creates an entity without recording changes.</summary>
    public static PatchEntity Create(
        string? name = null,
        decimal? value = null,
        PatchComplex? complexProperty = null,
        List<PatchComplex>? complexProperties = null,
        List<string>? emails = null,
        bool? isRetired = null) =>
        new()
        {
            _name = name,
            _value = value,
            _complexProperty = complexProperty,
            _complexProperties = complexProperties,
            _emails = emails,
            _isRetired = isRetired,
        };
}

/// <summary>Entity with one field of each EDM primitive type, as in the OData JSON format reference payload.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<ReferenceObject>))]
public class ReferenceObject : VdmEntity<ReferenceObject>
{
    private static readonly VdmTypeInfo<ReferenceObject> s_typeInfo = new VdmTypeInfoBuilder<ReferenceObject>("TestEntity")
        .Field("ByteValue", e => e.ByteValue, (e, v) => e.ByteValue = v)
        .Field("SByteValue", e => e.SByteValue, (e, v) => e.SByteValue = v)
        .Field("Int16Value", e => e.Int16Value, (e, v) => e.Int16Value = v)
        .Field("Int32Value", e => e.Int32Value, (e, v) => e.Int32Value = v)
        .Field("Int64Value", e => e.Int64Value, (e, v) => e.Int64Value = v)
        .Field("SingleValue", e => e.SingleValue, (e, v) => e.SingleValue = v)
        .Field("DoubleValue", e => e.DoubleValue, (e, v) => e.DoubleValue = v)
        .Field("DecimalValue", e => e.DecimalValue, (e, v) => e.DecimalValue = v)
        .Field("BooleanValue", e => e.BooleanValue, (e, v) => e.BooleanValue = v)
        .Field("StringValue", e => e.StringValue, (e, v) => e.StringValue = v)
        .Field("BinaryValue", e => e.BinaryValue, (e, v) => e.BinaryValue = v)
        .Field("GuidValue", e => e.GuidValue, (e, v) => e.GuidValue = v)
        .Field("TimeOfDayValue", e => e.TimeOfDayValue, (e, v) => e.TimeOfDayValue = v)
        .Field("DateValue", e => e.DateValue, (e, v) => e.DateValue = v)
        .Field("DateTimeOffsetValue", e => e.DateTimeOffsetValue, (e, v) => e.DateTimeOffsetValue = v)
        .Build();

    public byte ByteValue { get; set; }

    public sbyte SByteValue { get; set; }

    public short Int16Value { get; set; }

    public int Int32Value { get; set; }

    public long Int64Value { get; set; }

    public float SingleValue { get; set; }

    public double DoubleValue { get; set; }

    public decimal DecimalValue { get; set; }

    public bool BooleanValue { get; set; }

    public string? StringValue { get; set; }

    public byte[]? BinaryValue { get; set; }

    public Guid GuidValue { get; set; }

    public TimeOnly TimeOfDayValue { get; set; }

    public DateOnly DateValue { get; set; }

    public DateTimeOffset DateTimeOffsetValue { get; set; }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "EntityCollection";
}

/// <summary>Entity with a name; used by the function and action tests.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<NamedEntity>))]
public class NamedEntity : VdmEntity<NamedEntity>
{
    private static readonly VdmTypeInfo<NamedEntity> s_typeInfo = new VdmTypeInfoBuilder<NamedEntity>("com.sap.cloud.sdk.TestEntity")
        .Field("Name", e => e.Name, (e, v) => e.Name = v)
        .Build();

    private string? _name;

    public string? Name
    {
        get => _name;
        set
        {
            RememberChangedField("Name", _name);
            _name = value;
        }
    }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "EntityCollection";

    protected override string? DefaultServicePath => "/odata/default";
}

/// <summary>Complex type with a city and a country; used by the function and action tests.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<CityCountry>))]
public class CityCountry : VdmComplex<CityCountry>
{
    private static readonly VdmTypeInfo<CityCountry> s_typeInfo = new VdmTypeInfoBuilder<CityCountry>("com.sap.cloud.sdk.ComplexType")
        .Field("City", e => e.City, (e, v) => e.City = v)
        .Field("Country", e => e.Country, (e, v) => e.Country = v)
        .Build();

    private string? _city;
    private string? _country;

    public string? City
    {
        get => _city;
        set
        {
            RememberChangedField("City", _city);
            _city = value;
        }
    }

    public string? Country
    {
        get => _country;
        set
        {
            RememberChangedField("Country", _country);
            _country = value;
        }
    }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;
}

/// <summary>Entity without any property: everything the service returns becomes a custom field.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<BareEntity>))]
public class BareEntity : VdmEntity<BareEntity>
{
    private static readonly VdmTypeInfo<BareEntity> s_typeInfo = new VdmTypeInfoBuilder<BareEntity>("Example.TestEntity").Build();

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "/EntityCollection";
}

/// <summary>Entity of the response tests with a property named differently from its OData name.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<FooObject>))]
public class FooObject : VdmEntity<FooObject>
{
    private static readonly VdmTypeInfo<FooObject> s_typeInfo = new VdmTypeInfoBuilder<FooObject>("TestObject")
        .Field("foo", e => e.Name, (e, v) => e.Name = v)
        .Build();

    private string? _name;

    public string? Name
    {
        get => _name;
        set
        {
            RememberChangedField("foo", _name);
            _name = value;
        }
    }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "TestObject";
}

/// <summary>An entity that is not exposed as an entity set.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<EntityWithoutEntitySet>))]
public class EntityWithoutEntitySet : VdmEntity<EntityWithoutEntitySet>
{
    private static readonly VdmTypeInfo<EntityWithoutEntitySet> s_typeInfo =
        new VdmTypeInfoBuilder<EntityWithoutEntitySet>("outside-of-entity-set").Build();

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "outside-of-entity-set";
}

/// <summary>An entity that is exposed as an entity set and navigates to an entity without entity set.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<EntityWithEntitySet>))]
public class EntityWithEntitySet : VdmEntity<EntityWithEntitySet>, IVdmEntitySet
{
    private static readonly VdmTypeInfo<EntityWithEntitySet> s_typeInfo =
        new VdmTypeInfoBuilder<EntityWithEntitySet>("outside-of-entity-set").Build();

    public static readonly NavigationProperty.Collection<EntityWithEntitySet, EntityWithoutEntitySet> NavigationProperty =
        Core.NavigationProperty.Collection<EntityWithEntitySet, EntityWithoutEntitySet>.Of("Navigations");

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "outside-of-entity-set";
}

/// <summary>Entity with properties that use their own JSON converters (Java: <c>@JsonAdapter</c> on a field).</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<UserEntity>))]
public class UserEntity : VdmEntity<UserEntity>
{
    private static readonly VdmTypeInfo<UserEntity> s_typeInfo = new VdmTypeInfoBuilder<UserEntity>("Example.User")
        .Field("DateOfBirth", e => e.DateOfBirth, (e, v) => e.DateOfBirth = v)
        .Field("LocaleTime", e => e.LocaleTime, (e, v) => e.LocaleTime = v, new OffsetOfTheDayConverter())
        .Field("RegistrationDate", e => e.RegistrationDate, (e, v) => e.RegistrationDate = v, new UtcLocalDateTimeConverter())
        .Build();

    public DateTimeOffset? DateOfBirth { get; set; }

    public DateTimeOffset? LocaleTime { get; set; }

    public DateTime? RegistrationDate { get; set; }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "People";

    /// <summary>Writes with the ISO offset format; reads the same format.</summary>
    private sealed class OffsetOfTheDayConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value!.Value.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Writes a local date-time as UTC offset date-time.</summary>
    private sealed class UtcLocalDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture).DateTime;

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(new DateTimeOffset(DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc)).ToString("yyyy-MM-ddTHH:mm:ssK", System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// <summary>Entity with a decimal that must not be written in exponent notation.</summary>
[JsonConverter(typeof(VdmObjectJsonConverter<DecimalEntity>))]
public class DecimalEntity : VdmEntity<DecimalEntity>
{
    private static readonly VdmTypeInfo<DecimalEntity> s_typeInfo = new VdmTypeInfoBuilder<DecimalEntity>("TestEntity")
        .Field("BigDecimalField", e => e.BigDecimalField, (e, v) => e.BigDecimalField = v)
        .Build();

    public decimal? BigDecimalField { get; set; }

    protected override VdmTypeInfo TypeInfo => s_typeInfo;

    protected override string EntityCollection => "EntityCollection";
}
