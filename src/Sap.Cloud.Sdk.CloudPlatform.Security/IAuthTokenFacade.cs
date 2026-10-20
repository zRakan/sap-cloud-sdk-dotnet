namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>Provides the authorization token of the current request.</summary>
public interface IAuthTokenFacade
{
    /// <summary>Gets the current token.</summary>
    /// <returns>The token, or a failure if none is available.</returns>
    Result<AuthToken> TryGetCurrentToken();
}
