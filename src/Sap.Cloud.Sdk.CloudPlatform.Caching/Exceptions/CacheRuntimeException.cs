namespace Sap.Cloud.Sdk.CloudPlatform.Cache.Exceptions;

/// <summary>Thrown when an operation executed on behalf of a cache fails.</summary>
public class CacheRuntimeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CacheRuntimeException"/> class.</summary>
    public CacheRuntimeException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CacheRuntimeException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public CacheRuntimeException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CacheRuntimeException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public CacheRuntimeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
