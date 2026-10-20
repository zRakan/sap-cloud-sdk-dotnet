namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http.Tests;

public class UriQueryMergerTests
{
    private static Uri Rel(string uri) => new(uri, UriKind.Relative);

    private static DestinationUriQueryMerger Merger(DefaultHttpDestination.Builder builder) => new DestinationUriQueryMerger(builder.Build());

    [Fact]
    public void ContainsDestinationQueryParameters()
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/service/").Property("URL.queries.sap-client", "100"));

        Assert.Equal("sap-client=100", sut.MergeRequestUri(Rel("")).Query.TrimStart('?'));
    }

    [Fact]
    public void ContainsMultipleDestinationQueryParameters()
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/service/")
            .Property("URL.queries.sap-client", "100")
            .Property("URL.queries.lang", "en"));

        var query = sut.MergeRequestUri(Rel("")).Query;

        Assert.Contains("sap-client=100", query, StringComparison.Ordinal);
        Assert.Contains("lang=en", query, StringComparison.Ordinal);
    }

    [Fact]
    public void HasNoQuery_WhenDestinationHasNoQueryProperties()
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/service/"));

        Assert.Equal(string.Empty, sut.MergeRequestUri(Rel("")).Query);
    }

    [Fact]
    public void IncludesQueryOfTheDestinationUri_AndTheProperties()
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/service/?sap-client=200").Property("URL.queries.sap-client", "100"));

        var query = sut.MergeRequestUri(Rel("")).Query;

        Assert.Contains("sap-client=200", query, StringComparison.Ordinal);
        Assert.Contains("sap-client=100", query, StringComparison.Ordinal);
    }

    [Fact]
    public void PropertyPrefix_IsMatchedIgnoringCase()
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/").Property("url.queries.a", "1"));

        Assert.Equal("a=1", sut.MergeRequestUri(Rel("")).Query.TrimStart('?'));
    }

    [Theory]
    [InlineData("a b", "a%20b")]
    [InlineData("a&b=c", "a%26b%3Dc")]
    [InlineData("safe_*-:,/'().|", "safe_*-:,/'().|")]
    [InlineData("ä", "%C3%A4")]
    public void QueryValues_AreEscapedLikeGuavasPercentEscaper(string value, string expected)
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/").Property("URL.queries.k", value));

        Assert.Equal("k=" + expected, sut.MergeRequestUri(Rel("")).OriginalString.Split('?')[1]);
    }

    [Fact]
    public void RequestPath_IsAppended()
    {
        var sut = Merger(DefaultHttpDestination.CreateBuilder("http://foo.com/service").Property("URL.queries.a", "1"));

        Assert.Equal("http://foo.com/service/Entity?x=2&a=1", sut.MergeRequestUri(Rel("Entity?x=2")).OriginalString);
    }
}
