using System;
using System.Windows.Input;
using R3;

namespace R3Ext;

/// <summary>
/// Additional convenience extensions for piping observables into <see cref="RxCommand{TInput,TOutput}"/> or any <see cref="ICommand"/>.
/// Inspired by ReactiveUI's ReactiveCommandMixins but adapted to R3 primitives.
/// </summary>
public static class RxCommandMixins
{
    /// <summary>
    /// Pipes each element of the source observable into a plain <see cref="ICommand"/> after checking <c>CanExecute</c>.
    /// </summary>
    public static IDisposable InvokeCommand<T>(this Observable<T> source, ICommand command)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));
        return source
            .Where(v => command.CanExecute(v))
            .Subscribe(v => command.Execute(v));
    }

    /// <summary>
    /// Pipes each element into a parameterless RxCommand (Unit parameter) gated by <c>CanExecute</c>.
    /// </summary>
    public static IDisposable InvokeCommand<T>(this Observable<T> source, RxCommand<Unit, Unit> command)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));
        return source
            .WithLatestFrom(command.CanExecute, (v, can) => can)
            .Where(can => can)
            .Subscribe(_ => command.Execute().Subscribe(_ => { }));
    }

    /// <summary>
    /// Pipes each element into a RxCommand with same input type, ignoring results.
    /// </summary>
    public static IDisposable InvokeCommand<TInput, TOutput>(this Observable<TInput> source, RxCommand<TInput, TOutput> command)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));
        return source
            .WithLatestFrom(command.CanExecute, (v, can) => (v, can))
            .Where(x => x.can)
            .Subscribe(x => command.Execute(x.v).Subscribe(_ => { }, _ => { }));
    }

    /// <summary>
    /// Pipes each element (after projection) into a RxCommand, ignoring results.
    /// </summary>
    public static IDisposable InvokeCommand<TSource, TInput, TOutput>(
        this Observable<TSource> source,
        RxCommand<TInput, TOutput> command,
        Func<TSource, TInput> selector)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        return source
            .Select(selector)
            .WithLatestFrom(command.CanExecute, (param, can) => (param, can))
            .Where(x => x.can)
            .Subscribe(x => command.Execute(x.param).Subscribe(_ => { }, _ => { }));
    }

    /// <summary>
    /// System.IObservable bridge overload (projection variant).
    /// </summary>
    public static IDisposable InvokeCommand<TSource, TInput, TOutput>(
        this IObservable<TSource> source,
        RxCommand<TInput, TOutput> command,
        Func<TSource, TInput> selector)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        return source.ToObservable().InvokeCommand(command, selector);
    }
}