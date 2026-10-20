using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Tenant.Tests;

public class TenantTests : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        AuthTokenAccessor.FallbackToken = null;
        TenantAccessor.TenantFacade = null!;
        TenantAccessor.FallbackTenant = null;
    }

    private static T WithToken<T>(object claims, Func<T> func) =>
        RequestHeaderAccessor.ExecuteWithHeaderContainer(
            DefaultRequestHeaderContainer.Builder().WithHeader("Authorization", "Bearer " + TestJwt.Create(claims)).Build(),
            func);

    private static void SetBindings(params IServiceBinding[] bindings) =>
        DefaultServiceBindingAccessor.Instance = new FixedBindings(bindings);

    private static DefaultServiceBinding Binding(string service, Dictionary<string, object?> credentials) =>
        new(service + "-binding", service, null, [], credentials);

    [Fact]
    public void XsuaaToken_ProvidesTenantIdAndSubdomain()
    {
        var tenant = WithToken(new { zid = "zone-1", iss = "https://consumer.authentication.eu10.hana.ondemand.com/oauth/token" },
            TenantAccessor.GetCurrentTenant);
        Assert.Equal(new DefaultTenant("zone-1", "consumer"), tenant);
    }

    [Fact]
    public void IasToken_ProvidesTenant()
    {
        Assert.Equal("app-tid", WithToken(new { app_tid = "app-tid", iss = "https://sub.accounts.ondemand.com" },
            () => TenantAccessor.GetCurrentTenant().TenantId));
        Assert.Equal("zone-uuid", WithToken(new { zone_uuid = "zone-uuid", iss = "https://sub.accounts.ondemand.com" },
            () => TenantAccessor.GetCurrentTenant().TenantId));
    }

    [Theory]
    [InlineData("""{"iss":"https://sub.example.com"}""")]
    [InlineData("""{"zid":"z"}""")]
    [InlineData("""{"zid":"z","iss":"https://localhost"}""")]
    public void IncompleteToken_FallsThroughToServiceBinding_ThenFails(string claimsJson)
    {
        var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(claimsJson)!;
        var result = WithToken(claims, TenantAccessor.TryGetCurrentTenant);

        var exception = Assert.IsType<TenantAccessException>(result.Exception);
        Assert.Equal(3, Assert.IsType<AggregateException>(exception.InnerException).InnerExceptions.Count);
    }

    [Fact]
    public void XsuaaBinding_ProvidesProviderTenant()
    {
        SetBindings(Binding("xsuaa", new() { ["identityzone"] = "provider", ["identityzoneid"] = "provider-id" }));
        Assert.Equal(new DefaultTenant("provider-id", "provider"), TenantAccessor.GetCurrentTenant());
    }

    [Fact]
    public void XsuaaBinding_TriesTenantIdKeysInOrder()
    {
        SetBindings(Binding("xsuaa", new() { ["identityzone"] = "provider", ["tenantid"] = "t", ["zoneid"] = "z" }));
        Assert.Equal("z", TenantAccessor.GetCurrentTenant().TenantId);
    }

    [Fact]
    public void IasBinding_ProvidesProviderTenant()
    {
        SetBindings(Binding("identity", new() { ["url"] = "https://provider.accounts.ondemand.com", ["app_tid"] = "app" }));
        Assert.Equal(new DefaultTenant("app", "provider"), TenantAccessor.GetCurrentTenant());
    }

    [Fact]
    public void IncompleteBinding_IsSkipped()
    {
        SetBindings(
            Binding("xsuaa", new() { ["identityzoneid"] = "no-subdomain" }),
            Binding("identity", new() { ["url"] = "https://provider.accounts.ondemand.com", ["app_tid"] = "app" }));
        Assert.Equal("app", TenantAccessor.GetCurrentTenant().TenantId);
    }

    [Fact]
    public void Token_WinsOverServiceBinding()
    {
        SetBindings(Binding("xsuaa", new() { ["identityzone"] = "provider", ["identityzoneid"] = "provider-id" }));
        var tenantId = WithToken(new { zid = "consumer-id", iss = "https://consumer.example.com" },
            () => TenantAccessor.GetCurrentTenant().TenantId);
        Assert.Equal("consumer-id", tenantId);
    }

    [Fact]
    public async Task ExecuteWithTenant_FlowsIntoChildTasksAndIsIsolated()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant($"t{i}"), async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                return await Task.Run(() => TenantAccessor.GetCurrentTenant().TenantId);
            })));

        Assert.Equal(Enumerable.Range(0, 20).Select(i => $"t{i}"), results);
        Assert.IsType<TenantAccessException>(TenantAccessor.TryGetCurrentTenant().Exception);
    }

    [Fact]
    public void ExecuteWithTenant_OverridesTokenTenant()
    {
        var tenantId = WithToken(new { zid = "from-token", iss = "https://consumer.example.com" },
            () => TenantAccessor.ExecuteWithTenant(new DefaultTenant("explicit"), () => TenantAccessor.GetCurrentTenant().TenantId));
        Assert.Equal("explicit", tenantId);
    }

    [Fact]
    public void DefaultListeners_CaptureTenantWhenContextIsCreated()
    {
        // Mirrors the Java SDK: a new context resolves the tenant (here from the token) while it is initialized.
        var headers = DefaultRequestHeaderContainer.Builder()
            .WithHeader("Authorization", "Bearer " + TestJwt.Create(new { zid = "z", iss = "https://s.example.com" }))
            .Build();
        var captured = RequestHeaderAccessor.ExecuteWithHeaderContainer(headers, () =>
            ThreadContextAccessor.GetCurrentContext().GetPropertyValue<ITenant>(TenantThreadContextListener.PropertyTenant));
        Assert.Equal("z", captured.GetValueOrThrow().TenantId);
    }

    [Fact]
    public void FallbackTenant()
    {
        Assert.Equal("fb", TenantAccessor.ExecuteWithFallbackTenant(() => new DefaultTenant("fb"), () => TenantAccessor.GetCurrentTenant().TenantId));

        TenantAccessor.FallbackTenant = () => null;
        Assert.IsType<TenantAccessException>(TenantAccessor.TryGetCurrentTenant().Exception);

        TenantAccessor.FallbackTenant = () => new DefaultTenant("global-fallback");
        Assert.Equal("global-fallback", TenantAccessor.GetCurrentTenant().TenantId);
    }

    [Fact]
    public void CustomFacade_IsUsed_ButExecuteWithIsRejected()
    {
        TenantAccessor.TenantFacade = new FixedFacade(new DefaultTenant("custom"));
        Assert.Equal("custom", TenantAccessor.GetCurrentTenant().TenantId);
        Assert.Throws<ThreadContextExecutionException>(() => TenantAccessor.ExecuteWithTenant(new DefaultTenant("x"), () => 1));
    }

    private sealed class FixedFacade(ITenant tenant) : ITenantFacade
    {
        public Result<ITenant> TryGetCurrentTenant() => Result.Success(tenant);
    }

    private sealed class FixedBindings(IReadOnlyList<IServiceBinding> bindings) : IServiceBindingAccessor
    {
        public IReadOnlyList<IServiceBinding> GetServiceBindings() => bindings;
    }
}
