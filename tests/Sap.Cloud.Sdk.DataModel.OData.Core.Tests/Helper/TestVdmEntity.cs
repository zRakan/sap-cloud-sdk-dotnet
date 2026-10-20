using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>A hand-written entity covering many EDM types, a complex property and navigation properties (port of the Java <c>TestVdmEntity</c>).</summary>
public class TestVdmEntity : VdmEntity<TestVdmEntity>
{
    private static readonly VdmTypeMetadata Metadata = VdmTypeMetadata.For<TestVdmEntity>()
        .Property("IntegerValue", static e => e._integerValue, static (e, v) => e._integerValue = v, key: true)
        .Property("GuidValue", static e => e._guidValue, static (e, v) => e._guidValue = v)
        .Property("StringValue", static e => e._stringValue, static (e, v) => e._stringValue = v)
        .Property("OffsetDateTimeValue", static e => e._offsetDateTimeValue, static (e, v) => e._offsetDateTimeValue = v)
        .Navigation("to_Parent", static e => e._toParent, static (e, v) => e._toParent = v)
        .NavigationList("to_Children", static e => e._toChildren, static (e, v) => e._toChildren = v)
        .Property("DecimalValue", static e => e._decimalValue, static (e, v) => e._decimalValue = v)
        .Property("DoubleValue", static e => e._doubleValue, static (e, v) => e._doubleValue = v)
        .Property("LocalTimeValue", static e => e._localTimeValue, static (e, v) => e._localTimeValue = v)
        .Property("LocalDateTimeValue", static e => e._localDateTimeValue, static (e, v) => e._localDateTimeValue = v)
        .Property("BooleanValue", static e => e._booleanValue, static (e, v) => e._booleanValue = v)
        .Property("ComplexValue", static e => e._complexValue, static (e, v) => e._complexValue = v)
        .Build();

    private int? _integerValue;
    private Guid? _guidValue;
    private string? _stringValue;
    private DateTimeOffset? _offsetDateTimeValue;
    private TestVdmEntity? _toParent;
    private List<TestVdmEntity>? _toChildren;
    private decimal? _decimalValue;
    private double? _doubleValue;
    private TimeOnly? _localTimeValue;
    private DateTime? _localDateTimeValue;
    private bool? _booleanValue;
    private TestVdmComplex? _complexValue;

    /// <summary>Creates an entity and applies the initializer without recording changes (like the Java builder).</summary>
    public static TestVdmEntity Built(Action<TestVdmEntity>? initializer = null)
    {
        var entity = new TestVdmEntity();
        initializer?.Invoke(entity);
        entity.ResetChangedFields();
        return entity;
    }

    public int? IntegerValue
    {
        get => _integerValue;
        set
        {
            RememberChangedField("IntegerValue", _integerValue);
            _integerValue = value;
        }
    }

    public Guid? GuidValue
    {
        get => _guidValue;
        set
        {
            RememberChangedField("GuidValue", _guidValue);
            _guidValue = value;
        }
    }

    public string? StringValue
    {
        get => _stringValue;
        set
        {
            RememberChangedField("StringValue", _stringValue);
            _stringValue = value;
        }
    }

    public DateTimeOffset? OffsetDateTimeValue
    {
        get => _offsetDateTimeValue;
        set
        {
            RememberChangedField("OffsetDateTimeValue", _offsetDateTimeValue);
            _offsetDateTimeValue = value;
        }
    }

    public decimal? DecimalValue
    {
        get => _decimalValue;
        set
        {
            RememberChangedField("DecimalValue", _decimalValue);
            _decimalValue = value;
        }
    }

    public double? DoubleValue
    {
        get => _doubleValue;
        set
        {
            RememberChangedField("DoubleValue", _doubleValue);
            _doubleValue = value;
        }
    }

    public TimeOnly? LocalTimeValue
    {
        get => _localTimeValue;
        set
        {
            RememberChangedField("LocalTimeValue", _localTimeValue);
            _localTimeValue = value;
        }
    }

    public DateTime? LocalDateTimeValue
    {
        get => _localDateTimeValue;
        set
        {
            RememberChangedField("LocalDateTimeValue", _localDateTimeValue);
            _localDateTimeValue = value;
        }
    }

    public bool? BooleanValue
    {
        get => _booleanValue;
        set
        {
            RememberChangedField("BooleanValue", _booleanValue);
            _booleanValue = value;
        }
    }

    public TestVdmComplex? ComplexValue
    {
        get => _complexValue;
        set
        {
            RememberChangedField("ComplexValue", _complexValue);
            _complexValue = value;
        }
    }

    public TestVdmEntity? GetToParentIfPresent() => _toParent;

    public void SetToParent(TestVdmEntity? value) => _toParent = value;

    public IReadOnlyList<TestVdmEntity>? GetToChildrenIfPresent() => _toChildren;

    public void AddToChildren(params TestVdmEntity[] entity)
    {
        _toChildren ??= [];
        _toChildren.AddRange(entity);
    }

    public Task<TestVdmEntity?> FetchToParentAsync(CancellationToken cancellationToken = default) =>
        FetchFieldAsSingleAsync<TestVdmEntity>("to_Parent", cancellationToken)!;

    public Task<IReadOnlyList<TestVdmEntity>> FetchToChildrenAsync(CancellationToken cancellationToken = default) =>
        FetchFieldAsListAsync<TestVdmEntity>("to_Children", cancellationToken);

    /// <inheritdoc />
    protected override VdmTypeMetadata TypeMetadata => Metadata;

    /// <inheritdoc />
    protected override string? EntityCollection => "Entities";

    /// <inheritdoc />
    protected override string? DefaultServicePath => "/service";

    /// <summary>Exposes the protected state for tests.</summary>
    public string? CollectionName => EntityCollection;
}

/// <summary>A hand-written complex type with a nested complex property (port of the Java <c>TestVdmComplex</c>).</summary>
public class TestVdmComplex : VdmComplex<TestVdmComplex>
{
    private static readonly VdmTypeMetadata Metadata = VdmTypeMetadata.For<TestVdmComplex>()
        .Property("SomeValue", static e => e._someValue, static (e, v) => e._someValue = v)
        .Property("OtherValue", static e => e._otherValue, static (e, v) => e._otherValue = v)
        .Property("ComplexValue", static e => e._complexValue, static (e, v) => e._complexValue = v)
        .Build();

    private string? _someValue;
    private string? _otherValue;
    private TestVdmComplex? _complexValue;

    public static TestVdmComplex Built(Action<TestVdmComplex> initializer)
    {
        var complex = new TestVdmComplex();
        initializer(complex);
        complex.ResetChangedFields();
        return complex;
    }

    public string? SomeValue
    {
        get => _someValue;
        set
        {
            RememberChangedField("SomeValue", _someValue);
            _someValue = value;
        }
    }

    public string? OtherValue
    {
        get => _otherValue;
        set
        {
            RememberChangedField("OtherValue", _otherValue);
            _otherValue = value;
        }
    }

    public TestVdmComplex? ComplexValue
    {
        get => _complexValue;
        set
        {
            RememberChangedField("ComplexValue", _complexValue);
            _complexValue = value;
        }
    }

    /// <inheritdoc />
    protected override VdmTypeMetadata TypeMetadata => Metadata;
}

/// <summary>The entity of the Java <c>ODataSerializationTest</c>: more numeric types, no collection.</summary>
public class SerializationTestEntity : VdmEntity<SerializationTestEntity>
{
    private static readonly VdmTypeMetadata Metadata = VdmTypeMetadata.For<SerializationTestEntity>()
        .Property("IntegerValue", static e => e._integerValue, static (e, v) => e._integerValue = v, key: true)
        .Property("GuidValue", static e => e._guidValue, static (e, v) => e._guidValue = v)
        .Property("StringValue", static e => e._stringValue, static (e, v) => e._stringValue = v)
        .Property("OffsetDateTimeValue", static e => e._offsetDateTimeValue, static (e, v) => e._offsetDateTimeValue = v)
        .Navigation("to_Parent", static e => e._toParent, static (e, v) => e._toParent = v)
        .NavigationList("to_Children", static e => e._toChildren, static (e, v) => e._toChildren = v)
        .Property("DecimalValue", static e => e._decimalValue, static (e, v) => e._decimalValue = v)
        .Property("DoubleValue", static e => e._doubleValue, static (e, v) => e._doubleValue = v)
        .Property("LocalTimeValue", static e => e._localTimeValue, static (e, v) => e._localTimeValue = v)
        .Property("LocalDateTimeValue", static e => e._localDateTimeValue, static (e, v) => e._localDateTimeValue = v)
        .Property("BooleanValue", static e => e._booleanValue, static (e, v) => e._booleanValue = v)
        .Property("SByteValue", static e => e._sByteValue, static (e, v) => e._sByteValue = v)
        .Property("Int16Value", static e => e._int16Value, static (e, v) => e._int16Value = v)
        .Property("Int64Value", static e => e._int64Value, static (e, v) => e._int64Value = v)
        .Property("SingleValue", static e => e._singleValue, static (e, v) => e._singleValue = v)
        .Property("BinaryValue", static e => e._binaryValue, static (e, v) => e._binaryValue = v)
        .Build();

    private int? _integerValue;
    private Guid? _guidValue;
    private string? _stringValue;
    private DateTimeOffset? _offsetDateTimeValue;
    private SerializationTestEntity? _toParent;
    private List<SerializationTestEntity>? _toChildren;
    private decimal? _decimalValue;
    private double? _doubleValue;
    private TimeOnly? _localTimeValue;
    private DateTime? _localDateTimeValue;
    private bool? _booleanValue;
    private sbyte? _sByteValue;
    private short? _int16Value;
    private long? _int64Value;
    private float? _singleValue;
    private byte[]? _binaryValue;

    public int? IntegerValue { get => _integerValue; set { RememberChangedField("IntegerValue", _integerValue); _integerValue = value; } }

    public Guid? GuidValue { get => _guidValue; set { RememberChangedField("GuidValue", _guidValue); _guidValue = value; } }

    public string? StringValue { get => _stringValue; set { RememberChangedField("StringValue", _stringValue); _stringValue = value; } }

    public DateTimeOffset? OffsetDateTimeValue { get => _offsetDateTimeValue; set { RememberChangedField("OffsetDateTimeValue", _offsetDateTimeValue); _offsetDateTimeValue = value; } }

    public decimal? DecimalValue { get => _decimalValue; set { RememberChangedField("DecimalValue", _decimalValue); _decimalValue = value; } }

    public double? DoubleValue { get => _doubleValue; set { RememberChangedField("DoubleValue", _doubleValue); _doubleValue = value; } }

    public TimeOnly? LocalTimeValue { get => _localTimeValue; set { RememberChangedField("LocalTimeValue", _localTimeValue); _localTimeValue = value; } }

    public DateTime? LocalDateTimeValue { get => _localDateTimeValue; set { RememberChangedField("LocalDateTimeValue", _localDateTimeValue); _localDateTimeValue = value; } }

    public bool? BooleanValue { get => _booleanValue; set { RememberChangedField("BooleanValue", _booleanValue); _booleanValue = value; } }

    public sbyte? SByteValue { get => _sByteValue; set { RememberChangedField("SByteValue", _sByteValue); _sByteValue = value; } }

    public short? Int16Value { get => _int16Value; set { RememberChangedField("Int16Value", _int16Value); _int16Value = value; } }

    public long? Int64Value { get => _int64Value; set { RememberChangedField("Int64Value", _int64Value); _int64Value = value; } }

    public float? SingleValue { get => _singleValue; set { RememberChangedField("SingleValue", _singleValue); _singleValue = value; } }

    public byte[]? BinaryValue { get => _binaryValue; set { RememberChangedField("BinaryValue", _binaryValue); _binaryValue = value; } }

    public SerializationTestEntity? GetToParentIfPresent() => _toParent;

    public IReadOnlyList<SerializationTestEntity>? GetToChildrenIfPresent() => _toChildren;

    /// <inheritdoc />
    protected override VdmTypeMetadata TypeMetadata => Metadata;

    /// <inheritdoc />
    protected override string? EntityCollection => null;

    /// <inheritdoc />
    protected override string? DefaultServicePath => "/";
}
