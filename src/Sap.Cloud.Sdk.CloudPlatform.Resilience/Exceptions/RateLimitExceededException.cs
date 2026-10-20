namespace Sap.Cloud.Sdk.CloudPlatform.Resilience.Exceptions;

/// <summary>
/// Raised inside a decorated operation when no rate limiter permit is available within the configured timeout.
/// Replaces resilience4j's <c>RequestNotPermitted</c>. Callers normally see it as the cause of a
/// <see cref="ResilienceRuntimeException"/> or as the argument of the fallback.
/// </summary>
public class RateLimitExceededException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RateLimitExceededException"/> class.</summary>
    public RateLimitExceededException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RateLimitExceededException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public RateLimitExceededException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RateLimitExceededException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public RateLimitExceededException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
