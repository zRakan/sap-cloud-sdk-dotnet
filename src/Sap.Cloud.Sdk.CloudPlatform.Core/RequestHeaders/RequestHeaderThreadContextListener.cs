using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>Stores the request headers in the <see cref="IThreadContext"/>.</summary>
public class RequestHeaderThreadContextListener : IThreadContextListener
{
    /// <summary>The name of the thread context property holding the <see cref="IRequestHeaderContainer"/>.</summary>
    public const string PropertyRequestHeaders =
        "Sap.Cloud.Sdk.CloudPlatform.RequestHeaders.RequestHeaderThreadContextListener:request_headers";

    private readonly IRequestHeaderContainer? _requestHeaders;

    /// <summary>
    /// Creates a listener that copies the headers available before the context switch (if any) into the new context.
    /// </summary>
    public RequestHeaderThreadContextListener()
    {
    }

    /// <summary>Creates a listener that stores the given headers.</summary>
    /// <param name="requestHeaders">The headers.</param>
    public RequestHeaderThreadContextListener(IRequestHeaderContainer requestHeaders)
    {
        ArgumentNullException.ThrowIfNull(requestHeaders);
        _requestHeaders = requestHeaders;
    }

    /// <inheritdoc/>
    public int Priority => ThreadContextListenerPriorities.RequestHeaderListener;

    /// <inheritdoc/>
    public void BeforeInitialize(IThreadContext threadContext)
    {
        ArgumentNullException.ThrowIfNull(threadContext);
        if (_requestHeaders is not null)
        {
            threadContext.SetProperty(PropertyRequestHeaders, ThreadContextProperty.Of(_requestHeaders));
        }
        else
        {
            threadContext.SetPropertyIfAbsent(
                PropertyRequestHeaders,
                () => ThreadContextProperty.Capture(RequestHeaderAccessor.GetHeaderContainer));
        }
    }
}
