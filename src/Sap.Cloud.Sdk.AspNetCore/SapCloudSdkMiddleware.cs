using Microsoft.AspNetCore.Http;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.AspNetCore;

/// <summary>
/// Runs each request in a fresh <see cref="IThreadContext"/> that holds the request's headers, so that
/// <c>TenantAccessor</c>, <c>PrincipalAccessor</c>, <c>AuthTokenAccessor</c> and <c>RequestHeaderAccessor</c> work
/// anywhere in the request pipeline, including in background tasks started from it.
/// </summary>
/// <remarks>
/// Replaces the Java SDK's servlet filter. Exceptions from the pipeline propagate unchanged.
/// </remarks>
/// <param name="next">The next middleware.</param>
public sealed class SapCloudSdkMiddleware(RequestDelegate next)
{
    /// <summary>Processes a request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the request has been processed.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var headers = DefaultRequestHeaderContainer.FromMultiValueMap(
            context.Request.Headers.Select(h => KeyValuePair.Create(h.Key, (IEnumerable<string?>?)h.Value)));

        return ThreadContextExecutor.FromNewContext()
            .WithListeners(new RequestHeaderThreadContextListener(headers))
            .WithoutExceptionWrapping()
            .ExecuteAsync(() => next(context));
    }
}
