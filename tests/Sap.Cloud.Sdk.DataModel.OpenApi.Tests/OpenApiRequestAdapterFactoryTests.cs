using System.Net;
using System.Text;
using Microsoft.Kiota.Abstractions;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OpenApi.Tests.SodaStore;
using Sap.Cloud.Sdk.DataModel.OpenApi.Tests.SodaStore.Models;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.DataModel.OpenApi.Tests;

public sealed class OpenApiRequestAdapterFactoryTests : StaticStateReset
{
    private readonly List<(HttpMethod Method, Uri Uri, string? Authorization, string Body)> _requests = [];
    private Func<HttpRequestMessage, HttpResponseMessage> _respond = _ => Json("[]");

    public OpenApiRequestAdapterFactoryTests()
    {
        HttpClientAccessor.HttpClientFactory = new TestFactory(this);
    }

    protected override void Reset()
    {
        base.Reset();
        HttpClientAccessor.HttpClientFactory = null!;
    }

    private static readonly DefaultHttpDestination Destination =
        DefaultHttpDestination.CreateBuilder("https://soda.example.com/api/v1").Name("soda").BasicCredentials("user", "pass").Build();

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GeneratedClient_UsesDestinationUrlAndAuthentication()
    {
        _respond = _ => Json("""[{"id":1,"name":"Cola","brand":"Acme","quantity":5,"price":1.5}]""");
        var client = new SodaStoreClient(OpenApiRequestAdapterFactory.Create(Destination));

        var sodas = await client.Sodas.GetAsync();

        var soda = Assert.Single(sodas!);
        Assert.Equal((1L, "Cola", "Acme", 5), (soda.Id, soda.Name, soda.Brand, soda.Quantity));
        var request = Assert.Single(_requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://soda.example.com/api/v1/sodas", request.Uri.AbsoluteUri);
        Assert.Equal("Basic dXNlcjpwYXNz", request.Authorization);
    }

    [Fact]
    public async Task PathParametersAndBodies_AreSent()
    {
        _respond = r => r.Method == HttpMethod.Put
            ? Json("""{"name":"Fanta","brand":"Acme"}""")
            : Json("""{"id":5,"name":"Fanta"}""");
        var client = new SodaStoreClient(OpenApiRequestAdapterFactory.Create(Destination));

        var byId = await client.Sodas[5].GetAsync();
        await client.Sodas.PutAsync(new SodaWithId { Id = 5, Name = "Fanta", Brand = "Acme" });

        Assert.Equal("Fanta", byId!.Name);
        Assert.Equal("https://soda.example.com/api/v1/sodas/5", _requests[0].Uri.AbsoluteUri);
        Assert.Contains("\"name\":\"Fanta\"", _requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"id\":5", _requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorResponses_BecomeApiExceptions()
    {
        _respond = _ => Json("""{"message":"not found"}""", HttpStatusCode.NotFound);
        var client = new SodaStoreClient(OpenApiRequestAdapterFactory.Create(Destination));

        var e = await Assert.ThrowsAnyAsync<ApiException>(() => client.Sodas[42].GetAsync());
        Assert.Equal(404, e.ResponseStatusCode);
    }

    [Fact]
    public void Create_WithHttpClient_UsesBaseAddress()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://other.example.com/base/") };
        Assert.Equal("https://other.example.com/base", OpenApiRequestAdapterFactory.Create(httpClient).BaseUrl);
    }

    private sealed class TestFactory(OpenApiRequestAdapterFactoryTests test) : IDestinationHttpClientFactory
    {
        public HttpClient CreateHttpClient(IHttpDestinationProperties destination) =>
            new(new DestinationRequestHandler(destination) { InnerHandler = new Recorder(test) }) { BaseAddress = destination.Uri };

        public HttpClient CreateHttpClient() => new(new Recorder(test));
    }

    private sealed class Recorder(OpenApiRequestAdapterFactoryTests test) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            test._requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
            return test._respond(request);
        }
    }
}
