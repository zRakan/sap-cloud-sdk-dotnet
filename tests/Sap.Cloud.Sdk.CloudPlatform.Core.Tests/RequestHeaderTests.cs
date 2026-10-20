using Sap.Cloud.Sdk.CloudPlatform.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public class RequestHeaderTests : StaticStateReset
{
    [Fact]
    public void Container_NormalizesNamesAndMergesValues()
    {
        var headers = DefaultRequestHeaderContainer.FromMultiValueMap(
        [
            KeyValuePair.Create(" Content-Type ", (IEnumerable<string?>?)["a"]),
            KeyValuePair.Create("content-type", (IEnumerable<string?>?)["b", null]),
            KeyValuePair.Create("X-Empty", (IEnumerable<string?>?)[]),
            KeyValuePair.Create("X-Null", (IEnumerable<string?>?)null),
        ]);

        Assert.Equal(["content-type"], headers.HeaderNames);
        Assert.Equal(["a", "b"], headers.GetHeaderValues("CONTENT-TYPE"));
        Assert.True(headers.ContainsHeader("Content-Type"));
        Assert.Empty(headers.GetHeaderValues("missing"));
    }

    [Fact]
    public void Builder_AddsReplacesAndRemoves()
    {
        var headers = DefaultRequestHeaderContainer.Builder()
            .WithHeader("A", "1", "2")
            .WithHeader("a", "3")
            .WithHeader("B", "x")
            .ReplaceHeader("b", "y")
            .WithHeader("C", "z")
            .WithoutHeader("c")
            .Build();

        Assert.Equal(["1", "2", "3"], headers.GetHeaderValues("a"));
        Assert.Equal(["y"], headers.GetHeaderValues("b"));
        Assert.False(headers.ContainsHeader("c"));

        Assert.Equal(headers, headers.ToBuilder().Build());
        Assert.Equal(headers.GetHashCode(), headers.ToBuilder().Build().GetHashCode());
    }

    [Fact]
    public void ToString_DoesNotContainValues()
    {
        var headers = DefaultRequestHeaderContainer.FromSingleValueMap(
            new Dictionary<string, string?> { ["Authorization"] = "Bearer secret" });
        Assert.DoesNotContain("secret", headers.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Accessor_WithoutContext_Fails()
    {
        Assert.Throws<RequestHeadersAccessException>(RequestHeaderAccessor.GetHeaderContainer);
    }

    [Fact]
    public async Task Accessor_ReturnsHeadersWithinExecution()
    {
        var headers = DefaultRequestHeaderContainer.Builder().WithHeader("X-Test", "value").Build();

        var value = await RequestHeaderAccessor.ExecuteWithHeaderContainerAsync(headers, async () =>
        {
            await Task.Yield();
            return RequestHeaderAccessor.GetHeaderContainer().GetHeaderValues("x-test").Single();
        });

        Assert.Equal("value", value);
    }

    [Fact]
    public void NestedExecution_InheritsHeaders()
    {
        var headers = DefaultRequestHeaderContainer.Builder().WithHeader("X-Test", "value").Build();
        var value = RequestHeaderAccessor.ExecuteWithHeaderContainer(headers, () =>
            Threading.ThreadContextExecutor.FromCurrentContext().Execute(() =>
                RequestHeaderAccessor.GetHeaderContainer().GetHeaderValues("x-test").Single()));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Fallback_IsUsedOnlyWithoutHeaders()
    {
        var fallback = DefaultRequestHeaderContainer.Builder().WithHeader("X-Source", "fallback").Build();
        var actual = DefaultRequestHeaderContainer.Builder().WithHeader("X-Source", "actual").Build();

        var withoutHeaders = RequestHeaderAccessor.ExecuteWithFallbackHeaderContainer(
            () => fallback, () => RequestHeaderAccessor.GetHeaderContainer().GetHeaderValues("x-source").Single());
        var withHeaders = RequestHeaderAccessor.ExecuteWithHeaderContainer(actual, () =>
            RequestHeaderAccessor.ExecuteWithFallbackHeaderContainer(
                () => fallback, () => RequestHeaderAccessor.GetHeaderContainer().GetHeaderValues("x-source").Single()));

        Assert.Equal("fallback", withoutHeaders);
        Assert.Equal("actual", withHeaders);

        RequestHeaderAccessor.FallbackHeaderContainer = () => fallback;
        Assert.Equal("fallback", RequestHeaderAccessor.GetHeaderContainer().GetHeaderValues("x-source").Single());
    }

    [Fact]
    public void ExecuteWith_RequiresDefaultFacade()
    {
        RequestHeaderAccessor.HeaderFacade = new CustomFacade();
        Assert.Throws<ThreadContextExecutionException>(
            () => RequestHeaderAccessor.ExecuteWithHeaderContainer(DefaultRequestHeaderContainer.Empty, () => 1));
    }

    private sealed class CustomFacade : IRequestHeaderFacade
    {
        public Result<IRequestHeaderContainer> TryGetRequestHeaders() => Result.Success(DefaultRequestHeaderContainer.Empty);
    }
}
