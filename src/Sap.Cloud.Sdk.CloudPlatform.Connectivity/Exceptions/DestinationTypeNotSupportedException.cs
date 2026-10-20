namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown when a destination has a type that is not supported by the requested operation.</summary>
public class DestinationTypeNotSupportedException : DestinationAccessException
{
    /// <summary>Initializes a new instance of the <see cref="DestinationTypeNotSupportedException"/> class.</summary>
    public DestinationTypeNotSupportedException()
        : this(null, (DestinationType?)null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationTypeNotSupportedException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public DestinationTypeNotSupportedException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationTypeNotSupportedException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationTypeNotSupportedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationTypeNotSupportedException"/> class.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="destinationType">The unsupported type.</param>
    public DestinationTypeNotSupportedException(string? destinationName, DestinationType? destinationType)
        : base(
            destinationName,
            nameof(DestinationType)
            + " "
            + (destinationType is not null ? "'" + destinationType.Value.GetIdentifier() + "'" : "")
            + " is not supported.")
    {
        DestinationType = destinationType;
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationTypeNotSupportedException"/> class.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="destinationType">The unsupported type.</param>
    /// <param name="message">The error message.</param>
    public DestinationTypeNotSupportedException(string? destinationName, DestinationType? destinationType, string? message)
        : base(destinationName, message)
    {
        DestinationType = destinationType;
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationTypeNotSupportedException"/> class.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="destinationType">The unsupported type.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationTypeNotSupportedException(
        string? destinationName,
        DestinationType? destinationType,
        string? message,
        Exception? innerException)
        : base(destinationName, message, innerException)
    {
        DestinationType = destinationType;
    }

    /// <summary>The unsupported destination type, if known.</summary>
    public DestinationType? DestinationType { get; }
}
