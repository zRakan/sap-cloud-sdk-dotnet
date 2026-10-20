using System.Net;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Executing the fluent helpers against a fake server: requests sent and results returned.</summary>
public class ExecutionTests : IDisposable
{
    private const string CustomerResponse =
        """
        {"d":{"__metadata":{"id":"x","uri":"x","type":"Customer","etag":"W/\"meta\""},"Id":1,"Name":"Alice","Email":null,"AddressId":2,
         "Address":{"__deferred":{"uri":"x"}}}}
        """;

    private readonly ISdkGroceryStoreService _service = new DefaultSdkGroceryStoreService().WithServicePath("/endpoint/url");

    public void Dispose()
    {
        ODataHttpClientProvider.Reset();
        GC.SuppressFinalize(this);
    }

    // ---- read ----

    [Fact]
    public async Task Read_ReturnsEntities_WithoutFetchingACsrfToken()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"results":[{"Id":1,"Name":"A"},{"Id":2,"Name":"B"}]}}"""));

        var customers = await _service.GetAllCustomer().ExecuteAsync(server.Client);

        Assert.Equal(["A", "B"], customers.Select(c => c.Name));
        var request = Assert.Single(server.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/endpoint/url/Customers", request.PathAndQuery);
        Assert.Equal("application/json", request.GetFirstHeader("Accept"));
        Assert.All(customers, c => Assert.Empty(c.GetChangedFields()));
    }

    [Fact]
    public async Task Read_SendsQueryAndCustomHeader()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"results":[]}}"""));

        var result = await _service.GetAllProduct()
            .Select(Product.Fields.Name)
            .Filter(Product.Fields.Id.Gt(5))
            .Top(3)
            .WithHeader("customKey", "customValue")
            .ExecuteAsync(server.Client);

        Assert.Empty(result);
        var request = server.SingleDataRequest;
        Assert.Equal("/endpoint/url/Products?$select=Name&$filter=Id%20gt%205&$top=3", request.PathAndQuery);
        Assert.Equal("customValue", request.GetFirstHeader("customKey"));
    }

    [Fact]
    public async Task Read_WithDestination_UsesTheProvider()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"results":[{"Id":1}]}}"""));
        IHttpDestinationProperties? seen = null;
        ODataHttpClientProvider.Provider = destination =>
        {
            seen = destination;
            return server.Client;
        };
        var destination = DefaultHttpDestination.CreateBuilder("https://example.org").Name("dest").Build();

        var customers = await _service.GetAllCustomer().ExecuteAsync(destination);

        Assert.Same(destination, seen);
        Assert.Single(customers);
        Assert.Same(destination, customers[0].DestinationForFetch);
        Assert.Equal("/endpoint/url", customers[0].ServicePathForFetch);
    }

    [Fact]
    public async Task Read_AttachesEntitiesToTheClient_ForLazyLoadingOfNavigationProperties()
    {
        var server = new FakeODataServer(
            request => request.PathAndQuery.EndsWith("/Customers", StringComparison.Ordinal)
                ? Responses.Json("""{"d":{"results":[{"Id":1,"Name":"Alice","AddressId":2,"Address":{"__deferred":{"uri":"x"}}}]}}""")
                : Responses.Json("""{"d":{"Id":2,"City":"Walldorf"}}"""));

        var customer = Assert.Single(await _service.GetAllCustomer().ExecuteAsync(server.Client));
        Assert.Null(customer.GetAddressIfPresent());

        var address = await customer.GetAddressOrFetchAsync();

        Assert.Equal("Walldorf", address!.City);
        Assert.Same(address, customer.GetAddressIfPresent());
        Assert.Equal("/endpoint/url/Customers(1)/Address", server.Requests[^1].PathAndQuery);
        Assert.Equal("/endpoint/url", address.ServicePathForFetch);
    }

    [Fact]
    public async Task FetchOfToManyNavigationProperty_ReturnsAttachedEntities()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"results":[{"Id":10,"Name":"Milk"},{"Id":11,"Name":"Bread"}]}}"""));
        var shelf = new Shelf { Id = 5 };
        shelf.AttachToService(null, server.Client);

        var products = await shelf.GetProductsOrFetchAsync();

        Assert.Equal(["Milk", "Bread"], products.Select(p => p.Name));
        Assert.Equal("/com.sap.cloud.sdk.store.grocery/Shelves(5)/Products", server.SingleDataRequest.PathAndQuery);
        Assert.Same(shelf.GetProductsIfPresent(), products);
        Assert.All(products, p => Assert.NotNull(p.ServicePathForFetch));
    }

    // ---- pagination ----

    private static FakeODataServer PagedServer() =>
        new(request =>
            request.PathAndQuery.Contains("skiptoken=2", StringComparison.Ordinal)
                ? Responses.Json("""{"d":{"results":[{"Id":3}]}}""")
                : request.PathAndQuery.Contains("skiptoken=1", StringComparison.Ordinal)
                    ? Responses.Json("""{"d":{"results":[{"Id":2}],"__next":"https://host/endpoint/url/Customers?$skiptoken=2"}}""")
                    : Responses.Json("""{"d":{"results":[{"Id":1}],"__next":"https://host/endpoint/url/Customers?$skiptoken=1"}}"""));

    [Fact]
    public async Task Read_FollowsNextLinks()
    {
        var server = PagedServer();

        var customers = await _service.GetAllCustomer().ExecuteAsync(server.Client);

        Assert.Equal([1, 2, 3], customers.Select(c => c.Id));
        Assert.Equal(3, server.DataRequests.Count);
        Assert.Equal("/endpoint/url/Customers?$skiptoken=1", server.DataRequests[1].PathAndQuery);
    }

    [Fact]
    public async Task IteratingPages_RequestsPagesLazily()
    {
        var server = PagedServer();

        var pages = await _service.GetAllCustomer().IteratingPages().ExecuteAsync(server.Client);
        Assert.Single(server.DataRequests);

        var sizes = new List<int>();
        await foreach (var page in pages)
        {
            sizes.Add(page.Count);
        }
        Assert.Equal([1, 1, 1], sizes);
        Assert.Equal(3, server.DataRequests.Count);
    }

    [Fact]
    public async Task IteratingEntities_FlattensPages()
    {
        var server = PagedServer();

        var ids = new List<int?>();
        await foreach (var customer in await _service.GetAllCustomer().IteratingEntities().ExecuteAsync(server.Client))
        {
            ids.Add(customer.Id);
        }

        Assert.Equal([1, 2, 3], ids);
    }

    // ---- by key ----

    [Fact]
    public async Task ByKey_ReturnsEntity_WithVersionIdentifierFromTheMetadata()
    {
        var server = new FakeODataServer(Responses.Json(CustomerResponse));

        var customer = await _service.GetCustomerByKey(1).ExecuteAsync(server.Client);

        Assert.Equal("/endpoint/url/Customers(1)", server.SingleDataRequest.PathAndQuery);
        Assert.Equal("Alice", customer.Name);
        Assert.Equal("W/\"meta\"", customer.VersionIdentifier);
        Assert.Null(customer.Email);
        Assert.Null(customer.GetAddressIfPresent());
    }

    [Fact]
    public async Task ByKey_PrefersTheEtagHeader()
    {
        var server = new FakeODataServer(Responses.Json(CustomerResponse).WithHeader("ETag", "W/\"header\""));

        var customer = await _service.GetCustomerByKey(1).ExecuteAsync(server.Client);

        Assert.Equal("W/\"header\"", customer.VersionIdentifier);
    }

    [Fact]
    public async Task ByKey_ForUnknownEntity_ThrowsServiceError()
    {
        var server = new FakeODataServer(
            Responses.Json("""{"error":{"code":"AB/100","message":{"lang":"en","value":"Not found"}}}""", 404));

        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => _service.GetCustomerByKey(99).ExecuteAsync(server.Client));

        Assert.Equal(404, exception.HttpCode);
        Assert.Equal("AB/100", exception.ODataError.ODataCode);
        Assert.Equal("Not found", exception.ODataError.ODataMessage);
    }

    // ---- count ----

    [Fact]
    public async Task Count_ReadsPlainTextNumber()
    {
        var server = new FakeODataServer(Responses.Text("42"));

        var count = await _service.GetAllProduct().Filter(Product.Fields.Name.Eq("a")).Count().ExecuteAsync(server.Client);

        Assert.Equal(42, count);
        Assert.Equal("/endpoint/url/Products/$count?$filter=Name%20eq%20'a'", server.SingleDataRequest.PathAndQuery);
    }

    [Fact]
    public async Task Count_ReadsJsonNumber()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"__count":"7"}}"""));

        Assert.Equal(7, await _service.GetAllProduct().Count().WithCsrfToken().ExecuteAsync(server.Client));
        Assert.Equal(HttpMethod.Head, server.Requests[0].Method);
    }

    // ---- create ----

    [Fact]
    public async Task Create_FetchesCsrfToken_AndPostsTheEntity()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"Id":100,"Name":"Alice","Address":{"Id":5,"City":"Walldorf"}}}""", 201).WithHeader("ETag", "W/\"new\""));
        var customer = Customer.Builder().Name("Alice").Address(Address.Builder().City("Walldorf").Build()).Build();

        var response = await _service.CreateCustomer(customer).ExecuteAsync(server.Client);

        Assert.Equal(HttpMethod.Head, server.Requests[0].Method);
        Assert.Equal("fetch", server.Requests[0].GetFirstHeader("x-csrf-token"), ignoreCase: true);
        var post = server.SingleDataRequest;
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/endpoint/url/Customers", post.PathAndQuery);
        Assert.Equal(FakeODataServer.CsrfToken, post.GetFirstHeader("x-csrf-token"));
        Assert.Equal("application/json", post.GetFirstHeader("Content-Type"));
        JsonAssert.Equal("""{"Name":"Alice","Address":{"City":"Walldorf"}}""", post.Body);

        Assert.Equal(201, response.ResponseStatusCode);
        Assert.Same(customer, response.RequestEntity);
        Assert.Equal(100, response.ModifiedEntity.Id);
        Assert.Equal(100, response.ResponseEntity!.Id);
        Assert.Equal("Walldorf", response.ModifiedEntity.GetAddressIfPresent()!.City);
        Assert.Equal("W/\"new\"", response.UpdatedVersionIdentifier);
        Assert.Equal("W/\"new\"", response.ModifiedEntity.VersionIdentifier);
        Assert.NotSame(response.ResponseEntity, response.ModifiedEntity);
        Assert.Null(customer.Id);
        Assert.Contains("W/\"new\"", response.ResponseHeaders["ETag"]);
    }

    [Fact]
    public async Task Create_ModifiedEntityIsAttachedToTheService()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"Id":100}}""", 201));

        var response = await _service.CreateCustomer(Customer.Builder().Name("Alice").Build()).ExecuteAsync(server.Client);

        Assert.Equal("/endpoint/url", response.ModifiedEntity.ServicePathForFetch);
        Assert.NotNull(response.ModifiedEntity.FetchTarget);
    }

    [Fact]
    public async Task Create_WithoutCsrfToken_SendsNoHeadRequest()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{}}""", 201));

        await _service.CreateCustomer(new Customer()).WithoutCsrfToken().ExecuteAsync(server.Client);

        Assert.Equal(HttpMethod.Post, Assert.Single(server.Requests).Method);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(405)]
    public async Task Create_WhenCsrfTokenRetrievalFails_StillPosts(int csrfStatus)
    {
        var handler = new FakeHttpHandler(request =>
            request.Method == HttpMethod.Head ? Responses.Empty(csrfStatus) : Responses.Json("""{"d":{"Id":1}}""", 201));

        var response = await _service.CreateCustomer(new Customer()).ExecuteAsync(handler.CreateClient());

        Assert.Equal(1, response.ModifiedEntity.Id);
        Assert.False(handler.Requests[^1].HasHeader("x-csrf-token"));
    }

    [Fact]
    public async Task Create_WhenTheServiceIssuesNoToken_SendsNoTokenHeader()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"Id":1}}""", 201), issueCsrfToken: false);

        await _service.CreateCustomer(new Customer()).ExecuteAsync(server.Client);

        Assert.False(server.SingleDataRequest.HasHeader("x-csrf-token"));
    }

    [Fact]
    public async Task Create_WithServiceError_ThrowsWithTheODataError()
    {
        var server = new FakeODataServer(
            Responses.Json(
                """
                {"error":{"code":"AB/100","message":{"lang":"en","value":"Invalid method invocation: 'CREATE' method is called on the non-root entity"},
                 "innererror":{"errordetails":[{"code":"INNER-CODE-1","message":"INNER-MESSAGE-1","propertyref":"","severity":"error","target":""}]}}}
                """,
                501));

        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => _service.CreateCustomer(new Customer()).ExecuteAsync(server.Client));

        Assert.Equal("The HTTP response code (501) indicates an error. The OData service responded with an error message.", exception.Message);
        Assert.Equal(501, exception.HttpCode);
        Assert.Equal("AB/100", exception.ODataError.ODataCode);
        Assert.StartsWith("Invalid method invocation: 'CREATE'", exception.ODataError.ODataMessage, StringComparison.Ordinal);
        var detail = Assert.Single(exception.ODataError.Details);
        Assert.Equal("INNER-CODE-1", detail.ODataCode);
        Assert.Equal("INNER-MESSAGE-1", detail.ODataMessage);
    }

    [Fact]
    public async Task ErrorWithoutODataPayload_ThrowsResponseException()
    {
        var server = new FakeODataServer(Responses.Text("<error>an exception was raised </error>", "application/xml", 400));

        var exception = await Assert.ThrowsAsync<ODataResponseException>(() => _service.CreateCustomer(new Customer()).ExecuteAsync(server.Client));

        Assert.IsNotType<ODataServiceErrorException>(exception);
        Assert.Equal(400, exception.HttpCode);
    }

    [Fact]
    public async Task ConnectionFailure_IsReportedAsConnectionException()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("no route to host"));

        await Assert.ThrowsAsync<ODataConnectionException>(() => _service.GetAllCustomer().ExecuteAsync(handler.CreateClient()));
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"results":[]}}"""));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GetAllCustomer().ExecuteAsync(server.Client, cts.Token));
    }

    // ---- update ----

    [Fact]
    public async Task Update_Patch_SendsChangedFieldsAndIfMatch()
    {
        var server = new FakeODataServer(Responses.Empty(204).WithHeader("ETag", "W/\"2\""));
        var product = Product.Builder().Id(123).Name("Mate").Build();
        product.VersionIdentifier = "W/\"1\"";
        product.Name = "Changed";

        var response = await _service.UpdateProduct(product).ExecuteAsync(server.Client);

        var patch = server.SingleDataRequest;
        Assert.Equal(HttpMethod.Patch, patch.Method);
        Assert.Equal("/endpoint/url/Products(123)", patch.PathAndQuery);
        Assert.Equal("W/\"1\"", patch.GetFirstHeader("If-Match"));
        Assert.Equal("{\"Name\":\"Changed\"}", patch.Body);
        Assert.Equal(FakeODataServer.CsrfToken, patch.GetFirstHeader("x-csrf-token"));

        Assert.Equal(204, response.ResponseStatusCode);
        Assert.Null(response.ResponseEntity);
        Assert.Equal("W/\"2\"", response.UpdatedVersionIdentifier);
        Assert.Equal("Changed", response.ModifiedEntity.Name);
        Assert.Equal("W/\"2\"", response.ModifiedEntity.VersionIdentifier);
        Assert.Equal("W/\"1\"", product.VersionIdentifier);
    }

    [Fact]
    public async Task Update_Put_SendsTheWholeEntity()
    {
        var server = new FakeODataServer(Responses.Empty(204));
        var product = Product.Builder().Id(123).Name("Mate").Build();

        await _service.UpdateProduct(product).ReplacingEntity().MatchAnyVersionIdentifier().ExecuteAsync(server.Client);

        var put = server.SingleDataRequest;
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal("*", put.GetFirstHeader("If-Match"));
        JsonAssert.Equal("""{"Id":123,"Name":"Mate","ShelfId":null,"VendorId":null,"Price":null,"Image":null,"Vendor":null,"Shelf":[]}""", put.Body);
    }

    [Fact]
    public async Task Update_WithoutVersionIdentifier_SendsNoIfMatch()
    {
        var server = new FakeODataServer(Responses.Empty(204));

        await _service.UpdateProduct(Product.Builder().Id(1).Build()).ExecuteAsync(server.Client);

        Assert.False(server.SingleDataRequest.HasHeader("If-Match"));
    }

    [Fact]
    public async Task Update_ReturnedEntityOfTheResponseBecomesTheModifiedEntity()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"__metadata":{"etag":"W/\"3\""},"Id":123,"Name":"FromServer"}}"""));

        var response = await _service.UpdateProduct(Product.Builder().Id(123).Name("Mate").Build()).ExecuteAsync(server.Client);

        Assert.Equal("FromServer", response.ModifiedEntity.Name);
        Assert.Equal("W/\"3\"", response.UpdatedVersionIdentifier);
        Assert.Equal("W/\"3\"", response.ModifiedEntity.VersionIdentifier);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_SendsIfMatchAndHeaders()
    {
        var server = new FakeODataServer(Responses.Empty(204));
        var shelf = Shelf.Builder().Id(101).Build();
        shelf.VersionIdentifier = "W/\"7\"";

        var response = await _service.DeleteShelf(shelf).WithHeader("my-header", "my-value").ExecuteAsync(server.Client);

        var delete = server.SingleDataRequest;
        Assert.Equal(HttpMethod.Delete, delete.Method);
        Assert.Equal("/endpoint/url/Shelves(101)", delete.PathAndQuery);
        Assert.Equal("W/\"7\"", delete.GetFirstHeader("If-Match"));
        Assert.Equal("my-value", delete.GetFirstHeader("my-header"));
        Assert.Equal(204, response.ResponseStatusCode);
    }

    // ---- function imports ----

    [Fact]
    public async Task FunctionImport_Get_ReturnsCollection_WithoutCsrf()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"results":[{"ProductId":1,"Quantity":10},{"ProductId":2,"Quantity":20}]}}"""));

        var counts = await _service.GetProductQuantities(3, 1).ExecuteAsync(server.Client);

        Assert.Equal([10, 20], counts.Select(c => c.Quantity));
        var request = Assert.Single(server.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/endpoint/url/GetProductQuantities?ShelfId=3&ProductId=1", request.PathAndQuery);
    }

    [Fact]
    public async Task FunctionImport_Get_CollectionWrappedInFunctionName()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"GetProductQuantities":{"results":[{"ProductId":1,"Quantity":10}]}}}"""));

        var counts = await _service.GetProductQuantities(3, 1).ExecuteAsync(server.Client);

        Assert.Equal(10, Assert.Single(counts).Quantity);
    }

    [Fact]
    public async Task FunctionImport_Post_ReturnsEntity_WithCsrfToken()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"OrderProduct":{"__metadata":{"etag":"W/\"r\""},"Id":5,"CustomerId":1,"Date":"/Date(694224000000)/","ProductCount1":{"ProductId":2,"Quantity":3}}}}"""));

        var receipt = await _service.OrderProduct(1, 2, 3).ExecuteAsync(server.Client);

        Assert.NotNull(receipt);
        Assert.Equal(5, receipt.Id);
        Assert.Equal(new DateTime(1992, 1, 1), receipt.Date);
        Assert.Equal(3, receipt.ProductCount1!.Quantity);
        Assert.Equal("W/\"r\"", receipt.VersionIdentifier);
        var post = server.SingleDataRequest;
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/endpoint/url/OrderProduct?CustomerId=1&ProductId=2&Quantity=3", post.PathAndQuery);
        Assert.Equal(FakeODataServer.CsrfToken, post.GetFirstHeader("x-csrf-token"));
        Assert.Equal("/endpoint/url", receipt.ServicePathForFetch);
    }

    [Fact]
    public async Task FunctionImport_Post_WithoutCsrfTokenInResponse_SendsNoToken()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"OrderProduct":{"Id":5}}}"""), issueCsrfToken: false);

        await _service.OrderProduct(1, 2, 3).ExecuteAsync(server.Client);

        Assert.False(server.SingleDataRequest.HasHeader("x-csrf-token"));
    }

    [Fact]
    public async Task FunctionImport_Post_WithoutCsrf_DoesNotFetchAToken()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"OrderProduct":{"Id":5}}}"""));

        await _service.OrderProduct(1, 2, 3).WithoutCsrfToken().ExecuteAsync(server.Client);

        Assert.Equal(HttpMethod.Post, Assert.Single(server.Requests).Method);
    }

    [Fact]
    public async Task FunctionImport_Get_WithCsrfToken_FetchesAToken()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"IsStoreOpen":true}}"""));

        var open = await _service.IsStoreOpen(new DateTime(2022, 1, 1, 10, 0, 0)).WithCsrfToken().ExecuteAsync(server.Client);

        Assert.True(open);
        Assert.Equal(HttpMethod.Head, server.Requests[0].Method);
        Assert.Equal(FakeODataServer.CsrfToken, server.Requests[1].GetFirstHeader("x-csrf-token"));
    }

    [Fact]
    public async Task FunctionImport_PrimitiveResults()
    {
        Assert.True(await _service.IsStoreOpen(DateTime.UnixEpoch).ExecuteAsync(new FakeODataServer(Responses.Json("""{"d":{"IsStoreOpen":true}}""")).Client));
        Assert.Equal("<xml/>", await _service.PrintReceipt(1).ExecuteAsync(new FakeODataServer(Responses.Json("""{"d":{"PrintReceipt":"<xml/>"}}""")).Client));
        Assert.Equal(
            42L,
            await FluentHelperFactory.WithServicePath("/s").FunctionSingle<long>(new Dictionary<string, object?>(), "Fn", HttpMethod.Get)
                .ExecuteAsync(new FakeODataServer(Responses.Json("""{"d":{"Fn":"42"}}""")).Client));
    }

    [Fact]
    public async Task FunctionImport_WithoutReturnValue_ReturnsNull()
    {
        var server = new FakeODataServer(Responses.Empty(204));

        var result = await FluentHelperFactory.WithServicePath("/s")
            .FunctionSingle<VoidResult>(new Dictionary<string, object?>(), "Fn", HttpMethod.Post)
            .ExecuteAsync(server.Client);

        Assert.Null(result);
    }

    [Fact]
    public async Task FunctionImport_CustomWrapper_IsFoundByFunctionName()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"Testing":{"Id":9}}}"""));

        var result = await FluentHelperFactory.WithServicePath("/s")
            .FunctionSingle<Customer>(new Dictionary<string, object?>(), "Testing", HttpMethod.Get)
            .ExecuteAsync(server.Client);

        Assert.Equal(9, result!.Id);
    }

    [Fact]
    public async Task FunctionImport_JsonErrorBody_ThrowsODataException()
    {
        var server = new FakeODataServer(Responses.Json("""{"error": "an exception was raised"}""", 400));

        await Assert.ThrowsAsync<ODataResponseException>(() => _service.OrderProduct(1, 2, 3).ExecuteAsync(server.Client));
    }

    // ---- media ----

    [Fact]
    public async Task MediaEntity_FetchesTheStream()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF };
        var server = new FakeODataServer(request =>
            request.PathAndQuery.EndsWith("$value", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : Responses.Json("""{"d":{"Id":7,"Name":"logo"}}"""));
        var product = await _service.GetProductByKey(7).ExecuteAsync(server.Client);

        await using var stream = await product.FetchMediaStreamAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(bytes, buffer.ToArray());
        Assert.Equal("/endpoint/url/Products(7)/$value", server.Requests[^1].PathAndQuery);
    }

    [Fact]
    public async Task MediaEntity_FetchError_Throws()
    {
        var server = new FakeODataServer(Responses.Text("gone", "text/plain", 404));
        var product = Product.Builder().Id(7).Build();
        product.AttachToService("/s", server.Client);

        await Assert.ThrowsAsync<ODataResponseException>(() => product.FetchMediaStreamAsync());
    }

    [Fact]
    public async Task MediaEntity_Unattached_Throws() =>
        await Assert.ThrowsAsync<ODataRequestException>(() => Product.Builder().Id(7).Build().FetchMediaStreamAsync());
}
