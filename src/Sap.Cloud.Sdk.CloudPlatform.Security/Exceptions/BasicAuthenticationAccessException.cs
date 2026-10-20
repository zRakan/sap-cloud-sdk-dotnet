namespace Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;

/// <summary>Thrown when the Basic Authentication credentials of the current request cannot be accessed.</summary>
public class BasicAuthenticationAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BasicAuthenticationAccessException"/> class.</summary>
    public BasicAuthenticationAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BasicAuthenticationAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public BasicAuthenticationAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BasicAuthenticationAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public BasicAuthenticationAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
