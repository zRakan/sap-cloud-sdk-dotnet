namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown when the URI of a destination and the URI of a request cannot be merged.</summary>
public class DestinationPathsNotMergeableException : DestinationAccessException
{
    /// <summary>Initializes a new instance of the <see cref="DestinationPathsNotMergeableException"/> class.</summary>
    public DestinationPathsNotMergeableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationPathsNotMergeableException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public DestinationPathsNotMergeableException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationPathsNotMergeableException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationPathsNotMergeableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationPathsNotMergeableException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    public DestinationPathsNotMergeableException(string? destinationName, string? message)
        : base(destinationName, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationPathsNotMergeableException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationPathsNotMergeableException(string? destinationName, string? message, Exception? innerException)
        : base(destinationName, message, innerException)
    {
    }
}
