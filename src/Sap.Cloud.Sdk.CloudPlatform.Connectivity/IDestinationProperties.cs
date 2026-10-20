using System.Diagnostics.CodeAnalysis;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>A read-only set of named destination properties.</summary>
/// <remarks>
/// Port of <c>DestinationProperties</c>. Property names are case-insensitive. Java's <c>Option&lt;T&gt;</c> returning
/// convenience overloads are the extension methods in <see cref="DestinationPropertiesExtensions"/>.
/// </remarks>
public interface IDestinationProperties
{
    /// <summary>The names of all properties.</summary>
    IReadOnlyCollection<string> PropertyNames { get; }

    /// <summary>Gets the raw value of a property.</summary>
    /// <param name="key">The property name (case-insensitive).</param>
    /// <returns>The value, or <see langword="null"/> if the property does not exist.</returns>
    object? GetProperty(string key);
}

/// <summary>Typed access to <see cref="IDestinationProperties"/>.</summary>
public static class DestinationPropertiesExtensions
{
    /// <summary>Gets a property using the conversion of the key.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="properties">The properties.</param>
    /// <param name="key">The typed key.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> if the property exists.</returns>
    /// <exception cref="ArgumentException">The stored value has an unexpected type or format.</exception>
    public static bool TryGet<T>(
        this IDestinationProperties properties,
        DestinationPropertyKey<T> key,
        [MaybeNullWhen(false)] out T value)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(key);
        var raw = properties.GetProperty(key.KeyName);
        if (raw is null)
        {
            value = default;
            return false;
        }
        value = key.FromMapConversion(raw);
        return true;
    }

    /// <summary>Gets a property and converts it with the given function.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="properties">The properties.</param>
    /// <param name="key">The property name.</param>
    /// <param name="conversion">The conversion.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> if the property exists.</returns>
    public static bool TryGet<T>(
        this IDestinationProperties properties,
        string key,
        Func<object, T> conversion,
        [MaybeNullWhen(false)] out T value)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(conversion);
        var raw = properties.GetProperty(key);
        if (raw is null)
        {
            value = default;
            return false;
        }
        value = conversion(raw);
        return true;
    }

    /// <summary>Gets a property and casts it to the expected type.</summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="properties">The properties.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> if the property exists.</returns>
    /// <exception cref="InvalidCastException">The value is not a <typeparamref name="T"/>.</exception>
    public static bool TryGetAs<T>(
        this IDestinationProperties properties,
        string key,
        [MaybeNullWhen(false)] out T value) =>
        properties.TryGet(key, o => (T)o, out value);

    /// <summary>Gets a property, or a default if it does not exist.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="properties">The properties.</param>
    /// <param name="key">The typed key.</param>
    /// <param name="defaultValue">The default.</param>
    /// <returns>The value or the default.</returns>
    public static T GetOrDefault<T>(this IDestinationProperties properties, DestinationPropertyKey<T> key, T defaultValue) =>
        properties.TryGet(key, out var value) ? value : defaultValue;

    /// <summary>Gets a reference-typed property, or <see langword="null"/> if it does not exist.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="properties">The properties.</param>
    /// <param name="key">The typed key.</param>
    /// <returns>The value or <see langword="null"/>.</returns>
    public static T? GetOrNull<T>(this IDestinationProperties properties, DestinationPropertyKey<T> key)
        where T : class =>
        properties.TryGet(key, out var value) ? value : null;
}
