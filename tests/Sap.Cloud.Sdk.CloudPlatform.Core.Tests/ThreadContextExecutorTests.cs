using Sap.Cloud.Sdk.CloudPlatform.Threading;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;
using Sap.Cloud.Sdk.TestSupport;

namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public class ThreadContextExecutorTests : StaticStateReset
{
    private sealed class RecordingListener(int priority, List<string> log, string value) : IThreadContextListener
    {
        public int Priority => priority;

        public void BeforeInitialize(IThreadContext threadContext)
        {
            log.Add($"before:{priority}:current={ThreadContextAccessor.GetCurrentContextOrNull() == threadContext}");
        }

        public void AfterInitialize(IThreadContext threadContext)
        {
            log.Add($"after:{priority}:current={ThreadContextAccessor.GetCurrentContextOrNull() == threadContext}");
            threadContext.SetProperty($"p{priority}", ThreadContextProperty.Of(value));
        }
    }

    [Fact]
    public void Execute_SetsContextAndRestoresPrevious()
    {
        Assert.Null(ThreadContextAccessor.GetCurrentContextOrNull());

        var inner = ThreadContextExecutor.FromNewContext().Execute(() => ThreadContextAccessor.GetCurrentContext());

        Assert.NotNull(inner);
        Assert.Null(ThreadContextAccessor.GetCurrentContextOrNull());
    }

    [Fact]
    public void Execute_RestoresOuterContextAfterNestedExecution()
    {
        ThreadContextExecutor.FromNewContext().WithListeners(new RecordingListener(1, [], "outer")).Execute(() =>
        {
            var outer = ThreadContextAccessor.GetCurrentContext();
            var innerValue = ThreadContextExecutor.FromCurrentContext()
                .WithListeners(new RecordingListener(1, [], "inner"))
                .Execute(() => ThreadContextAccessor.GetCurrentContext().GetPropertyValue<string>("p1").GetValueOrThrow());

            Assert.Equal("inner", innerValue);
            Assert.Same(outer, ThreadContextAccessor.GetCurrentContext());
            Assert.Equal("outer", outer.GetPropertyValue<string>("p1").GetValueOrThrow());
        });
    }

    [Fact]
    public void Listeners_RunInPriorityOrder_BeforeSeesPreviousContext()
    {
        var log = new List<string>();
        ThreadContextExecutor.FromNewContext()
            .WithoutDefaultListeners()
            .WithListeners(new RecordingListener(5, log, "b"), new RecordingListener(-1, log, "a"))
            .Execute(() => { });

        Assert.Equal(
            ["before:-1:current=False", "before:5:current=False", "after:-1:current=True", "after:5:current=True"],
            log);
    }

    [Fact]
    public void Execute_WrapsExceptions_ButNotCancellation()
    {
        var executor = ThreadContextExecutor.FromNewContext();
        var wrapped = Assert.Throws<ThreadContextExecutionException>(
            () => executor.Execute(() => throw new InvalidOperationException("boom")));
        Assert.IsType<InvalidOperationException>(wrapped.InnerException);

        Assert.Throws<OperationCanceledException>(() => executor.Execute(() => throw new OperationCanceledException()));
        Assert.Null(ThreadContextAccessor.GetCurrentContextOrNull());
    }

    [Fact]
    public async Task WithoutExceptionWrapping_PropagatesOriginalException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ThreadContextExecutor.FromNewContext()
            .WithoutExceptionWrapping()
            .ExecuteAsync(() => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task ExecuteAsync_ContextFlowsAcrossAwaitAndIntoChildTasks()
    {
        var values = await ThreadContextExecutor.FromNewContext()
            .WithListeners(new RecordingListener(1, [], "value"))
            .ExecuteAsync(async () =>
            {
                await Task.Yield();
                var afterAwait = ThreadContextAccessor.GetCurrentContext().GetPropertyValue<string>("p1").GetValueOrThrow();
                var inChildTask = await Task.Run(
                    () => ThreadContextAccessor.GetCurrentContext().GetPropertyValue<string>("p1").GetValueOrThrow());
                return (afterAwait, inChildTask);
            });

        Assert.Equal(("value", "value"), values);
        Assert.Null(ThreadContextAccessor.GetCurrentContextOrNull());
    }

    [Fact]
    public async Task ExecuteAsync_ParallelFlowsAreIsolated()
    {
        var tasks = Enumerable.Range(0, 50).Select(i => ThreadContextExecutor.FromNewContext()
            .WithListeners(new RecordingListener(1, [], $"v{i}"))
            .ExecuteAsync(async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 10));
                return ThreadContextAccessor.GetCurrentContext().GetPropertyValue<string>("p1").GetValueOrThrow();
            }));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(0, 50).Select(i => $"v{i}"), results);
    }

    [Fact]
    public void FromCurrentContext_WithoutContext_Throws()
    {
        Assert.Throws<ThreadContextAccessException>(ThreadContextExecutor.FromCurrentContext);
    }

    [Fact]
    public void Execution_DoesNotModifyTheSourceContext()
    {
        var source = new DefaultThreadContext();
        ThreadContextExecutor.Using(source)
            .WithListeners(new RecordingListener(1, [], "value"))
            .Execute(() => { });
        Assert.False(source.ContainsProperty("p1"));
    }
}
