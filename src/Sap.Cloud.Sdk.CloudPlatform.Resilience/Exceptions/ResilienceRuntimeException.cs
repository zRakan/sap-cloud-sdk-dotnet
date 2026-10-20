namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

/// <summary>Thrown when an operation decorated with resilience patterns fails or cannot be decorated.</summary>
public class ResilienceRuntimeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ResilienceRuntimeException"/> class.</summary>
    public ResilienceRuntimeException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ResilienceRuntimeException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ResilienceRuntimeException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ResilienceRuntimeException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ResilienceRuntimeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
