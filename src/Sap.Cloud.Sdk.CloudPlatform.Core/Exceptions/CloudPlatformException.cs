namespace Sap.Cloud.Sdk.CloudPlatform.Exceptions;

/// <summary>Generic failure while interacting with the cloud platform.</summary>
public class CloudPlatformException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CloudPlatformException"/> class.</summary>
    public CloudPlatformException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CloudPlatformException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public CloudPlatformException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CloudPlatformException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public CloudPlatformException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
