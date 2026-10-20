using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

/// <summary>Thrown when a property of an <see cref="IThreadContext"/> cannot be read or written.</summary>
public class ThreadContextPropertyException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ThreadContextPropertyException"/> class.</summary>
    public ThreadContextPropertyException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ThreadContextPropertyException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ThreadContextPropertyException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ThreadContextPropertyException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ThreadContextPropertyException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
