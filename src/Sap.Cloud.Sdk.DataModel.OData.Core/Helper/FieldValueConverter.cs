using Sap.Cloud.Sdk.TypeConverter;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>Converts the value of a custom field between the type used by the application and the type held by the entity.</summary>
/// <typeparam name="TField">The application's type of the field.</typeparam>
/// <remarks>
/// Java's <c>EntityField</c> holds a <c>TypeConverter&lt;FieldT, ?&gt;</c>; .NET has no wildcard, so the domain type is erased by this
/// interface. Use <see cref="FieldValueConverter.From{TField, TDomain}"/> to create one from an <see cref="ITypeConverter{T, TDomain}"/>.
/// </remarks>
public interface IFieldValueConverter<TField>
{
    /// <summary>Converts the application's value to the value stored in the entity.</summary>
    /// <param name="value">The application's value.</param>
    /// <returns>The stored value.</returns>
    /// <exception cref="Sap.Cloud.Sdk.TypeConverter.Exceptions.ObjectNotConvertibleException">The value cannot be converted.</exception>
    object? ToDomain(TField? value);

    /// <summary>Converts the stored value to the application's value.</summary>
    /// <param name="domainValue">The stored value.</param>
    /// <returns>The application's value.</returns>
    /// <exception cref="Sap.Cloud.Sdk.TypeConverter.Exceptions.ObjectNotConvertibleException">The value cannot be converted.</exception>
    TField? FromDomain(object? domainValue);
}

/// <summary>Factory for <see cref="IFieldValueConverter{TField}"/>.</summary>
public static class FieldValueConverter
{
    /// <summary>Wraps a type converter.</summary>
    /// <typeparam name="TField">The application's type of the field.</typeparam>
    /// <typeparam name="TDomain">The type of the stored value.</typeparam>
    /// <param name="typeConverter">The type converter.</param>
    /// <returns>The field value converter.</returns>
    public static IFieldValueConverter<TField> From<TField, TDomain>(ITypeConverter<TField, TDomain> typeConverter)
    {
        ArgumentNullException.ThrowIfNull(typeConverter);
        return new Adapter<TField, TDomain>(typeConverter);
    }

    private sealed class Adapter<TField, TDomain>(ITypeConverter<TField, TDomain> typeConverter) : IFieldValueConverter<TField>
    {
        public object? ToDomain(TField? value) => typeConverter.ToDomain(value).Get();

        public TField? FromDomain(object? domainValue) =>
            typeConverter.FromDomain(domainValue is null ? default : (TDomain)domainValue).Get();
    }
}
