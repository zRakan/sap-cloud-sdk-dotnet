using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.AspNetCore;

/// <summary>Adds the SAP Cloud SDK to an ASP.NET Core application.</summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Connects the SDK's logging to the application's <see cref="ILoggerFactory"/> and adds
    /// <see cref="SapCloudSdkMiddleware"/>.
    /// </summary>
    /// <remarks>
    /// Call this after <c>UseAuthentication()</c>. The SDK reads tenant and principal from the <c>Authorization</c>
    /// header without validating the token; requests that reach application code must have been authenticated.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseSapCloudSdk(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.ApplicationServices.GetService<ILoggerFactory>() is { } loggerFactory)
        {
            SdkLogging.LoggerFactory = loggerFactory;
        }

        // Register the default thread context listeners of all SDK modules now, so that every request is set up the
        // same way regardless of which accessor the application happens to touch first.
        RuntimeHelpers.RunModuleConstructor(typeof(AuthTokenAccessor).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(TenantAccessor).Module.ModuleHandle);

        return app.UseMiddleware<SapCloudSdkMiddleware>();
    }
}
