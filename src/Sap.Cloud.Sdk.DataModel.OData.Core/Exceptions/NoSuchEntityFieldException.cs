namespace Sap.Cloud.Sdk.DataModel.OData.Exceptions;

/// <summary>Thrown when a field that does not exist on an entity or complex object is requested.</summary>
public class NoSuchEntityFieldException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NoSuchEntityFieldException"/> class.</summary>
    public NoSuchEntityFieldException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NoSuchEntityFieldException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public NoSuchEntityFieldException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NoSuchEntityFieldException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public NoSuchEntityFieldException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
