namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>
/// An immutable, case-insensitive collection of the HTTP headers of the incoming request.
/// </summary>
public interface IRequestHeaderContainer
{
    /// <summary>The normalized (trimmed, lower-case) names of all headers.</summary>
    IReadOnlyList<string> HeaderNames { get; }

    /// <summary>Whether the header exists. The name is matched case-insensitively.</summary>
    /// <param name="headerName">The header name.</param>
    /// <returns><see langword="true"/> if the header exists.</returns>
    bool ContainsHeader(string headerName);

    /// <summary>Gets the values of a header. The name is matched case-insensitively.</summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>The values, or an empty list if the header does not exist.</returns>
    IReadOnlyList<string> GetHeaderValues(string headerName);

    /// <summary>Creates a builder initialized with the headers of this container.</summary>
    /// <returns>The builder.</returns>
    RequestHeaderContainerBuilder ToBuilder();
}
