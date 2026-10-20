using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Static description of one property of a generated entity or complex type: its OData name and how to read and
/// write it without reflection.
/// </summary>
/// <remarks>
/// Replaces the <c>@ElementName</c> annotations together with the reflective field access of the Gson adapters.
/// </remarks>
public abstract class VdmPropertyInfo
{
    internal VdmPropertyInfo(string name, bool isNavigation, Type valueType)
    {
        Name = name;
        IsNavigation = isNavigation;
        ValueType = valueType;
    }

    /// <summary>The name of the property in the OData payload.</summary>
    public string Name { get; }

    /// <summary>Whether the property is a navigation property (as opposed to a simple or complex property).</summary>
    public bool IsNavigation { get; }

    /// <summary>The .NET type of the property.</summary>
    public Type ValueType { get; }

    /// <summary>Gets the current value of the property.</summary>
    /// <param name="owner">The entity or complex value.</param>
    /// <returns>The value.</returns>
    public abstract object? GetValue(VdmObject owner);

    internal abstract void ReadJson(VdmObject owner, ref Utf8JsonReader reader, JsonSerializerOptions options);

    internal abstract void WriteJson(VdmObject owner, Utf8JsonWriter writer, JsonSerializerOptions options);
}

/// <summary>A property of <typeparamref name="TOwner"/> with the .NET type <typeparamref name="TValue"/>.</summary>
/// <typeparam name="TOwner">The owning entity or complex type.</typeparam>
/// <typeparam name="TValue">The property type.</typeparam>
public sealed class VdmPropertyInfo<TOwner, TValue> : VdmPropertyInfo
    where TOwner : VdmObject
{
    private readonly Func<TOwner, TValue> _getter;
    private readonly Action<TOwner, TValue> _setter;
    private readonly JsonConverter<TValue>? _converter;

    internal VdmPropertyInfo(
        string name,
        bool isNavigation,
        Func<TOwner, TValue> getter,
        Action<TOwner, TValue> setter,
        JsonConverter<TValue>? converter)
        : base(name, isNavigation, typeof(TValue))
    {
        _getter = getter;
        _setter = setter;
        _converter = converter ?? (typeof(TValue) == typeof(byte[]) ? (JsonConverter<TValue>)(object)BinaryUrlSafeJsonConverter.Instance : null);
    }

    /// <inheritdoc />
    public override object? GetValue(VdmObject owner) => _getter((TOwner)owner);

    internal override void ReadJson(VdmObject owner, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var value = _converter is null || reader.TokenType == JsonTokenType.Null
            ? JsonSerializer.Deserialize<TValue>(ref reader, options)
            : _converter.Read(ref reader, typeof(TValue), options);
        _setter((TOwner)owner, value!);
    }

    internal override void WriteJson(VdmObject owner, Utf8JsonWriter writer, JsonSerializerOptions options)
    {
        var value = _getter((TOwner)owner);
        if (_converter is null || value is null)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
        else
        {
            _converter.Write(writer, value, options);
        }
    }
}

/// <summary>Static description of a generated entity or complex type.</summary>
public abstract class VdmTypeInfo
{
    private readonly Dictionary<string, VdmPropertyInfo> _byName;

    internal VdmTypeInfo(string oDataType, IReadOnlyList<VdmPropertyInfo> properties)
    {
        ODataType = oDataType;
        Properties = properties;
        Fields = properties.Where(p => !p.IsNavigation).ToList();
        Navigations = properties.Where(p => p.IsNavigation).ToList();
        _byName = properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
    }

    /// <summary>The qualified OData type name, for example <c>Trippin.Person</c>.</summary>
    public string ODataType { get; }

    /// <summary>All properties in declaration order.</summary>
    public IReadOnlyList<VdmPropertyInfo> Properties { get; }

    /// <summary>The simple and complex properties in declaration order.</summary>
    public IReadOnlyList<VdmPropertyInfo> Fields { get; }

    /// <summary>The navigation properties in declaration order.</summary>
    public IReadOnlyList<VdmPropertyInfo> Navigations { get; }

    /// <summary>Looks up a property by its OData name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="property">The property, if found.</param>
    /// <returns><see langword="true"/> if the type declares the property.</returns>
    public bool TryGetProperty(string name, out VdmPropertyInfo property) => _byName.TryGetValue(name, out property!);

    /// <summary>Creates an empty instance of the described type.</summary>
    /// <returns>The new instance.</returns>
    public abstract VdmObject CreateInstance();
}

/// <summary>Static description of the generated type <typeparamref name="TOwner"/>.</summary>
/// <typeparam name="TOwner">The generated entity or complex type.</typeparam>
public sealed class VdmTypeInfo<TOwner> : VdmTypeInfo
    where TOwner : VdmObject, new()
{
    internal VdmTypeInfo(string oDataType, IReadOnlyList<VdmPropertyInfo> properties)
        : base(oDataType, properties)
    {
    }

    /// <inheritdoc />
    public override VdmObject CreateInstance() => new TOwner();
}

/// <summary>Builds the <see cref="VdmTypeInfo{TOwner}"/> of a generated type.</summary>
/// <typeparam name="TOwner">The generated entity or complex type.</typeparam>
public sealed class VdmTypeInfoBuilder<TOwner>
    where TOwner : VdmObject, new()
{
    private readonly string _oDataType;
    private readonly List<VdmPropertyInfo> _properties = [];

    /// <summary>Initializes a new instance of the <see cref="VdmTypeInfoBuilder{TOwner}"/> class.</summary>
    /// <param name="oDataType">The qualified OData type name.</param>
    public VdmTypeInfoBuilder(string oDataType)
    {
        ArgumentNullException.ThrowIfNull(oDataType);
        _oDataType = oDataType;
    }

    /// <summary>Declares a simple or complex property.</summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="name">The OData name.</param>
    /// <param name="getter">Reads the property.</param>
    /// <param name="setter">Writes the property; it should go through the public setter to record changes.</param>
    /// <param name="converter">
    /// A converter for this property only; replaces Java's <c>@JsonAdapter</c> on a field. By default the converters of
    /// the serializer options apply.
    /// </param>
    /// <returns>This builder.</returns>
    public VdmTypeInfoBuilder<TOwner> Field<TValue>(
        string name,
        Func<TOwner, TValue> getter,
        Action<TOwner, TValue> setter,
        JsonConverter<TValue>? converter = null)
    {
        _properties.Add(new VdmPropertyInfo<TOwner, TValue>(name, false, getter, setter, converter));
        return this;
    }

    /// <summary>Declares a navigation property.</summary>
    /// <typeparam name="TValue">The property type: an entity or a list of entities.</typeparam>
    /// <param name="name">The OData name.</param>
    /// <param name="getter">Reads the backing field.</param>
    /// <param name="setter">Writes the backing field without recording a change.</param>
    /// <returns>This builder.</returns>
    public VdmTypeInfoBuilder<TOwner> Navigation<TValue>(string name, Func<TOwner, TValue> getter, Action<TOwner, TValue> setter)
    {
        _properties.Add(new VdmPropertyInfo<TOwner, TValue>(name, true, getter, setter, null));
        return this;
    }

    /// <summary>Creates the description.</summary>
    /// <returns>The immutable description.</returns>
    public VdmTypeInfo<TOwner> Build() => new(_oDataType, _properties.ToArray());
}
