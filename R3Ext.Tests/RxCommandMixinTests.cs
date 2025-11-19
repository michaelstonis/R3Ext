using R3;

namespace R3Ext.Tests;

public class RxCommandMixinTests
{
    [Fact]
    public void InvokeCommand_WithProjection_ExecutesWhenCan()
    {
        ReactiveProperty<bool> can = new(true);
        List<int> execs = new();
        RxCommand<int, int> cmd = RxCommand<int, int>.Create(
            x =>
            {
                execs.Add(x);
                return x * 2;
            }, can);
        Subject<string> src = new();

        using IDisposable sub = src.InvokeCommand(cmd, s => int.Parse(s));

        src.OnNext("1");
        src.OnNext("2");
        can.Value = false;
        src.OnNext("3"); // suppressed
        can.Value = true;
        src.OnNext("4");

        Assert.Equal(new[] { 1, 2, 4, }, execs);
    }

    [Fact]
    public void InvokeCommand_SystemObservable_Bridge()
    {
        List<int> execs = new();
        RxCommand<int, int> cmd = RxCommand<int, int>.Create(x =>
        {
            execs.Add(x);
            return x;
        });
        TestObservableSource src = new();
        using IDisposable sub = src.InvokeCommand(cmd); // uses existing extension on IObservable
        src.OnNext(10);
        src.OnNext(11);
        Assert.Equal(new[] { 10, 11, }, execs);
    }

    [Fact]
    public void InvokeCommand_ICommand_Gating()
    {
        int count = 0;
        TestCommand fake = new(() => count++);
        Subject<int> src = new();
        using IDisposable sub = src.InvokeCommand(fake);
        src.OnNext(1);
        fake.Can = false;
        src.OnNext(2); // suppressed
        fake.Can = true;
        src.OnNext(3);
        Assert.Equal(2, count);
    }

    private sealed class TestCommand(Action action) : System.Windows.Input.ICommand
    {
        public bool Can { get; set; } = true;

        public bool CanExecute(object? parameter)
        {
            return Can;
        }

        public void Execute(object? parameter)
        {
            action();
        }

        public event EventHandler? CanExecuteChanged;

        public void Raise()
        {
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

#pragma warning disable CA1001
    private sealed class TestObservableSource : IObservable<int>
#pragma warning restore CA1001
    {
        private readonly Subject<int> _inner = new();

        public IDisposable Subscribe(IObserver<int> observer)
        {
            return _inner.AsObservable().Subscribe(
                v => observer.OnNext(v),
                ex => observer.OnError(ex),
                r =>
                {
                    if (r.IsSuccess)
                    {
                        observer.OnCompleted();
                    }
                    else if (r.Exception != null)
                    {
                        observer.OnError(r.Exception);
                    }
                });
        }

        public void OnNext(int value)
        {
            _inner.OnNext(value);
        }
    }
}
