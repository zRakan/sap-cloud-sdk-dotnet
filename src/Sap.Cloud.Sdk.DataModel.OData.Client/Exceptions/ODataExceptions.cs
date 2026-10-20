using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;

/// <summary>Base class of all exceptions raised by the OData client.</summary>
/// <remarks>Java: extends <c>IllegalStateException</c>; here it extends <see cref="InvalidOperationException"/>.</remarks>
public class ODataException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="ODataException"/> class.</summary>
    /// <param name="request">The request that caused the exception.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    public ODataException(ODataRequestGeneric? request, string? message, Exception? cause)
        : base(message, cause)
    {
        Request = request;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataException"/> class.</summary>
    public ODataException()
        : this(null, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataException(string? message)
        : this(null, message, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataException(string? message, Exception? innerException)
        : this(null, message, innerException)
    {
    }

    /// <summary>The request that caused the exception.</summary>
    public ODataRequestGeneric? Request { get; }
}

/// <summary>Thrown when an OData request cannot be built or executed.</summary>
public class ODataRequestException : ODataException
{
    /// <summary>Initializes a new instance of the <see cref="ODataRequestException"/> class.</summary>
    /// <param name="request">The request.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    public ODataRequestException(ODataRequestGeneric? request, string? message, Exception? cause)
        : base(request, message, cause)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestException"/> class.</summary>
    public ODataRequestException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataRequestException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataRequestException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataRequestException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when the HTTP request could not be sent (connection problems).</summary>
public class ODataConnectionException : ODataRequestException
{
    /// <summary>Initializes a new instance of the <see cref="ODataConnectionException"/> class.</summary>
    /// <param name="request">The OData request.</param>
    /// <param name="httpRequest">The HTTP request that was attempted.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    public ODataConnectionException(
        ODataRequestGeneric? request,
        HttpRequestMessage? httpRequest,
        string? message,
        Exception? cause)
        : base(request, message, cause)
    {
        HttpRequest = httpRequest;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataConnectionException"/> class.</summary>
    public ODataConnectionException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataConnectionException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataConnectionException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataConnectionException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataConnectionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The HTTP request that was attempted.</summary>
    public HttpRequestMessage? HttpRequest { get; }
}

/// <summary>Thrown when a request payload cannot be serialized.</summary>
public class ODataSerializationException : ODataRequestException
{
    /// <summary>Initializes a new instance of the <see cref="ODataSerializationException"/> class.</summary>
    /// <param name="request">The request.</param>
    /// <param name="nonSerializableObject">The object that could not be serialized.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    public ODataSerializationException(
        ODataRequestGeneric? request,
        object? nonSerializableObject,
        string? message,
        Exception? cause)
        : base(request, message, cause)
    {
        NonSerializableObject = nonSerializableObject;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataSerializationException"/> class.</summary>
    public ODataSerializationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataSerializationException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataSerializationException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataSerializationException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataSerializationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The object that could not be serialized.</summary>
    public object? NonSerializableObject { get; }
}

/// <summary>Thrown when the HTTP response indicates a problem or cannot be interpreted.</summary>
public class ODataResponseException : ODataException
{
    /// <summary>Initializes a new instance of the <see cref="ODataResponseException"/> class.</summary>
    /// <param name="request">The request.</param>
    /// <param name="httpResponse">The HTTP response.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    public ODataResponseException(
        ODataRequestGeneric? request,
        ODataHttpResponse httpResponse,
        string? message,
        Exception? cause)
        : base(request, message, cause)
    {
        ArgumentNullException.ThrowIfNull(httpResponse);
        HttpCode = httpResponse.StatusCode;
        HttpHeaders = httpResponse.Headers.ToList();
        HttpBody = httpResponse.ReadBufferedText();
    }

    /// <summary>Initializes a new instance of the <see cref="ODataResponseException"/> class.</summary>
    public ODataResponseException()
    {
        HttpHeaders = [];
    }

    /// <summary>Initializes a new instance of the <see cref="ODataResponseException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataResponseException(string? message)
        : base(message)
    {
        HttpHeaders = [];
    }

    /// <summary>Initializes a new instance of the <see cref="ODataResponseException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataResponseException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        HttpHeaders = [];
    }

    /// <summary>The HTTP status code.</summary>
    public int HttpCode { get; }

    /// <summary>The HTTP response headers.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> HttpHeaders { get; }

    /// <summary>The HTTP response body, if it could be read.</summary>
    public string? HttpBody { get; }
}

/// <summary>Thrown when the response payload cannot be deserialized.</summary>
public class ODataDeserializationException : ODataResponseException
{
    /// <summary>Initializes a new instance of the <see cref="ODataDeserializationException"/> class.</summary>
    /// <param name="request">The request.</param>
    /// <param name="httpResponse">The HTTP response.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    public ODataDeserializationException(
        ODataRequestGeneric? request,
        ODataHttpResponse httpResponse,
        string? message,
        Exception? cause)
        : base(request, httpResponse, message, cause)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataDeserializationException"/> class.</summary>
    public ODataDeserializationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataDeserializationException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataDeserializationException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataDeserializationException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataDeserializationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when the OData service responded with an error payload.</summary>
public class ODataServiceErrorException : ODataResponseException
{
    /// <summary>Initializes a new instance of the <see cref="ODataServiceErrorException"/> class.</summary>
    /// <param name="request">The request.</param>
    /// <param name="httpResponse">The HTTP response.</param>
    /// <param name="message">The error message.</param>
    /// <param name="cause">The cause.</param>
    /// <param name="odataError">The error reported by the service.</param>
    public ODataServiceErrorException(
        ODataRequestGeneric? request,
        ODataHttpResponse httpResponse,
        string? message,
        Exception? cause,
        ODataServiceError odataError)
        : base(request, httpResponse, message, cause)
    {
        ODataError = odataError;
    }

    /// <summary>Initializes a new instance of the <see cref="ODataServiceErrorException"/> class.</summary>
    public ODataServiceErrorException()
    {
        ODataError = new ODataServiceError();
    }

    /// <summary>Initializes a new instance of the <see cref="ODataServiceErrorException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public ODataServiceErrorException(string? message)
        : base(message)
    {
        ODataError = new ODataServiceError();
    }

    /// <summary>Initializes a new instance of the <see cref="ODataServiceErrorException"/> class with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ODataServiceErrorException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        ODataError = new ODataServiceError();
    }

    /// <summary>The error reported by the service.</summary>
    public ODataServiceError ODataError { get; }
}
