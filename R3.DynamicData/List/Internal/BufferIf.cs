// Port of DynamicData to R3.

using System;
using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class BufferIf<T>(Observable<IChangeSet<T>> source, Observable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null)
    where T : notnull
{
    private readonly Observable<bool> _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));
    private readonly Observable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly TimeSpan _timeOut = timeOut ?? TimeSpan.Zero;

    public Observable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
        observer =>
        {
            var paused = initialPauseState;
            var buffer = new ChangeSet<T>();
            Subject<bool> timeoutSubject = new();
            SerialDisposable timeoutSubscriber = new();

            var bufferSelector = Observable.Return(initialPauseState)
                .Concat(_pauseIfTrueSelector.Merge(timeoutSubject))
                .Publish();

            var pause = bufferSelector.Where(state => state).Subscribe(
                _ =>
                {
                    paused = true;

                    // add pause timeout if required
                    if (_timeOut != TimeSpan.Zero)
                    {
                        timeoutSubscriber.Disposable = Observable.Timer(_timeOut)
                            .Subscribe(_ => timeoutSubject.OnNext(false));
                    }
                });

            var resume = bufferSelector.Where(state => !state).Subscribe(
                _ =>
                {
                    paused = false;

                    // publish changes and clear buffer
                    if (buffer.Count == 0)
                    {
                        return;
                    }

                    observer.OnNext(buffer);
                    buffer = new ChangeSet<T>();

                    // kill off timeout if required
                    timeoutSubscriber.Disposable = Disposable.Empty;
                });

            var updateSubscriber = _source.Subscribe(
                updates =>
                {
                    if (paused)
                    {
                        buffer.AddRange(updates);
                    }
                    else
                    {
                        observer.OnNext(updates);
                    }
                });

            var connected = bufferSelector.Connect();

            return Disposable.Create(() =>
            {
                connected.Dispose();
                pause.Dispose();
                resume.Dispose();
                updateSubscriber.Dispose();
                timeoutSubject.OnCompleted();
                timeoutSubject.Dispose();
                timeoutSubscriber.Dispose();
            });
        });
}
