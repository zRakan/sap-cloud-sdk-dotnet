namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Indicates an error during the retrieval of a CSRF token.</summary>
public class CsrfTokenRetrievalException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CsrfTokenRetrievalException"/> class.</summary>
    public CsrfTokenRetrievalException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CsrfTokenRetrievalException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public CsrfTokenRetrievalException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CsrfTokenRetrievalException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public CsrfTokenRetrievalException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
