#pragma warning disable CA1711 // The name mirrors the Java VdmEnum.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Base class of generated OData enumerations.
/// </summary>
/// <remarks>
/// Port of the <c>VdmEnum</c> interface. Java enums implement the interface; .NET enums cannot carry behavior, so a
/// generated enumeration is a sealed class with one static instance per member, derived from this class and
/// implementing <see cref="IVdmEnum{TSelf}"/>. Unknown names read from a payload become <see langword="null"/>, as in Java.
/// </remarks>
public abstract class VdmEnum : IEquatable<VdmEnum>
{
    /// <summary>Initializes a new instance of the <see cref="VdmEnum"/> class.</summary>
    /// <param name="name">The name of the member as defined by the service.</param>
    /// <param name="value">The numeric value of the member, if the service defines one.</param>
    protected VdmEnum(string name, long? value = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Value = value;
    }

    /// <summary>The name of the member as defined by the service.</summary>
    public string Name { get; }

    /// <summary>The numeric value of the member, if the service defines one.</summary>
    public long? Value { get; }

    /// <summary>Finds a member by its service name.</summary>
    /// <typeparam name="TEnum">The enumeration type.</typeparam>
    /// <param name="identifier">The member name.</param>
    /// <returns>The member, or <see langword="null"/> if there is none.</returns>
    public static TEnum? GetConstant<TEnum>(string? identifier)
        where TEnum : VdmEnum, IVdmEnum<TEnum>
    {
        foreach (var member in TEnum.Values)
        {
            if (string.Equals(member.Name, identifier, StringComparison.Ordinal))
            {
                return member;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool Equals(VdmEnum? other) => other is not null && GetType() == other.GetType() && Name == other.Name;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as VdmEnum);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Name);

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <summary>Compares two members.</summary>
    /// <param name="left">The first member.</param>
    /// <param name="right">The second member.</param>
    /// <returns><see langword="true"/> if both are equal.</returns>
    public static bool operator ==(VdmEnum? left, VdmEnum? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Compares two members.</summary>
    /// <param name="left">The first member.</param>
    /// <param name="right">The second member.</param>
    /// <returns><see langword="true"/> if both differ.</returns>
    public static bool operator !=(VdmEnum? left, VdmEnum? right) => !(left == right);
}

/// <summary>Exposes the members of a generated enumeration without reflection.</summary>
/// <typeparam name="TSelf">The enumeration type.</typeparam>
public interface IVdmEnum<TSelf>
    where TSelf : VdmEnum
{
    /// <summary>All members, in declaration order.</summary>
    static abstract IReadOnlyList<TSelf> Values { get; }
}

/// <summary>Reads and writes a generated enumeration as the name of the member.</summary>
/// <typeparam name="TEnum">The enumeration type.</typeparam>
public sealed class VdmEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : VdmEnum, IVdmEnum<TEnum>
{
    /// <inheritdoc />
    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? VdmEnum.GetConstant<TEnum>(reader.GetString()) : null;

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Name);
    }
}
