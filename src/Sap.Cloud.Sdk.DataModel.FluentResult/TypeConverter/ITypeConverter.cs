namespace Sap.Cloud.Sdk.TypeConverter;

/// <summary>Converts between a type and its domain representation.</summary>
/// <remarks>Port of <c>TypeConverter</c>; renamed because of the namespace of the same name.</remarks>
/// <typeparam name="T">The type.</typeparam>
/// <typeparam name="TDomain">The domain type.</typeparam>
public interface ITypeConverter<T, TDomain>
{
    /// <summary>The type.</summary>
    Type Type { get; }

    /// <summary>The domain type.</summary>
    Type DomainType { get; }

    /// <summary>Converts to the domain type.</summary>
    /// <param name="obj">The object.</param>
    /// <returns>The converted object.</returns>
    ConvertedObject<TDomain> ToDomain(T? obj);

    /// <summary>Converts from the domain type.</summary>
    /// <param name="domainObject">The domain object.</param>
    /// <returns>The converted object.</returns>
    ConvertedObject<T> FromDomain(TDomain? domainObject);
}
