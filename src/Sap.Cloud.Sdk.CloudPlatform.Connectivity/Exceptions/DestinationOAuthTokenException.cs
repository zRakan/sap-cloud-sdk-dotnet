namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown when the OAuth token for a destination cannot be obtained or used.</summary>
public class DestinationOAuthTokenException : DestinationAccessException
{
    /// <summary>Initializes a new instance of the <see cref="DestinationOAuthTokenException"/> class.</summary>
    public DestinationOAuthTokenException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationOAuthTokenException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <remarks>Unlike the other constructors with a single string, the argument is the destination name, as in Java.</remarks>
    public DestinationOAuthTokenException(string? destinationName)
        : base(
            destinationName,
            "Failed to access or use OAuth token for destination '"
            + destinationName
            + "'. Please make sure that the destination configuration is correct.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationOAuthTokenException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    public DestinationOAuthTokenException(string? destinationName, string? message)
        : base(destinationName, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationOAuthTokenException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationOAuthTokenException(string? destinationName, string? message, Exception? innerException)
        : base(destinationName, message, innerException)
    {
    }
}
