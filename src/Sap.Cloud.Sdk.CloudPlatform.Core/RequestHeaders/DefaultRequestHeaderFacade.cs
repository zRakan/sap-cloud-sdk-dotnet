using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>Reads the request headers from the current <see cref="IThreadContext"/>.</summary>
public class DefaultRequestHeaderFacade : IRequestHeaderFacade
{
    /// <inheritdoc/>
    public Result<IRequestHeaderContainer> TryGetRequestHeaders() =>
        ThreadContextAccessor.TryGetCurrentContext()
            .Bind(c => c.GetPropertyValue<IRequestHeaderContainer>(
                RequestHeaderThreadContextListener.PropertyRequestHeaders));

    internal static ThreadContextExecutor CreateExecutor(IRequestHeaderContainer headers) =>
        ThreadContextExecutor.FromCurrentOrNewContext()
            .WithListeners(new RequestHeaderThreadContextListener(headers));
}
