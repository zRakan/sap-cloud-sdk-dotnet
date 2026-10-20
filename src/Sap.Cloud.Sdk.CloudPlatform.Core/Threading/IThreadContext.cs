using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>
/// A set of named properties (tenant, principal, auth token, request headers, ...) that belong to the current logical
/// flow of execution.
/// </summary>
/// <remarks>
/// The name is kept from the Java SDK for traceability. In .NET the context is not bound to a thread: it is stored in
/// an <see cref="AsyncLocal{T}"/> and flows across <see langword="await"/>, <see cref="Task.Run(Action)"/> and the
/// thread pool automatically.
/// </remarks>
public interface IThreadContext
{
    /// <summary>Gets the value of a property.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="name">The property name.</param>
    /// <returns>
    /// The value; a failure with <see cref="ThreadContextPropertyNotFoundException"/> if the property does not exist,
    /// or a failure with <see cref="ThreadContextPropertyException"/> if it has a different type.
    /// </returns>
    Result<T> GetPropertyValue<T>(string name);

    /// <summary>Sets a property unless one with the same name exists.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property.</param>
    void SetPropertyIfAbsent(string name, ThreadContextProperty value);

    /// <summary>
    /// Sets a property unless one with the same name exists. <paramref name="valueFactory"/> is only called if the
    /// property is absent.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="valueFactory">Produces the property.</param>
    /// <exception cref="ThreadContextPropertyException">If <paramref name="valueFactory"/> throws.</exception>
    void SetPropertyIfAbsent(string name, Func<ThreadContextProperty> valueFactory);

    /// <summary>Sets a property, replacing any existing one with the same name.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property.</param>
    void SetProperty(string name, ThreadContextProperty value);

    /// <summary>Removes a property.</summary>
    /// <param name="name">The property name.</param>
    /// <returns>The removed property, or <see langword="null"/> if it did not exist.</returns>
    ThreadContextProperty? RemoveProperty(string name);

    /// <summary>Whether a property with the given name exists.</summary>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> if it exists.</returns>
    bool ContainsProperty(string name);

    /// <summary>Creates an independent copy of this context.</summary>
    /// <returns>The copy.</returns>
    /// <exception cref="ThreadContextAccessException">If this implementation does not support duplication.</exception>
    IThreadContext Duplicate();
}
