namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

/// <summary>
/// Raised inside a decorated operation when the bulkhead has no free slot within the configured maximum wait duration.
/// Replaces resilience4j's <c>BulkheadFullException</c>. Callers normally see it as the cause of a
/// <see cref="ResilienceRuntimeException"/> or as the argument of the fallback.
/// </summary>
public class BulkheadFullException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BulkheadFullException"/> class.</summary>
    public BulkheadFullException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BulkheadFullException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public BulkheadFullException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BulkheadFullException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public BulkheadFullException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
