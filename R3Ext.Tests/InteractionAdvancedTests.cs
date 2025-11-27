using R3;
using Xunit;

namespace R3Ext.Tests;

public class InteractionAdvancedTests
{
    [Fact]
    public async Task MultipleHandlers_OnlyFirstThatHandlesExecutes()
    {
        var interaction = new Interaction<int, string>();
        var handler1Executed = false;
        var handler2Executed = false;
        var handler3Executed = false;

        interaction.RegisterHandler(ctx =>
        {
            handler1Executed = true;

            // Don't handle
        });

        interaction.RegisterHandler(ctx =>
        {
            handler2Executed = true;
            ctx.SetOutput("handler2");
        });

        interaction.RegisterHandler(ctx =>
        {
            handler3Executed = true;
            ctx.SetOutput("handler3");
        });

        var result = await interaction.Handle(42).FirstAsync();

        Assert.Equal("handler3", result); // Latest registered
        Assert.False(handler1Executed);
        Assert.False(handler2Executed);
        Assert.True(handler3Executed);
    }

    [Fact]
    public async Task ConditionalHandling_BasedOnInput()
    {
        var interaction = new Interaction<int, string>();

        interaction.RegisterHandler(ctx =>
        {
            if (ctx.Input > 0)
            {
                ctx.SetOutput("positive");
            }
        });

        interaction.RegisterHandler(ctx =>
        {
            if (ctx.Input < 0)
            {
                ctx.SetOutput("negative");
            }
        });

        interaction.RegisterHandler(ctx => ctx.SetOutput("zero"));

        var result1 = await interaction.Handle(5).FirstAsync();
        var result2 = await interaction.Handle(-3).FirstAsync();
        var result3 = await interaction.Handle(0).FirstAsync();

        Assert.Equal("positive", result1);
        Assert.Equal("negative", result2);
        Assert.Equal("zero", result3);
    }

    [Fact]
    public async Task RegisterHandler_ThrowsOnNullAction()
    {
        var interaction = new Interaction<int, string>();
        Assert.Throws<ArgumentNullException>(() => interaction.RegisterHandler((Action<IInteractionContext<int, string>>)null!));
    }

    [Fact]
    public async Task RegisterHandler_ThrowsOnNullTask()
    {
        var interaction = new Interaction<int, string>();
        Assert.Throws<ArgumentNullException>(() => interaction.RegisterHandler((Func<IInteractionContext<int, string>, Task>)null!));
    }

    [Fact]
    public async Task RegisterHandler_ThrowsOnNullObservable()
    {
        var interaction = new Interaction<int, string>();
        Assert.Throws<ArgumentNullException>(() => interaction.RegisterHandler((Func<IInteractionContext<int, string>, Observable<Unit>>)null!));
    }

    [Fact]
    public async Task TaskHandler_PropagatesExceptionCorrectly()
    {
        var interaction = new Interaction<int, string>();

        interaction.RegisterHandler(async ctx =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Handler failed");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await interaction.Handle(42).FirstAsync());
    }

    [Fact]
    public async Task ObservableHandler_SequentialExecution()
    {
        var interaction = new Interaction<int, string>();
        var executionOrder = new List<string>();

        interaction.RegisterHandler(ctx =>
        {
            return Observable.Create<Unit>(observer =>
            {
                var _ = Task.Run(async () =>
                {
                    executionOrder.Add("handler1-start");
                    await Task.Delay(10);
                    executionOrder.Add("handler1-end");
                    observer.OnCompleted();
                });
                return Disposable.Empty;
            });
        });

        interaction.RegisterHandler(ctx =>
        {
            return Observable.Create<Unit>(observer =>
            {
                executionOrder.Add("handler2");
                ctx.SetOutput("done");
                observer.OnCompleted();
                return Disposable.Empty;
            });
        });

        await interaction.Handle(1).FirstAsync();

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("handler2", executionOrder[0]);
        Assert.Equal("handler1-start", executionOrder[1]);
        Assert.Equal("handler1-end", executionOrder[2]);
    }

    [Fact]
    public async Task MultipleDisposals_DoNotThrow()
    {
        var interaction = new Interaction<int, string>();

        var reg1 = interaction.RegisterHandler(ctx => ctx.SetOutput("test"));

        reg1.Dispose();
        reg1.Dispose(); // Should not throw

        await Assert.ThrowsAsync<UnhandledInteractionException<int, string>>(async () =>
            await interaction.Handle(1).FirstAsync());
    }

    [Fact]
    public async Task ConcurrentHandleCalls_ExecuteIndependently()
    {
        var interaction = new Interaction<int, string>();
        var handlerCount = 0;

        interaction.RegisterHandler(async ctx =>
        {
            Interlocked.Increment(ref handlerCount);
            await Task.Delay(10);
            ctx.SetOutput($"result-{ctx.Input}");
        });

        var task1 = interaction.Handle(1).FirstAsync();
        var task2 = interaction.Handle(2).FirstAsync();
        var task3 = interaction.Handle(3).FirstAsync();

        var results = await Task.WhenAll(task1, task2, task3);

        Assert.Equal(3, handlerCount);
        Assert.Contains("result-1", results);
        Assert.Contains("result-2", results);
        Assert.Contains("result-3", results);
    }

    [Fact]
    public async Task HandlerRegistrationDuringExecution_DoesNotAffectCurrentExecution()
    {
        var interaction = new Interaction<int, string>();
        var tcs = new TaskCompletionSource<bool>();

        interaction.RegisterHandler(async ctx =>
        {
            await tcs.Task;
            ctx.SetOutput("original");
        });

        var handleTask = interaction.Handle(1).FirstAsync();

        // Register another handler while first is executing
        interaction.RegisterHandler(ctx => ctx.SetOutput("new"));

        tcs.SetResult(true);

        var result = await handleTask;

        Assert.Equal("original", result);
    }

    [Fact]
    public async Task IObservableHandler_ConvertsToR3Observable()
    {
        var interaction = new Interaction<int, string>();

        // RegisterHandler with IObservable accepts both R3 and System.Reactive observables
        interaction.RegisterHandler(ctx =>
        {
            ctx.SetOutput("done");
            return Observable.Return("converted").AsSystemObservable();
        });

        var result = await interaction.Handle(1).FirstAsync();

        Assert.Equal("done", result);
    }

    [Fact]
    public async Task ContextInput_ReflectsProvidedValue()
    {
        var interaction = new Interaction<int, string>();
        int? capturedInput = null;

        interaction.RegisterHandler(ctx =>
        {
            capturedInput = ctx.Input;
            ctx.SetOutput("received");
        });

        await interaction.Handle(99).FirstAsync();

        Assert.Equal(99, capturedInput);
    }

    [Fact]
    public async Task EmptyHandlerList_ThrowsUnhandledException()
    {
        var interaction = new Interaction<int, string>();

        var ex = await Assert.ThrowsAsync<UnhandledInteractionException<int, string>>(async () =>
            await interaction.Handle(42).FirstAsync());

        Assert.NotNull(ex);
        Assert.Equal(42, ex.Input);
    }

    [Fact]
    public async Task HandlerException_StopsExecution()
    {
        var interaction = new Interaction<int, string>();
        var handler2Executed = false;

        interaction.RegisterHandler(ctx =>
        {
            handler2Executed = true;
            ctx.SetOutput("should-not-reach");
        });

        interaction.RegisterHandler(ctx =>
        {
            throw new InvalidOperationException("Handler error");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await interaction.Handle(1).FirstAsync());

        Assert.False(handler2Executed);
    }

    [Fact]
    public async Task UnhandledInteractionException_ContainsCorrectDetails()
    {
        var interaction = new Interaction<int, string>();

        var ex = await Assert.ThrowsAsync<UnhandledInteractionException<int, string>>(async () =>
            await interaction.Handle(123).FirstAsync());

        Assert.Equal(123, ex.Input);
        Assert.Same(interaction, ex.Interaction);
        Assert.Contains("123", ex.Message);
    }
}
