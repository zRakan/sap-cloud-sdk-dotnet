using Sap.Cloud.Sdk.CloudPlatform.Security.Principal.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Security.Principal;

/// <summary>
/// Reads the principal from the current <see cref="IThreadContext"/>, then from the current JWT (XSUAA, then IAS),
/// then from Basic Authentication credentials.
/// </summary>
public class DefaultPrincipalFacade : IPrincipalFacade
{
    private readonly IReadOnlyList<IPrincipalExtractor> _extractors;

    /// <summary>Creates the facade with the default extractors.</summary>
    public DefaultPrincipalFacade()
        : this([new OAuth2AuthTokenPrincipalExtractor(), new OidcAuthTokenPrincipalExtractor(), new BasicCredentialsPrincipalExtractor()])
    {
    }

    internal DefaultPrincipalFacade(IReadOnlyList<IPrincipalExtractor> extractors)
    {
        _extractors = extractors;
    }

    /// <inheritdoc/>
    public Result<ISecurityPrincipal> TryGetCurrentPrincipal()
    {
        var fromContext = ThreadContextAccessor.TryGetCurrentContext()
            .Bind(c => c.GetPropertyValue<ISecurityPrincipal>(PrincipalThreadContextListener.PropertyPrincipal));
        if (fromContext.IsSuccess)
        {
            return fromContext;
        }

        var failures = new List<Exception> { fromContext.Exception };
        foreach (var extractor in _extractors)
        {
            var principal = extractor.TryGetCurrentPrincipal();
            if (principal.IsSuccess)
            {
                return principal;
            }
            failures.Add(principal.Exception);
        }
        return Result.Failure<ISecurityPrincipal>(new PrincipalAccessException(
            "Could not read a principal from thread context, JWT, nor Basic Auth header.",
            new AggregateException(failures)));
    }

    internal static ThreadContextExecutor CreateExecutor(ISecurityPrincipal principal) =>
        ThreadContextExecutor.FromCurrentOrNewContext().WithListeners(new PrincipalThreadContextListener(principal));
}
