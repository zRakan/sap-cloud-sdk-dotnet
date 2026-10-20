using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Create, update and delete over HTTP in the details (Java: CreateViaNavigationPropertyTest, VdmComplexTest, ModificationResponseTest, FluentHelperEtagParsingTest).</summary>
public class ModificationTests
{
    private readonly ISdkGroceryStoreService _service = new DefaultSdkGroceryStoreService().WithServicePath("/path/to/service");

    [Fact]
    public async Task CreateViaNavigationProperty_PostsToTheNavigationUrl()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"__metadata":{"type":"TEST_SERVICE.A_RelatedEntityType"}}}""", 201));

        await FluentHelperFactory.WithServicePath("/service/path")
            .Create("Products", Product.Builder().Name("DE").Build())
            .AsChildOf(Shelf.Builder().Id(2).Build(), Shelf.Fields.Products)
            .ExecuteAsync(server.Client);

        Assert.Equal(HttpMethod.Head, server.Requests[0].Method);
        Assert.Equal("/service/path", server.Requests[0].PathAndQuery);
        var post = server.SingleDataRequest;
        Assert.Equal("/service/path/Shelves(2)/Products", post.PathAndQuery);
        JsonAssert.Equal("""{"Name":"DE"}""", post.Body);
    }

    [Fact]
    public async Task CreateEntityWithComplexProperty_SendsAndReceivesTheComplexObject()
    {
        var server = new FakeODataServer(
            Responses.Json(
                """
                {"d":{"__metadata":{"id":"https://127.0.0.1/path/to/service(100)","uri":"https://127.0.0.1/path/to/service(100)","type":"SERVICE.SomeEntity"},
                 "Id":100,"CustomerId":7,
                 "ProductCount1":{"__metadata":{"type":"SERVICE.ProductCount"},"ProductId":3,"Quantity":4}}}
                """,
                201));
        var receipt = Receipt.Builder().CustomerId(7).ProductCount1(ProductCount.Builder().ProductId(3).Quantity(4).Build()).Build();

        var response = await _service.CreateReceipt(receipt).ExecuteAsync(server.Client);

        Assert.Equal("/path/to/service", server.Requests[0].PathAndQuery);
        Assert.Equal("/path/to/service/Receipts", server.SingleDataRequest.PathAndQuery);
        JsonAssert.Equal("""{"CustomerId":7,"ProductCount1":{"ProductId":3,"Quantity":4}}""", server.SingleDataRequest.Body);
        Assert.NotEqual(receipt, response.ModifiedEntity);
        Assert.Equal(receipt.ProductCount1, response.ModifiedEntity.ProductCount1);
        Assert.Empty(response.ModifiedEntity.GetChangedFields());
    }

    [Fact]
    public async Task Update_SendsChangedCustomFields()
    {
        var server = new FakeODataServer(Responses.Empty(204));
        var customer = new Customer();
        customer.SetCustomField("YY1_Custom", "value");
        customer.Id = 1;

        await _service.CreateCustomer(customer).ExecuteAsync(server.Client);
        JsonAssert.Equal("""{"Id":1,"YY1_Custom":"value"}""", server.SingleDataRequest.Body);

        var shelf = Shelf.Builder().Id(5).Build();
        shelf.SetCustomField(Shelf.Fields.Custom<DateTimeOffset>("Created"), new DateTimeOffset(2022, 2, 2, 4, 20, 0, TimeSpan.Zero));
        var patchServer = new FakeODataServer(Responses.Empty(204));
        await _service.UpdateShelf(shelf).ExecuteAsync(patchServer.Client);
        Assert.Equal("{\"Created\":\"/Date(1643775600000)/\"}", patchServer.SingleDataRequest.Body);
    }

    [Fact]
    public async Task ModificationResponse_WithUnparsableBody_FallsBackToTheRequestEntity()
    {
        var server = new FakeODataServer(Responses.Text("not json", "text/plain", 200));
        var product = Product.Builder().Id(1).Name("Mate").Build();

        var response = await _service.UpdateProduct(product).ExecuteAsync(server.Client);

        Assert.Null(response.ResponseEntity);
        Assert.Equal("Mate", response.ModifiedEntity.Name);
        Assert.NotSame(product, response.ModifiedEntity);
        Assert.Equal(product, response.ModifiedEntity);
    }

    [Fact]
    public async Task ModificationResponse_UpdatedVersionIdentifier_FallsBackToTheReturnedEntity()
    {
        var server = new FakeODataServer(Responses.Json("""{"d":{"__metadata":{"etag":"W/\"from-body\""},"Id":1}}"""));

        var response = await _service.UpdateProduct(Product.Builder().Id(1).Build()).ExecuteAsync(server.Client);

        Assert.Equal("W/\"from-body\"", response.UpdatedVersionIdentifier);
    }

    [Fact]
    public async Task ModificationResponse_ModifiedEntityDoesNotShareStateWithTheRequestEntity()
    {
        var server = new FakeODataServer(Responses.Empty(204).WithHeader("ETag", "W/\"2\""));
        var product = Product.Builder().Id(1).Name("Mate").Build();
        product.SetCustomField("Custom", new List<object?> { 1 });

        var response = await _service.UpdateProduct(product).ExecuteAsync(server.Client);

        Assert.Null(product.VersionIdentifier);
        Assert.Equal("W/\"2\"", response.ModifiedEntity.VersionIdentifier);
        Assert.NotSame(product.CustomFields["Custom"], response.ModifiedEntity.CustomFields["Custom"]);
    }

    [Fact]
    public async Task Delete_WithoutVersionIdentifier_SendsNoIfMatch()
    {
        var server = new FakeODataServer(Responses.Empty(204));

        await _service.DeleteAddress(Address.Builder().Id(3).Build()).ExecuteAsync(server.Client);

        var delete = server.SingleDataRequest;
        Assert.Equal("/path/to/service/Addresses(3)", delete.PathAndQuery);
        Assert.False(delete.HasHeader("If-Match"));
    }
}
