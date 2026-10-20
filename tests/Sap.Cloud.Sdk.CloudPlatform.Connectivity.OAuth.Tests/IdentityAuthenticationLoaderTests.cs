using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth.Tests;

public sealed class IdentityAuthenticationLoaderTests : OAuthTestBase
{
    private sealed class RecordingLoader : IServiceBindingDestinationLoader
    {
        public ServiceBindingDestinationOptions? Options { get; private set; }

        public Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options)
        {
            Options = options;
            return Result.Success<IHttpDestination>(DefaultHttpDestination.CreateBuilder("https://result").Build());
        }
    }

    private static Dictionary<string, object?> IasBackedCredentials(params (string Name, Dictionary<string, object?> Entry)[] endpoints) => Map(
        ("authentication-service", Map(("service-label", "identity"))),
        ("endpoints", endpoints.ToDictionary(e => e.Name, e => (object?)e.Entry)));

    [Fact]
    public void SingleHttpEndpoint_IsUsedAsTarget()
    {
        // The application's own IAS binding, which the loader looks up for the token exchange.
        Sap.Cloud.Sdk.CloudPlatform.ServiceBindings.DefaultServiceBindingAccessor.Instance =
            new Sap.Cloud.Sdk.CloudPlatform.ServiceBindings.CombiningServiceBindingAccessor(
                new FixedBindings(Binding("identity", Map(("clientid", "app"), ("clientsecret", "s"), ("url", "https://p.accounts.ondemand.com"), ("app_tid", "t")))));
        var delegateLoader = new RecordingLoader();
        var loader = new IdentityAuthenticationServiceBindingDestinationLoader(delegateLoader);
        var credentials = IasBackedCredentials(
            ("api", Map(("uri", "https://reuse.example.com/api"), ("always-requires-token", false))),
            ("grpc", Map(("uri", "grpc://x"), ("protocol", "grpc"))));

        var result = loader.TryGetDestination(Options(Binding("reuse-service", credentials), OnBehalfOf.NamedUserCurrentTenant));

        Assert.True(result.IsSuccess, result.Exception?.ToString());
        Assert.True(delegateLoader.Options!.TryGetOption<BtpServiceOptions.AuthenticationServiceOptions.TargetUri, Uri>(out var target));
        Assert.Equal("https://reuse.example.com/api", target.AbsoluteUri);
        Assert.True(delegateLoader.Options.TryGetOption<BtpServiceOptions.IasOptions.NoTokenForTechnicalProviderUser, bool>(out var noToken) && noToken);
        Assert.Equal(OnBehalfOf.NamedUserCurrentTenant, delegateLoader.Options.OnBehalfOf);
    }

    [Fact]
    public void NotBackedByIas_IsNotFound()
    {
        var loader = new IdentityAuthenticationServiceBindingDestinationLoader(new RecordingLoader());
        Assert.IsType<DestinationNotFoundException>(loader.TryGetDestination(Options(Binding("x", Map(("clientid", "a"))))).Exception);
    }

    [Fact]
    public void MultipleHttpEndpoints_Fail()
    {
        var loader = new IdentityAuthenticationServiceBindingDestinationLoader(new RecordingLoader());
        var credentials = IasBackedCredentials(("a", Map(("uri", "https://a"))), ("b", Map(("uri", "https://b"))));
        Assert.IsType<DestinationAccessException>(loader.TryGetDestination(Options(Binding("x", credentials))).Exception);
    }

    [Fact]
    public void WrongAttributeType_Fails()
    {
        var loader = new IdentityAuthenticationServiceBindingDestinationLoader(new RecordingLoader());
        var credentials = IasBackedCredentials(("a", Map(("uri", "https://a"), ("requires-mtls", "yes"))));
        var e = Assert.IsType<DestinationAccessException>(loader.TryGetDestination(Options(Binding("x", credentials))).Exception);
        Assert.Contains("requires-mtls", e.InnerException!.Message, StringComparison.Ordinal);
    }

    private sealed class FixedBindings(params Sap.Cloud.Sdk.CloudPlatform.ServiceBindings.IServiceBinding[] bindings)
        : Sap.Cloud.Sdk.CloudPlatform.ServiceBindings.IServiceBindingAccessor
    {
        public IReadOnlyList<Sap.Cloud.Sdk.CloudPlatform.ServiceBindings.IServiceBinding> GetServiceBindings() => bindings;
    }
}
