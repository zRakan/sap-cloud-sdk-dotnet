namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;

/// <summary>Thrown when the current principal cannot be accessed.</summary>
public class PrincipalAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PrincipalAccessException"/> class.</summary>
    public PrincipalAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PrincipalAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public PrincipalAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PrincipalAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public PrincipalAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
