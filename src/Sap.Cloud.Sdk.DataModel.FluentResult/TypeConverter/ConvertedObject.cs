using Sap.Cloud.Sdk.TypeConverter.Exceptions;

namespace Sap.Cloud.Sdk.TypeConverter;

/// <summary>The outcome of a conversion: a (possibly <see langword="null"/>) value or "not convertible".</summary>
/// <typeparam name="T">The converted type.</typeparam>
public sealed record ConvertedObject<T>
{
    private readonly T? _object;

    private ConvertedObject(bool notConvertible, T? obj)
    {
        IsNotConvertible = notConvertible;
        _object = obj;
    }

    /// <summary>Whether the object could not be converted.</summary>
    public bool IsNotConvertible { get; }

    /// <summary>Whether the object could be converted.</summary>
    public bool IsConvertible => !IsNotConvertible;

    /// <summary>Gets the converted value.</summary>
    /// <returns>The value, which may be <see langword="null"/> if a <see langword="null"/> was converted.</returns>
    /// <exception cref="ObjectNotConvertibleException">The object was not convertible.</exception>
    public T? Get() => IsNotConvertible ? throw new ObjectNotConvertibleException("Object is not convertible.") : _object;

    /// <summary>Gets the converted value or <see langword="null"/>.</summary>
    /// <returns>The value or <see langword="null"/>.</returns>
    public T? OrNull() => OrElse(default);

    /// <summary>Gets the converted value or the given default.</summary>
    /// <param name="defaultValue">The value used if the object is not convertible.</param>
    /// <returns>The value or the default.</returns>
    public T? OrElse(T? defaultValue) => IsNotConvertible ? defaultValue : _object;

#pragma warning disable CA1000 // Static factories mirror the Java API (ConvertedObject.of).
    /// <summary>Creates a convertible result.</summary>
    /// <param name="convertedObject">The value.</param>
    /// <returns>The result.</returns>
    public static ConvertedObject<T> Of(T? convertedObject) => new(false, convertedObject);

    /// <summary>Creates a convertible result holding <see langword="null"/>.</summary>
    /// <returns>The result.</returns>
    public static ConvertedObject<T> OfNull() => Of(default);

    /// <summary>Creates a "not convertible" result.</summary>
    /// <returns>The result.</returns>
    public static ConvertedObject<T> OfNotConvertible() => new(true, default);
#pragma warning restore CA1000
}
