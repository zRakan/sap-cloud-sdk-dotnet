namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown when an HTTP client for a destination cannot be created.</summary>
public class HttpClientInstantiationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="HttpClientInstantiationException"/> class.</summary>
    public HttpClientInstantiationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HttpClientInstantiationException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public HttpClientInstantiationException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HttpClientInstantiationException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public HttpClientInstantiationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
