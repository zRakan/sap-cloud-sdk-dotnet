using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>Stores the <see cref="AuthToken"/> in the <see cref="IThreadContext"/>.</summary>
public class AuthTokenThreadContextListener : IThreadContextListener
{
    /// <summary>The name of the thread context property holding the <see cref="AuthToken"/>.</summary>
    public const string PropertyAuthToken =
        "Sap.Cloud.Sdk.CloudPlatform.Security.AuthTokenThreadContextListener:authToken";

    private readonly AuthToken? _authToken;

    /// <summary>Creates a listener that resolves the current token when the context is initialized.</summary>
    public AuthTokenThreadContextListener()
    {
    }

    /// <summary>Creates a listener that stores the given token.</summary>
    /// <param name="authToken">The token.</param>
    public AuthTokenThreadContextListener(AuthToken authToken)
    {
        ArgumentNullException.ThrowIfNull(authToken);
        _authToken = authToken;
    }

    /// <inheritdoc/>
    public int Priority => ThreadContextListenerPriorities.AuthTokenListener;

    /// <inheritdoc/>
    public void AfterInitialize(IThreadContext threadContext)
    {
        ArgumentNullException.ThrowIfNull(threadContext);
        if (_authToken is not null)
        {
            threadContext.SetProperty(PropertyAuthToken, ThreadContextProperty.OfConfidential(_authToken));
        }
        else
        {
            threadContext.SetPropertyIfAbsent(
                PropertyAuthToken,
                () => ThreadContextProperty.CaptureConfidential(AuthTokenAccessor.GetCurrentToken));
        }
    }
}
