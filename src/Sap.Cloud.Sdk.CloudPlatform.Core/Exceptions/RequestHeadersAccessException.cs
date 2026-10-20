namespace Sap.Cloud.Sdk.CloudPlatform.Exceptions;

/// <summary>Thrown when the headers of the current request cannot be accessed.</summary>
public class RequestHeadersAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RequestHeadersAccessException"/> class.</summary>
    public RequestHeadersAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RequestHeadersAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public RequestHeadersAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RequestHeadersAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public RequestHeadersAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
