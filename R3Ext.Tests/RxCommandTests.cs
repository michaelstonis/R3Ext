using System.Windows.Input;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class RxCommandTests
{
    [Fact]
    public void Create_WithSyncAction_ExecutesSuccessfully()
    {
        int executionCount = 0;
        RxCommand<Unit, Unit> command = RxCommand.Create(() => executionCount++);
        LiveList<Unit> results = command.Execute().ToLiveList();
        Assert.Equal(1, executionCount);
        Assert.Single(results);
        Assert.Equal(Unit.Default, results[0]);
        Assert.True(results.IsCompleted);
    }

    [Fact]
    public void Create_WithGenericSyncFunc_ReturnsCorrectValue()
    {
        RxCommand<int, string> command = RxCommand<int, string>.Create(x => $"Value: {x}");
        LiveList<string> results = command.Execute(42).ToLiveList();
        Assert.Single(results);
        Assert.Equal("Value: 42", results[0]);
        Assert.True(results.IsCompleted);
    }

    [Fact]
    public async Task CreateFromTask_ExecutesAsyncOperation()
    {
        bool executed = false;
        RxCommand<Unit, Unit> command = RxCommand.CreateFromTask(async () =>
        {
            await Task.Delay(10);
            executed = true;
        });
        await command.Execute().FirstAsync();
        Assert.True(executed);
    }

    [Fact]
    public async Task CreateFromTask_WithCancellationToken_PropagatesToken()
    {
        CancellationToken captured = default;
        bool got = false;
        RxCommand<Unit, Unit> command = RxCommand<Unit, Unit>.CreateFromTask(async (_, ct) =>
        {
            captured = ct;
            got = true;
            await Task.Delay(10, ct);
            return Unit.Default;
        });
        await command.Execute().FirstAsync();
        Assert.True(got);
        Assert.NotEqual(default, captured);
    }

    [Fact]
    public async Task CreateFromObservable_ExecutesObservableLogic()
    {
        RxCommand<int, int> command = RxCommand<int, int>.CreateFromR3Observable(x => Observable.Return(x * 2));
        int result = await command.Execute(21).FirstAsync();
        Assert.Equal(42, result);
    }

    [Fact]
    public void CanExecute_PreventsExecutionWhenFalse()
    {
        ReactiveProperty<bool> canProp = new(true);
        RxCommand<Unit, Unit> command = RxCommand.Create(() => { }, canProp);
        canProp.Value = false;
        bool can = ((ICommand)command).CanExecute(null);
        Assert.False(can);
    }

    [Fact]
    public async Task CanExecute_AllowsExecutionWhenTrue()
    {
        Subject<bool> canSubject = new();
        bool executed = false;
        RxCommand<Unit, Unit> command = RxCommand.Create(() => executed = true, canSubject);
        canSubject.OnNext(true);
        await command.Execute().FirstAsync();
        Assert.True(executed);
    }

    [Fact]
    public async Task IsExecuting_ReflectsExecutionState()
    {
        TaskCompletionSource<Unit> tcs = new();
        RxCommand<Unit, Unit> command = RxCommand.CreateFromTask(() => tcs.Task);
        LiveList<bool> isExec = command.IsExecuting.ToLiveList();
        await Task.Delay(10);
        Assert.False(isExec[^1]);
        Task<Unit> task = command.Execute().FirstAsync();
        await Task.Delay(100);
        Assert.True(isExec[^1]);
        tcs.SetResult(Unit.Default);
        await task;
        await Task.Delay(50);
        Assert.False(isExec[^1]);
    }

    [Fact]
    public async Task ThrownExceptions_CapturesErrors()
    {
        InvalidOperationException expected = new("Test error");
        RxCommand<Unit, Unit> command = RxCommand.Create(() => throw expected);
        LiveList<Exception> exceptions = command.ThrownExceptions.ToLiveList();
        try
        {
            await command.Execute().FirstAsync();
        }
        catch
        {
        }

        Assert.Single(exceptions);
        Assert.Equal(expected, exceptions[0]);
    }

    [Fact]
    public async Task CreateCombined_ExecutesAllChildCommands()
    {
        int c1 = 0;
        int c2 = 0;
        int c3 = 0;
        RxCommand<int, int> cmd1 = RxCommand<int, int>.Create(x =>
        {
            c1++;
            return x * 2;
        });
        RxCommand<int, int> cmd2 = RxCommand<int, int>.Create(x =>
        {
            c2++;
            return x * 3;
        });
        RxCommand<int, int> cmd3 = RxCommand<int, int>.Create(x =>
        {
            c3++;
            return x * 4;
        });
        RxCommand<int, int[]> combined = RxCommand<int, int>.CreateCombined(cmd1, cmd2, cmd3);
        int[] results = await combined.Execute(5).FirstAsync();
        Assert.Equal(1, c1);
        Assert.Equal(1, c2);
        Assert.Equal(1, c3);
        Assert.Equal(new[] { 10, 15, 20, }, results);
    }

    [Fact]
    public async Task CreateCombined_CanExecuteOnlyWhenAllChildrenCanExecute()
    {
        Subject<bool> can1 = new();
        Subject<bool> can2 = new();
        RxCommand<int, int> cmd1 = RxCommand<int, int>.Create(x => x * 2, can1);
        RxCommand<int, int> cmd2 = RxCommand<int, int>.Create(x => x * 3, can2);
        RxCommand<int, int[]> combined = RxCommand<int, int>.CreateCombined(cmd1, cmd2);
        LiveList<bool> vals = combined.CanExecute.ToLiveList();
        can1.OnNext(true);
        can2.OnNext(false);
        await Task.Delay(50);
        Assert.False(vals[^1]);
        can1.OnNext(true);
        can2.OnNext(true);
        await Task.Delay(50);
        Assert.True(vals[^1]);
        can1.OnNext(false);
        can2.OnNext(true);
        await Task.Delay(50);
        Assert.False(vals[^1]);
    }

    [Fact]
    public void InvokeCommand_ExecutesCommandWhenSourceEmits()
    {
        int count = 0;
        RxCommand<int, int> command = RxCommand<int, int>.Create(x =>
        {
            count++;
            return x * 2;
        });
        Subject<int> source = new();
        IDisposable sub = source.AsSystemObservable().InvokeCommand(command);
        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        Thread.Sleep(100);
        Assert.Equal(3, count);
        sub.Dispose();
    }

    [Fact]
    public void IObservable_Subscribe_ReceivesExecutionResults()
    {
        RxCommand<int, int> command = RxCommand<int, int>.Create(x => x * 2);
        List<int> list = new();
        TestObserver<int> observer = new(list);
        using IDisposable sub = ((IObservable<int>)command).Subscribe(observer);
        command.Execute(21).Subscribe(_ => { });
        command.Execute(42).Subscribe(_ => { });
        Thread.Sleep(100);
        Assert.Equal(2, list.Count);
        Assert.Contains(42, list);
        Assert.Contains(84, list);
    }

    private sealed class TestObserver<T>(List<T> sink) : IObserver<T>
    {
        public void OnNext(T value)
        {
            sink.Add(value);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    [Fact]
    public void ICommand_Execute_InvokesCommand()
    {
        bool executed = false;
        RxCommand<Unit, Unit> command = RxCommand.Create(() => executed = true);
        ICommand icmd = (ICommand)command;
        icmd.Execute(null);
        Thread.Sleep(100);
        Assert.True(executed);
    }

    [Fact]
    public void ICommand_CanExecuteChanged_FiresWhenCanExecuteChanges()
    {
        Subject<bool> subj = new();
        RxCommand<Unit, Unit> command = RxCommand.Create(() => { }, subj);
        ICommand icmd = (ICommand)command;
        bool fired = false;
        icmd.CanExecuteChanged += (_, _) => fired = true;
        subj.OnNext(false);
        Thread.Sleep(50);
        Assert.True(fired);
    }

    [Fact]
    public void Dispose_StopsExecution()
    {
        RxCommand<Unit, Unit> command = RxCommand.Create(() => { });
        ICommand icmd = (ICommand)command;
        command.Dispose();
        Assert.False(icmd.CanExecute(null));
    }

    [Fact]
    public async Task CreateRunInBackground_ExecutesOnThreadPool()
    {
        int mainId = Thread.CurrentThread.ManagedThreadId;
        int execId = 0;
        RxCommand<Unit, int> command = RxCommand<Unit, int>.CreateRunInBackground(_ =>
        {
            execId = Thread.CurrentThread.ManagedThreadId;
            return execId;
        });
        int result = await command.Execute().FirstAsync();
        Assert.NotEqual(mainId, execId);
        Assert.Equal(execId, result);
    }

    [Fact]
    public async Task Execute_ReturnsObservableThatCompletesAfterExecution()
    {
        RxCommand<int, int> command = RxCommand<int, int>.Create(x => x * 2);
        LiveList<int> list = command.Execute(21).ToLiveList();
        await Task.Delay(100);
        Assert.Single(list);
        Assert.Equal(42, list[0]);
        Assert.True(list.IsCompleted);
    }

    [Fact]
    public void AsObservable_ReturnsObservableOfExecutionResults()
    {
        RxCommand<int, int> command = RxCommand<int, int>.Create(x => x * 2);
        LiveList<int> list = command.AsObservable().ToLiveList();
        command.Execute(10).Subscribe(_ => { });
        command.Execute(20).Subscribe(_ => { });
        command.Execute(30).Subscribe(_ => { });
        Thread.Sleep(100);
        Assert.Equal(3, list.Count);
        Assert.Equal(20, list[0]);
        Assert.Equal(40, list[1]);
        Assert.Equal(60, list[2]);
    }
}
