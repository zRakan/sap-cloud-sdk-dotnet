using System.Net;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>
/// The HTTP requests to the destination service (port of <c>DestinationServiceAdapterTest</c>): paths, headers, error mapping
/// and the destination that reaches the service.
/// </summary>
public sealed class DestinationServiceAdapterTests : DestinationServiceTestBase
{
    private const string DestinationResponse = "{ response }";
    private const string ServiceBaseUrl = "https://destination.example.com/destination-service";

    private readonly RecordingHandler _handler = new();

    private sealed record Recorded(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(DestinationResponse) };

        public List<Recorded> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new Recorded(
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
            return Task.FromResult(Respond(request));
        }
    }

    private sealed class StaticHeaderProvider(Header header) : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) => [header];
    }

    private static DefaultHttpDestination ServiceDestination(OnBehalfOf behalf) =>
        DefaultHttpDestination.CreateBuilder(ServiceBaseUrl)
            .Name("destination-service-" + behalf)
            .HeaderProviders(new StaticHeaderProvider(new Header("Authorization", "Bearer service-token-" + behalf)))
            .Build();

    private DestinationServiceAdapter CreateAdapter(string? providerTenantId = null) =>
        new(
            (behalf, _) => Task.FromResult<IHttpDestination>(ServiceDestination(behalf)),
            () => throw new NoServiceBindingException(),
            providerTenantId,
            destination => new HttpClient(new DestinationRequestHandler(destination) { InnerHandler = _handler }, disposeHandler: false)
            {
                BaseAddress = destination.Uri,
            });

    private static DefaultServiceBinding Binding(string service, params (string Key, object? Value)[] credentials) =>
        new("name", service, "plan", [], credentials.ToDictionary(c => c.Key, c => c.Value));

    private sealed class FixedBindings(params IServiceBinding[] bindings) : IServiceBindingAccessor
    {
        public IReadOnlyList<IServiceBinding> GetServiceBindings() => bindings;
    }

    [Fact]
    public async Task WithoutToken_SendsOnlyTheAuthorizationOfTheServiceDestination()
    {
        var response = await CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            default);

        Assert.Equal(DestinationResponse, response);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://destination.example.com/destination-service/destination-configuration/v1/destinations/test",
            request.Uri.AbsoluteUri);
        Assert.Equal("Bearer service-token-TechnicalUserProvider", request.Headers["Authorization"]);
        Assert.False(request.Headers.ContainsKey("x-user-token"));
        Assert.False(request.Headers.ContainsKey("x-refresh-token"));
    }

    [Fact]
    public async Task EachIdentity_UsesItsOwnServiceDestination()
    {
        var adapter = CreateAdapter();

        await adapter.GetConfigurationAsJsonAsync("/v1/destinations/a", DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.NamedUserCurrentTenant), default);
        await adapter.GetConfigurationAsJsonAsync("/v1/destinations/a", DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant), default);

        Assert.Equal(
            ["Bearer service-token-NamedUserCurrentTenant", "Bearer service-token-TechnicalUserCurrentTenant"],
            _handler.Requests.Select(r => r.Headers["Authorization"]));
    }

    [Fact]
    public async Task UserTokenForwarding_SendsTheTokenInAHeader()
    {
        const string token = "the.user.token";

        await CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserCurrentTenant, token),
            default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(token, request.Headers["x-user-token"]);
        Assert.Equal("Bearer service-token-TechnicalUserCurrentTenant", request.Headers["Authorization"]);
        Assert.False(request.Headers.ContainsKey("x-refresh-token"));
    }

    [Fact]
    public async Task RefreshTokenFlow_SendsTheRefreshTokenInAHeader()
    {
        await CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithRefreshToken(OnBehalfOf.TechnicalUserCurrentTenant, "refreshToken"),
            default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("refreshToken", request.Headers["x-refresh-token"]);
        Assert.False(request.Headers.ContainsKey("x-user-token"));
    }

    [Fact]
    public async Task FragmentName_IsSentInAHeader()
    {
        await CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant).WithFragmentName("my-fragment"),
            default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("my-fragment", request.Headers["x-fragment-name"]);
        Assert.False(request.Headers.ContainsKey("x-user-token"));
    }

    [Fact]
    public async Task CustomHeaders_AreSent()
    {
        await CreateAdapter().GetConfigurationAsJsonAsync(
            "/v2/destinations/test@instance",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant)
                .WithAdditionalHeaders([new Header("X-Custom-Header-1", "value1"), new Header("X-Custom-Header-2", "value2")]),
            default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("value1", request.Headers["X-Custom-Header-1"]);
        Assert.Equal("value2", request.Headers["X-Custom-Header-2"]);
        Assert.EndsWith("/destination-configuration/v2/destinations/test@instance", request.Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/v1/subaccountDestinations")]
    [InlineData("/v1/instanceDestinations")]
    public async Task Headers_AreOnlyAddedForSingleDestinationCalls(string path)
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };

        await CreateAdapter().GetConfigurationAsJsonAsync(
            path,
            DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.TechnicalUserCurrentTenant, "some-user-token")
                .WithAdditionalHeaders([new Header("X-Custom-Header", "should-not-appear")]),
            default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("Bearer service-token-TechnicalUserCurrentTenant", request.Headers["Authorization"]);
        Assert.False(request.Headers.ContainsKey("X-Custom-Header"));
        Assert.False(request.Headers.ContainsKey("x-user-token"));
    }

    [Fact]
    public async Task Status404_IsReportedAsDestinationNotFound()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("Nothing here.") };

        var failure = await Assert.ThrowsAsync<DestinationNotFoundException>(() => CreateAdapter().GetConfigurationAsJsonAsync(
            "/service-path",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            default));

        Assert.Contains("Destination could not be found", failure.Message, StringComparison.Ordinal);
        Assert.Contains("/destination-configuration/service-path", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OtherStatusCodes_AreReportedAsDestinationAccessException_WithoutTheBody()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad, evil request") };

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => CreateAdapter().GetConfigurationAsJsonAsync(
            "/service-path",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            default));

        Assert.Contains("status 400", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyBody_IsReportedAsDestinationAccessException()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            default));

        Assert.Contains("no body returned", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionFailures_AreReportedAsDestinationAccessException()
    {
        _handler.Respond = _ => throw new HttpRequestException("connection refused");

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            default));

        Assert.IsType<HttpRequestException>(failure.InnerException);
    }

    [Fact]
    public async Task Cancellation_IsNotWrapped()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateAdapter().GetConfigurationAsJsonAsync(
            "/v1/destinations/test",
            DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider),
            cancelled.Token));
    }

    [Fact]
    public void ProviderTenant_IsReadFromTheServiceBinding()
    {
        var binding = Binding("destination", ("tenantid", "provider-tenant-id"));
        var adapter = new DestinationServiceAdapter(null, () => binding, null);

        Assert.Equal("provider-tenant-id", adapter.GetProviderTenantId());
    }

    [Fact]
    public void ProviderTenant_PrefersTheConfiguredOne()
    {
        var adapter = new DestinationServiceAdapter(null, () => throw new NoServiceBindingException(), "configured");

        Assert.Equal("configured", adapter.GetProviderTenantId());
    }

    [Fact]
    public void ProviderTenant_ThrowsIfItCannotBeDetermined()
    {
        // no service binding
        var noBinding = new DestinationServiceAdapter(null, () => throw new NoServiceBindingException(), null);
        var failure = Assert.Throws<DestinationAccessException>(noBinding.GetProviderTenantId);
        Assert.IsType<NoServiceBindingException>(failure.InnerException);

        // multiple service bindings
        var multiple = new DestinationServiceAdapter(null, () => throw new MultipleServiceBindingsException(), null);
        failure = Assert.Throws<DestinationAccessException>(multiple.GetProviderTenantId);
        Assert.IsType<MultipleServiceBindingsException>(failure.InnerException);

        // service binding without tenant id
        var withoutTenant = new DestinationServiceAdapter(null, () => Binding("destination", ("clientid", "x")), null);
        failure = Assert.Throws<DestinationAccessException>(withoutTenant.GetProviderTenantId);
        Assert.Equal(
            "The provider tenant id is not defined in the service binding. "
            + "Please verify that the service binding contains the field 'tenantid' in the credentials list.",
            failure.Message);
    }

    [Fact]
    public void DestinationServiceBinding_IsFoundByServiceIdentifier()
    {
        var destination = Binding("destination");
        DefaultServiceBindingAccessor.Instance = new FixedBindings(Binding("xsuaa"), destination, new DefaultServiceBinding("n", null, null, [], new Dictionary<string, object?>()));

        Assert.Same(destination, DestinationServiceAdapter.GetDestinationServiceBinding());
    }

    [Fact]
    public void DestinationServiceBinding_ThrowsWithoutOrWithMultipleBindings()
    {
        DefaultServiceBindingAccessor.Instance = new FixedBindings(Binding("xsuaa"));
        Assert.IsType<NoServiceBindingException>(Record.Exception(DestinationServiceAdapter.GetDestinationServiceBinding));

        DefaultServiceBindingAccessor.Instance = new FixedBindings();
        Assert.IsType<NoServiceBindingException>(Record.Exception(DestinationServiceAdapter.GetDestinationServiceBinding));

        DefaultServiceBindingAccessor.Instance = new FixedBindings(Binding("destination"), Binding("destination"));
        Assert.IsType<MultipleServiceBindingsException>(Record.Exception(DestinationServiceAdapter.GetDestinationServiceBinding));
    }

    [Fact]
    public void NoServiceBindingExceptions_AreCloudPlatformExceptions()
    {
        Assert.IsAssignableFrom<CloudPlatformException>(new NoServiceBindingException("x"));
        Assert.IsAssignableFrom<CloudPlatformException>(new MultipleServiceBindingsException("x"));
    }

    private sealed class RecordingBindingLoader(Func<ServiceBindingDestinationOptions, Result<IHttpDestination>> respond)
        : IServiceBindingDestinationLoader
    {
        public List<ServiceBindingDestinationOptions> Options { get; } = [];

        public Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options)
        {
            Options.Add(options);
            return respond(options);
        }
    }

    [Fact]
    public async Task ServiceDestination_IsCreatedFromTheBinding_PerIdentity_AndRemembered()
    {
        SaveServiceBindingLoaders();
        ServiceBindingDestinationLoader.ClearDefaultLoaders();
        var loader = new RecordingBindingLoader(options => Result.Success<IHttpDestination>(ServiceDestination(options.OnBehalfOf)));
        ServiceBindingDestinationLoader.RegisterDefaultLoader(loader);
        var binding = Binding("destination", ("tenantid", "t"));
        var adapter = new DestinationServiceAdapter(null, () => binding, null, destination => new HttpClient(_handler, disposeHandler: false) { BaseAddress = destination.Uri });

        await adapter.GetConfigurationAsJsonAsync("/v1/destinations/a", DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider), default);
        await adapter.GetConfigurationAsJsonAsync("/v1/destinations/b", DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserProvider), default);
        await adapter.GetConfigurationAsJsonAsync("/v1/destinations/a", DestinationRetrievalStrategy.WithUserToken(OnBehalfOf.NamedUserCurrentTenant, "token"), default);

        Assert.Equal([OnBehalfOf.TechnicalUserProvider, OnBehalfOf.NamedUserCurrentTenant], loader.Options.Select(o => o.OnBehalfOf));
        Assert.All(loader.Options, o => Assert.Same(binding, o.ServiceBinding));
    }

    [Fact]
    public async Task ServiceDestination_FailuresAreNotRemembered()
    {
        SaveServiceBindingLoaders();
        ServiceBindingDestinationLoader.ClearDefaultLoaders();
        var attempts = 0;
        var loader = new RecordingBindingLoader(options => ++attempts == 1
            ? Result.Failure<IHttpDestination>(new DestinationAccessException("no credentials"))
            : Result.Success<IHttpDestination>(ServiceDestination(options.OnBehalfOf)));
        ServiceBindingDestinationLoader.RegisterDefaultLoader(loader);
        var binding = Binding("destination");
        var adapter = new DestinationServiceAdapter(null, () => binding, null, destination => new HttpClient(_handler, disposeHandler: false) { BaseAddress = destination.Uri });
        var strategy = DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant);

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => adapter.GetConfigurationAsJsonAsync("/v1/destinations/a", strategy, default));
        Assert.Contains("on behalf of TechnicalUserCurrentTenant", failure.Message, StringComparison.Ordinal);

        Assert.Equal(DestinationResponse, await adapter.GetConfigurationAsJsonAsync("/v1/destinations/a", strategy, default));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ServiceDestination_WithoutBindingOrLoaders_FailsWithDestinationAccessException()
    {
        SaveServiceBindingLoaders();
        ServiceBindingDestinationLoader.ClearDefaultLoaders();
        var strategy = DestinationRetrievalStrategy.WithoutToken(OnBehalfOf.TechnicalUserCurrentTenant);

        var noBinding = new DestinationServiceAdapter(null, () => throw new NoServiceBindingException("bind it"), null);
        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => noBinding.GetConfigurationAsJsonAsync("/v1/destinations/a", strategy, default));
        Assert.IsType<NoServiceBindingException>(failure.InnerException);

        var noLoaders = new DestinationServiceAdapter(null, () => Binding("destination"), null);
        failure = await Assert.ThrowsAsync<DestinationAccessException>(() => noLoaders.GetConfigurationAsJsonAsync("/v1/destinations/a", strategy, default));
        Assert.Contains("on behalf of TechnicalUserCurrentTenant", failure.Message, StringComparison.Ordinal);
    }
}
