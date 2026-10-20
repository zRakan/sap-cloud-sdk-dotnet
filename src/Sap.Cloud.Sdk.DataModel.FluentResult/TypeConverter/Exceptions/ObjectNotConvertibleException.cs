namespace Sap.Cloud.Sdk.TypeConverter.Exceptions;

/// <summary>Thrown when an object cannot be converted by a type converter.</summary>
public class ObjectNotConvertibleException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ObjectNotConvertibleException"/> class.</summary>
    public ObjectNotConvertibleException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ObjectNotConvertibleException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ObjectNotConvertibleException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ObjectNotConvertibleException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ObjectNotConvertibleException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
