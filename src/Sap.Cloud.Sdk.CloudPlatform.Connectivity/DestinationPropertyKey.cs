using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>A typed key of a destination property, together with the conversion from the stored value.</summary>
/// <typeparam name="T">The type of the property value.</typeparam>
/// <remarks>Port of <c>DestinationPropertyKey</c>; the Java constructor is package-private, so keys are created via <see cref="DestinationPropertyKey"/>.</remarks>
public sealed class DestinationPropertyKey<T>
{
    internal DestinationPropertyKey(string keyName, Func<object, T> fromMapConversion)
    {
        KeyName = keyName;
        FromMapConversion = fromMapConversion;
    }

    /// <summary>The name of the property.</summary>
    public string KeyName { get; }

    /// <summary>Converts the raw stored value into <typeparamref name="T"/>.</summary>
    public Func<object, T> FromMapConversion { get; }

    /// <summary>Reads the property from a map.</summary>
    /// <param name="properties">The properties; keys are matched case-insensitively if the dictionary is.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> if the property exists.</returns>
    /// <exception cref="ArgumentException">The stored value has an unexpected type or format.</exception>
    public bool TryGet(IReadOnlyDictionary<string, object> properties, [MaybeNullWhen(false)] out T value)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (properties.TryGetValue(KeyName, out var raw) && raw is not null)
        {
            value = FromMapConversion(raw);
            return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => KeyName;
}

/// <summary>Factory methods for <see cref="DestinationPropertyKey{T}"/>.</summary>
public static class DestinationPropertyKey
{
    /// <summary>
    /// Creates a key whose value is either already a <typeparamref name="T"/> or a string that is converted with
    /// <paramref name="fallback"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="keyName">The property name.</param>
    /// <param name="fallback">The conversion from string.</param>
    /// <returns>The key.</returns>
    public static DestinationPropertyKey<T> CreateProperty<T>(string keyName, Func<string, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        ArgumentNullException.ThrowIfNull(fallback);
        return new(keyName, o => o switch
        {
            T t => t,
            string s => fallback(s),
            _ => throw new ArgumentException(
                $"Unexpected type: {typeof(T).Name} or String was expected. Type found was: {o.GetType().Name}"),
        });
    }

    /// <summary>Creates a string key.</summary>
    /// <param name="keyName">The property name.</param>
    /// <returns>The key.</returns>
    public static DestinationPropertyKey<string> CreateStringProperty(string keyName) =>
        CreateProperty(keyName, s => s);

    /// <summary>Creates a URI key.</summary>
    /// <param name="keyName">The property name.</param>
    /// <returns>The key.</returns>
    public static DestinationPropertyKey<Uri> CreateUriProperty(string keyName) =>
        CreateProperty(keyName, s => new Uri(s, UriKind.RelativeOrAbsolute));

    /// <summary>Creates a key for a list of untyped values.</summary>
    /// <param name="keyName">The property name.</param>
    /// <returns>The key.</returns>
    public static DestinationPropertyKey<IReadOnlyList<object?>> CreateListProperty(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        return new(keyName, o => o switch
        {
            IReadOnlyList<object?> list => list,
            string => throw UnexpectedType("List", o),
            IEnumerable enumerable => [.. enumerable.Cast<object?>()],
            _ => throw UnexpectedType("List", o),
        });
    }

    /// <summary>Creates a key for a collection of strings.</summary>
    /// <param name="keyName">The property name.</param>
    /// <returns>The key.</returns>
    public static DestinationPropertyKey<IReadOnlyCollection<string>> CreateCollectionProperty(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        return new(keyName, o => o switch
        {
            IReadOnlyCollection<string> collection => collection,
            string => throw UnexpectedType("Collection", o),
            IEnumerable enumerable => [.. enumerable.Cast<object?>().Select(e => e?.ToString() ?? string.Empty)],
            _ => throw UnexpectedType("Collection", o),
        });
    }

    private static ArgumentException UnexpectedType(string expected, object found) =>
        new($"Unexpected type: {expected} was expected. Type found was: {found.GetType().Name}");
}
