using System.Windows.Input;
using R3;

namespace R3Ext;

public sealed class RxCommand<TInput, TOutput> : ICommand, IObservable<TOutput>, IDisposable
{
    private readonly Subject<TOutput> _executionResults = new();
    private readonly Subject<Exception> _thrownExceptions = new();
    private readonly ReactiveProperty<bool> _isExecuting = new(false);
    private readonly Func<TInput, CancellationToken, ValueTask<TOutput>> _executeAsync;
    private readonly Observable<bool> _canExecute;
    private readonly TimeProvider? _outputScheduler;
    private readonly DisposableBag _disposables = default;

    private bool _isDisposed;

    public RxCommand(
        Func<TInput, CancellationToken, ValueTask<TOutput>> executeAsync,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute ?? Observable.Return(true);
        _outputScheduler = outputScheduler;
        _canExecute
            .DistinctUntilChanged()
            .Subscribe(_ => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty))
            .AddTo(ref _disposables);
    }

    public Observable<TOutput> AsObservable()
    {
        return _executionResults.AsObservable();
    }

    public Observable<bool> IsExecuting => _isExecuting;

    public Observable<bool> CanExecute => _canExecute;

    public Observable<Exception> ThrownExceptions => _thrownExceptions;

    public event EventHandler? CanExecuteChanged;

    public Observable<TOutput> Execute(TInput parameter)
    {
        return Observable.Create<TOutput>(async (observer, ct) =>
        {
            if (_isDisposed)
            {
                observer.OnCompleted();
                return;
            }

            try
            {
                _isExecuting.Value = true;
                TOutput result = await _executeAsync(parameter, ct);
                if (_outputScheduler != null)
                {
                    await Task.Delay(TimeSpan.Zero, _outputScheduler, ct);
                }

                _executionResults.OnNext(result);
                observer.OnNext(result);
                observer.OnCompleted();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _thrownExceptions.OnNext(ex);
                observer.OnErrorResume(ex);
                observer.OnCompleted(Result.Failure(ex));
            }
            finally
            {
                _isExecuting.Value = false;
            }
        });
    }

    public Observable<TOutput> Execute()
    {
        return this.Execute(default!);
    }

    bool ICommand.CanExecute(object? parameter)
    {
        if (_isDisposed)
        {
            return false;
        }

        if (_canExecute is ReactiveProperty<bool> rp)
        {
            return rp.CurrentValue && !_isExecuting.CurrentValue;
        }

        bool canExecuteValue = true;
        bool gotValue = false;
        using (_canExecute.Subscribe(val =>
        {
            canExecuteValue = val;
            gotValue = true;
        }))
        {
        }

        if (!gotValue)
        {
            canExecuteValue = true;
        }

        return canExecuteValue && !_isExecuting.CurrentValue;
    }

    void ICommand.Execute(object? parameter)
    {
        if (_isDisposed)
        {
            return;
        }

        TInput typed = parameter is TInput p ? p : default!;
        this.Execute(typed).Subscribe(_ => { });
    }

    IDisposable IObservable<TOutput>.Subscribe(IObserver<TOutput> observer)
    {
        if (_isDisposed)
        {
            observer.OnCompleted();
            return Disposable.Empty;
        }

        return _executionResults.Subscribe(
            value => observer.OnNext(value),
            ex => observer.OnError(ex),
            result =>
            {
                if (result.IsSuccess)
                {
                    observer.OnCompleted();
                }
                else if (result.Exception != null)
                {
                    observer.OnError(result.Exception);
                }
            });
    }

    // Factory methods
    public static RxCommand<TInput, TOutput> Create(
        Func<TInput, TOutput> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : new RxCommand<TInput, TOutput>((p, _) => new ValueTask<TOutput>(execute(p)), canExecute, outputScheduler);
    }

    public static RxCommand<TInput, TOutput> CreateFromTask(
        Func<TInput, CancellationToken, Task<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : new RxCommand<TInput, TOutput>(async (p, ct) => await execute(p, ct), canExecute, outputScheduler);
    }

    public static RxCommand<TInput, TOutput> CreateFromTask(
        Func<TInput, Task<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : new RxCommand<TInput, TOutput>(async (p, _) => await execute(p), canExecute, outputScheduler);
    }

    public static RxCommand<TInput, TOutput> CreateFromObservable(
        Func<TInput, IObservable<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : new RxCommand<TInput, TOutput>(
                async (p, ct) =>
                {
                    IObservable<TOutput> sys = execute(p);
                    Observable<TOutput> r3 = sys.ToObservable();
                    return await r3.FirstAsync(ct);
                }, canExecute, outputScheduler);
    }

    public static RxCommand<TInput, TOutput> CreateFromR3Observable(
        Func<TInput, Observable<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : new RxCommand<TInput, TOutput>(async (p, ct) => await execute(p).FirstAsync(ct), canExecute, outputScheduler);
    }

    public static RxCommand<TInput, TOutput[]> CreateCombined(params RxCommand<TInput, TOutput>[] childCommands)
    {
        if (childCommands == null || childCommands.Length == 0)
        {
            throw new ArgumentException("At least one child command is required", nameof(childCommands));
        }

        Observable<bool> combinedCanExecute = childCommands.Select(c => c.CanExecute).CombineLatestValuesAreAllTrue();
        return new RxCommand<TInput, TOutput[]>(
            async (p, ct) =>
            {
                Task<TOutput>[] tasks = childCommands.Select(c => c.Execute(p).FirstAsync(ct)).ToArray();
                return await Task.WhenAll(tasks);
            }, combinedCanExecute, null);
    }

    public static RxCommand<TInput, TOutput> CreateRunInBackground(
        Func<TInput, TOutput> execute,
        Observable<bool>? canExecute = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : new RxCommand<TInput, TOutput>(async (p, ct) => await Task.Run(() => execute(p), ct), canExecute, TimeProvider.System);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _disposables.Dispose();
        _executionResults.Dispose();
        _thrownExceptions.Dispose();
        _isExecuting.Dispose();
    }
}

public static class RxCommand
{
    public static RxCommand<Unit, Unit> Create(
        Action execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : RxCommand<Unit, Unit>.Create(
                _ =>
                {
                    execute();
                    return Unit.Default;
                }, canExecute, outputScheduler);
    }

    public static RxCommand<Unit, Unit> CreateFromTask(
        Func<CancellationToken, Task> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : RxCommand<Unit, Unit>.CreateFromTask(
                async (_, ct) =>
                {
                    await execute(ct);
                    return Unit.Default;
                }, canExecute, outputScheduler);
    }

    public static RxCommand<Unit, Unit> CreateFromTask(
        Func<Task> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        return execute == null
            ? throw new ArgumentNullException(nameof(execute))
            : RxCommand<Unit, Unit>.CreateFromTask(
                async (_, _) =>
                {
                    await execute();
                    return Unit.Default;
                }, canExecute, outputScheduler);
    }
}

public static class ReactiveCommandExtensions
{
    public static IDisposable InvokeCommand<TInput, TOutput>(
        this IObservable<TInput> source,
        RxCommand<TInput, TOutput> command)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        return source.ToObservable()
            .WithLatestFrom(command.CanExecute, (value, can) => (value, can))
            .Where(x => x.can)
            .Subscribe(x => command.Execute(x.value).Subscribe(_ => { }));
    }

    public static IDisposable InvokeCommand<T>(
        this IObservable<T> source,
        RxCommand<Unit, Unit> command)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        return source.ToObservable()
            .WithLatestFrom(command.CanExecute, (value, can) => (value, can))
            .Where(x => x.can)
            .Subscribe(_ => command.Execute().Subscribe(_ => { }));
    }
}
