using Sap.Cloud.Sdk.CloudPlatform.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

/// <summary>Thrown if multiple bindings to a service are found but only a single one is supported.</summary>
/// <remarks>Port of <c>MultipleServiceBindingsException</c>, which lives in the <c>exception</c> package of the Java module.</remarks>
public class MultipleServiceBindingsException : CloudPlatformException
{
    /// <summary>Initializes a new instance of the <see cref="MultipleServiceBindingsException"/> class.</summary>
    public MultipleServiceBindingsException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MultipleServiceBindingsException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public MultipleServiceBindingsException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MultipleServiceBindingsException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public MultipleServiceBindingsException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
