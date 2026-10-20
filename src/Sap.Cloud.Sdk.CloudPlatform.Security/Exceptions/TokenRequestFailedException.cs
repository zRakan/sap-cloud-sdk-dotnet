namespace Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;

/// <summary>Thrown when an OAuth 2.0 token could not be retrieved.</summary>
public class TokenRequestFailedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TokenRequestFailedException"/> class.</summary>
    public TokenRequestFailedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TokenRequestFailedException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public TokenRequestFailedException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TokenRequestFailedException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public TokenRequestFailedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
