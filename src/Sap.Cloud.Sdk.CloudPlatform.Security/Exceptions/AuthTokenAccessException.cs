namespace Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;

/// <summary>Thrown when the current authorization token cannot be accessed.</summary>
public class AuthTokenAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AuthTokenAccessException"/> class.</summary>
    public AuthTokenAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuthTokenAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public AuthTokenAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuthTokenAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public AuthTokenAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
