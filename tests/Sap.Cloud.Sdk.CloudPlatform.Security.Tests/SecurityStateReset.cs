using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Tests;

public abstract class SecurityStateReset : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        AuthTokenAccessor.AuthTokenFacade = null!;
        AuthTokenAccessor.FallbackToken = null;
        BasicAuthenticationAccessor.BasicAuthenticationFacade = null!;
        PrincipalAccessor.PrincipalFacade = null!;
        PrincipalAccessor.FallbackPrincipal = null;
    }

    protected static T WithAuthorization<T>(string authorization, Func<T> func) =>
        RequestHeaderAccessor.ExecuteWithHeaderContainer(
            DefaultRequestHeaderContainer.Builder().WithHeader("Authorization", authorization).Build(), func);
}
