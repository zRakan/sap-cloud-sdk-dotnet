using System.Net;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;

/// <summary>Thrown when a request to an OAuth 2.0 token endpoint fails.</summary>
/// <remarks>Port of <c>com.sap.cloud.security.xsuaa.client.OAuth2ServiceException</c>.</remarks>
public class OAuth2ServiceException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="OAuth2ServiceException"/> class.</summary>
    public OAuth2ServiceException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public OAuth2ServiceException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and a cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public OAuth2ServiceException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The HTTP status code returned by the token endpoint, if a response was received.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>The token endpoint.</summary>
    public Uri? TokenEndpoint { get; init; }

    /// <summary>The response body returned by the token endpoint, if any.</summary>
    public string? ResponseBody { get; init; }

    /// <inheritdoc/>
    public override string Message =>
        $"{base.Message} [Server URI: {TokenEndpoint}; HTTP status code: {(int?)StatusCode}; Response body: {ResponseBody}]";
}
