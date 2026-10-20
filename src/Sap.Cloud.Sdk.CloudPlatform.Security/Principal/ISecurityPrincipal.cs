namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal;

/// <summary>The user or technical client on whose behalf the current request is processed.</summary>
/// <remarks>
/// Named <c>Principal</c> in the Java SDK; renamed to avoid a clash with
/// <see cref="System.Security.Principal.IPrincipal"/>.
/// </remarks>
public interface ISecurityPrincipal
{
    /// <summary>The identifier of the principal (user name, user ID or client ID).</summary>
    string PrincipalId { get; }
}

/// <summary>Default implementation of <see cref="ISecurityPrincipal"/>.</summary>
/// <param name="PrincipalId">The identifier of the principal.</param>
public sealed record DefaultPrincipal(string PrincipalId) : ISecurityPrincipal;
