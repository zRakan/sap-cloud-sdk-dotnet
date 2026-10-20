using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>
/// Static description of an entity or complex type: its OData properties (simple, complex and navigation) in declaration
/// order, with accessors and key information.
/// </summary>
/// <remarks>
/// <para>
/// Replaces what the Java SDK derives by reflection from <c>@SerializedName</c>, <c>@ODataField</c>, <c>@Key</c> and
/// <c>@JsonAdapter</c> on private fields, plus the generated <c>toMapOfFields</c>, <c>toMapOfNavigationProperties</c>,
/// <c>getKey</c> and <c>fromMap</c> methods. Generated types build one table in a static field and return it from
/// <c>VdmObject.TypeMetadata</c>; serialization, deserialization, change tracking, key and equality are all driven by it, without
/// reflection at run time.
/// </para>
/// <para>The accessors passed to the builder read and write the <em>backing field</em>, so that deserialization does not mark fields as changed.</para>
/// </remarks>
public sealed class VdmTypeMetadata
{
    private readonly Dictionary<string, VdmProperty> _byName;

    internal VdmTypeMetadata(Type type, IReadOnlyList<VdmProperty> properties, Func<VdmObject> factory)
    {
        Type = type;
        Properties = properties;
        Factory = factory;
        _byName = properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
        KeyProperties = properties.Where(p => p.IsKey).ToList();
        FieldProperties = properties.Where(p => !p.IsNavigation).ToList();
        NavigationProperties = properties.Where(p => p.IsNavigation).ToList();
    }

    /// <summary>The described type.</summary>
    public Type Type { get; }

    internal IReadOnlyList<VdmProperty> Properties { get; }

    internal IReadOnlyList<VdmProperty> KeyProperties { get; }

    internal IReadOnlyList<VdmProperty> FieldProperties { get; }

    internal IReadOnlyList<VdmProperty> NavigationProperties { get; }

    internal Func<VdmObject> Factory { get; }

    internal bool TryGetProperty(string name, out VdmProperty property) => _byName.TryGetValue(name, out property!);

    /// <summary>Starts a table for the given type. The properties of the base type <see cref="VdmEntity"/> are included.</summary>
    /// <typeparam name="T">The entity or complex type.</typeparam>
    /// <returns>The builder.</returns>
    public static VdmTypeMetadataBuilder<T> For<T>()
        where T : VdmObject, new() =>
        new(typeof(T).IsAssignableTo(typeof(VdmEntity)) ? VdmEntity.BaseMetadata : null);

    /// <summary>Starts a table for a type derived from a type that has a table already.</summary>
    /// <typeparam name="T">The derived type.</typeparam>
    /// <param name="parent">The table of the base type; its properties come first.</param>
    /// <returns>The builder.</returns>
    public static VdmTypeMetadataBuilder<T> Extending<T>(VdmTypeMetadata parent)
        where T : VdmObject, new()
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new(parent);
    }
}

/// <summary>Fluent builder of <see cref="VdmTypeMetadata"/>.</summary>
/// <typeparam name="T">The entity or complex type.</typeparam>
public sealed class VdmTypeMetadataBuilder<T>
    where T : VdmObject, new()
{
    private readonly List<VdmProperty> _properties = [];

    internal VdmTypeMetadataBuilder(VdmTypeMetadata? parent)
    {
        if (parent is not null)
        {
            _properties.AddRange(parent.Properties);
        }
    }

    /// <summary>Declares a simple or complex property.</summary>
    /// <typeparam name="TValue">The property type, for example <c>int?</c>, <c>string?</c> or a complex type.</typeparam>
    /// <param name="name">The name in the OData service.</param>
    /// <param name="getter">Reads the backing field.</param>
    /// <param name="setter">Writes the backing field (without change tracking).</param>
    /// <param name="key">Whether the property is part of the entity key.</param>
    /// <param name="converter">The JSON converter; by default the converter for the property type is used.</param>
    /// <returns>This builder.</returns>
    public VdmTypeMetadataBuilder<T> Property<TValue>(
        string name,
        Func<T, TValue?> getter,
        Action<T, TValue?> setter,
        bool key = false,
        JsonConverter<TValue>? converter = null) =>
        Add(new VdmProperty<T, TValue>(name, getter, setter, key, isNavigation: false, converter ?? ODataValueConverters.Find<TValue>()));

    /// <summary>Declares a navigation property to a single entity.</summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <param name="name">The name in the OData service.</param>
    /// <param name="getter">Reads the backing field.</param>
    /// <param name="setter">Writes the backing field.</param>
    /// <returns>This builder.</returns>
    public VdmTypeMetadataBuilder<T> Navigation<TEntity>(string name, Func<T, TEntity?> getter, Action<T, TEntity?> setter)
        where TEntity : VdmObject =>
        Add(new VdmProperty<T, TEntity>(name, getter, setter, isKey: false, isNavigation: true, converter: null));

    /// <summary>Declares a navigation property to many entities.</summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <param name="name">The name in the OData service.</param>
    /// <param name="getter">Reads the backing field.</param>
    /// <param name="setter">Writes the backing field.</param>
    /// <returns>This builder.</returns>
    public VdmTypeMetadataBuilder<T> NavigationList<TEntity>(string name, Func<T, List<TEntity>?> getter, Action<T, List<TEntity>?> setter)
        where TEntity : VdmObject =>
        Add(new VdmListProperty<T, TEntity>(name, getter, setter));

    /// <summary>Creates the table.</summary>
    /// <returns>The table.</returns>
    public VdmTypeMetadata Build() => new(typeof(T), _properties.ToList(), static () => new T());

    private VdmTypeMetadataBuilder<T> Add(VdmProperty property)
    {
        var existing = _properties.FindIndex(p => p.Name == property.Name);
        if (existing >= 0)
        {
            // Like in Java, a property of a subclass overwrites a property of the same name from the superclass.
            _properties[existing] = property;
        }
        else
        {
            _properties.Add(property);
        }
        return this;
    }
}

/// <summary>A property of an entity or complex type.</summary>
internal abstract class VdmProperty
{
    protected VdmProperty(string name, bool isKey, bool isNavigation)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        IsKey = isKey;
        IsNavigation = isNavigation;
    }

    internal string Name { get; }

    internal bool IsKey { get; }

    internal bool IsNavigation { get; }

    internal abstract object? GetValue(VdmObject owner);

    internal abstract void SetValue(VdmObject owner, object? value);

    /// <summary>Writes the property; null values only if <paramref name="writeNulls"/> is set.</summary>
    internal abstract void Write(Utf8JsonWriter writer, VdmObject owner, JsonSerializerOptions options, bool writeNulls);

    /// <summary>Reads the property value at the reader's current token and stores it.</summary>
    internal abstract void Read(ref Utf8JsonReader reader, VdmObject owner, JsonSerializerOptions options);
}

internal sealed class VdmProperty<TOwner, TValue> : VdmProperty
    where TOwner : VdmObject
{
    private static readonly bool IsVdmObject = typeof(VdmObject).IsAssignableFrom(typeof(TValue));

    private readonly Func<TOwner, TValue?> _getter;
    private readonly Action<TOwner, TValue?> _setter;
    private readonly JsonConverter<TValue>? _converter;

    internal VdmProperty(
        string name,
        Func<TOwner, TValue?> getter,
        Action<TOwner, TValue?> setter,
        bool isKey,
        bool isNavigation,
        JsonConverter<TValue>? converter)
        : base(name, isKey, isNavigation)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _getter = getter;
        _setter = setter;
        _converter = converter;
    }

    internal override object? GetValue(VdmObject owner) => _getter((TOwner)owner);

    internal override void SetValue(VdmObject owner, object? value) => _setter((TOwner)owner, (TValue?)value);

    internal override void Write(Utf8JsonWriter writer, VdmObject owner, JsonSerializerOptions options, bool writeNulls)
    {
        var value = _getter((TOwner)owner);
        if (value is null)
        {
            if (writeNulls)
            {
                writer.WriteNull(Name);
            }
            return;
        }
        writer.WritePropertyName(Name);
        if (_converter is not null)
        {
            _converter.Write(writer, value, options);
        }
        else if (IsVdmObject)
        {
            // Serialize by runtime type so that subclasses of the declared type keep their properties.
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }

    internal override void Read(ref Utf8JsonReader reader, VdmObject owner, JsonSerializerOptions options)
    {
        TValue? value;
        if (reader.TokenType == JsonTokenType.Null)
        {
            value = default;
        }
        else if (_converter is not null)
        {
            value = _converter.Read(ref reader, typeof(TValue), options);
        }
        else
        {
            value = JsonSerializer.Deserialize<TValue>(ref reader, options);
        }
        _setter((TOwner)owner, value);
    }
}

/// <summary>A navigation property to many entities; reads <c>{"results": [...]}</c>, plain arrays and <c>__deferred</c>.</summary>
internal sealed class VdmListProperty<TOwner, TEntity> : VdmProperty
    where TOwner : VdmObject
    where TEntity : VdmObject
{
    private readonly Func<TOwner, List<TEntity>?> _getter;
    private readonly Action<TOwner, List<TEntity>?> _setter;

    internal VdmListProperty(string name, Func<TOwner, List<TEntity>?> getter, Action<TOwner, List<TEntity>?> setter)
        : base(name, isKey: false, isNavigation: true)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _getter = getter;
        _setter = setter;
    }

    internal override object? GetValue(VdmObject owner) => _getter((TOwner)owner);

    internal override void SetValue(VdmObject owner, object? value) => _setter((TOwner)owner, (List<TEntity>?)value);

    internal override void Write(Utf8JsonWriter writer, VdmObject owner, JsonSerializerOptions options, bool writeNulls)
    {
        var list = _getter((TOwner)owner);
        if (list is null)
        {
            if (writeNulls)
            {
                writer.WriteNull(Name);
            }
            return;
        }
        writer.WritePropertyName(Name);
        writer.WriteStartArray();
        foreach (var item in list)
        {
            if (item is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                JsonSerializer.Serialize(writer, item, item.GetType(), options);
            }
        }
        writer.WriteEndArray();
    }

    internal override void Read(ref Utf8JsonReader reader, VdmObject owner, JsonSerializerOptions options)
    {
        List<TEntity>? list = null;
        switch (reader.TokenType)
        {
            case JsonTokenType.StartArray:
                list = ReadArray(ref reader, options);
                break;
            case JsonTokenType.StartObject:
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = reader.GetString();
                    reader.Read();
                    if (key == "results" && reader.TokenType == JsonTokenType.StartArray)
                    {
                        list = ReadArray(ref reader, options);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                break;
            default:
                reader.Skip();
                break;
        }
        _setter((TOwner)owner, list);
    }

    private static List<TEntity> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var result = new List<TEntity>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            result.Add(reader.TokenType == JsonTokenType.Null ? null! : JsonSerializer.Deserialize<TEntity>(ref reader, options)!);
        }
        return result;
    }
}
