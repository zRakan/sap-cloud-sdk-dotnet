using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

public class UriPathMergerTests
{
    private static Uri Abs(string uri) => new(uri, UriKind.Absolute);

    private static Uri Rel(string uri) => new(uri, UriKind.Relative);

    [Theory]
    [InlineData("https://host", "/api/v1", "https://host/api/v1")]
    [InlineData("https://host/", "api/v1", "https://host/api/v1")]
    [InlineData("https://host/base", "/api", "https://host/base/api")]
    [InlineData("https://host/base/", "api", "https://host/base/api")]
    [InlineData("https://host/base", "base/api", "https://host/base/api")]
    [InlineData("https://host/base", "", "https://host/base")]
    [InlineData("https://host/a%20b", "c%20d", "https://host/a%20b/c%20d")]
    public void RelativeRequest_IsAppendedToDestinationPath(string destination, string request, string expected) =>
        Assert.Equal(Abs(expected), UriPathMerger.Merge(Abs(destination), Rel(request)));

    [Fact]
    public void Queries_AreConcatenated_AndRequestFragmentWins()
    {
        var merged = UriPathMerger.Merge(Abs("https://host/base?a=1#fromDestination"), Rel("path?b=2#fromRequest"));

        Assert.Equal("https://host/base/path?a=1&b=2#fromRequest", merged.OriginalString);
    }

    [Fact]
    public void DestinationFragmentIsKept_IfRequestHasNone()
    {
        var merged = UriPathMerger.Merge(Abs("https://host/base#frag"), Rel("path"));

        Assert.Equal("https://host/base/path#frag", merged.OriginalString);
    }

    [Fact]
    public void BlankQueries_AreDropped()
    {
        var merged = UriPathMerger.Merge(Abs("https://host/base?"), Rel("path?"));

        Assert.Equal("https://host/base/path", merged.OriginalString);
    }

    [Fact]
    public void AbsoluteRequest_OnTheSameHost_UsesItsPath()
    {
        var merged = UriPathMerger.Merge(Abs("https://host/base"), Abs("https://host/other/path"));

        Assert.Equal(Abs("https://host/other/path"), merged);
    }

    [Fact]
    public void AbsoluteRequest_OnAnotherHost_Throws()
    {
        var e = Assert.Throws<DestinationPathsNotMergeableException>(
            () => UriPathMerger.Merge(Abs("https://host/base"), Abs("https://evil.example/path")));
        Assert.Contains("differ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoRequestUri_YieldsTheDestinationUri_WithRootPathIfEmpty()
    {
        Assert.Equal(Abs("https://host/base"), UriPathMerger.Merge(Abs("https://host/base"), null));
        Assert.Equal("https://host/", UriPathMerger.Merge(Abs("https://host"), null).ToString());
    }

    [Fact]
    public void EmptyDestination_RequiresAbsoluteRequest()
    {
        var empty = Rel("");

        Assert.Equal(Abs("https://host/path"), UriPathMerger.Merge(empty, Abs("https://host/path")));
        Assert.Equal("https://host/", UriPathMerger.Merge(empty, Abs("https://host")).ToString());
        Assert.Throws<DestinationPathsNotMergeableException>(() => UriPathMerger.Merge(empty, Rel("path")));
        Assert.Throws<DestinationPathsNotMergeableException>(() => UriPathMerger.Merge(empty, null));
    }

    [Fact]
    public void RelativeDestination_Throws() =>
        Assert.Throws<DestinationPathsNotMergeableException>(() => UriPathMerger.Merge(Rel("relative/path"), Rel("x")));
}
