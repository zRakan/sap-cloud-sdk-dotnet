namespace Sap.Cloud.Sdk.DataModel.OData.Exceptions;

/// <summary>Thrown when an OData payload cannot be parsed.</summary>
public class ODataPayloadParsingFailedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ODataPayloadParsingFailedException"/> class.</summary>
    public ODataPayloadParsingFailedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataPayloadParsingFailedException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataPayloadParsingFailedException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataPayloadParsingFailedException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataPayloadParsingFailedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
