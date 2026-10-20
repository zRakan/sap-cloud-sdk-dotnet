namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown when a destination does not exist.</summary>
/// <remarks>
/// In contrast to <see cref="DestinationAccessException"/>, this exception means that a lookup worked but yielded
/// nothing, so other sources may still be asked (see <see cref="DestinationLoaderChain"/>).
/// </remarks>
public class DestinationNotFoundException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DestinationNotFoundException"/> class.</summary>
    public DestinationNotFoundException()
        : this((string?)null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationNotFoundException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    public DestinationNotFoundException(string? destinationName)
        : base("Destination " + (destinationName is not null ? "'" + destinationName + "'" : "") + " not found.")
    {
        DestinationName = destinationName;
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationNotFoundException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    public DestinationNotFoundException(string? destinationName, string? message)
        : base(message)
    {
        DestinationName = destinationName;
    }

    /// <summary>Initializes a new instance of the <see cref="DestinationNotFoundException"/> class for a named destination.</summary>
    /// <param name="destinationName">The name of the destination.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DestinationNotFoundException(string? destinationName, string? message, Exception? innerException)
        : base(message, innerException)
    {
        DestinationName = destinationName;
    }

    /// <summary>The name of the destination, if known.</summary>
    public string? DestinationName { get; }
}
