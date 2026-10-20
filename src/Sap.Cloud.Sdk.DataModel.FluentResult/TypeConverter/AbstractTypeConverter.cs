namespace Sap.Cloud.Sdk.TypeConverter;

/// <summary>Base class for converters: handles <see langword="null"/> and maps failures to "not convertible".</summary>
/// <typeparam name="T">The type.</typeparam>
/// <typeparam name="TDomain">The domain type.</typeparam>
public abstract class AbstractTypeConverter<T, TDomain> : ITypeConverter<T, TDomain>
{
    /// <inheritdoc />
    public virtual Type Type => typeof(T);

    /// <inheritdoc />
    public virtual Type DomainType => typeof(TDomain);

#pragma warning disable CA1031 // A failing conversion yields "not convertible", as in Java.
    /// <inheritdoc />
    public ConvertedObject<TDomain> ToDomain(T? obj)
    {
        if (obj is null)
        {
            return ConvertedObject<TDomain>.OfNull();
        }
        try
        {
            return ToDomainNonNull(obj);
        }
        catch (Exception)
        {
            return ConvertedObject<TDomain>.OfNotConvertible();
        }
    }

    /// <inheritdoc />
    public ConvertedObject<T> FromDomain(TDomain? domainObject)
    {
        if (domainObject is null)
        {
            return ConvertedObject<T>.OfNull();
        }
        try
        {
            return FromDomainNonNull(domainObject);
        }
        catch (Exception)
        {
            return ConvertedObject<T>.OfNotConvertible();
        }
    }
#pragma warning restore CA1031

    /// <summary>Converts a non-null object to the domain type.</summary>
    /// <param name="obj">The object.</param>
    /// <returns>The converted object.</returns>
    public abstract ConvertedObject<TDomain> ToDomainNonNull(T obj);

    /// <summary>Converts a non-null domain object.</summary>
    /// <param name="domainObject">The domain object.</param>
    /// <returns>The converted object.</returns>
    public abstract ConvertedObject<T> FromDomainNonNull(TDomain domainObject);
}
