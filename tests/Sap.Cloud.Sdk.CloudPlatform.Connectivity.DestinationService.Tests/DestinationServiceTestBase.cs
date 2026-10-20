using Microsoft.IdentityModel.JsonWebTokens;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>
/// Restores all static state the destination service touches. The "current" tenant, principal and token of a test are plain
/// settable properties that the accessors' fallbacks read, so that tests can switch them in the middle of a test.
/// </summary>
public abstract class DestinationServiceTestBase : StaticStateReset
{
    private IReadOnlyList<IServiceBindingDestinationLoader>? _savedServiceBindingLoaders;

    protected DestinationServiceTestBase()
    {
        TenantAccessor.FallbackTenant = () => CurrentTenant;
        PrincipalAccessor.FallbackPrincipal = () => CurrentPrincipal;
        AuthTokenAccessor.AuthTokenFacade = new CurrentTokenFacade(() => CurrentToken);
    }

    protected ITenant? CurrentTenant { get; set; }

    protected ISecurityPrincipal? CurrentPrincipal { get; set; }

    protected AuthToken? CurrentToken { get; set; }

    protected override void Reset()
    {
        base.Reset();
        TenantAccessor.TenantFacade = null!;
        TenantAccessor.FallbackTenant = null;
        PrincipalAccessor.PrincipalFacade = null!;
        PrincipalAccessor.FallbackPrincipal = null;
        AuthTokenAccessor.AuthTokenFacade = null!;
        AuthTokenAccessor.FallbackToken = null;
        DestinationService.Cache.Reset();
        TransparentProxy.Reset();
        TransparentProxy.NetworkVerifier = new NetworkVerifier();
        TransparentProxy.HttpClientSource = destination => HttpClientAccessor.GetHttpClient(destination);
        ResilienceDecorator.DecorationStrategy = null!;
        DestinationAccessor.Loader = null!;
        if (_savedServiceBindingLoaders is not null)
        {
            ServiceBindingDestinationLoader.ClearDefaultLoaders();
            foreach (var loader in _savedServiceBindingLoaders)
            {
                ServiceBindingDestinationLoader.RegisterDefaultLoader(loader);
            }
        }
    }

    /// <summary>Remembers the registered service binding loaders, so that <see cref="Reset"/> restores them.</summary>
    protected void SaveServiceBindingLoaders() => _savedServiceBindingLoaders ??= [.. ServiceBindingDestinationLoader.DefaultLoaders];

    /// <summary>Creates a token that the SDK treats like one issued by XSUAA.</summary>
    protected static AuthToken XsuaaToken(string? zid = null, string? user = null) =>
        new(new JsonWebToken(TestJwt.Create(new
        {
            ext_attr = new { enhancer = "XSUAA" },
            zid,
            user_name = user,
            jti = Guid.NewGuid().ToString(),
        })));

    /// <summary>Creates a token that does not originate from XSUAA.</summary>
    protected static AuthToken PlainToken() =>
        new(new JsonWebToken(TestJwt.Create(new { jti = Guid.NewGuid().ToString() })));

    /// <summary>Creates a destination service that talks to the adapter, with its own resilience state and no time limit.</summary>
    private protected static DestinationService CreateService(IDestinationServiceAdapter adapter, bool timeLimiter = false) =>
        new(
            adapter,
            DestinationService.CreateResilienceConfiguration(
                "singleDestResilience" + Guid.NewGuid(),
                timeLimiter ? DestinationService.DefaultTimeLimiter : ResilienceConfiguration.TimeLimiterConfiguration.Disabled(),
                DestinationService.DefaultSingleDestCircuitBreaker),
            DestinationService.CreateResilienceConfiguration(
                "allDestResilience" + Guid.NewGuid(),
                timeLimiter ? DestinationService.DefaultTimeLimiter : ResilienceConfiguration.TimeLimiterConfiguration.Disabled(),
                DestinationService.DefaultAllDestCircuitBreaker));

    protected static DestinationOptions Options(params IDestinationOptionsAugmenter[] augmenters)
    {
        var builder = DestinationOptions.CreateBuilder();
        foreach (var augmenter in augmenters)
        {
            builder.AugmentBuilder(augmenter);
        }
        return builder.Build();
    }

    protected static DestinationServiceOptionsAugmenter Augmenter() => DestinationServiceOptionsAugmenter.Augmenter();

    protected static DestinationOptions NoOptions => DestinationOptions.CreateBuilder().Build();
}

/// <summary>Provides the token a test has set, or fails like the default facade does.</summary>
internal sealed class CurrentTokenFacade(Func<AuthToken?> token) : IAuthTokenFacade
{
    public Result<AuthToken> TryGetCurrentToken() =>
        token() is { } current
            ? Result.Success(current)
            : Result.Failure<AuthToken>(new AuthTokenAccessException("No token has been set for this test."));
}
