namespace Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

/// <summary>Thrown when a requested property does not exist in the current thread context.</summary>
public class ThreadContextPropertyNotFoundException : ThreadContextPropertyException
{
    /// <summary>Initializes a new instance of the <see cref="ThreadContextPropertyNotFoundException"/> class.</summary>
    public ThreadContextPropertyNotFoundException()
    {
    }

    /// <summary>Initializes a new instance for the property with the given name.</summary>
    /// <param name="propertyName">The name of the missing property.</param>
    public ThreadContextPropertyNotFoundException(string? propertyName)
        : base($"ThreadContextProperty '{propertyName}' does not exist.")
    {
        PropertyName = propertyName;
    }

    /// <summary>Initializes a new instance with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ThreadContextPropertyNotFoundException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The name of the missing property, if known.</summary>
    public string? PropertyName { get; }
}
