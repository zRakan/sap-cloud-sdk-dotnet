using System.Text.RegularExpressions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Fixtures;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>
/// Batch requests with reads and change sets: request bodies and response mapping. Ports
/// <c>ODataV4BatchRequestUnitTest</c>, <c>ODatav4BatchConnectionTest</c> and <c>ODataV4BatchReferenceServiceUnitTest</c>.
/// </summary>
public partial class BatchTests
{
    private const string ResponseContentType = "multipart/mixed; boundary=batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef";
    private const string RequestContentType = "multipart/mixed;boundary=batch_00000000-0000-0000-0000-000000000001";

    [GeneratedRegex("(?<!\\r)\\n")]
    private static partial Regex LineFeed();

    private static string Crlf(string directory, string file) =>
        LineFeed().Replace(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", directory, file)), "\r\n");

    private static FakeHttpHandler Handler(string responseBody, int status = 200) =>
        new(request => request.Method == HttpMethod.Head
            ? Responses.NoContent().WithHeader("x-csrf-token", "awesome-csrf-token")
            : Responses.Text(responseBody, ResponseContentType, status));

    // ------------------------------------------------------------------ TestEntity service

    [Fact]
    public async Task AllOperations_RequestBodyAndResponseParsing()
    {
        var handler = Handler(Crlf("ODataV4BatchRequestUnitTest", "BatchReadsAndWritesSuccessResponse.txt"));
        var entityUpdate = TestEntity.WithId("upd");
        var entityCreate = new TestEntity();
        var readAll = TestEntityService.GetTestEntities();
        var readByKey = TestEntityService.GetTestEntitiesByKey("foobar");
        var create = TestEntityService.CreateTestEntity(entityCreate);
        var update = TestEntityService.UpdateTestEntity(entityUpdate);
        var delete = TestEntityService.DeleteTestEntity(TestEntity.WithId("del"));
        var functionSingle = TestEntityService.FunctionSingleResult();
        var functionMultiple = TestEntityService.FunctionMultipleResult();
        var actionSingle = TestEntityService.ActionSingleResult();
        var actionMultiple = TestEntityService.ActionMultipleResult();

        using var response = await TestEntityService.Batch()
            .AddReadOperations(readAll, readByKey)
            .AddChangeset(create, update, delete)
            .AddReadOperations(functionSingle, functionMultiple)
            .AddChangeset(actionSingle, actionMultiple)
            .ExecuteAsync(handler.CreateClient());

        Assert.NotEmpty(await response.GetReadResultAsync(readAll));
        Assert.NotNull(await response.GetReadResultAsync(readByKey));
        var createResult = await response.GetModificationResultAsync(create);
        Assert.NotNull(createResult.ResponseEntity);
        Assert.NotEqual(entityCreate, createResult.ResponseEntity);
        Assert.Equal(["https://localhost/service/TestEntities('new')"], createResult.ResponseHeaders["Location"]);
        var updateResult = await response.GetModificationResultAsync(update);
        Assert.NotNull(updateResult.ResponseEntity);
        var deleteResult = await response.GetModificationResultAsync(delete);
        Assert.Null(deleteResult.ResponseEntity);
        Assert.Equal(42, await response.GetReadResultAsync(functionSingle));
        Assert.NotEmpty(await response.GetReadResultAsync(functionMultiple));
        var actionSingleResult = await response.GetModificationResultAsync(actionSingle);
        Assert.True(actionSingleResult.HasResponseResult);
        var actionMultipleResult = await response.GetModificationResultAsync(actionMultiple);
        Assert.NotEmpty(actionMultipleResult.ResponseResult!);

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("/$batch", post.PathAndQuery);
        Assert.Equal([RequestContentType], post.GetHeader("Content-Type").Select(v => v.Replace("; ", ";", StringComparison.Ordinal)));
        Assert.Equal("awesome-csrf-token", post.GetFirstHeader("x-csrf-token"));
        Assert.Equal(Crlf("ODataV4BatchRequestUnitTest", "BatchReadsAndWritesSuccessRequest.txt"), post.Body);
        var head = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Head);
        Assert.Equal("/", head.PathAndQuery);
        Assert.Equal("fetch", head.GetFirstHeader("x-csrf-token"));
    }

    [Fact]
    public async Task IdenticalOperationsAreMappedByIdentity()
    {
        var handler = Handler(Crlf("ODataV4BatchRequestUnitTest", "BatchReadsAndWritesSuccessResponse.txt"));
        var entity = new TestEntity();
        var create = TestEntityService.CreateTestEntity(entity);
        var identicalCreate = TestEntityService.CreateTestEntity(entity);

        using var response = await TestEntityService.Batch()
            .AddReadOperations(TestEntityService.GetTestEntities(), TestEntityService.GetTestEntitiesByKey("foobar"))
            .AddChangeset(create, identicalCreate)
            .ExecuteAsync(handler.CreateClient());

        var result1 = await response.GetModificationResultAsync(create);
        var result2 = await response.GetModificationResultAsync(identicalCreate);
        Assert.Equal("new", result1.ModifiedEntity.Id);
        Assert.Equal("updated", result2.ModifiedEntity.Id);
        Assert.Single(handler.Requests, r => r.Method == HttpMethod.Head);
    }

    [Fact]
    public async Task WithoutCsrfToken_DoesNotFetchOne()
    {
        var handler = Handler(Crlf("ODataV4BatchRequestUnitTest", "BatchReadsAndWritesSuccessResponse.txt"));

        using var response = await TestEntityService.Batch().WithoutCsrfToken().AddReadOperations(TestEntityService.GetTestEntities()).ExecuteAsync(handler.CreateClient());

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Head);
    }

    [Fact]
    public async Task LowLevelResultCanBeConvertedToTheTypedResponse()
    {
        var handler = Handler(Crlf("ODataV4BatchRequestUnitTest", "BatchReadsAndWritesSuccessResponse.txt"));
        var readAll = TestEntityService.GetTestEntities();
        var create = TestEntityService.CreateTestEntity(new TestEntity());
        var batchBuilder = TestEntityService.Batch()
            .AddReadOperations(readAll, TestEntityService.GetTestEntitiesByKey("foobar"))
            .AddChangeset(create);
        var lowLevelResult = await batchBuilder.ToRequest().ExecuteAsync(handler.CreateClient());

        using var converted = BatchResponse.Of(lowLevelResult, batchBuilder);

        Assert.Equal(3, (await converted.GetReadResultAsync(readAll)).Count);
        Assert.Equal("new", (await converted.GetModificationResultAsync(create)).ModifiedEntity.Id);
        Assert.Equal(200, converted.ResponseStatusCode);
    }

    [Fact]
    public async Task OperationsThatWereNotAddedAreRejected()
    {
        var handler = Handler(Crlf("ODataV4BatchRequestUnitTest", "BatchReadsAndWritesSuccessResponse.txt"));
        using var response = await TestEntityService.Batch().AddReadOperations(TestEntityService.GetTestEntities()).ExecuteAsync(handler.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => response.GetReadResultAsync(TestEntityService.GetTestEntities()));
    }

    [Fact]
    public async Task ResponseWithoutChangeset()
    {
        var handler = Handler(Crlf("ODatav4BatchConnectionTest", "BatchResponseWithoutChangeset.txt"));
        var readAll = TestEntityService.GetTestEntities();
        var readByKey = TestEntityService.GetTestEntitiesByKey("foobar");

        using var response = await TestEntityService.Batch().AddReadOperations(readAll, readByKey).ExecuteAsync(handler.CreateClient());

        Assert.NotEmpty(await response.GetReadResultAsync(readAll));
        Assert.NotNull(await response.GetReadResultAsync(readByKey));
    }

    [Fact]
    public async Task ResponseWithChangeset()
    {
        var handler = Handler(Crlf("ODatav4BatchConnectionTest", "BatchResponseWithChangeset.txt"));
        var readAll = TestEntityService.GetTestEntities();
        var readByKey = TestEntityService.GetTestEntitiesByKey("foobar");
        var create = TestEntityService.CreateTestEntity(new TestEntity());
        var update = TestEntityService.UpdateTestEntity(TestEntity.WithId("upd"));
        var delete = TestEntityService.DeleteTestEntity(TestEntity.WithId("del"));

        using var response = await TestEntityService.Batch()
            .AddReadOperations(readAll, readByKey)
            .AddChangeset(create, update, delete)
            .ExecuteAsync(handler.CreateClient());

        Assert.NotEmpty(await response.GetReadResultAsync(readAll));
        Assert.NotNull(await response.GetReadResultAsync(readByKey));
        Assert.NotNull(await response.GetModificationResultAsync(create));
        Assert.NotNull((await response.GetModificationResultAsync(update)).ResponseEntity);
        Assert.Null((await response.GetModificationResultAsync(delete)).ResponseEntity);
    }

    [Fact]
    public async Task ResponseWithError()
    {
        var handler = Handler(Crlf("ODatav4BatchConnectionTest", "BatchResponseWithError.txt"));
        var readAll = TestEntityService.GetTestEntities();
        var delete = TestEntityService.DeleteTestEntity(TestEntity.WithId("del"));

        using var response = await TestEntityService.Batch()
            .AddReadOperations(readAll, TestEntityService.GetTestEntitiesByKey("foobar"))
            .AddChangeset(delete)
            .ExecuteAsync(handler.CreateClient());

        Assert.NotEmpty(await response.GetReadResultAsync(readAll));
        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => response.GetModificationResultAsync(delete));
        Assert.Equal(400, exception.HttpCode);
    }

    [Fact]
    public async Task CountInABatch()
    {
        const string body = "--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef\r\nContent-Type: application/http\r\nContent-Transfer-Encoding: binary\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n42\r\n--batchresponse_76ef6b0a-a0e2-4f31-9f70-f5d3f73a6bef--";
        var handler = Handler(body);
        var count = TestEntityService.CountTestEntities();

        using var response = await TestEntityService.Batch().AddReadOperations(count).ExecuteAsync(handler.CreateClient());

        Assert.Equal(42L, await response.GetReadResultAsync(count));
        Assert.Contains("GET EntityCollection/%24count HTTP/1.1", Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post).Body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ TripPin service

    private static BatchRequestBuilder TripPinBatch()
    {
        var counter = 0;
        return new BatchRequestBuilder(ITrippinService.DefaultServicePath, () => new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)++counter));
    }

    private static string BatchUrl => ITrippinService.DefaultServicePath + "/$batch";

    [Fact]
    public async Task EmptyBatch()
    {
        var handler = Handler(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchEmptyResponse.txt"), 202);

        using var response = await TripPinBatch().ExecuteAsync(handler.CreateClient());

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(BatchUrl, post.PathAndQuery);
        Assert.False(post.HasHeader("Accept"));
        Assert.Equal([RequestContentType], post.GetHeader("Content-Type").Select(v => v.Replace("; ", ";", StringComparison.Ordinal)));
        Assert.Equal(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchEmptyRequest.txt"), post.Body);
        Assert.Equal(202, response.ResponseStatusCode);
        Assert.NotEmpty(response.ResponseHeaders);
    }

    [Fact]
    public async Task BatchWithOnlyReads()
    {
        var handler = Handler(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchOnlyReadsResponse.txt"), 202);
        var service = new DefaultTrippinService();
        var getAll1 = service.GetAllPeople().Top(1);
        var getAll2 = service.GetAllPeople().Top(2).Skip(1);

        using var response = await TripPinBatch().AddReadOperations(getAll1, getAll2).ExecuteAsync(handler.CreateClient());

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchOnlyReadsRequest.txt"), post.Body);
        Assert.Equal(202, response.ResponseStatusCode);
        Assert.NotEmpty(response.ResponseHeaders);
        Assert.NotEmpty(await response.GetReadResultAsync(getAll1));
    }

    [Fact]
    public async Task BatchWithErrorReads()
    {
        var handler = Handler(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchOnlyReadsErrorResponse.txt"), 202);
        var service = new DefaultTrippinService();
        var getAll1 = service.GetAllPeople().Top(1);
        var getAll2 = service.GetAllPeople().Top(2).Skip(1);

        using var response = await TripPinBatch().AddReadOperations(getAll1, getAll2).ExecuteAsync(handler.CreateClient());

        Assert.NotEmpty(await response.GetReadResultAsync(getAll1));
        var exception = await Assert.ThrowsAsync<ODataServiceErrorException>(() => response.GetReadResultAsync(getAll2));
        Assert.Equal(400, exception.HttpCode);
        Assert.Equal("ZCU/100", exception.ODataError.ODataCode);
        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchOnlyReadsRequest.txt"), post.Body);
    }

    [Fact]
    public async Task BatchWithReadsAndWrites()
    {
        var handler = Handler(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchReadsAndWritesSuccessResponse.txt"), 202);
        var service = new DefaultTrippinService();
        var person1 = Person.Builder().UserName("JohnDoe1").FirstName("John").Build();
        var person2 = Person.Builder().UserName("JohnDoe2").FirstName("John").Build();
        var createPerson1 = service.CreatePeople(person1);
        var createPerson2 = service.CreatePeople(person2);
        var getPerson = service.GetPeopleByKey("foo");

        using var response = await TripPinBatch().AddChangeset(createPerson1, createPerson2).AddReadOperations(getPerson).ExecuteAsync(handler.CreateClient());

        Assert.Equal(202, response.ResponseStatusCode);
        Assert.NotEmpty(response.ResponseHeaders);
        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(Crlf("ODataV4BatchReferenceServiceUnitTest", "BatchReadsAndWritesSuccessRequest.txt"), post.Body);
    }

    [Fact]
    public void UnsupportedOperationsAreRejected()
    {
        var batch = TripPinBatch();

        Assert.Throws<ArgumentException>(() => batch.AddReadOperations(new FakeReadBuilder()));
        Assert.Throws<ArgumentException>(() => batch.AddChangeset(new FakeModificationBuilder()));
    }

    private sealed class FakeReadBuilder : IReadRequestBuilder
    {
        public OData.Client.Request.ODataRequestGeneric ToRequest() => new OData.Client.Request.ODataRequestDelete("/", OData.Client.Expression.ODataResourcePath.Of("x"), null, OData.Client.ODataProtocol.V4);
    }

    private sealed class FakeModificationBuilder : IModificationRequestBuilder
    {
        public OData.Client.Request.ODataRequestGeneric ToRequest() => new OData.Client.Request.ODataRequestRead("/", "x", null, OData.Client.ODataProtocol.V4);
    }
}
