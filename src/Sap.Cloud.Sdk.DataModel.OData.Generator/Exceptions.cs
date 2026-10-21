namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>Base class of the failures of the OData code generator.</summary>
/// <remarks>Port of <c>ODataGeneratorException</c>.</remarks>
public class ODataGeneratorException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorException"/> class.</summary>
    public ODataGeneratorException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorException"/> class.</summary>
    /// <param name="message">The message.</param>
    public ODataGeneratorException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataGeneratorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The input of the generator (metadata, swagger or mapping files) cannot be read or is invalid.</summary>
/// <remarks>Port of <c>ODataGeneratorReadException</c>.</remarks>
public class ODataGeneratorReadException : ODataGeneratorException
{
    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorReadException"/> class.</summary>
    public ODataGeneratorReadException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorReadException"/> class.</summary>
    /// <param name="message">The message.</param>
    public ODataGeneratorReadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorReadException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataGeneratorReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The generated files cannot be written, for example because a file exists and may not be overwritten.</summary>
/// <remarks>Port of <c>ODataGeneratorWriteException</c>.</remarks>
public class ODataGeneratorWriteException : ODataGeneratorException
{
    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorWriteException"/> class.</summary>
    public ODataGeneratorWriteException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorWriteException"/> class.</summary>
    /// <param name="message">The message.</param>
    public ODataGeneratorWriteException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataGeneratorWriteException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataGeneratorWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
