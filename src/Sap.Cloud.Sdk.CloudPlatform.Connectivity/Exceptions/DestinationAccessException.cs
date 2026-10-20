namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown when a destination cannot be accessed or is misconfigured.</summary>
public class DestinationAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DestinationAccessException"/> class.</summary>
    public DestinationAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public DestinationAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationAccessException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    public DestinationAccessException(string? destinationName, string? message)
        : base(message)
    {
        DestinationName = destinationName;
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationAccessException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationAccessException(string? destinationName, string? message, Exception? innerException)
        : base(message, innerException)
    {
        DestinationName = destinationName;
    }

    /// <summary>The name of the destination, if known.</summary>
    public string? DestinationName { get; }
}
