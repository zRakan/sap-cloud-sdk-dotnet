using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

/// <summary>Thrown when an operation executed within an <see cref="IThreadContext"/> fails.</summary>
public class ThreadContextExecutionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ThreadContextExecutionException"/> class.</summary>
    public ThreadContextExecutionException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ThreadContextExecutionException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ThreadContextExecutionException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ThreadContextExecutionException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ThreadContextExecutionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
