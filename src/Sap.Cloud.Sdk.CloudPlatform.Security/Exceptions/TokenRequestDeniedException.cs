namespace Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;

/// <summary>Thrown when the token endpoint denied an OAuth 2.0 token request.</summary>
public class TokenRequestDeniedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TokenRequestDeniedException"/> class.</summary>
    public TokenRequestDeniedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TokenRequestDeniedException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public TokenRequestDeniedException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TokenRequestDeniedException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public TokenRequestDeniedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
