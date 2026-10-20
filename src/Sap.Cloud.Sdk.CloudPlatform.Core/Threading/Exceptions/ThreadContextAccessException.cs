using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

/// <summary>Thrown when the current <see cref="IThreadContext"/> cannot be accessed.</summary>
public class ThreadContextAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ThreadContextAccessException"/> class.</summary>
    public ThreadContextAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ThreadContextAccessException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ThreadContextAccessException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ThreadContextAccessException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ThreadContextAccessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
