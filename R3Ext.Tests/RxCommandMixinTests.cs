using System;
using System.Collections.Generic;
using System.Threading;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class RxCommandMixinTests
{
    [Fact]
    public void InvokeCommand_WithProjection_ExecutesWhenCan()
    {
        var can = new ReactiveProperty<bool>(true);
        var execs = new List<int>();
        var cmd = RxCommand<int,int>.Create(x => { execs.Add(x); return x * 2; }, can);
        var src = new Subject<string>();

        using var sub = src.InvokeCommand(cmd, s => int.Parse(s));

        src.OnNext("1");
        src.OnNext("2");
        can.Value = false;
        src.OnNext("3"); // suppressed
        can.Value = true;
        src.OnNext("4");

        Assert.Equal(new[] {1,2,4}, execs);
    }

    [Fact]
    public void InvokeCommand_SystemObservable_Bridge()
    {
        var execs = new List<int>();
        var cmd = RxCommand<int,int>.Create(x => { execs.Add(x); return x; });
        var src = new TestObservableSource();
        using var sub = src.InvokeCommand(cmd); // uses existing extension on IObservable
        src.OnNext(10);
        src.OnNext(11);
        Assert.Equal(new[]{10,11}, execs);
    }

    [Fact]
    public void InvokeCommand_ICommand_Gating()
    {
        var count = 0;
        var fake = new TestCommand(() => count++);
        var src = new Subject<int>();
        using var sub = src.InvokeCommand(fake);
        src.OnNext(1);
        fake.Can = false;
        src.OnNext(2); // suppressed
        fake.Can = true;
        src.OnNext(3);
        Assert.Equal(2, count);
    }

    private sealed class TestCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public bool Can { get; set; } = true;
        public TestCommand(Action action) => _action = action;
        public bool CanExecute(object? parameter) => Can;
        public void Execute(object? parameter) => _action();
        public event EventHandler? CanExecuteChanged;
        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TestObservableSource : IObservable<int>
    {
        private readonly Subject<int> _inner = new();
        public IDisposable Subscribe(IObserver<int> observer) => _inner.AsObservable().Subscribe(
            v => observer.OnNext(v),
            ex => observer.OnError(ex),
            r =>
            {
                if (r.IsSuccess) observer.OnCompleted();
                else if (r.Exception != null) observer.OnError(r.Exception);
            });
        public void OnNext(int value) => _inner.OnNext(value);
    }
}