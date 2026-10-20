using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>URLs, queries and payloads built by the fluent helpers of the sample service (Java: the odata-api-sample ...ToRequestTest classes, ODataSelectExpandTest, ODataFilterTest).</summary>
public class RequestBuildingTests
{
    private const string EndpointUrl = "/endpoint/url";

    private readonly ISdkGroceryStoreService _service = new DefaultSdkGroceryStoreService().WithServicePath(EndpointUrl);

    // ---- read ----

    [Fact]
    public void Read_WithoutOptions_HasNoQuery()
    {
        var request = _service.GetAllProduct().ToRequest();

        Assert.Equal("/endpoint/url/Products", request.Url());
        Assert.Equal("Accept", Assert.Single(request.GetHeaders()).Key);
    }

    [Fact]
    public void Read_OrderBy() =>
        Assert.Equal("$orderby=Name%20asc", _service.GetAllProduct().OrderBy(Product.Fields.Name, Order.Asc).ToRequest().Query());

    [Fact]
    public void Read_OrderBy_LastCallPerFieldWins() =>
        Assert.Equal(
            "$orderby=Name%20desc",
            _service.GetAllProduct()
                .OrderBy(Product.Fields.Name, Order.Asc)
                .OrderBy(Product.Fields.Name, Order.Desc)
                .OrderBy(Product.Fields.Name, Order.Asc)
                .OrderBy(Product.Fields.Name, Order.Desc)
                .ToRequest()
                .Query());

    [Fact]
    public void Read_OrderBy_Multiple() =>
        Assert.Equal(
            "$orderby=Name%20asc,Price%20desc",
            _service.GetAllProduct().OrderBy(Product.Fields.Name, Order.Asc).OrderBy(Product.Fields.Price, Order.Desc).ToRequest().Query());

    [Fact]
    public void Read_Skip_Top()
    {
        Assert.Equal("$skip=0", _service.GetAllProduct().Skip(0).ToRequest().Query());
        Assert.Equal("$top=10", _service.GetAllProduct().Top(10).ToRequest().Query());
        Assert.Equal("$top=10&$skip=5", _service.GetAllProduct().Skip(5).Top(10).ToRequest().Query());
    }

    [Fact]
    public void Read_CustomParameter_IsEncodedAndAppended()
    {
        var request = _service.GetAllProduct().WithQueryParameter("customKey", "custom Value").ToRequest();

        Assert.Equal("/endpoint/url/Products?customKey=custom%20Value", request.Url());
    }

    [Fact]
    public void Read_Select() =>
        Assert.Equal("$select=Name", _service.GetAllProduct().Select(Product.Fields.Name).ToRequest().Query());

    [Fact]
    public void Read_SelectDuplicates_AreIgnored() =>
        Assert.Equal("$select=Name,Price", _service.GetAllProduct().Select(Product.Fields.Name, Product.Fields.Price, Product.Fields.Name).ToRequest().Query());

    [Fact]
    public void Read_SelectAll() =>
        Assert.Equal("$select=*", _service.GetAllProduct().Select(Product.Fields.All).ToRequest().Query());

    [Fact]
    public void Read_SelectAndExpand()
    {
        var request = _service.GetAllShelf()
            .Select(Shelf.Fields.Id, Shelf.Fields.FloorPlan, Shelf.Fields.Products.Select(Product.Fields.Name))
            .ToRequest();

        Assert.Equal("$select=Id,FloorPlan/*,Products/Name&$expand=FloorPlan,Products", request.Query());
        Assert.Equal(
            "/endpoint/url/Shelves?$select=Id,FloorPlan/*,Products/Name&$expand=FloorPlan,Products",
            request.Url());
    }

    [Fact]
    public void Read_NestedExpand() =>
        Assert.Equal(
            "$select=FloorPlan/*,Products/Vendor/*&$expand=FloorPlan,Products/Vendor",
            _service.GetAllShelf().Select(Shelf.Fields.FloorPlan, Shelf.Fields.Products.Select(Product.Fields.Vendor)).ToRequest().Query());

    [Fact]
    public void Read_LinkSelect_DoesNotChangeTheOriginalLink()
    {
        var selected = Shelf.Fields.Products.Select(Product.Fields.Name);

        Assert.Equal(["Products/*"], Shelf.Fields.Products.Selections);
        Assert.Equal(["Products/Name"], selected.Selections);
        Assert.Equal(["Products"], selected.Expansions);
        Assert.NotEqual(Shelf.Fields.Products, selected);
    }

    [Fact]
    public void Read_Filters() =>
        Assert.Equal(
            "$filter=((Name%20eq%20'Cloud%20SDK')%20and%20(VendorId%20eq%2042))%20or%20(VendorId%20eq%20100)",
            _service.GetAllProduct()
                .Filter(Product.Fields.Name.Eq("Cloud SDK").And(Product.Fields.VendorId.Eq(42)).Or(Product.Fields.VendorId.Eq(100)))
                .ToRequest()
                .Query());

    [Fact]
    public void Read_FilterEncoding() =>
        Assert.Equal(
            "$filter=Name%20eq%20'Cl%23ou''''d''%20S%26*%26%24*d%7C%5C/K'",
            _service.GetAllProduct().Filter(Product.Fields.Name.Eq("Cl#ou''d' S&*&$*d|\\/K")).ToRequest().Query());

    [Fact]
    public void Read_TemporalFilter() =>
        Assert.Equal(
            "$filter=(DayOfWeek%20eq%203)%20and%20(OpenTime%20lt%20time'PT18H59M59S')",
            _service.GetAllOpeningHours()
                .Filter(OpeningHours.Fields.DayOfWeek.Eq(3).And(OpeningHours.Fields.OpenTime.Lt(new TimeOnly(18, 59, 59))))
                .ToRequest()
                .Query());

    [Fact]
    public void Read_FilterWithDateTime() =>
        Assert.Equal(
            "$filter=Date%20ge%20datetime'2001-01-01T20:15:00'",
            _service.GetAllReceipt().Filter(Receipt.Fields.Date.Ge(new DateTime(2001, 1, 1, 20, 15, 0))).ToRequest().Query());

    [Fact]
    public void Read_MultipleFilters_AreCombinedWithAnd() =>
        Assert.Equal(
            "$filter=Name%20eq%20'a'%20and%20Price%20gt%205M",
            _service.GetAllProduct().Filter(Product.Fields.Name.Eq("a")).Filter(Product.Fields.Price.Gt(5m)).ToRequest().Query());

    [Fact]
    public void Read_PreferredPageSize_AndCustomHeader()
    {
        var request = _service.GetAllProduct().WithPreferredPageSize(20).WithHeader("customKey", "customValue").ToRequest();

        var headers = request.GetHeaders();
        Assert.Equal("odata.maxpagesize=20", Assert.Single(headers["Prefer"]));
        Assert.Equal("customValue", Assert.Single(headers["customKey"]));
    }

    [Fact]
    public void Read_ServicePathWithoutLeadingOrTrailingSlash_IsNormalized()
    {
        Assert.Equal("/service/path/Products", new DefaultSdkGroceryStoreService().WithServicePath("service/path/").GetAllProduct().ToRequest().Url());
        Assert.Equal("/a/b/Products", new DefaultSdkGroceryStoreService().WithServicePath("//a//b//").GetAllProduct().ToRequest().Url());
    }

    // ---- count ----

    [Fact]
    public void Count_KeepsFiltersAndCustomParameters()
    {
        var request = _service.GetAllProduct()
            .Filter(Product.Fields.Name.Eq("a"))
            .Top(5)
            .OrderBy(Product.Fields.Name, Order.Asc)
            .Count()
            .ToRequest();

        Assert.Equal("/endpoint/url/Products/$count?$filter=Name%20eq%20'a'", request.Url());
    }

    // ---- filter expressions ----

    [Theory]
    [MemberData(nameof(FilterCases))]
    public void FilterExpression_IsRendered(Func<ExpressionFluentHelper<Product>> expression, string expected)
    {
        var request = _service.GetAllProduct().Filter(expression()).ToRequest();

        Assert.Equal("$filter=" + expected.Replace(" ", "%20", StringComparison.Ordinal), request.Query());
    }

    public static TheoryData<Func<ExpressionFluentHelper<Product>>, string> FilterCases()
    {
        var name = Product.Fields.Name;
        var id = Product.Fields.Id;
        return new TheoryData<Func<ExpressionFluentHelper<Product>>, string>
        {
            { () => name.Eq("va'l''ue"), "Name eq 'va''l''''ue'" },
            { () => name.SubstringOf("foo"), "substringof('foo',Name)" },
            { () => name.EndsWith("foo"), "endswith(Name,'foo')" },
            { () => name.StartsWith("foo"), "startswith(Name,'foo')" },
            { () => name.StartsWith("foo").Or(name.Ne("bar")), "startswith(Name,'foo') or (Name ne 'bar')" },
            { () => name.EndsWith("foo").Not(), "not endswith(Name,'foo')" },
            {
                () => name.SubstringOf("bar").And(name.EndsWith("foo")).Or(name.StartsWith("foobar").Not()).Not(),
                "not ((substringof('bar',Name) and endswith(Name,'foo')) or (not startswith(Name,'foobar')))"
            },
            { () => id.Eq(1).And(ExpressionFluentHelper<Product>.Not(id.Ne(2))).Or(id.Eq(3)).Not(), "not (((Id eq 1) and (not (Id ne 2))) or (Id eq 3))" },
            { () => id.Le(1).And(id.Ge(2)).And(id.Lt(3).And(id.Gt(4))), "((Id le 1) and (Id ge 2)) and ((Id lt 3) and (Id gt 4))" },
            { () => id.Eq(null), "Id eq null" },
            { () => id.EqNull(), "Id eq null" },
            { () => id.Ne(null), "Id ne null" },
            { () => id.NeNull(), "Id ne null" },
            { () => id.Gt(null), "Id gt null" },
            { () => id.Ge(null), "Id ge null" },
            { () => id.Lt(null), "Id lt null" },
            { () => id.Le(null), "Id le null" },
        };
    }

    [Fact]
    public void FilterOnOneToOneNavigationProperty()
    {
        Assert.Equal("Customer/Name eq 'Chuck Norris'", FilterOf(Receipt.Fields.Customer.Filter(Customer.Fields.Name.Eq("Chuck Norris"))));
        Assert.Equal(
            "(Customer/Name eq 'Chuck Norris' and Customer/Email ne 'bruce@lee.com') or (not Customer/Id ne 1337)",
            FilterOf(
                Receipt.Fields.Customer.Filter(Customer.Fields.Name.Eq("Chuck Norris"))
                    .And(Receipt.Fields.Customer.Filter(Customer.Fields.Email.Ne("bruce@lee.com")))
                    .Or(ExpressionFluentHelper<Receipt>.Not(Receipt.Fields.Customer.Filter(Customer.Fields.Id.Ne(1337))))));
        Assert.Equal(
            "Customer/Address/City eq 'Potsdam'",
            FilterOf(Receipt.Fields.Customer.Filter(Customer.Fields.Address.Filter(Address.Fields.City.Eq("Potsdam")))));
        Assert.Equal(
            "Customer/(Name eq 'Chuck Norris') and (Email ne 'chuck@x.com')",
            FilterOf(Receipt.Fields.Customer.Filter(Customer.Fields.Name.Eq("Chuck Norris").And(Customer.Fields.Email.Ne("chuck@x.com")))));
    }

    private string FilterOf(ExpressionFluentHelper<Receipt> helper) =>
        Uri.UnescapeDataString(_service.GetAllReceipt().Filter(helper).ToRequest().Query()["$filter=".Length..]);

    // ---- by key ----

    [Fact]
    public void ByKey_Url()
    {
        Assert.Equal("/endpoint/url/Products(123)", _service.GetProductByKey(123).ToRequest().Url());
        Assert.Equal("/endpoint/url/Products(123)?$select=Name", _service.GetProductByKey(123).Select(Product.Fields.Name).ToRequest().Url());
        Assert.Equal("/endpoint/url/Products(123)?customKey=customValue", _service.GetProductByKey(123).WithQueryParameter("customKey", "customValue").ToRequest().Url());
    }

    // ---- create / update / delete ----

    [Fact]
    public void Create_Url_Payload_AndCsrfDefault()
    {
        var customer = Customer.Builder().Id(1).Name("Alice").Address(Address.Builder().City("Walldorf").Build()).Build();

        var request = _service.CreateCustomer(customer).ToRequest();

        Assert.Equal("/endpoint/url/Customers", request.Url());
        JsonAssert.Equal("""{"Id":1,"Name":"Alice","Address":{"City":"Walldorf"}}""", request.SerializedEntity);
    }

    private static Product NewProduct() =>
        Product.Builder().Id(123).Name("Mate").Price(9.99m).Image([1, 2, 3]).Build();

    [Fact]
    public void Update_DefaultsToPatch()
    {
        var product = NewProduct();

        Assert.Equal(UpdateStrategy.ModifyWithPatch, _service.UpdateProduct(product).ToRequest().UpdateStrategy);
        Assert.Equal(UpdateStrategy.ModifyWithPatch, _service.UpdateProduct(product).ModifyingEntity().ToRequest().UpdateStrategy);
        Assert.Equal(UpdateStrategy.ReplaceWithPut, _service.UpdateProduct(product).ReplacingEntity().ToRequest().UpdateStrategy);
        Assert.Equal(UpdateStrategy.ModifyWithPatch, _service.UpdateProduct(product).ReplacingEntity().ModifyingEntity().ToRequest().UpdateStrategy);
    }

    [Fact]
    public void Update_Put_SendsEverythingAndTheVersionIdentifier()
    {
        var product = NewProduct();
        product.VersionIdentifier = "TestEtag";

        var request = _service.UpdateProduct(product).ReplacingEntity().ToRequest();

        Assert.Equal("/endpoint/url/Products(123)", request.Url());
        Assert.Equal("TestEtag", request.VersionIdentifier);
        Assert.Equal(ODataEntitySerializer.SerializeEntityForUpdatePut(product, null), request.SerializedEntity);
        JsonAssert.Equal(
            """{"Id":123,"Name":"Mate","ShelfId":null,"VendorId":null,"Price":"9.99","Image":"AQID","Vendor":null,"Shelf":[]}""",
            request.SerializedEntity);
    }

    [Fact]
    public void Update_Put_WithExcludedFields()
    {
        var product = NewProduct();

        var request = _service.UpdateProduct(product).ReplacingEntity().ExcludingFields(Product.Fields.Name, Product.Fields.Vendor).ToRequest();

        JsonAssert.Equal("""{"Id":123,"ShelfId":null,"VendorId":null,"Price":"9.99","Image":"AQID","Shelf":[]}""", request.SerializedEntity);
    }

    [Fact]
    public void Update_Patch_SendsChangedFieldsOnly()
    {
        var product = NewProduct();
        product.VersionIdentifier = "TestEtag";
        product.Name = "ChangedName";

        var request = _service.UpdateProduct(product).ModifyingEntity().ToRequest();

        Assert.Equal("{\"Name\":\"ChangedName\"}", request.SerializedEntity);
        Assert.Equal("TestEtag", request.VersionIdentifier);
    }

    [Fact]
    public void Update_Patch_WithoutChanges_SendsEmptyObject() =>
        Assert.Equal("{}", _service.UpdateProduct(NewProduct()).ToRequest().SerializedEntity);

    [Fact]
    public void Update_Patch_ContainsUpdatedAndIncludedFields()
    {
        var product = NewProduct();
        product.Name = "ChangedName";

        var request = _service.UpdateProduct(product).IncludingFields(Product.Fields.Image, Product.Fields.Shelf).ToRequest();

        JsonAssert.Equal("""{"Shelf":[],"Image":"AQID","Name":"ChangedName"}""", request.SerializedEntity);
    }

    [Fact]
    public void Update_Patch_IncludedFieldThatWasAlsoChanged_IsSentOnce()
    {
        var product = NewProduct();
        product.Name = "ChangedName";

        var request = _service.UpdateProduct(product).IncludingFields(Product.Fields.Image, Product.Fields.Name).ToRequest();

        JsonAssert.Equal("""{"Image":"AQID","Name":"ChangedName"}""", request.SerializedEntity);
    }

    [Fact]
    public void Update_Patch_SetToNull_IsSent()
    {
        var product = NewProduct();
        product.Price = null;
        product.Name = "ChangedName";

        JsonAssert.Equal("""{"Price":null,"Name":"ChangedName"}""", _service.UpdateProduct(product).ToRequest().SerializedEntity);
    }

    [Fact]
    public void Update_Patch_ComplexPropertyDeltaAndFull()
    {
        var count1 = ProductCount.Builder().ProductId(123).Quantity(10).Build();
        var receipt = Receipt.Builder().Id(1001).CustomerId(9001).ProductCount1(count1).Build();
        count1.Quantity = 20;

        var helper = FluentHelperFactory.WithServicePath(EndpointUrl);
        Assert.Equal(
            "{\"ProductCount1\":{\"Quantity\":20}}",
            helper.Update("Receipts", receipt).ModifyingEntity(ModifyPatchStrategy.RecursiveDelta).ToRequest().SerializedEntity);
        Assert.Equal(
            "{\"ProductCount1\":{\"ProductId\":123,\"Quantity\":20}}",
            helper.Update("Receipts", receipt).ModifyingEntity(ModifyPatchStrategy.RecursiveFull).ToRequest().SerializedEntity);
        Assert.Equal("{}", helper.Update("Receipts", receipt).ModifyingEntity(ModifyPatchStrategy.Shallow).ToRequest().SerializedEntity);
    }

    [Fact]
    public void Update_VersionIdentifierStrategies()
    {
        var product = NewProduct();
        product.VersionIdentifier = "TestEtag";

        Assert.Equal("*", _service.UpdateProduct(product).MatchAnyVersionIdentifier().ToRequest().VersionIdentifier);
        Assert.Null(_service.UpdateProduct(product).DisableVersionIdentifier().ToRequest().VersionIdentifier);
        Assert.Equal("TestEtag", _service.UpdateProduct(product).ToRequest().VersionIdentifier);
    }

    [Fact]
    public void Delete_Url_AndVersionIdentifier()
    {
        var shelf = Shelf.Builder().Id(101).Build();

        var request = _service.DeleteShelf(shelf).ToRequest();
        Assert.Equal("/endpoint/url/Shelves(101)", request.Url());
        Assert.Null(request.VersionIdentifier);

        shelf.VersionIdentifier = "W/\"1\"";
        Assert.Equal("W/\"1\"", _service.DeleteShelf(shelf).ToRequest().VersionIdentifier);
        Assert.Equal("*", _service.DeleteShelf(shelf).MatchAnyVersionIdentifier().ToRequest().VersionIdentifier);
        Assert.Null(_service.DeleteShelf(shelf).DisableVersionIdentifier().ToRequest().VersionIdentifier);
    }

    [Fact]
    public void Create_AsChildOf_UsesTheNavigationPropertyOfTheParent()
    {
        var shelf = Shelf.Builder().Id(7).Build();
        var product = Product.Builder().Name("Milk").Build();

        var request = FluentHelperFactory.WithServicePath(EndpointUrl)
            .Create("Products", product)
            .AsChildOf(shelf, Shelf.Fields.Products)
            .ToRequest();

        Assert.Equal("/endpoint/url/Shelves(7)/Products", request.Url());
        Assert.Equal("{\"Name\":\"Milk\"}", request.SerializedEntity);
    }

    [Fact]
    public void Create_AsChildOf_NullArguments_CreatesInTheCollection()
    {
        var request = FluentHelperFactory.WithServicePath(EndpointUrl)
            .Create("Products", Product.Builder().Name("Milk").Build())
            .AsChildOf<Shelf, ShelfLink<Product>>(null, null)
            .ToRequest();

        Assert.Equal("/endpoint/url/Products", request.Url());
    }

    // ---- function imports ----

    [Fact]
    public void FunctionImport_Get_PutsParametersInTheQuery()
    {
        var request = _service.GetProductQuantities(3, 5).ToRequest();

        Assert.IsType<ODataRequestFunction>(request);
        Assert.Equal("/endpoint/url/GetProductQuantities?ShelfId=3&ProductId=5", request.Url());
    }

    [Fact]
    public void FunctionImport_Post_IsAnAction()
    {
        var request = _service.OrderProduct(1, 2, 3).ToRequest();

        Assert.IsType<ODataRequestAction>(request);
        Assert.Equal("/endpoint/url/OrderProduct?CustomerId=1&ProductId=2&Quantity=3", request.Url());
    }

    [Fact]
    public void FunctionImport_ParameterLiterals()
    {
        Assert.Equal(
            "/endpoint/url/IsStoreOpen?DateTime=datetime'2015-01-12T12:12:00'",
            _service.IsStoreOpen(new DateTime(2015, 1, 12, 12, 12, 0)).ToRequest().Url());
        Assert.Equal("/endpoint/url/PrintReceipt?ReceiptId=7", _service.PrintReceipt(7).ToRequest().Url());
    }

    [Theory]
    [InlineData("ba+r", "foo='ba%2Br'")]
    [InlineData("b a+  r ", "foo='b%20a%2B%20%20r%20'")]
    [InlineData("Unusual? Road #99 with 100%", "foo='Unusual%3F%20Road%20%2399%20with%20100%25'")]
    [InlineData("Potsdam (Germany)", "foo='Potsdam%20(Germany)'")]
    [InlineData("it's", "foo='it''s'")]
    public void FunctionImport_ParameterEncoding(string value, string expectedQuery)
    {
        var parameters = new Dictionary<string, object?> { ["foo"] = value };

        var request = FluentHelperFactory.WithServicePath("/some/service/path").FunctionSingle<string>(parameters, "Testing", HttpMethod.Get).ToRequest();

        Assert.Equal("/some/service/path/Testing?" + expectedQuery, request.Url());
    }

    [Fact]
    public void FunctionImport_GuidParameter()
    {
        var parameters = new Dictionary<string, object?> { ["GUID"] = Guid.Parse("571c74ab-c66a-4d34-ab32-ff16ec4653a5") };

        var request = FluentHelperFactory.WithServicePath("/s").FunctionSingle<string>(parameters, "Testing", HttpMethod.Get).ToRequest();

        Assert.Equal("/s/Testing?GUID=guid'571c74ab-c66a-4d34-ab32-ff16ec4653a5'", request.Url());
    }

    // ---- execution target ----

    [Fact]
    public async Task ExecuteAsync_WithDestination_UsesHttpClientAccessorAndDestinationAuthentication()
    {
        ODataHttpClientProvider.Reset();
        using var factory = Sap.Cloud.Sdk.TestSupport.RecordingDestinationHttpClientFactory.Install(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"d":{"results":[]}}""", System.Text.Encoding.UTF8, "application/json") });
        var destination = Sap.Cloud.Sdk.CloudPlatform.Connectivity.DefaultHttpDestination.CreateBuilder("https://grocery.example.org/sap").BasicCredentials("user", "pass").Build();

        var products = await _service.GetAllProduct().ExecuteAsync(destination);

        Assert.Empty(products);
        var request = Assert.Single(factory.Requests);
        Assert.StartsWith("https://grocery.example.org/sap/", request.Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("Basic dXNlcjpwYXNz", request.Authorization);
    }
}
