using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Sap.Cloud.Sdk.CloudPlatform.Security.Principal;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.AspNetCore.Tests;

public sealed class SapCloudSdkMiddlewareTests : StaticStateReset, IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web => web.UseTestServer().Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    // Runs outside the SDK middleware: sees the original exception type.
                    try
                    {
                        await next(context);
                    }
                    catch (InvalidOperationException e)
                    {
                        context.Response.StatusCode = 418;
                        await context.Response.WriteAsync(e.GetType().Name);
                    }
                });
                app.UseSapCloudSdk();
                app.Run(async context =>
                {
                    var response = context.Request.Path.Value switch
                    {
                        "/tenant" => TenantAccessor.GetCurrentTenant().TenantId,
                        "/tenant-in-background" => await Task.Run(() => TenantAccessor.GetCurrentTenant().TenantId),
                        "/principal" => PrincipalAccessor.GetCurrentPrincipal().PrincipalId,
                        "/throw" => throw new InvalidOperationException(),
                        _ => "unknown",
                    };
                    await context.Response.WriteAsync(response);
                });
            }))
            .StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private Task<HttpResponseMessage> GetAsync(string path, string tenantId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(new
        {
            zid = tenantId,
            iss = $"https://{tenantId}-sub.authentication.eu10.hana.ondemand.com",
            grant_type = "password",
            user_name = $"user-of-{tenantId}",
        }));
        return _client.SendAsync(request);
    }

    [Theory]
    [InlineData("/tenant", "tenant-a")]
    [InlineData("/tenant-in-background", "tenant-a")]
    [InlineData("/principal", "user-of-tenant-a")]
    public async Task AccessorsWorkInRequestPipeline(string path, string expected)
    {
        using var response = await GetAsync(path, "tenant-a");
        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ConcurrentRequests_AreIsolated()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 25).Select(async i =>
        {
            using var response = await GetAsync("/tenant-in-background", $"t{i}");
            return await response.Content.ReadAsStringAsync();
        }));
        Assert.Equal(Enumerable.Range(0, 25).Select(i => $"t{i}"), responses);
    }

    [Fact]
    public async Task Exceptions_PropagateUnwrapped()
    {
        using var response = await GetAsync("/throw", "tenant-a");
        Assert.Equal((HttpStatusCode)418, response.StatusCode);
        Assert.Equal(nameof(InvalidOperationException), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ContextDoesNotLeakOutOfRequest()
    {
        using var response = await GetAsync("/tenant", "tenant-a");
        Assert.Null(ThreadContextAccessor.GetCurrentContextOrNull());
    }
}
