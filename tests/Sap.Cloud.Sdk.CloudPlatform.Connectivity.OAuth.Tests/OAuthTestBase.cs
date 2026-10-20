using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

public abstract class OAuthTestBase : StaticStateReset
{
    protected override void Reset()
    {
        base.Reset();
        AuthTokenAccessor.FallbackToken = null;
        TenantAccessor.TenantFacade = null!;
        TenantAccessor.FallbackTenant = null;
        OAuth2ServiceBindingDestinationLoader.ResetPropertySuppliers();
        OAuth2Service.TokenServiceCache.Clear();
    }

    protected static Dictionary<string, object?> Map(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value);

    protected static IServiceBinding Binding(string service, Dictionary<string, object?> credentials) =>
        new DefaultServiceBinding(service + "-instance", service, "plan", [], credentials);

    protected static ServiceBindingDestinationOptions Options(IServiceBinding binding, OnBehalfOf behalf = OnBehalfOf.TechnicalUserCurrentTenant, params object[] options)
    {
        var builder = ServiceBindingDestinationOptions.ForService(binding).OnBehalfOf(behalf);
        foreach (var option in options)
        {
            // WithOption is generic over the option value type; dispatch dynamically for test brevity.
            typeof(ServiceBindingDestinationOptions.Builder).GetMethod("WithOption")!
                .MakeGenericMethod(option.GetType().GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IOptionsEnhancer<>)).GetGenericArguments()[0])
                .Invoke(builder, [option]);
        }
        return builder.Build();
    }

    private protected static OAuth2HeaderProvider OAuthProvider(IHttpDestination destination) =>
        Assert.IsType<OAuth2HeaderProvider>(Assert.Single(((DefaultHttpDestination)destination).CustomHeaderProviders));

    protected static Dictionary<string, object?> XsuaaCredentials(string clientId = "xsuaa-client") => Map(
        ("clientid", clientId),
        ("clientsecret", "xsuaa-secret"),
        ("url", "https://provider.authentication.eu10.hana.ondemand.com"),
        ("identityzone", "provider"));
}
