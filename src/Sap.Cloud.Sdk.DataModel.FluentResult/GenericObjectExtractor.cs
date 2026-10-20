namespace Sap.Cloud.Sdk.Result;

/// <summary>Extracts instances of <typeparamref name="T"/> from result elements.</summary>
/// <remarks>Replaces <c>GenericObjectExtractor</c>; the per-primitive Java extractor classes are replaced by delegates.</remarks>
/// <typeparam name="T">The target type.</typeparam>
public class GenericObjectExtractor<T>
{
    /// <summary>Extracts a <typeparamref name="T"/> from the element.</summary>
    /// <param name="resultElement">The element.</param>
    /// <returns>The extracted value.</returns>
    /// <exception cref="NotSupportedException">The element cannot be converted.</exception>
    public T Extract(IResultElement resultElement)
    {
        ArgumentNullException.ThrowIfNull(resultElement);
        if (resultElement.IsResultObject)
        {
            return resultElement.GetAsObject().AsType<T>();
        }
        if (resultElement.IsResultPrimitive)
        {
            return new PrimitiveBasedObjectExtractor<T>().Extract(resultElement);
        }
        throw new NotSupportedException($"Failed to convert IResultElement to type {typeof(T).FullName}: {resultElement}.");
    }
}
