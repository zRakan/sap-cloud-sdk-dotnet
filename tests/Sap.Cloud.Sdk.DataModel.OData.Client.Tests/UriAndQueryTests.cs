using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

/// <summary>Ports of ODataUriFactoryTest, the query parts of ODataRequestReadTest and the request construction tests.</summary>
public class UriAndQueryTests
{
    private const string ServicePath = "/A_Service";
    private const string EntityName = "A_Entity";
    private static readonly IODataProtocol V2 = ODataProtocol.V2;
    private static readonly IODataProtocol V4 = ODataProtocol.V4;

    private static Uri Create(string servicePath, string resource, string? query, UriEncodingStrategy strategy = UriEncodingStrategy.Regular) =>
        ODataUriFactory.CreateAndEncodeUri(servicePath, resource, query, strategy);

    [Fact]
    public void UriFactory_DoubleSlashesInPath()
    {
        Assert.Equal(ServicePath + "/" + EntityName, Create("//" + ServicePath + "////", EntityName, string.Empty).OriginalString);
    }

    [Fact]
    public void UriFactory_SpecialCharactersInPath()
    {
        Assert.Equal(
            "/%C3%84$_%3FSe&rv%20i%C3%9F%25%C3%AB%23/" + EntityName,
            Create("Ä$_?Se&rv iß%ë#", EntityName, string.Empty).OriginalString);
    }

    [Fact]
    public void UriFactory_SafeCharactersInPath()
    {
        const string specialPath = "A_Service/-._~!$'()*,;&=@:+";
        const string entityPath = "B_entityPath/-._~!$'()*,;&=@:+";
        Assert.Equal("/A_Service/-._~!$'()*,;&=@:+/" + entityPath, Create(specialPath, entityPath, string.Empty).OriginalString);
    }

    [Fact]
    public void UriFactory_EmptyEntityPath()
    {
        Assert.Equal(ServicePath + "/", ODataUriFactory.CreateAndEncodeUri(ServicePath, new ODataResourcePath(), null, UriEncodingStrategy.Regular).OriginalString);
    }

    [Fact]
    public void UriFactory_SpecialCharactersInEntityPath()
    {
        var uri = ODataUriFactory.CreateAndEncodeUri(ServicePath, ODataResourcePath.Of("A$_?En&ti t%y#"), null, UriEncodingStrategy.Regular);
        Assert.Equal(ServicePath + "/A$_%3FEn&ti%20t%25y%23", uri.OriginalString);
    }

    [Fact]
    public void UriFactory_NoDoubleEncodingInQuery()
    {
        const string query =
            "$expand=BestFriend($expand=Trips($filter=contains(Name,'%25%20%24%26%23%3F%22%5C+''')))&$filter=contains(FirstName,'%25%20%24%26%23%3F%22%5C+''')";
        var uri = Create(ServicePath, ODataResourcePath.Of("A$_?En&ti t%y#").ToEncodedPathString(), query);
        Assert.Equal(ServicePath + "/A$_%3FEn&ti%20t%25y%23?" + query, uri.OriginalString);
    }

    [Fact]
    public void UriFactory_NoDoubleEncodingInParameter()
    {
        var path = ODataResourcePath.Of("A$_?En&ti t%y#", new ODataEntityKey(V4).AddKeyProperty("key", "%abc"));
        var uri = ODataUriFactory.CreateAndEncodeUri(ServicePath, path, null, UriEncodingStrategy.Regular);
        Assert.Equal(ServicePath + "/A$_%3FEn&ti%20t%25y%23('%25abc')", uri.OriginalString);
    }

    [Theory]
    [InlineData("Brontë", "Bront%C3%AB")]
    [InlineData("위키백과:대문", "%EC%9C%84%ED%82%A4%EB%B0%B1%EA%B3%BC:%EB%8C%80%EB%AC%B8")]
    [InlineData("Günter", "G%C3%BCnter")]
    public void UriFactory_ForeignCharactersInFilterQuery(string unencoded, string encoded)
    {
        var filter = "$filter=" + ODataUriFactory.EncodeQuery($"(field-name eq '{unencoded}')");
        var expected = $"$filter=(field-name%20eq%20'{encoded}')";
        Assert.Equal(expected, filter);
        Assert.Equal("service/entity?" + expected, Create("service", "entity", filter).OriginalString.TrimStart('/'));
    }

    [Fact]
    public void UriFactory_PipeIsNotASafeQueryCharacter()
    {
        var encoded = ODataUriFactory.EncodeQuery("(Name eq 'Version|1')");
        Assert.Equal("(Name%20eq%20'Version%7C1')", encoded);
        Assert.Equal("/A_Service/A_Entity?$filter=" + encoded, Create(ServicePath, EntityName, "$filter=" + encoded).OriginalString);
    }

    [Fact]
    public void UriFactory_RejectsUnencodedQuery()
    {
        Assert.Throws<ArgumentException>(() => new ODataRequestRead("/odata/v4/Service/", "Authors", "$orderby=name asc,ID", V4).GetRelativeUri());
    }

    [Fact]
    public void UriFactory_EncodedQueryIsKept()
    {
        var uri = new ODataRequestRead("/odata/v4/Service/", "Authors", "$orderby=name%20asc,ID", V4).GetRelativeUri();
        Assert.Equal("/odata/v4/Service/Authors?$orderby=name%20asc,ID", uri.OriginalString);
    }

    [Fact]
    public void UriFactory_ExtractsTokens()
    {
        Assert.Equal("s3cReT", ODataUriFactory.ExtractSkipToken("/x?$skiptoken=s3cReT&foo=bar"));
        Assert.Equal("42", ODataUriFactory.ExtractDeltaToken("/x?foo=bar&$DeltaToken=42"));
        Assert.Null(ODataUriFactory.ExtractSkipToken("/x?$skiptoken=&foo=bar"));
        Assert.Null(ODataUriFactory.ExtractSkipToken(null));
    }

    [Fact]
    public void EncodingStrategies()
    {
        Assert.Equal("a b/c", UriEncodingStrategy.None.EscapePath("a b/c"));
        Assert.Equal("a%20b%2Fc", UriEncodingStrategy.Regular.EscapePath("a b/c"));
        Assert.Equal("a%20b/c", UriEncodingStrategy.Regular.EscapeQuery("a b/c"));
        Assert.Equal("%27k%27%3B%3D", UriEncodingStrategy.Batch.EscapePath("'k';="));
        Assert.Equal("%F0%9F%98%80", UriEncodingStrategy.Regular.EscapePath("\U0001F600"));
    }

    [Fact]
    public void V4QueryExpand()
    {
        var query = StructuredQuery.OnEntity("Movies", V4);
        var subQuery1 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V4);
        var subQuery2 = StructuredQuery.AsNestedQueryOnProperty("relatedMovies", V4);
        var subQuery3 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V4);
        subQuery2.Select(subQuery3);
        subQuery1.Select(subQuery2);
        query.Select(subQuery1);
        Assert.Equal("$expand=relatedBook($expand=relatedMovies($expand=relatedBook))", query.QueryString);
    }

    [Fact]
    public void V4QueryExpandWithOptions()
    {
        var query = StructuredQuery.OnEntity("Movies", V4);
        var subQuery1 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V4).Skip(3).Top(10);
        var subQuery2 = StructuredQuery.AsNestedQueryOnProperty("relatedMovies", V4).OrderBy("Duration", Order.Desc);
        var subQuery3 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V4).WithInlineCount();
        subQuery2.Select(subQuery3);
        subQuery1.Select(subQuery2);
        query.Select(subQuery1);
        Assert.Equal(
            "$expand=relatedBook($expand=relatedMovies($expand=relatedBook($count=true);$orderby=Duration desc);$top=10;$skip=3)",
            query.QueryString);
    }

    [Fact]
    public void V4QuerySelectAndFilter()
    {
        var query = StructuredQuery.OnEntity("Movies", V4);
        var subQuery1 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V4);
        var subQuery2 = StructuredQuery.AsNestedQueryOnProperty("relatedMovies", V4);
        subQuery1.Select(subQuery2);
        query.Select(subQuery1);
        subQuery2.Select("Director", "Producer");
        subQuery2.Filter(FieldReference.Of("Duration").GreaterThan(3600));
        query.Filter(FieldReference.OfPath("relatedBook", "Name").EqualTo("Forrest Gump"));
        Assert.Equal(
            [
                "$expand=relatedBook($expand=relatedMovies($select=Director,Producer;$filter=(Duration gt 3600)))",
                "$filter=(relatedBook/Name eq 'Forrest Gump')",
            ],
            query.QueryString.Split('&'));
        Assert.Equal(
            [
                "$expand=relatedBook($expand=relatedMovies($select=Director,Producer;$filter=(Duration%20gt%203600)))",
                "$filter=(relatedBook/Name%20eq%20'Forrest%20Gump')",
            ],
            query.EncodedQueryString.Split('&'));
    }

    [Fact]
    public void V2QueryExpand_IgnoresOptions()
    {
        var query = StructuredQuery.OnEntity("Movies", V2);
        var subQuery1 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V2).Skip(3).Top(10);
        var subQuery2 = StructuredQuery.AsNestedQueryOnProperty("relatedMovies", V2).OrderBy("Duration", Order.Desc);
        var subQuery3 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V2).WithInlineCount();
        subQuery2.Select(subQuery3);
        subQuery1.Select(subQuery2);
        query.Select(subQuery1);
        Assert.Equal("$expand=relatedBook,relatedBook/relatedMovies,relatedBook/relatedMovies/relatedBook", query.QueryString);
    }

    [Fact]
    public void V2QuerySelectAndFilter()
    {
        var query = StructuredQuery.OnEntity("Movies", V2);
        var subQuery1 = StructuredQuery.AsNestedQueryOnProperty("relatedBook", V2);
        var subQuery2 = StructuredQuery.AsNestedQueryOnProperty("relatedMovies", V2);
        subQuery1.Select(subQuery2);
        query.Select(subQuery1);
        subQuery2.Select("Director", "Producer");
        subQuery2.Filter(FieldReference.Of("Duration").GreaterThan(3600));
        query.Filter(FieldReference.OfPath("relatedBook", "Name").EqualTo("Forrest Gump"));
        Assert.Equal(
            [
                "$select=relatedBook/relatedMovies/Director,relatedBook/relatedMovies/Producer",
                "$expand=relatedBook,relatedBook/relatedMovies",
                "$filter=(relatedBook/Name eq 'Forrest Gump')",
            ],
            query.QueryString.Split('&'));
        Assert.Equal(
            [
                "$select=relatedBook/relatedMovies/Director,relatedBook/relatedMovies/Producer",
                "$expand=relatedBook,relatedBook/relatedMovies",
                "$filter=(relatedBook/Name%20eq%20'Forrest%20Gump')",
            ],
            query.EncodedQueryString.Split('&'));
    }

    [Theory]
    [InlineData(2, "/odata/v2/Service/Authors?$filter=(philosphy%20eq%20'Yin%20%26%20Yang')&$orderby=name%20asc,ID%20asc&$inlinecount=allpages")]
    [InlineData(4, "/odata/v4/Service/Authors?$filter=(philosphy%20eq%20'Yin%20%26%20Yang')&$orderby=name%20asc,ID%20asc&$count=true")]
    public void StructuredQuery_BuildsRequestUri(int version, string expectedUri)
    {
        var protocol = version == 2 ? V2 : V4;
        var structuredQuery = StructuredQuery
            .OnEntity("Authors", protocol)
            .Filter(FieldReference.Of("philosphy").EqualTo("Yin & Yang"))
            .OrderBy(OrderExpression.Of("name", Order.Asc).And("ID", Order.Asc))
            .WithInlineCount();
        var uri = new ODataRequestRead($"/odata/v{version}/Service/", "Authors", structuredQuery.EncodedQueryString, protocol).GetRelativeUri();
        Assert.Equal(expectedUri, uri.OriginalString);
    }

    [Fact]
    public void StructuredQuery_TopSkipSearchAndOrder()
    {
        var query = StructuredQuery.OnEntity("E", V4).Top(5).Skip(0).Search("a b").OrderBy("x", Order.Desc).OrderBy("y", Order.Asc);
        Assert.Equal("$top=5&$skip=0&$orderby=x desc,y asc&$search=a b", query.QueryString);
        Assert.Equal("$top=5&$skip=0&$orderby=x%20desc,y%20asc&$search=a%20b", query.EncodedQueryString);
    }

    [Fact]
    public void StructuredQuery_CustomParameters()
    {
        var structuredQuery = StructuredQuery
            .OnEntity("Authors", V4)
            .Filter(FieldReference.Of("philosophy").EqualTo("Yin & Yang"))
            .WithCustomParameter("$key", "$value");
        Assert.Equal("$filter=(philosophy%20eq%20'Yin%20%26%20Yang')&$key=%24value", structuredQuery.EncodedQueryString);
        Assert.Throws<InvalidOperationException>(() => StructuredQuery.AsNestedQueryOnProperty("name", V4).WithCustomParameter("key", "value"));
        Assert.Throws<ArgumentException>(() => StructuredQuery.OnEntity("name", V4).WithCustomParameter(string.Empty, "value"));
    }

    [Fact]
    public void StructuredQuery_V4NestedQueriesOnlyAllowExpandOptionsAsCustomParameters()
    {
        Assert.True(V4.AllowCustomQueryParameter(true, "foo"));
        Assert.True(V4.AllowCustomQueryParameter(false, "$top"));
        Assert.False(V4.AllowCustomQueryParameter(false, "foo"));
        Assert.False(V2.AllowCustomQueryParameter(false, "$top"));
    }

    [Fact]
    public void ReadRequest_Basics()
    {
        var read = new ODataRequestRead("/service/path", "EntityName", "foo=bar", V4);
        Assert.Equal("/EntityName", read.ResourcePath.ToString());
        Assert.Equal("/service/path", read.ServicePath);
        Assert.Equal("/service/path/EntityName?foo=bar", read.GetRelativeUri().OriginalString);
        Assert.Equal("foo=bar", read.GetRequestQuery());
        Assert.Equal("foo=bar", read.QueryString);
    }

    [Fact]
    public void RequestsWithKeys()
    {
        var key = new ODataEntityKey(V4).AddKeyProperty("key", 123);
        var byKey = new ODataRequestReadByKey("/service/path", "EntityName", key, "foo=bar", V4);
        Assert.Equal("/service/path/EntityName(123)?foo=bar", byKey.GetRelativeUri().OriginalString);
        var delete = new ODataRequestDelete("/service/path", "EntityName", key, string.Empty, V4);
        Assert.Equal("/service/path/EntityName(123)", delete.GetRelativeUri().OriginalString);
        Assert.Equal(["application/json"], delete.GetHeaders()["Accept"]);
        var update = new ODataRequestUpdate("/service/path", "EntityName", key, "{\"foo\": \"bar\"}", UpdateStrategy.ModifyWithPatch, string.Empty, V4);
        Assert.Equal("{\"foo\": \"bar\"}", update.SerializedEntity);
        Assert.Equal(UpdateStrategy.ModifyWithPatch, update.UpdateStrategy);
        update.UpdateStrategy = UpdateStrategy.ReplaceWithPut;
        Assert.Equal("PUT", update.HttpMethodName);
        var create = new ODataRequestCreate("/service/path", "EntityName", "{}", V4);
        Assert.Equal(string.Empty, create.GetRequestQuery());
        Assert.Equal(["application/json"], create.GetHeaders()["Content-Type"]);
    }

    [Fact]
    public void RequestsWithNullKey()
    {
        var nullKey = new ODataEntityKey(V4).AddKeyProperty("key", null);
        Assert.Equal("/service/path/EntityName(null)?foo=bar", new ODataRequestReadByKey("/service/path", "EntityName", nullKey, "foo=bar", V4).GetRelativeUri().OriginalString);
        Assert.Equal("/service/path/EntityName(null)", new ODataRequestDelete("/service/path", "EntityName", nullKey, string.Empty, V4).GetRelativeUri().OriginalString);
        Assert.Equal(
            "/service/path/EntityName(null)",
            new ODataRequestUpdate("/service/path", "EntityName", nullKey, "{}", UpdateStrategy.ModifyWithPatch, string.Empty, V4).GetRelativeUri().OriginalString);
    }

    [Fact]
    public void ReadConstructorWithStructuredQuery_DoesNotMutateResourcePath()
    {
        var structuredQuery = StructuredQuery
            .OnEntity("Authors", V4)
            .Filter(FieldReference.Of("philosphy").EqualTo("Yin & Yang"))
            .OrderBy(OrderExpression.Of("name", Order.Asc).And("ID", Order.Asc))
            .WithInlineCount();
        var expected = new ODataRequestRead("/some/service/path", "Authors", structuredQuery.EncodedQueryString, V4);
        var actual = new ODataRequestRead("/some/service/path", new ODataResourcePath(), structuredQuery);
        Assert.Equal(expected.GetRelativeUri(), actual.GetRelativeUri());
        Assert.Equal(expected.QueryString, actual.QueryString);

        var resourcePath = ODataResourcePath.Of("Authors");
        _ = new ODataRequestRead("/some/service/path", resourcePath, structuredQuery);
        Assert.Equal("/Authors", resourcePath.ToString());
    }

    [Fact]
    public void ReadByKeyConstructorWithStructuredQuery_UsesKeyOnLastSegment()
    {
        var key = new ODataEntityKey(V4).AddKeyProperty("key", 123);
        var structuredQuery = StructuredQuery.OnEntity("Entity", V4).WithInlineCount();
        var actual = new ODataRequestReadByKey("/service/", ODataResourcePath.Of("Entity"), key, structuredQuery);
        Assert.Equal("/service/Entity(123)?$count=true", actual.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void CountRequest_AppendsCountSegment()
    {
        var count = new ODataRequestCount("/some/path/SOME_API", "A_EntityName", string.Empty, V4);
        Assert.Equal("/some/path/SOME_API/A_EntityName/$count", count.GetRelativeUri().OriginalString);
        var resourcePath = ODataResourcePath.Of("A_EntityName");
        _ = new ODataRequestCount("/some/path/SOME_API", resourcePath, string.Empty, V4);
        Assert.Equal("/A_EntityName", resourcePath.ToString());
    }

    [Fact]
    public void FunctionRequest_ParameterHandling()
    {
        var parametersV2 = new ODataFunctionParameters(V2).AddParameter("key", "val");
        var parametersV4 = new ODataFunctionParameters(V4).AddParameter("key", "val");
        const string customQuery = "$foo=bar";
        var functionPath = ODataResourcePath.Of("function");
        Assert.Equal("/path/function?key='val'", new ODataRequestFunction("/path", functionPath, parametersV2, null, V2).GetRelativeUri().OriginalString);
        Assert.Equal("/path/function?key='val'&$foo=bar", new ODataRequestFunction("/path", functionPath, parametersV2, customQuery, V2).GetRelativeUri().OriginalString);
        Assert.Equal("/path/function(key='val')", new ODataRequestFunction("/path", functionPath, parametersV4, null, V4).GetRelativeUri().OriginalString);
        Assert.Equal("/path/function(key='val')?$foo=bar", new ODataRequestFunction("/path", functionPath, parametersV4, customQuery, V4).GetRelativeUri().OriginalString);
        Assert.Equal("/function", functionPath.ToString());
    }

    [Fact]
    public void FunctionRequest_WithStructuredQuery()
    {
        var structuredQuery = StructuredQuery
            .OnEntity("Authors", V4)
            .Filter(FieldReference.Of("philosphy").EqualTo("Yin & Yang"))
            .WithInlineCount();
        var actual = new ODataRequestFunction("/service/", ODataResourcePath.Of("TestFunction"), structuredQuery);
        Assert.Equal("/service/TestFunction/Authors?$filter=(philosphy%20eq%20'Yin%20%26%20Yang')&$count=true", actual.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void ActionRequest_BoundAction()
    {
        var path = ODataResourcePath.Of("Entity").AddSegment("TestAction");
        Assert.Equal("/service/Entity/TestAction", new ODataRequestAction("/service/", path, "{}", V4).GetRelativeUri().OriginalString);
    }

    [Fact]
    public void ResourcePath_Behaviour()
    {
        var path = ODataResourcePath.Of("Products", new ODataEntityKey(V2).AddKeyProperty("Id", 0)).AddSegment("Description");
        Assert.Equal("/Products(0)/Description", path.ToString());
        Assert.Equal("/Products(0)/Description", path.ToEncodedPathString());
        Assert.Equal(path, path.Copy());
        Assert.Throws<InvalidOperationException>(() => new ODataResourcePath().AddParameterToLastSegment(new ODataEntityKey(V4)));
        Assert.Throws<InvalidOperationException>(
            () => ODataResourcePath.Of("a", new ODataEntityKey(V4)).AddParameterToLastSegment(new ODataEntityKey(V4)));
    }

    [Fact]
    public void RequestQueryParametersAndHeaders()
    {
        var request = new ODataRequestRead("/service/", "Entity", "$top=1", V4);
        request.AddQueryParameter("query-param1", "qp1");
        request.AddQueryParameter("query-param2", "qp2");
        request.AddHeader("header-key1", "hk1");
        request.AddHeader("header-key1", "hk2");
        request.SetHeader("header-key2", "hk2");
        request.AddHeaderIfAbsent("header-key2", "ignored");
        Assert.Equal("$top=1&query-param1=qp1&query-param2=qp2", request.GetRequestQuery());
        var headers = request.GetHeaders();
        Assert.Equal(["hk1", "hk2"], headers["header-key1"]);
        Assert.Equal(["hk2"], headers["header-key2"]);
        Assert.Equal(["Accept", "header-key1", "header-key2"], headers.Keys);
    }

    [Fact]
    public void Protocols_AreIdentifiedByVersion()
    {
        Assert.Equal("2.0", V2.ProtocolVersion);
        Assert.Equal("4.0", V4.ProtocolVersion);
        Assert.True(V2.IsEqualTo(ODataProtocol.V2));
        Assert.False(V2.IsEqualTo(V4));
        Assert.Equal("OData 4.0", V4.ToString());
        Assert.Equal(new KeyValuePair<string, string>("$inlinecount", "allpages"), V2.GetQueryOptionInlineCount(true));
        Assert.Equal(new KeyValuePair<string, string>("$count", "false"), V4.GetQueryOptionInlineCount(false));
    }

    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(0.001, "0.001")]
    [InlineData(1234567.0, "1234567.0")]
    [InlineData(1.0E7, "1.0E7")]
    [InlineData(0.0001, "1.0E-4")]
    [InlineData(12345678.9, "1.23456789E7")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(0.0, "0.0")]
    [InlineData(3.14, "3.14")]
    public void JavaFormat_Double(double value, string expected)
    {
        Assert.Equal(expected, JavaFormat.ToString(value));
    }

    [Theory]
    [InlineData(0, "PT0S")]
    [InlineData(36000000000L, "PT1H")]
    [InlineData(864000000000L, "PT24H")]
    [InlineData(-36000000000L, "PT-1H")]
    [InlineData(5400000000L, "PT9M")]
    [InlineData(15000000L, "PT1.5S")]
    [InlineData(-15000000L, "PT-1.5S")]
    [InlineData(-5000000L, "PT-0.5S")]
    public void JavaFormat_Duration(long ticks, string expected)
    {
        Assert.Equal(expected, JavaFormat.ToIsoString(ticks));
    }

    [Theory]
    [InlineData("PT24H", 864000000000L)]
    [InlineData("P1D", 864000000000L)]
    [InlineData("PT1.5S", 15000000L)]
    [InlineData("-PT1H", -36000000000L)]
    public void JavaFormat_ParseDuration(string text, long expectedTicks)
    {
        Assert.True(JavaFormat.TryParseDuration(text, out var duration));
        Assert.Equal(TimeSpan.FromTicks(expectedTicks), duration);
    }

    [Fact]
    public void JavaFormat_ParseDurationWithNanoseconds_TruncatesToTicks()
    {
        Assert.True(JavaFormat.TryParseDuration("P12DT23H59M59.999999999S", out var duration));
        Assert.Equal(new TimeSpan(12, 23, 59, 59).Ticks + 9999999, duration.Ticks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("P")]
    [InlineData("PT")]
    [InlineData("1H")]
    public void JavaFormat_ParseDurationRejectsInvalid(string text)
    {
        Assert.False(JavaFormat.TryParseDuration(text, out _));
    }
}
