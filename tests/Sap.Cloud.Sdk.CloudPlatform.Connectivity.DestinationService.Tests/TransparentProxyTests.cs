using System.Net;
using System.Net.Sockets;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;
using Sap.Cloud.Sdk.CloudPlatform.Tenant.Exceptions;
using CrossLevelScope = Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceOptionsAugmenter.CrossLevelScope;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>The destination of a transparent proxy (port of <c>TransparentProxyDestinationTest</c>).</summary>
public sealed class TransparentProxyDestinationTests : DestinationServiceTestBase
{
    private static readonly Uri ValidUri = new("https://www.sap.de");

    [Fact]
    public void Properties_AreDelegated()
    {
        var destination = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString())
            .Property("someKey", "someValue")
            .Build();

        Assert.Equal("someValue", destination.GetProperty("someKey"));
        Assert.Contains("someKey", destination.PropertyNames);
    }

    [Fact]
    public void Uri_IsTheUriOfTheGateway() =>
        Assert.Equal(ValidUri, TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString()).Build().Uri);

    [Fact]
    public void NothingIsConfigured_ExceptTheGateway()
    {
        var destination = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString()).Build();

        Assert.Null(destination.TlsVersion);
        Assert.Null(destination.ProxyConfiguration);
        Assert.Null(destination.KeyStore);
        Assert.Null(destination.KeyStorePassword);
        Assert.False(destination.IsTrustingAllCertificates);
        Assert.Null(destination.BasicCredentials);
        Assert.Equal(AuthenticationType.NoAuthentication, destination.AuthenticationType);
        Assert.Null(destination.ProxyType);
        Assert.Null(destination.TrustStore);
        Assert.Null(destination.TrustStorePassword);
    }

    [Fact]
    public void Headers_ContainTheDestinationName_AndTheCustomHeaders()
    {
        var header1 = new Header("foo", "bar");
        var header2 = new Header("baz", "qux");
        var destination = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString())
            .Header(header1)
            .Header(header2)
            .Build();

        Assert.Equal(
            new[] { header1, header2, new Header(TransparentProxyDestination.DestinationNameHeaderKey, "testDest") }.Select(h => h.Name + h.Value).Order(),
            destination.GetHeaders(ValidUri).Select(h => h.Name + h.Value).Order());
    }

    [Fact]
    public void GatewayHeaders_DescribeTheLevelsAndTheFragment()
    {
        var destination = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString())
            .DestinationLevel(CrossLevelScope.Subaccount)
            .FragmentName("fragName")
            .FragmentLevel(CrossLevelScope.ProviderSubaccount)
            .FragmentOptional(true)
            .Build();

        var headers = destination.GetHeaders(ValidUri);

        Assert.Contains(new Header("x-destination-name", "testDest"), headers);
        Assert.Contains(new Header("x-destination-level", "subaccount"), headers);
        Assert.Contains(new Header("x-fragment-name", "fragName"), headers);
        Assert.Contains(new Header("x-fragment-level", "provider_subaccount"), headers);
        Assert.Contains(new Header("x-fragment-optional", "true"), headers);
    }

    [Fact]
    public void TenantHeaders_AreSentAsConfigured()
    {
        var withId = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).TenantId("tenantId").Build();
        var withSubdomain = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).TenantSubdomain("subdomain").Build();

        Assert.Contains(new Header("x-tenant-id", "tenantId"), withId.GetHeaders(ValidUri));
        Assert.Contains(new Header("x-tenant-subdomain", "subdomain"), withSubdomain.GetHeaders(ValidUri));
    }

    [Fact]
    public void CommonHeaders_HaveTheirNames()
    {
        var destination = TransparentProxyDestination.CreateBuilder(ValidUri.ToString())
            .TokenServiceTenant("tokenTenant")
            .ClientAssertion("clientAssert")
            .ClientAssertionType("clientAssertType")
            .ClientAssertionDestinationName("clientAssertDest")
            .Authorization("authValue")
            .SubjectTokenType("subjectType")
            .ActorToken("actorToken")
            .ActorTokenType("actorType")
            .RedirectUri("https://redirect")
            .CodeVerifier("verifier")
            .ChainName("chain")
            .ChainVarSubjectToken("chainSubject")
            .ChainVarSubjectTokenType("chainSubjectType")
            .ChainVarSamlProviderDestinationName("chainSaml")
            .Build();

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["x-token-service-tenant"] = "tokenTenant",
                ["x-client-assertion"] = "clientAssert",
                ["x-client-assertion-type"] = "clientAssertType",
                ["x-client-assertion-destination-name"] = "clientAssertDest",
                ["authorization"] = "authValue",
                ["x-subject-token-type"] = "subjectType",
                ["x-actor-token"] = "actorToken",
                ["x-actor-token-type"] = "actorType",
                ["x-redirect-uri"] = "https://redirect",
                ["x-code-verifier"] = "verifier",
                ["x-chain-name"] = "chain",
                ["x-chain-var-subjectToken"] = "chainSubject",
                ["x-chain-var-subjectTokenType"] = "chainSubjectType",
                ["x-chain-var-samlProviderDestinationName"] = "chainSaml",
            },
            destination.GetHeaders(ValidUri).ToDictionary(h => h.Name, h => h.Value!));
    }

    [Fact]
    public void TenantId_IsAddedPerRequest_UnlessSetExplicitly()
    {
        var destination = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).Build();
        CurrentTenant = new DefaultTenant("first");
        Assert.Contains(new Header("x-tenant-id", "first"), destination.GetHeaders(ValidUri));

        CurrentTenant = new DefaultTenant("second");
        Assert.Contains(new Header("x-tenant-id", "second"), destination.GetHeaders(ValidUri));

        var explicitTenant = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).TenantId("explicit").Build();
        Assert.Equal([new Header("x-tenant-id", "explicit")], explicitTenant.GetHeaders(ValidUri));

        var subdomain = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).TenantSubdomain("sub").Build();
        Assert.Equal([new Header("x-tenant-subdomain", "sub")], subdomain.GetHeaders(ValidUri));
    }

    [Fact]
    public async Task Authorization_IsTheRawTokenOfTheCurrentUser_AddedPerRequest()
    {
        var destination = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).Build();
        var token = XsuaaToken();
        CurrentToken = token;

        Assert.Equal([new Header("authorization", token.Jwt.EncodedToken)], destination.GetHeaders(ValidUri));
        Assert.Equal([new Header("authorization", token.Jwt.EncodedToken)], await destination.GetHeadersAsync(ValidUri));

        var explicitAuthorization = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).Authorization("mine").Build();
        Assert.Equal([new Header("authorization", "mine")], explicitAuthorization.GetHeaders(ValidUri));
    }

    [Fact]
    public void NoTenantAndNoToken_MeanNoContextHeaders()
    {
        var destination = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).Build();

        Assert.Empty(destination.GetHeaders(ValidUri));
    }

    [Fact]
    public void HeaderProviders_ContributeHeaders()
    {
        var destination = TransparentProxyDestination.CreateBuilder(ValidUri.ToString())
            .HeaderProvider(new FixedHeaderProvider(new Header("from-provider", "yes")))
            .Build();

        Assert.Equal([new Header("from-provider", "yes")], destination.GetHeaders(ValidUri));
    }

    private sealed class FixedHeaderProvider(Header header) : IDestinationHeaderProvider
    {
        public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) => [header];
    }

    [Fact]
    public void BuildingWithoutADestinationName_Fails()
    {
        Assert.Throws<ArgumentException>(() => TransparentProxyDestination.CreateGatewayBuilder(string.Empty, ValidUri.ToString()));
    }

    [Fact]
    public void Destinations_AreEqualIfPropertiesAndHeadersAreEqual()
    {
        var gateway = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString()).Build();
        var sameGateway = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString()).Build();
        var otherGateway = TransparentProxyDestination.CreateGatewayBuilder("other", ValidUri.ToString()).Build();
        var plain = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).Header("foo", "bar").Build();
        var samePlain = TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).Header("foo", "bar").Build();

        Assert.Equal(gateway, sameGateway);
        Assert.Equal(gateway.GetHashCode(), sameGateway.GetHashCode());
        Assert.NotEqual(gateway, otherGateway);
        Assert.Equal(plain, samePlain);
        Assert.NotEqual(plain, gateway);
    }

    [Fact]
    public void TenantIdAndSubdomain_CannotBePassedTogether()
    {
        var first = Assert.Throws<InvalidOperationException>(
            () => TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).TenantId("id").TenantSubdomain("sub"));
        var second = Assert.Throws<InvalidOperationException>(
            () => TransparentProxyDestination.CreateBuilder(ValidUri.ToString()).TenantSubdomain("sub").TenantId("id"));

        Assert.Equal(TransparentProxyDestination.TenantIdAndTenantSubdomainBothPassedErrorMessage, first.Message);
        Assert.Equal(TransparentProxyDestination.TenantIdAndTenantSubdomainBothPassedErrorMessage, second.Message);
    }

    [Fact]
    public void ToString_DoesNotRevealHeaderValues()
    {
        var destination = TransparentProxyDestination.CreateGatewayBuilder("testDest", ValidUri.ToString())
            .Authorization("Bearer secret-token")
            .ClientAssertion("secret-assertion")
            .Build();

        var text = destination.ToString();

        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.Contains("authorization", text, StringComparison.Ordinal);
    }
}

/// <summary>The loader of a transparent proxy (port of <c>TransparentProxyTest</c>).</summary>
public sealed class TransparentProxyTests : DestinationServiceTestBase
{
    private readonly FakeNetworkVerifier _verifier = new();
    private readonly GatewayHandler _gateway = new();

    private sealed class FakeNetworkVerifier : INetworkVerifier
    {
        public List<(string Host, int Port)> Verified { get; } = [];

        public Func<string, int, Exception?> Failure { get; set; } = (_, _) => null;

        public Task VerifyHostConnectivityAsync(string host, int port, CancellationToken cancellationToken)
        {
            Verified.Add((host, port));
            return Failure(host, port) is { } failure ? Task.FromException(failure) : Task.CompletedTask;
        }
    }

    private sealed class GatewayHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public List<(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
            return Task.FromResult(Respond(request));
        }
    }

    public TransparentProxyTests()
    {
        TransparentProxy.NetworkVerifier = _verifier;
        TransparentProxy.HttpClientSource = destination => new HttpClient(
            new DestinationRequestHandler(destination) { InnerHandler = _gateway },
            disposeHandler: false)
        {
            BaseAddress = destination.Uri,
        };
        CurrentTenant = new DefaultTenant("tenant-id");
    }

    private static DestinationOptions NoOptionsForProxy => DestinationOptions.CreateBuilder().Build();

    private static HttpResponseMessage BadGateway(string? code = null, string? message = null, string? origin = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
        if (code is not null)
        {
            response.Headers.Add("x-error-internal-code", code);
        }
        if (message is not null)
        {
            response.Headers.Add("x-error-message", message);
        }
        if (origin is not null)
        {
            response.Headers.Add("x-error-origin", origin);
        }
        return response;
    }

    [Theory]
    [InlineData("127.0.0.1", 80, "http://127.0.0.1:80")]
    [InlineData("gateway", 80, "http://gateway:80")]
    [InlineData("http://gateway", 80, "http://gateway:80")]
    [InlineData("https://gateway", 80, "https://gateway:80")]
    [InlineData("gateway", 8080, "http://gateway:8080")]
    [InlineData("https://gateway.svc.cluster.local", 443, "https://gateway.svc.cluster.local:443")]
    public async Task Register_NormalizesTheHost_AndVerifiesIt(string host, int port, string expectedUri)
    {
        await TransparentProxy.RegisterAsync(host, port);

        Assert.Equal(expectedUri, TransparentProxy.GatewayUri);
        var verified = Assert.Single(_verifier.Verified);
        Assert.Equal(port, verified.Port);
        Assert.Equal(new Uri(expectedUri).Host, verified.Host);
        Assert.Null(TransparentProxy.ProviderTenantId);
    }

    [Fact]
    public async Task Register_WithProviderTenant_StoresIt()
    {
        await TransparentProxy.RegisterAsync("gateway", 8080, "provider-tenant-123");

        Assert.Equal("provider-tenant-123", TransparentProxy.ProviderTenantId);
        Assert.Equal("http://gateway:8080", TransparentProxy.GatewayUri);
    }

    [Fact]
    public async Task Register_WithHostAndProviderTenant_UsesPort80()
    {
        await TransparentProxy.RegisterAsync("gateway", "provider-tenant");

        Assert.Equal("provider-tenant", TransparentProxy.ProviderTenantId);
        Assert.Equal("http://gateway:80", TransparentProxy.GatewayUri);
        Assert.Equal(("gateway", 80), Assert.Single(_verifier.Verified));
    }

    [Fact]
    public async Task Register_PrependsTheLoaderToTheChain()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");

        var chain = Assert.IsType<DestinationLoaderChain>(DestinationAccessor.Loader);
        Assert.IsType<TransparentProxy>(chain.Loaders[0]);
        var destination = await DestinationAccessor.GetDestinationAsync("test-destination");
        Assert.IsType<TransparentProxyDestination>(destination);
    }

    [Theory]
    [InlineData("gateway/")]
    [InlineData("http://gateway/")]
    [InlineData("http://gateway:80/")]
    [InlineData("http://gateway/path")]
    [InlineData("gateway/path/to/something")]
    [InlineData("https://gateway:8443/path")]
    public async Task Register_WithAPath_Fails(string host)
    {
        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => TransparentProxy.RegisterAsync(host, "tenant"));

        Assert.Contains("Paths are not allowed in host registration.", failure.Message, StringComparison.Ordinal);
        Assert.Empty(_verifier.Verified);
        Assert.Null(TransparentProxy.GatewayUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://")]
    public async Task Register_WithAnInvalidHost_Fails(string host) =>
        await Assert.ThrowsAsync<DestinationAccessException>(() => TransparentProxy.RegisterAsync(host, "tenant"));

    [Fact]
    public async Task Register_WithUnreachableHost_Fails_AndDoesNotRegister()
    {
        _verifier.Failure = (host, port) => new DestinationAccessException($"Host [{host}] on port [{port}] is not reachable.");

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => TransparentProxy.RegisterAsync("gateway", 65432));

        Assert.Contains("is not reachable", failure.Message, StringComparison.Ordinal);
        Assert.Null(TransparentProxy.GatewayUri);
    }

    [Fact]
    public async Task OnlyOneRegistration_IsAllowed()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant");

        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => TransparentProxy.RegisterAsync("other", "tenant"));

        Assert.Equal("TransparentProxy is already registered. Only one registration is allowed.", failure.Message);
        Assert.Equal("http://gateway:80", TransparentProxy.GatewayUri);
    }

    [Fact]
    public async Task Lookup_SendsAHeadRequestWithTheGatewayHeaders()
    {
        await TransparentProxy.RegisterAsync("gateway", 8080, "provider-tenant");
        CurrentToken = XsuaaToken();

        var result = await new TransparentProxy().TryGetDestinationAsync("test-destination", NoOptionsForProxy);

        var destination = Assert.IsType<TransparentProxyDestination>(result.GetValueOrThrow());
        Assert.Equal(new Uri("http://gateway:8080"), destination.Uri);
        var request = Assert.Single(_gateway.Requests);
        Assert.Equal(HttpMethod.Head, request.Method);
        Assert.Equal("http://gateway:8080/", request.Uri.AbsoluteUri);
        Assert.Equal("test-destination", request.Headers["x-destination-name"]);
        Assert.Equal("tenant-id", request.Headers["x-tenant-id"]);
        Assert.Equal(CurrentToken.Jwt.EncodedToken, request.Headers["authorization"]);
    }

    [Fact]
    public async Task Lookup_Synchronously_ReturnsTheSameResult()
    {
        await TransparentProxy.RegisterAsync("gateway", "provider-tenant");

        var result = new TransparentProxy().TryGetDestination("test-destination", NoOptionsForProxy);

        Assert.IsType<TransparentProxyDestination>(result.GetValueOrThrow());
    }

    [Fact]
    public async Task Lookup_UsesTheProviderTenant_WhenThereIsNoCurrentTenant()
    {
        await TransparentProxy.RegisterAsync("gateway", 8080, "provider-tenant-fallback");
        CurrentTenant = null;

        var destination = (await new TransparentProxy().TryGetDestinationAsync("dest", NoOptionsForProxy)).GetValueOrThrow();

        Assert.Equal("provider-tenant-fallback", _gateway.Requests.Single().Headers["x-tenant-id"]);
        Assert.IsType<TransparentProxyDestination>(destination);
    }

    [Fact]
    public async Task Lookup_UsesTheCurrentTenant_EvenIfAProviderTenantIsRegistered()
    {
        await TransparentProxy.RegisterAsync("gateway", 8080, "provider-tenant-fallback");

        await new TransparentProxy().TryGetDestinationAsync("dest", NoOptionsForProxy);

        Assert.Equal("tenant-id", _gateway.Requests.Single().Headers["x-tenant-id"]);
    }

    [Fact]
    public async Task Lookup_WithoutAnyTenant_ThrowsATenantAccessException()
    {
        await TransparentProxy.RegisterAsync("gateway");
        CurrentTenant = null;

        var failure = await Assert.ThrowsAsync<TenantAccessException>(
            () => new TransparentProxy().TryGetDestinationAsync("test-destination", NoOptionsForProxy));
        Assert.Contains("No current tenant defined", failure.Message, StringComparison.Ordinal);
        Assert.Contains("no provider tenant id configured", failure.Message, StringComparison.Ordinal);
        Assert.Throws<TenantAccessException>(() => new TransparentProxy().TryGetDestination("test-destination", NoOptionsForProxy));
        Assert.Empty(_gateway.Requests);
    }

    [Fact]
    public async Task Lookup_WithoutRegistration_Fails()
    {
        await Assert.ThrowsAsync<DestinationAccessException>(
            () => new TransparentProxy().TryGetDestinationAsync("test-destination", NoOptionsForProxy));
    }

    [Fact]
    public async Task Status502WithErrorCode404_MeansTheDestinationDoesNotExist()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");
        _gateway.Respond = _ => BadGateway("404", "Destination not found: non-existent-destination");

        var result = await new TransparentProxy().TryGetDestinationAsync("non-existent-destination", NoOptionsForProxy);

        var failure = Assert.IsType<DestinationNotFoundException>(result.Exception);
        Assert.Contains("Destination not found: non-existent-destination", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("500")]
    public async Task Status502WithoutOrWithAnotherErrorCode_MeansTheDestinationExists(string? code)
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");
        _gateway.Respond = _ => BadGateway(code);

        var result = await new TransparentProxy().TryGetDestinationAsync("destination-with-502", NoOptionsForProxy);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ErrorOrigin_LeadsToADestinationAccessException()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");
        _gateway.Respond = _ => BadGateway("401", "token rejected", "destination-service");

        var result = await new TransparentProxy().TryGetDestinationAsync("dest", NoOptionsForProxy);

        var failure = Assert.IsType<DestinationAccessException>(result.Exception);
        Assert.Contains("Origin: [destination-service]", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Code: [401]", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Message: [token rejected]", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionFailures_AreReportedAsFailures()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");
        _gateway.Respond = _ => throw new HttpRequestException("connection refused");

        var result = await new TransparentProxy().TryGetDestinationAsync("dest", NoOptionsForProxy);

        Assert.IsType<DestinationAccessException>(result.Exception);
    }

    [Fact]
    public async Task Options_AreForwardedToTheGateway()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");
        var options = Options(
            Augmenter()
                .FragmentName("a-fragment")
                .CrossLevelConsumption(CrossLevelScope.ProviderInstance)
                .CustomHeaders(new Header("X-Custom", "custom-value")));

        var destination = (TransparentProxyDestination)(await new TransparentProxy().TryGetDestinationAsync("dest", options)).GetValueOrThrow();

        var headers = destination.GetHeaders(destination.Uri);
        Assert.Contains(new Header("x-fragment-name", "a-fragment"), headers);
        Assert.Contains(new Header("x-fragment-level", "provider_instance"), headers);
        Assert.Contains(new Header("x-destination-level", "provider_instance"), headers);
        Assert.Contains(new Header("X-Custom", "custom-value"), headers);
        var request = Assert.Single(_gateway.Requests);
        Assert.Equal("a-fragment", request.Headers["x-fragment-name"]);
    }

    [Fact]
    public async Task CrossLevelScopeWithoutFragment_OnlySetsTheDestinationLevel()
    {
        await TransparentProxy.RegisterAsync("gateway", "tenant-id");
        var options = Options(Augmenter().CrossLevelConsumption(CrossLevelScope.Instance));

        await new TransparentProxy().TryGetDestinationAsync("dest", options);

        var request = Assert.Single(_gateway.Requests);
        Assert.Equal("instance", request.Headers["x-destination-level"]);
        Assert.False(request.Headers.ContainsKey("x-fragment-level"));
    }

    [Fact]
    public async Task TenantIdFromOptions_IsSentInAdditionToTheContextTenant()
    {
        await TransparentProxy.RegisterAsync("gateway", 8080, "provider-tenant-fallback");
        var options = Options(Augmenter().CustomHeaders(new Header("x-tenant-id", "options-tenant-123")));

        var destination = (TransparentProxyDestination)(await new TransparentProxy().TryGetDestinationAsync("dest", options)).GetValueOrThrow();

        Assert.Contains(
            destination.GetHeaders(destination.Uri),
            h => h.Name.Equals("x-tenant-id", StringComparison.OrdinalIgnoreCase) && h.Value == "options-tenant-123");
    }

    [Fact]
    public async Task NetworkVerifier_AcceptsListeningHosts_AndRejectsClosedPorts()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var verifier = new NetworkVerifier();

        await verifier.VerifyHostConnectivityAsync("127.0.0.1", port, default);

        listener.Stop();
        var failure = await Assert.ThrowsAsync<DestinationAccessException>(() => verifier.VerifyHostConnectivityAsync("127.0.0.1", port, default));
        Assert.Contains("is not reachable", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkVerifier_RejectsUnknownHosts()
    {
        var verifier = new NetworkVerifier();

        await Assert.ThrowsAsync<DestinationAccessException>(() => verifier.VerifyHostConnectivityAsync("no-such-host.invalid", 80, default));
    }
}
