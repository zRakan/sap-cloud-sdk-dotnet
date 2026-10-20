namespace Sap.Cloud.Sdk.Result;

/// <summary>An object result element with named children.</summary>
/// <remarks>Java's <c>get</c> and <c>as</c> are <c>GetElement</c> and <c>AsType</c> here (keyword clash).</remarks>
public interface IResultObject : IResultElement
{
    /// <summary>Gets the child with the given name.</summary>
    /// <param name="elementName">The name of the child.</param>
    /// <returns>The child, or <see langword="null"/> if there is no such child.</returns>
    /// <exception cref="NotSupportedException">The child cannot be represented as a result element.</exception>
    IResultElement? GetElement(string elementName);

    /// <summary>Deserializes this object to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="NotSupportedException">The deserialization failed.</exception>
    T AsType<T>();

    /// <summary>Deserializes this object to the given type.</summary>
    /// <param name="objectType">The target type.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="NotSupportedException">The deserialization failed.</exception>
    object AsType(Type objectType);
}
