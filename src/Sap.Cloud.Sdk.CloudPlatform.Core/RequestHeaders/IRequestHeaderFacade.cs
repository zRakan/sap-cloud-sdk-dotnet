namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>Provides the headers of the current request.</summary>
public interface IRequestHeaderFacade
{
    /// <summary>Gets the headers of the current request.</summary>
    /// <returns>The headers, or a failure if they are not available.</returns>
    Result<IRequestHeaderContainer> TryGetRequestHeaders();
}
