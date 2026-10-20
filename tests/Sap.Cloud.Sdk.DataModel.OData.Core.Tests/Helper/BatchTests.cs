using System.Text.RegularExpressions;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Namespaces.SdkGroceryStore.Batch;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Support;
using Sap.Cloud.Sdk.DataModel.OData.Helper;
using Sap.Cloud.Sdk.DataModel.OData.Helper.Batch;

namespace Sap.Cloud.Sdk.DataModel.OData.Core.Tests.Helper;

/// <summary>Batch requests and responses (Java: ODataV2BatchRequestUnitTest, ODatav2BatchConnectionTest, ODataV2BatchFunctionImportTest).</summary>
public partial class BatchTests
{
    private const string ResponseContentType = "multipart/mixed; boundary=batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef";

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex NewLines();

    private static string TestData(string name) =>
        NewLines().Replace(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name)), "\r\n");

    private static HttpResponseMessage BatchResponse(string body) => Responses.Text(body, ResponseContentType);

    // ---- hand-written batch helpers of the test entity (port of TestVdmEntityBatch) ----

    private sealed class TestBatch : BatchFluentHelperBasic<TestBatch, TestChangeSet>, IFluentHelperServiceBatch<TestBatch, TestChangeSet>
    {
        public TestBatch()
        {
            var counter = 0;
            UuidProvider = () => new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)Interlocked.Increment(ref counter));
        }

        protected override string ServicePathForBatchRequest => string.Empty;

        protected override TestBatch This => this;

        public override TestChangeSet BeginChangeSet() => new(this);
    }

    private sealed class TestChangeSet(TestBatch batch) : BatchChangeSetFluentHelperBasic<TestBatch, TestChangeSet>(batch, batch), IFluentHelperBatchEndChangeSet<TestBatch>
    {
        protected override TestChangeSet This => this;

        public TestChangeSet Create(TestVdmEntity entity) => AddRequestCreate(e => new TestEntityCreate(e), entity);

        public TestChangeSet Update(TestVdmEntity entity) => AddRequestUpdate(e => new TestEntityUpdate(e), entity);

        public TestChangeSet Delete(TestVdmEntity entity) => AddRequestDelete(e => new TestEntityDelete(e), entity);
    }

    private sealed class TestEntityRead : FluentHelperRead<TestEntityRead, TestVdmEntity, IEntitySelectable<TestVdmEntity>>
    {
        public TestEntityRead()
            : base(string.Empty, "Entities")
        {
        }
    }

    private sealed class TestEntityByKey(IReadOnlyDictionary<string, object?> key)
        : FluentHelperByKey<TestEntityByKey, TestVdmEntity, IEntitySelectable<TestVdmEntity>>(string.Empty, "Entities")
    {
        protected override IReadOnlyDictionary<string, object?> Key => key;
    }

    private sealed class TestEntityCreate(TestVdmEntity entity) : FluentHelperCreate<TestEntityCreate, TestVdmEntity>(string.Empty, "Entities")
    {
        protected override TestVdmEntity Entity => entity;
    }

    private sealed class TestEntityUpdate(TestVdmEntity entity) : FluentHelperUpdate<TestEntityUpdate, TestVdmEntity>(string.Empty, "Entities")
    {
        protected override TestVdmEntity Entity => entity;
    }

    private sealed class TestEntityDelete(TestVdmEntity entity) : FluentHelperDelete<TestEntityDelete, TestVdmEntity>(string.Empty, "Entities")
    {
        protected override TestVdmEntity Entity => entity;
    }

    private sealed class TestFunction(string method, params (string Name, object Value)[] parameters)
        : SingleValuedFluentHelperFunction<TestFunction, string, string?>(string.Empty)
    {
        private readonly Dictionary<string, object?> _values = parameters.ToDictionary(p => p.Name, p => (object?)p.Value);

        protected override string FunctionName => "awesomeFunction";

        protected override IReadOnlyDictionary<string, object?> Parameters => _values;

        protected override HttpMethod RequestMethod => new(method);

        protected override Task<string?> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>
            ExecuteSingleAsync(target, cancellationToken);
    }

    private sealed class TestCollectionFunction : CollectionValuedFluentHelperFunction<TestCollectionFunction, string, IReadOnlyList<string>>
    {
        public TestCollectionFunction()
            : base(string.Empty)
        {
        }

        protected override string FunctionName => "awesomeFunction";

        protected override IReadOnlyDictionary<string, object?> Parameters { get; } = new Dictionary<string, object?>();

        protected override HttpMethod RequestMethod => HttpMethod.Get;

        protected override Task<IReadOnlyList<string>> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>
            ExecuteMultipleAsync(target, cancellationToken);
    }

    private static TestVdmEntity Entity(int id) => TestVdmEntity.Built(e => e.IntegerValue = id);

    // ---- tests ----

    [Fact]
    public async Task AllOperations_AreSentAsOneBatchAndParsed()
    {
        var server = new FakeODataServer(BatchResponse(TestData("BatchResponse.txt")));
        var readAll = new TestEntityRead().Select(new CustomSelectable("StringValue"), new CustomSelectable("IntegerValue")).Top(10);
        var readByKey = new TestEntityByKey(new Dictionary<string, object?> { ["IntegerValue"] = 9000 });
        var batch = new TestBatch()
            .AddReadOperations(readAll)
            .BeginChangeSet().Create(Entity(12)).EndChangeSet()
            .AddReadOperations(readByKey)
            .BeginChangeSet().Update(Entity(13)).Delete(Entity(14)).EndChangeSet()
            .BeginChangeSet().Delete(Entity(15)).Delete(Entity(16)).EndChangeSet();

        using var response = await batch.ExecuteAsync(server.Client);

        // request
        var post = server.SingleDataRequest;
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/$batch", post.PathAndQuery);
        Assert.Matches("^multipart/mixed;\\s?boundary=batch_[a-z0-9-]+$", post.GetFirstHeader("Content-Type"));
        Assert.Equal(FakeODataServer.CsrfToken, post.GetFirstHeader("x-csrf-token"));
        Assert.Equal(TestData("BatchRequest.txt"), post.Body);

        // change sets
        var first = (await response.GetAsync(0)).GetValueOrThrow();
        var firstCreated = await first.GetCreatedEntitiesAsync();
        Assert.Equal(1337, Assert.IsType<TestVdmEntity>(Assert.Single(firstCreated)).IntegerValue);
        var second = await response.GetAsync(1);
        Assert.True(second.IsSuccess);
        Assert.Empty(await second.GetValueOrThrow().GetCreatedEntitiesAsync());
        var third = await response.GetAsync(2);
        Assert.True(third.IsFailure);
        Assert.IsType<ODataServiceErrorException>(third.Exception);
        var outOfRange = await response.GetAsync(3);
        Assert.True(outOfRange.IsFailure);
        Assert.IsType<ArgumentException>(outOfRange.Exception);

        // reads
        Assert.Equal("Foo", Assert.Single(await response.GetReadResultAsync(readAll)).StringValue);
        Assert.Equal(9000, (await response.GetReadResultAsync(readByKey)).IntegerValue);
    }

    private sealed class CustomSelectable(string name) : IEntitySelectable<TestVdmEntity>
    {
        public string FieldName => name;
    }

    [Fact]
    public async Task CustomHeaders_AreSentWithTheirOperation()
    {
        var server = new FakeODataServer(BatchResponse(TestData("BatchResponse.txt")));
        var readAll = new TestEntityRead().Select(new CustomSelectable("StringValue"), new CustomSelectable("IntegerValue")).Top(10).WithHeader("header-read_all", "read_all");
        var readByKey = new TestEntityByKey(new Dictionary<string, object?> { ["IntegerValue"] = 9000 }).WithHeader("header-read_by_key", "read_by_key");
        var batch = new TestBatch()
            .AddReadOperations(readAll)
            .AddChangeSet(new TestEntityCreate(Entity(12)).WithHeader("header-create", "create"))
            .AddReadOperations(readByKey)
            .AddChangeSet(new TestEntityUpdate(Entity(13)).WithHeader("header-update", "update"), new TestEntityDelete(Entity(14)).WithHeader("header-delete", "delete-entity14"))
            .AddChangeSet(new TestEntityDelete(Entity(15)).WithHeader("header-delete", "delete-entity15"), new TestEntityDelete(Entity(16)).WithHeader("header-delete", "delete-entity16"));

        using var response = await batch.ExecuteAsync(server.Client);

        Assert.Equal(TestData("BatchRequestWithCustomHeaders.txt"), server.SingleDataRequest.Body);
        Assert.True((await response.GetAsync(0)).IsSuccess);
        Assert.True((await response.GetAsync(1)).IsSuccess);
        Assert.True((await response.GetAsync(2)).IsFailure);
    }

    [Fact]
    public async Task WithoutCsrfToken_SendsNoHeadRequest()
    {
        var server = new FakeODataServer(BatchResponse(TestData("BatchResponse.txt")));

        using var response = await new TestBatch().WithoutCsrfToken().AddReadOperations(new TestEntityRead()).ExecuteAsync(server.Client);

        Assert.Equal(HttpMethod.Post, Assert.Single(server.Requests).Method);
    }

    [Fact]
    public async Task ResponseWithChangeSets_YieldsCreatedEntities()
    {
        var server = new FakeODataServer(BatchResponse(TestData("ODatav2BatchConnectionTest-BatchResponseWithChangeset.txt")));
        var read = new TestEntityRead();
        var readByKey = new TestEntityByKey(new Dictionary<string, object?> { ["IntegerValue"] = 9000 });
        var batch = new TestBatch()
            .AddReadOperations(read)
            .AddChangeSet(new TestEntityCreate(Entity(1337)))
            .AddReadOperations(readByKey)
            .AddChangeSet(new TestEntityUpdate(Entity(13)), new TestEntityDelete(Entity(14)), new TestEntityCreate(Entity(1447)));

        using var response = await batch.ExecuteAsync(server.Client);

        Assert.NotEmpty(await response.GetReadResultAsync(read));
        Assert.Equal(9000, (await response.GetReadResultAsync(readByKey)).IntegerValue);
        var created = await (await response.GetAsync(0)).GetValueOrThrow().GetCreatedEntitiesAsync();
        Assert.Equal(1337, Assert.IsType<TestVdmEntity>(Assert.Single(created)).IntegerValue);
        Assert.Single(await (await response.GetAsync(1)).GetValueOrThrow().GetCreatedEntitiesAsync());
    }

    [Fact]
    public async Task ResponseWithoutChangeSets_YieldsReadResults()
    {
        var server = new FakeODataServer(BatchResponse(TestData("ODatav2BatchConnectionTest-BatchResponseWithoutChangeset.txt")));
        var read = new TestEntityRead();
        var batch = new TestBatch().AddReadOperations(read);

        using var response = await batch.ExecuteAsync(server.Client);

        Assert.NotEmpty(await response.GetReadResultAsync(read));
    }

    [Fact]
    public async Task ResponseWithError_FailsTheChangeSet_ButReadsStillWork()
    {
        var server = new FakeODataServer(BatchResponse(TestData("ODatav2BatchConnectionTest-BatchResponseWithError.txt")));
        var read = new TestEntityRead();
        var readByKey = new TestEntityByKey(new Dictionary<string, object?> { ["IntegerValue"] = 9000 });
        var batch = new TestBatch().AddReadOperations(read).AddReadOperations(readByKey).AddChangeSet(new TestEntityDelete(Entity(14)));

        using var response = await batch.ExecuteAsync(server.Client);

        Assert.NotEmpty(await response.GetReadResultAsync(read));
        Assert.Equal(9000, (await response.GetReadResultAsync(readByKey)).IntegerValue);
        var changeSet = await response.GetAsync(0);
        Assert.True(changeSet.IsFailure);
        Assert.IsType<ODataServiceErrorException>(changeSet.Exception);
    }

    [Fact]
    public async Task FunctionImport_RequestBodies()
    {
        Assert.Equal(
            TestData("ODataV2BatchFunctionImportTest-BatchRequestFunctionImportWithGet.txt"),
            await BodyOfAsync(new TestBatch().AddReadOperations(new TestFunction("GET", ("FirstName", "John"), ("LastName", "Doe")))));
        Assert.Equal(
            TestData("ODataV2BatchFunctionImportTest-BatchRequestFunctionImportWithPost.txt"),
            await BodyOfAsync(new TestBatch().BeginChangeSet().AddFunctionImport(new TestFunction("POST", ("FirstName", "John"), ("LastName", "Doe"))).EndChangeSet()));
    }

    private static async Task<string> BodyOfAsync(TestBatch batch)
    {
        var server = new FakeODataServer(BatchResponse("--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef--"));
        using var response = await batch.ExecuteAsync(server.Client);
        return server.SingleDataRequest.Body!;
    }

    private sealed class TestEntityFunction(int integerValue, string stringValue)
        : SingleValuedFluentHelperFunction<TestEntityFunction, TestVdmEntity, TestVdmEntity?>(string.Empty)
    {
        private readonly Dictionary<string, object?> _values = new() { ["IntegerValue"] = integerValue, ["StringValue"] = stringValue };

        protected override string FunctionName => "awesomeFunction";

        protected override IReadOnlyDictionary<string, object?> Parameters => _values;

        protected override HttpMethod RequestMethod => HttpMethod.Get;

        protected override Task<TestVdmEntity?> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>
            ExecuteSingleAsync(target, cancellationToken);
    }

    [Fact]
    public async Task FunctionImport_ResultsAreRead()
    {
        var server = new FakeODataServer(BatchResponse(TestData("ODataV2BatchFunctionImportTest-BatchResponse.txt")));
        var readAll = new TestEntityRead().Top(10);
        var entityByKey = new TestEntityByKey(new Dictionary<string, object?> { ["IntegerValue"] = 9000 });
        var single = new TestFunction("GET", ("FirstName", "Alice"), ("LastName", "Bob"));
        var collection = new TestCollectionFunction();
        var entityFunction = new TestEntityFunction(33, "Alice");
        var batch = new TestBatch()
            .AddReadOperations(readAll)
            .AddChangeSet(new TestEntityCreate(Entity(1)))
            .AddReadOperations(entityByKey)
            .AddChangeSet(new TestEntityUpdate(Entity(2)), new TestEntityDelete(Entity(3)))
            .BeginChangeSet().Create(Entity(10)).AddFunctionImport(new TestFunction("POST", ("FirstName", "John"), ("LastName", "Doe"))).EndChangeSet()
            .AddReadOperations(single, collection)
            .AddReadOperations(entityFunction);

        using var response = await batch.ExecuteAsync(server.Client);

        Assert.Equal(9000, (await response.GetReadResultAsync(entityByKey)).IntegerValue);
        Assert.Equal("awesomeStuff", await response.GetReadResultAsync(single));
        Assert.Equal(["foo", "bar"], await response.GetReadResultAsync(collection));
        var entity = await response.GetReadResultAsync(entityFunction);
        Assert.Equal(33, entity.IntegerValue);
        Assert.Equal("Alice", entity.StringValue);
        Assert.True((await response.GetAsync(0)).IsSuccess);
        Assert.True((await response.GetAsync(1)).IsSuccess);
        Assert.True((await response.GetAsync(2)).IsFailure);
    }

    [Fact]
    public void FunctionImport_GetInChangeSet_IsRejected()
    {
        var changeSet = new TestBatch().BeginChangeSet();

        Assert.Throws<InvalidOperationException>(() => changeSet.AddFunctionImport(new TestFunction("GET")));
    }

    [Fact]
    public void FunctionImport_PostAsReadOperation_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new TestBatch().AddReadOperations(new TestFunction("POST")));
    }

    [Fact]
    public async Task SampleService_Batch()
    {
        var server = new FakeODataServer(
            BatchResponse(
                """
                --batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef
                Content-Type: application/http
                Content-Transfer-Encoding: binary

                HTTP/1.1 200 OK
                Content-Type: application/json

                {"d":{"results":[{"Id":1,"Name":"Alice"}]}}

                --batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef
                Content-Type: multipart/mixed; boundary=changesetresponse_1

                --changesetresponse_1
                Content-Type: application/http
                Content-Transfer-Encoding: binary

                HTTP/1.1 201 Created
                Content-Type: application/json

                {"d":{"Id":2,"Name":"Bob"}}
                --changesetresponse_1
                Content-Type: application/http
                Content-Transfer-Encoding: binary

                HTTP/1.1 204 No Content


                --changesetresponse_1--
                --batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef--
                """.ReplaceLineEndings("\r\n")));
        ISdkGroceryStoreService service = new DefaultSdkGroceryStoreService().WithServicePath("/sap/store");
        var readCustomers = service.GetAllCustomer();

        using var response = await service.Batch()
            .AddReadOperations(readCustomers)
            .BeginChangeSet()
            .CreateCustomer(Customer.Builder().Name("Bob").Build())
            .UpdateAddress(Address.Builder().Id(7).City("Walldorf").Build())
            .EndChangeSet()
            .ExecuteAsync(server.Client);

        var body = server.SingleDataRequest.Body!;
        Assert.Equal("/sap/store/$batch", server.SingleDataRequest.PathAndQuery);
        Assert.Contains("GET Customers HTTP/1.1", body, StringComparison.Ordinal);
        Assert.Contains("POST Customers HTTP/1.1", body, StringComparison.Ordinal);
        Assert.Contains("{\"Name\":\"Bob\"}", body, StringComparison.Ordinal);
        Assert.Contains("PATCH Addresses(7) HTTP/1.1", body, StringComparison.Ordinal);

        Assert.Equal("Alice", Assert.Single(await response.GetReadResultAsync(readCustomers)).Name);
        var created = await (await response.GetAsync(0)).GetValueOrThrow().GetCreatedEntitiesAsync();
        Assert.Equal("Bob", Assert.IsType<Customer>(Assert.Single(created)).Name);
    }
}
