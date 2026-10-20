using Sap.Cloud.Sdk.CloudPlatform;

namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public class ResultTests
{
    [Fact]
    public void Of_CapturesValueAndException()
    {
        Assert.Equal(42, Result.Of(() => 42).GetValueOrThrow());

        var failure = Result.Of<int>(() => throw new InvalidOperationException("boom"));
        Assert.True(failure.IsFailure);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public void GetValueOrThrow_RethrowsOriginalException()
    {
        var original = new InvalidOperationException("boom");
        var thrown = Assert.Throws<InvalidOperationException>(() => Result.Failure<int>(original).GetValueOrThrow());
        Assert.Same(original, thrown);
    }

    [Fact]
    public void MapAndBind_PropagateFailuresAndCaptureExceptions()
    {
        Assert.Equal("2", Result.Success(1).Map(x => (x + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)).GetValueOrThrow());
        Assert.True(Result.Success(1).Map<int>(_ => throw new InvalidOperationException()).IsFailure);
        Assert.True(Result.Success(1).Bind<int>(_ => throw new InvalidOperationException()).IsFailure);

        var cause = new InvalidOperationException();
        Assert.Same(cause, Result.Failure<int>(cause).Map(x => x + 1).Exception);
    }

    [Fact]
    public void RecoverOrElseOnFailure()
    {
        var failure = Result.Failure<int>(new InvalidOperationException());
        Assert.Equal(7, failure.Recover(_ => 7).GetValueOrThrow());
        Assert.Equal(8, failure.OrElse(() => Result.Success(8)).GetValueOrThrow());
        Assert.Equal(1, Result.Success(1).OrElse(() => Result.Success(8)).GetValueOrThrow());

        Exception? seen = null;
        failure.OnFailure(e => seen = e);
        Assert.Same(failure.Exception, seen);
    }
}
