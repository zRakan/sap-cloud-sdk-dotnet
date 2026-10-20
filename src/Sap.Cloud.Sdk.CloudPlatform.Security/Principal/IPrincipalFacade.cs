namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal;

/// <summary>Provides the principal of the current request.</summary>
public interface IPrincipalFacade
{
    /// <summary>Gets the current principal.</summary>
    /// <returns>The principal, or a failure.</returns>
    Result<ISecurityPrincipal> TryGetCurrentPrincipal();
}
