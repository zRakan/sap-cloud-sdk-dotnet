using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>
/// Reads the token from the current <see cref="IThreadContext"/>, or decodes it from the <c>Authorization</c> header of
/// the current request.
/// </summary>
public class DefaultAuthTokenFacade : IAuthTokenFacade
{
    private readonly IAuthTokenDecoder _tokenDecoder;

    /// <summary>Creates the facade.</summary>
    public DefaultAuthTokenFacade()
        : this(new DefaultAuthTokenDecoder())
    {
    }

    internal DefaultAuthTokenFacade(IAuthTokenDecoder tokenDecoder)
    {
        _tokenDecoder = tokenDecoder;
    }

    /// <inheritdoc/>
    public Result<AuthToken> TryGetCurrentToken()
    {
        var context = ThreadContextAccessor.GetCurrentContextOrNull();
        if (context is not null && context.ContainsProperty(AuthTokenThreadContextListener.PropertyAuthToken))
        {
            return context.GetPropertyValue<AuthToken>(AuthTokenThreadContextListener.PropertyAuthToken);
        }
        return RequestHeaderAccessor.TryGetHeaderContainer().Bind(_tokenDecoder.Decode);
    }

    internal static ThreadContextExecutor CreateExecutor(AuthToken authToken) =>
        ThreadContextExecutor.FromCurrentOrNewContext().WithListeners(new AuthTokenThreadContextListener(authToken));
}
