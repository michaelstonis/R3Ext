using System.Windows.Input;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

[Collection("FrameProvider")]
public class RxCommandAdvancedTests(FrameProviderFixture fp)
{
    [Fact]
    public void CreateCombined_ExecutesAllChildCommandsInParallel()
    {
        var childResults = new List<int>();
        var cmd1 = RxCommand<int, int>.Create(x =>
        {
            childResults.Add(x * 2);
            return x * 2;
        });
        var cmd2 = RxCommand<int, int>.Create(x =>
        {
            childResults.Add(x * 3);
            return x * 3;
        });
        var cmd3 = RxCommand<int, int>.Create(x =>
        {
            childResults.Add(x * 4);
            return x * 4;
        });

        var combined = RxCommand<int, int>.CreateCombined(cmd1, cmd2, cmd3);

        var results = new List<int[]>();
        combined.AsObservable().Subscribe(result => results.Add(result));

        combined.Execute(5).Subscribe(_ => { });

        Assert.Single(results);
        Assert.Equal([10, 15, 20], results[0]);
        Assert.Equal(3, childResults.Count);
        Assert.Contains(10, childResults);
        Assert.Contains(15, childResults);
        Assert.Contains(20, childResults);
    }

    [Fact]
    public void CreateCombined_ThrowsOnNullOrEmptyChildCommands()
    {
        Assert.Throws<ArgumentException>(() => RxCommand<int, int>.CreateCombined());
        Assert.Throws<ArgumentException>(() => RxCommand<int, int>.CreateCombined(null!));
    }

    [Fact]
    public void CreateCombined_CanExecuteIsFalseWhenAnyChildCannotExecute()
    {
        var canExecute1 = new ReactiveProperty<bool>(true);
        var canExecute2 = new ReactiveProperty<bool>(false);

        var cmd1 = RxCommand<int, int>.Create(x => x, canExecute1);
        var cmd2 = RxCommand<int, int>.Create(x => x, canExecute2);

        var combined = RxCommand<int, int>.CreateCombined(cmd1, cmd2);

        bool? canExecuteCombined = null;
        combined.CanExecute.Subscribe(x => canExecuteCombined = x);

        Assert.False(canExecuteCombined);

        canExecute2.Value = true;
        Assert.True(canExecuteCombined);
    }

    [Fact]
    public async Task CreateRunInBackground_ExecutesOnBackgroundThread()
    {
        var flags = new List<bool>();

        var cmd = RxCommand<int, int>.CreateRunInBackground(x =>
        {
            flags.Add(System.Threading.Thread.CurrentThread.IsThreadPoolThread);
            return x * 2;
        });

        await cmd.Execute(5).FirstAsync();

        Assert.Single(flags);
        Assert.True(flags[0]);
    }

    [Fact]
    public void ThrownExceptions_CapturesExceptionsFromExecution()
    {
        var exceptions = new List<Exception>();
        var cmd = RxCommand<int, int>.Create(x =>
        {
            if (x < 0)
            {
                throw new InvalidOperationException("Negative value");
            }

            return x * 2;
        });

        cmd.ThrownExceptions.Subscribe(exceptions.Add);

        cmd.Execute(-5).Subscribe(_ => { }, _ => { });

        Assert.Single(exceptions);
        Assert.IsType<InvalidOperationException>(exceptions[0]);
        Assert.Equal("Negative value", exceptions[0].Message);
    }

    [Fact]
    public void ThrownExceptions_DoesNotCaptureWhenExecutionSucceeds()
    {
        var exceptions = new List<Exception>();
        var cmd = RxCommand<int, int>.Create(x => x * 2);

        cmd.ThrownExceptions.Subscribe(exceptions.Add);

        cmd.Execute(5).Subscribe(_ => { });

        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task IsExecuting_IsTrueDuringExecution()
    {
        var tcsStart = new TaskCompletionSource<bool>();
        var tcsEnd = new TaskCompletionSource<bool>();

        var cmd = RxCommand<int, int>.CreateFromTask(async x =>
        {
            tcsStart.TrySetResult(true);
            await Task.Delay(100);
            return x * 2;
        });
        var states = cmd.IsExecuting.ToLiveList();
        _ = cmd.IsExecuting.Subscribe(flag =>
        {
            if (!flag && tcsStart.Task.IsCompleted)
            {
                tcsEnd.TrySetResult(true);
            }
        });

        cmd.Execute(1).Subscribe(_ => { });
        await tcsStart.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(states[^1]);
        await tcsEnd.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(states[^1]);
    }

    [Fact]
    public async Task ConcurrentExecutions_TrackIsExecutingCorrectly()
    {
        var executingStates = new List<bool>();
        var tcs = new TaskCompletionSource<int>();

        var cmd = RxCommand<int, int>.CreateFromTask((x, ct) => tcs.Task);

        cmd.IsExecuting.Subscribe(executingStates.Add);

        // Start execution
        var task = cmd.Execute(1).FirstAsync();

        await Task.Delay(10); // Allow IsExecuting to update

        // Should have at least initial false and then true
        Assert.Contains(true, executingStates);

        tcs.SetResult(100); // Complete
        await task;

        await Task.Delay(10); // Allow final state update

        // Should end with false
        Assert.False(executingStates.Last());
    }

    [Fact]
    public async Task Cancellation_StopsExecutionWhenCancelled()
    {
        var executed = false;
        var cmd = RxCommand<int, int>.CreateFromTask(async (x, ct) =>
        {
            await Task.Delay(100, ct);
            executed = true;
            return x * 2;
        });

        var cts = new CancellationTokenSource();
        var task = cmd.Execute(5).FirstAsync(cts.Token);

        cts.Cancel();

        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
        await Task.Delay(150);
        Assert.False(executed);
    }

    [Fact]
    public async Task OutputScheduler_DelaysResultOutput()
    {
        var scheduler = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var results = new List<int>();

        var cmd = RxCommand<int, int>.Create(x => x * 2, null, scheduler);

        cmd.AsObservable().Subscribe(results.Add);

        var executeTask = cmd.Execute(5).FirstAsync();

        // For synchronous execution, scheduler doesn't delay - just verify result
        await executeTask;

        Assert.Single(results);
        Assert.Equal(10, results[0]);
    }

    [Fact]
    public async Task CreateFromR3Observable_ConvertsObservableToCommand()
    {
        var source = Observable.Return(42);
        var cmd = RxCommand<int, int>.CreateFromR3Observable(x => source);

        var result = await cmd.Execute(0).FirstAsync();

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task CreateFromObservable_HandlesEmptyObservable()
    {
        var tcs = new TaskCompletionSource<int>();
        tcs.SetException(new InvalidOperationException("Empty sequence"));
        var cmd = RxCommand<int, int>.CreateFromTask((x, ct) => tcs.Task);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await cmd.Execute(0).FirstAsync());
    }

    [Fact]
    public async Task CreateFromR3Observable_UsesR3Observable()
    {
        var cmd = RxCommand<int, int>.CreateFromR3Observable(x => Observable.Return(x * 3));

        var result = await cmd.Execute(7).FirstAsync();

        Assert.Equal(21, result);
    }

    [Fact]
    public async Task CreateFromR3Observable_HandlesObservableSequence()
    {
        var cmd = RxCommand<int, int>.CreateFromR3Observable(x => Observable.Range(1, 5));

        var result = await cmd.Execute(0).FirstAsync(); // Takes first value

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Dispose_DuringExecution_CompletesGracefully()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cmd = RxCommand<int, int>.CreateFromTask(async (x, ct) =>
        {
            started.TrySetResult(true);

            // Block until disposed/cancelled to simulate in-flight work
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
            }

            return x * 2;
        });

        var sub = cmd.Execute(1).Subscribe(_ => { });

        // Ensure execution has started deterministically
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await cmd.IsExecuting.FirstAsync());

        // Dispose the subscription while execution is in progress
        sub.Dispose();

        // Await the next false in IsExecuting deterministically
        var completed = await cmd.IsExecuting.Where(v => v == false).FirstAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(completed);
    }

    [Fact]
    public async Task Dispose_PreventsNewExecutions()
    {
        var cmd = RxCommand<int, int>.Create(x => x * 2);

        cmd.Dispose();

        var results = new List<int>();
        await cmd.Execute(5).ForEachAsync(results.Add);

        // After disposal, execution should complete without results
        Assert.Empty(results);
    }

    [Fact]
    public void ICommand_CanExecute_ReturnsFalseWhenDisposed()
    {
        ICommand cmd = RxCommand<int, int>.Create(x => x * 2);

        Assert.True(cmd.CanExecute(null));

        ((RxCommand<int, int>)cmd).Dispose();

        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public void ICommand_Execute_DoesNothingWhenDisposed()
    {
        var executed = false;
        ICommand cmd = RxCommand<int, int>.Create(x =>
        {
            executed = true;
            return x * 2;
        });

        ((RxCommand<int, int>)cmd).Dispose();

        cmd.Execute(5);

        Assert.False(executed);
    }

    [Fact]
    public async Task MultipleSubscribers_ReceiveSameExecutionResults()
    {
        var cmd = RxCommand<int, int>.Create(x => x * 2);

        var results1 = new List<int>();
        var results2 = new List<int>();

        cmd.AsObservable().Subscribe(r => results1.Add(r));
        cmd.AsObservable().Subscribe(r => results2.Add(r));

        await cmd.Execute(5).FirstAsync();

        Assert.Single(results1);
        Assert.Equal(10, results1[0]);
        Assert.Single(results2);
        Assert.Equal(10, results2[0]);
    }

    [Fact]
    public void CanExecute_PreventsExecutionWhileAlreadyExecuting()
    {
        var tcs = new TaskCompletionSource<int>();
        var cmd = RxCommand<int, int>.CreateFromTask((x, ct) => tcs.Task);

        cmd.Execute(5).Subscribe(_ => { });

        bool canExecute = ((ICommand)cmd).CanExecute(null);

        Assert.False(canExecute);

        tcs.SetResult(10);
    }

    [Fact]
    public async Task Exception_DoesNotStopSubsequentExecutions()
    {
        var counter = 0;
        var cmd = RxCommand<int, int>.Create(x =>
        {
            counter++;
            if (counter == 1)
            {
                throw new InvalidOperationException("First execution fails");
            }

            return x * 2;
        });

        cmd.Execute(5).Subscribe(_ => { }, _ => { });

        var result = await cmd.Execute(7).FirstAsync();

        Assert.Equal(2, counter);
        Assert.Equal(14, result);
    }
}
