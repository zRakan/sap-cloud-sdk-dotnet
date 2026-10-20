using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Tenant;

/// <summary>
/// Reads the tenant from the current <see cref="IThreadContext"/>, then from the current JWT, then from the XSUAA or
/// IAS service binding (the provider tenant).
/// </summary>
public class DefaultTenantFacade : ITenantFacade
{
    private const string XsuaaJwtZoneId = "zid";
    private const string IasJwtZoneId = "zone_uuid";
    private const string IasJwtAppTid = "app_tid";
    private const string JwtIssuer = "iss";
    private static readonly string[] TenantIdClaims = [XsuaaJwtZoneId, IasJwtAppTid, IasJwtZoneId];

    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultTenantFacade>();

    /// <inheritdoc/>
    public Result<ITenant> TryGetCurrentTenant()
    {
        var fromContext = ThreadContextAccessor.TryGetCurrentContext()
            .Bind(c => c.GetPropertyValue<ITenant>(TenantThreadContextListener.PropertyTenant));
        if (fromContext.IsSuccess)
        {
            return fromContext;
        }

        var failures = new List<Exception> { fromContext.Exception };
        return TryGetTenantFromAuthToken(AuthTokenAccessor.TryGetCurrentToken())
            .OnFailure(failures.Add)
            .OrElse(TryGetTenantFromServiceBinding)
            .OnFailure(failures.Add)
            .OrElse(() => Result.Failure<ITenant>(
                new TenantAccessException("Failed to get current tenant.", new AggregateException(failures))));
    }

    private static Result<ITenant> TryGetTenantFromAuthToken(Result<AuthToken> authToken) =>
        authToken.Map<ITenant>(token => new DefaultTenant(GetTenantId(token), GetSubdomain(GetIssuer(token))));

    private static string GetTenantId(AuthToken token) =>
        TenantIdClaims.Select(token.GetClaimAsString).FirstOrDefault(id => id is not null)
        ?? throw new TenantAccessException(
            $"No tenant identifier (one of these elements [{string.Join(", ", TenantIdClaims)}]) found in JWT.");

    private static string GetIssuer(AuthToken token) =>
        token.GetClaimAsString(JwtIssuer)
        ?? throw new TenantAccessException($"No subdomain ('{JwtIssuer}' element) found in JWT.");

    private static string GetSubdomain(string issuer)
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) || !issuerUri.Host.Contains('.', StringComparison.Ordinal))
        {
            throw new TenantAccessException($"Failed to get subdomain from issuer URI '{issuer}'.");
        }
        return issuerUri.Host.Split('.')[0];
    }

    private static Result<ITenant> TryGetTenantFromServiceBinding()
    {
        var bindings = DefaultServiceBindingAccessor.Instance.GetServiceBindings();
        foreach (var extractor in ServiceBindingTenantExtractor.All)
        {
            foreach (var binding in bindings.Where(b => b.ServiceName == extractor.Service))
            {
                if (Log.IsEnabled(LogLevel.Trace))
                {
                    Log.LogTrace("Trying to extract tenant information from service binding {Binding}.", binding);
                }
                if (extractor.Extract(binding.Credentials) is { } tenant)
                {
                    return Result.Success<ITenant>(tenant);
                }
            }
        }
        return Result.Failure<ITenant>(new CloudPlatformException("Failed to extract tenant from service bindings."));
    }

    internal static ThreadContextExecutor CreateExecutor(ITenant tenant) =>
        ThreadContextExecutor.FromCurrentOrNewContext().WithListeners(new TenantThreadContextListener(tenant));
}
