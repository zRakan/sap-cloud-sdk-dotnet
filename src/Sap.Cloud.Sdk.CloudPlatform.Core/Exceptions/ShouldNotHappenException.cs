namespace Sap.Cloud.Sdk.CloudPlatform.Exceptions;

/// <summary>Thrown when an internal invariant is violated. Indicates a bug in the SDK.</summary>
public class ShouldNotHappenException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ShouldNotHappenException"/> class.</summary>
    public ShouldNotHappenException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ShouldNotHappenException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ShouldNotHappenException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ShouldNotHappenException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ShouldNotHappenException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
