using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public class DefaultThreadContextTests
{
    [Fact]
    public void MissingProperty_IsNotFoundFailure()
    {
        var result = new DefaultThreadContext().GetPropertyValue<string>("missing");
        var exception = Assert.IsType<ThreadContextPropertyNotFoundException>(result.Exception);
        Assert.Equal("missing", exception.PropertyName);
    }

    [Fact]
    public void WrongType_IsPropertyFailure()
    {
        var context = new DefaultThreadContext();
        context.SetProperty("p", ThreadContextProperty.Of("text"));
        Assert.IsType<ThreadContextPropertyException>(context.GetPropertyValue<int>("p").Exception);
    }

    [Fact]
    public void SetPropertyIfAbsent_KeepsExistingAndDoesNotCallFactory()
    {
        var context = new DefaultThreadContext();
        context.SetProperty("p", ThreadContextProperty.Of("first"));

        context.SetPropertyIfAbsent("p", ThreadContextProperty.Of("second"));
        context.SetPropertyIfAbsent("p", () => throw new InvalidOperationException("must not be called"));

        Assert.Equal("first", context.GetPropertyValue<string>("p").GetValueOrThrow());
    }

    [Fact]
    public void SetPropertyIfAbsent_WrapsFactoryException()
    {
        var context = new DefaultThreadContext();
        var e = Assert.Throws<ThreadContextPropertyException>(
            () => context.SetPropertyIfAbsent("p", () => throw new InvalidOperationException()));
        Assert.IsType<InvalidOperationException>(e.InnerException);
    }

    [Fact]
    public void SetProperty_ReplacesAndRemoveReturnsProperty()
    {
        var context = new DefaultThreadContext();
        context.SetProperty("p", ThreadContextProperty.Of("first"));
        context.SetProperty("p", ThreadContextProperty.Of("second"));
        Assert.Equal("second", context.GetPropertyValue<string>("p").GetValueOrThrow());

        Assert.NotNull(context.RemoveProperty("p"));
        Assert.Null(context.RemoveProperty("p"));
        Assert.False(context.ContainsProperty("p"));
    }

    [Fact]
    public void Duplicate_IsIndependent()
    {
        var context = new DefaultThreadContext();
        context.SetProperty("p", ThreadContextProperty.Of("value"));

        var duplicate = context.Duplicate();
        duplicate.SetProperty("q", ThreadContextProperty.Of("other"));
        context.RemoveProperty("p");

        Assert.True(duplicate.ContainsProperty("p"));
        Assert.False(context.ContainsProperty("q"));
    }
}
