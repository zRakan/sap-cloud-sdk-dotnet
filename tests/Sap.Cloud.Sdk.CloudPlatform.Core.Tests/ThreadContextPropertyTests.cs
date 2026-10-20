using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public class ThreadContextPropertyTests
{
    [Fact]
    public void NullValue_IsStoredAsFailure()
    {
        var property = ThreadContextProperty.OfResult(Result.Success<string?>(null));
        Assert.IsType<ThreadContextPropertyException>(property.Value.Exception);
    }

    [Fact]
    public void Capture_StoresExceptionInsteadOfThrowing()
    {
        var property = ThreadContextProperty.Capture(() => throw new InvalidOperationException("boom"));
        Assert.IsType<InvalidOperationException>(property.Value.Exception);
    }

    [Fact]
    public void ConfidentialValues_AreHiddenInToString()
    {
        Assert.DoesNotContain("secret", ThreadContextProperty.OfConfidential("secret").ToString(), StringComparison.Ordinal);
        Assert.Contains("visible", ThreadContextProperty.Of("visible").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", ThreadContextProperty.LazyConfidential(() => "secret").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Lazy_IsEvaluatedOnceAndNotByToString()
    {
        var calls = 0;
        var property = ThreadContextProperty.Lazy(() => ++calls);

        Assert.Contains("not yet evaluated", property.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, calls);

        Assert.Equal(1, property.Value.GetValueOrThrow());
        Assert.Equal(1, property.Value.GetValueOrThrow());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Lazy_CopyIsEvaluatedIndependently()
    {
        var calls = 0;
        var property = ThreadContextProperty.Lazy(() => ++calls);
        _ = property.Value;

        var original = new DefaultThreadContext();
        original.SetProperty("p", property);
        var duplicate = original.Duplicate();

        Assert.Equal(2, duplicate.GetPropertyValue<int>("p").GetValueOrThrow());
    }
}
