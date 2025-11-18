using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class ReactiveCommandTests
{
    [Fact]
    public void Create_WithSyncAction_ExecutesSuccessfully()
    {
        var executionCount = 0;
        var command = RxCommand.Create(() => executionCount++);
        var results = command.Execute().ToLiveList();
        Assert.Equal(1, executionCount);
        Assert.Single(results);
        Assert.Equal(Unit.Default, results[0]);
        Assert.True(results.IsCompleted);
    }

    [Fact]
    public void Create_WithGenericSyncFunc_ReturnsCorrectValue()
    {
        var command = RxCommand<int, string>.Create(x => $"Value: {x}");
        var results = command.Execute(42).ToLiveList();
        Assert.Single(results);
        Assert.Equal("Value: 42", results[0]);
        Assert.True(results.IsCompleted);
    }

    [Fact]
    public async Task CreateFromTask_ExecutesAsyncOperation()
    {
        var executed = false;
        var command = RxCommand.CreateFromTask(async () =>
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
        var got = false;
        var command = RxCommand<Unit, Unit>.CreateFromTask(async (_, ct) =>
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
        var command = RxCommand<int, int>.CreateFromR3Observable(x => Observable.Return(x * 2));
        var result = await command.Execute(21).FirstAsync();
        Assert.Equal(42, result);
    }

    [Fact]
    public void CanExecute_PreventsExecutionWhenFalse()
    {
        var canProp = new ReactiveProperty<bool>(true);
        var command = RxCommand.Create(() => { }, canProp);
        canProp.Value = false;
        var can = ((ICommand)command).CanExecute(null);
        Assert.False(can);
    }

    [Fact]
    public async Task CanExecute_AllowsExecutionWhenTrue()
    {
        var canSubject = new Subject<bool>();
        var executed = false;
        var command = RxCommand.Create(() => executed = true, canSubject);
        canSubject.OnNext(true);
        await command.Execute().FirstAsync();
        Assert.True(executed);
    }

    [Fact]
    public async Task IsExecuting_ReflectsExecutionState()
    {
        var tcs = new TaskCompletionSource<Unit>();
        var command = RxCommand.CreateFromTask(() => tcs.Task);
        var isExec = command.IsExecuting.ToLiveList();
        await Task.Delay(10);
        Assert.False(isExec[^1]);
        var task = command.Execute().FirstAsync();
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
        var expected = new InvalidOperationException("Test error");
        var command = RxCommand.Create(() => throw expected);
        var exceptions = command.ThrownExceptions.ToLiveList();
        try { await command.Execute().FirstAsync(); } catch { }
        Assert.Single(exceptions);
        Assert.Equal(expected, exceptions[0]);
    }

    [Fact]
    public async Task CreateCombined_ExecutesAllChildCommands()
    {
        var c1=0; var c2=0; var c3=0;
        var cmd1 = RxCommand<int,int>.Create(x=> { c1++; return x*2; });
        var cmd2 = RxCommand<int,int>.Create(x=> { c2++; return x*3; });
        var cmd3 = RxCommand<int,int>.Create(x=> { c3++; return x*4; });
        var combined = RxCommand<int,int>.CreateCombined(cmd1, cmd2, cmd3);
        var results = await combined.Execute(5).FirstAsync();
        Assert.Equal(1,c1); Assert.Equal(1,c2); Assert.Equal(1,c3);
        Assert.Equal(new[]{10,15,20}, results);
    }

    [Fact]
    public async Task CreateCombined_CanExecuteOnlyWhenAllChildrenCanExecute()
    {
        var can1 = new Subject<bool>();
        var can2 = new Subject<bool>();
        var cmd1 = RxCommand<int,int>.Create(x=> x*2, can1);
        var cmd2 = RxCommand<int,int>.Create(x=> x*3, can2);
        var combined = RxCommand<int,int>.CreateCombined(cmd1, cmd2);
        var vals = combined.CanExecute.ToLiveList();
        can1.OnNext(true); can2.OnNext(false); await Task.Delay(50); Assert.False(vals[^1]);
        can1.OnNext(true); can2.OnNext(true); await Task.Delay(50); Assert.True(vals[^1]);
        can1.OnNext(false); can2.OnNext(true); await Task.Delay(50); Assert.False(vals[^1]);
    }

    [Fact]
    public void InvokeCommand_ExecutesCommandWhenSourceEmits()
    {
        int count=0;
        var command = RxCommand<int,int>.Create(x=> { count++; return x*2; });
        var source = new Subject<int>();
        var sub = source.AsSystemObservable().InvokeCommand(command);
        source.OnNext(1); source.OnNext(2); source.OnNext(3);
        Thread.Sleep(100);
        Assert.Equal(3,count);
        sub.Dispose();
    }

    [Fact]
    public void IObservable_Subscribe_ReceivesExecutionResults()
    {
        var command = RxCommand<int,int>.Create(x=> x*2);
        var list = new System.Collections.Generic.List<int>();
        var observer = new TestObserver<int>(list);
        using var sub = ((IObservable<int>)command).Subscribe(observer);
        command.Execute(21).Subscribe(_=>{});
        command.Execute(42).Subscribe(_=>{});
        Thread.Sleep(100);
        Assert.Equal(2,list.Count);
        Assert.Contains(42,list);
        Assert.Contains(84,list);
    }

    private sealed class TestObserver<T> : IObserver<T>
    {
        private readonly System.Collections.Generic.List<T> _sink;
        public TestObserver(System.Collections.Generic.List<T> sink) => _sink = sink;
        public void OnNext(T value) => _sink.Add(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public void ICommand_Execute_InvokesCommand()
    {
        bool executed=false;
        var command = RxCommand.Create(()=> executed=true);
        var icmd = (ICommand)command;
        icmd.Execute(null);
        Thread.Sleep(100);
        Assert.True(executed);
    }

    [Fact]
    public void ICommand_CanExecuteChanged_FiresWhenCanExecuteChanges()
    {
        var subj = new Subject<bool>();
        var command = RxCommand.Create(()=>{}, subj);
        var icmd = (ICommand)command;
        bool fired=false;
        icmd.CanExecuteChanged += (_,_)=> fired=true;
        subj.OnNext(false);
        Thread.Sleep(50);
        Assert.True(fired);
    }

    [Fact]
    public void Dispose_StopsExecution()
    {
        var command = RxCommand.Create(()=>{});
        var icmd = (ICommand)command;
        command.Dispose();
        Assert.False(icmd.CanExecute(null));
    }

    [Fact]
    public async Task CreateRunInBackground_ExecutesOnThreadPool()
    {
        var mainId = Thread.CurrentThread.ManagedThreadId;
        int execId = 0;
        var command = RxCommand<Unit,int>.CreateRunInBackground(_=> { execId = Thread.CurrentThread.ManagedThreadId; return execId; });
        var result = await command.Execute().FirstAsync();
        Assert.NotEqual(mainId, execId);
        Assert.Equal(execId, result);
    }

    [Fact]
    public async Task Execute_ReturnsObservableThatCompletesAfterExecution()
    {
        var command = RxCommand<int,int>.Create(x=> x*2);
        var list = command.Execute(21).ToLiveList();
        await Task.Delay(100);
        Assert.Single(list);
        Assert.Equal(42,list[0]);
        Assert.True(list.IsCompleted);
    }

    [Fact]
    public void AsObservable_ReturnsObservableOfExecutionResults()
    {
        var command = RxCommand<int,int>.Create(x=> x*2);
        var list = command.AsObservable().ToLiveList();
        command.Execute(10).Subscribe(_=>{});
        command.Execute(20).Subscribe(_=>{});
        command.Execute(30).Subscribe(_=>{});
        Thread.Sleep(100);
        Assert.Equal(3,list.Count);
        Assert.Equal(20,list[0]);
        Assert.Equal(40,list[1]);
        Assert.Equal(60,list[2]);
    }
}
