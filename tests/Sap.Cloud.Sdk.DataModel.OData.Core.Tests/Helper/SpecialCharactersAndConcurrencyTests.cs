using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Encoding of keys and filter values (Java: SpecialCharactersTest) and thread safety of the cached serializer (Java: SerializerRaceConditionTest).</summary>
public class SpecialCharactersAndConcurrencyTests
{
    private const string ServiceUrl = "/service/path";

    /// <summary>An entity with a composite string key, defined without any metadata beyond the key override.</summary>
    private sealed class Ticket : VdmEntity<Ticket>
    {
        private static readonly VdmTypeMetadata Metadata = VdmTypeMetadata.For<Ticket>().Build();

        public string? KeyVal1 { get; set; }

        public string? KeyVal2 { get; set; }

        public Task<Ticket> FetchAsync(string fieldName) => FetchFieldAsSingleAsync<Ticket>(fieldName);

        protected override VdmTypeMetadata TypeMetadata => Metadata;

        protected override string? EntityCollection => "Tickets";

        protected override IReadOnlyDictionary<string, object?> GetKey()
        {
            var key = new OrderedDictionary<string, object?> { ["Key1"] = KeyVal1 };
            if (KeyVal2 is not null)
            {
                key["Key2"] = KeyVal2;
            }
            return key;
        }
    }

    private static FakeODataServer Server(string body) => new(Responses.Json(body));

    [Fact]
    public async Task GetByKey_EncodesCompositeKeys()
    {
        var server = Server("""{"d":{"Foo":"0"}}""");
        var key = new Dictionary<string, object?> { ["Key1"] = "F?oo", ["Key2"] = "A/ #" };

        var item = await FluentHelperFactory.WithServicePath(ServiceUrl).ReadByKey<Ticket, IEntitySelectable>("Tickets", key).ExecuteAsync(server.Client);

        Assert.Equal("0", item.CustomFields["Foo"]);
        Assert.Equal(ServiceUrl + "/Tickets(Key1='F%3Foo',Key2='A%2F%20%23')", Assert.Single(server.Requests).PathAndQuery);
    }

    [Fact]
    public async Task FetchNavigationProperty_EncodesTheKey()
    {
        var server = Server("""{"d":{"Foo":"0"}}""");
        var item = new Ticket { KeyVal1 = "s4L3s/ 0rd3?r", KeyVal2 = "1t3#m" };
        item.AttachToService(ServiceUrl, server.Client);

        var next = await item.FetchAsync("to_NextTicket");

        Assert.NotNull(next);
        Assert.Same(item.FetchTarget, next.FetchTarget);
        Assert.Equal(ServiceUrl + "/Tickets(Key1='s4L3s%2F%200rd3%3Fr',Key2='1t3%23m')/to_NextTicket", Assert.Single(server.Requests).PathAndQuery);
    }

    [Fact]
    public async Task Filter_EncodesSpecialCharacters()
    {
        var server = Server("""{"d":{"results":[{"Foo":"0"}]}}""");
        var field1 = new EntityField<Ticket, string>("Field1");
        var field2 = new EntityField<Ticket, string>("Field2");

        var items = await FluentHelperFactory.WithServicePath(ServiceUrl).Read<Ticket, IEntitySelectable>("Tickets")
            .Filter(field1.Eq("&?:/").And(field2.Eq("#/\\")))
            .ExecuteAsync(server.Client);

        Assert.Single(items);
        Assert.Equal(ServiceUrl + "/Tickets?$filter=(Field1%20eq%20'%26%3F:/')%20and%20(Field2%20eq%20'%23/%5C')", Assert.Single(server.Requests).PathAndQuery);
    }

    [Fact]
    public async Task NestedSingleQuotes_AreEscapedInFiltersAndKeys()
    {
        var filterServer = Server("""{"d":{"results":[{"Foo":"foo'bar"}]}}""");
        await FluentHelperFactory.WithServicePath(ServiceUrl).Read<Ticket, IEntitySelectable>("Tickets")
            .Filter(new EntityField<Ticket, string>("Field1").Eq("foo'bar"))
            .ExecuteAsync(filterServer.Client);
        Assert.Equal(ServiceUrl + "/Tickets?$filter=Field1%20eq%20'foo''bar'", Assert.Single(filterServer.Requests).PathAndQuery);

        var keyServer = Server("""{"d":{"Foo":"foo'bar"}}""");
        await FluentHelperFactory.WithServicePath(ServiceUrl)
            .ReadByKey<Ticket, IEntitySelectable>("Tickets", new Dictionary<string, object?> { ["Field1"] = "foo'bar" })
            .ExecuteAsync(keyServer.Client);
        Assert.Equal(ServiceUrl + "/Tickets('foo''bar')", Assert.Single(keyServer.Requests).PathAndQuery);

        var navigationServer = Server("""{"d":{"results":[{"Foo":"0"}]}}""");
        var item = new Ticket { KeyVal1 = "foo'bar" };
        item.AttachToService(ServiceUrl, navigationServer.Client);
        await item.FetchAsync("to_NextTicket");
        Assert.Equal(ServiceUrl + "/Tickets('foo''bar')/to_NextTicket", Assert.Single(navigationServer.Requests).PathAndQuery);
    }

    [Fact]
    public async Task SingleQuoteOnlyKeys_AreEscaped()
    {
        var server = Server("""{"d":{"Foo":"0"}}""");

        await FluentHelperFactory.WithServicePath(ServiceUrl)
            .ReadByKey<Ticket, IEntitySelectable>("Tickets", new Dictionary<string, object?> { ["Key1"] = "'" })
            .ExecuteAsync(server.Client);

        Assert.Equal(ServiceUrl + "/Tickets('''')", Assert.Single(server.Requests).PathAndQuery);
    }

    [Fact]
    public async Task TimeValues_TravelBothWays()
    {
        var server = new FakeODataServer(request =>
            request.Method == HttpMethod.Get
                ? Responses.Json("""{"d":{"Id":1,"DayOfWeek":3,"OpenTime":"PT8H30M0S","CloseTime":"PT18H0M0S"}}""")
                : Responses.Empty(204));
        var service = new DefaultSdkGroceryStoreService().WithServicePath(ServiceUrl);

        var hours = await service.GetOpeningHoursByKey(1).ExecuteAsync(server.Client);
        Assert.Equal(new TimeOnly(8, 30), hours.OpenTime);
        Assert.Empty(hours.GetChangedFields());

        hours.CloseTime = new TimeOnly(19, 15, 30, 250);
        await service.UpdateOpeningHours(hours).ExecuteAsync(server.Client);

        Assert.Equal("{\"CloseTime\":\"PT19H15M30.25S\"}", server.DataRequests[^1].Body);
    }

    [Fact]
    public async Task ConcurrentSerializationAndDeserialization_IsSafe()
    {
        var service = new DefaultSdkGroceryStoreService().WithServicePath(ServiceUrl);
        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            for (var round = 0; round < 50; round++)
            {
                var product = Product.Builder().Id(i).Name("Name" + round).Price(i + 0.5m).Build();
                product.Name = "Changed" + round;
                var payload = service.UpdateProduct(product).ToRequest().SerializedEntity;
                Assert.Equal("{\"Name\":\"Changed" + round + "\"}", payload);
                var read = System.Text.Json.JsonSerializer.Deserialize<Product>(
                    "{\"Id\":" + i + ",\"Name\":\"x\",\"Price\":\"" + i + ".5\",\"Unknown\":" + round + "}",
                    Adapter.ODataVdmJsonOptions.Get())!;
                Assert.Equal(i + 0.5m, read.Price);
                Assert.Equal(round, read.CustomFields["Unknown"]);
            }
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void JsonOptions_AreCached()
    {
        Assert.Same(Adapter.ODataVdmJsonOptions.Get(), Adapter.ODataVdmJsonOptions.Get());
        Assert.Same(Adapter.ODataVdmJsonOptions.Get(true), Adapter.ODataVdmJsonOptions.Get(true));
        Assert.NotSame(Adapter.ODataVdmJsonOptions.Get(true), Adapter.ODataVdmJsonOptions.Get());
    }
}
