using Sap.Cloud.Sdk.CloudPlatform.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown if no binding is found to a service on SAP Business Technology Platform Cloud Foundry.</summary>
/// <remarks>Port of <c>NoServiceBindingException</c>, which lives in the <c>exception</c> package of the Java module.</remarks>
public class NoServiceBindingException : CloudPlatformException
{
    /// <summary>Initializes a new instance of the <see cref="NoServiceBindingException"/> class.</summary>
    public NoServiceBindingException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NoServiceBindingException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public NoServiceBindingException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NoServiceBindingException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public NoServiceBindingException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
