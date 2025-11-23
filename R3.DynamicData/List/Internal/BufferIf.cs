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

    public Observable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>, BufferIfState<T>>(
        new BufferIfState<T>(_source, _pauseIfTrueSelector, _timeOut, initialPauseState),
        static (observer, state) =>
        {
            var bufferSelector = Observable.Return(state.InitialPauseState)
                .Concat(state.PauseIfTrueSelector.Merge(state.TimeoutSubject))
                .Publish();

            var pause = bufferSelector.Where(s => s).Subscribe(
                _ =>
                {
                    state.Paused = true;

                    // add pause timeout if required
                    if (state.TimeOut != TimeSpan.Zero)
                    {
                        state.TimeoutSubscriber.Disposable = Observable.Timer(state.TimeOut)
                            .Subscribe(_ => state.TimeoutSubject.OnNext(false));
                    }
                });

            var resume = bufferSelector.Where(s => !s).Subscribe(
                _ =>
                {
                    state.Paused = false;

                    // publish changes and clear buffer
                    if (state.Buffer.Count == 0)
                    {
                        return;
                    }

                    observer.OnNext(state.Buffer);
                    state.Buffer = new ChangeSet<T>();

                    // kill off timeout if required
                    state.TimeoutSubscriber.Disposable = Disposable.Empty;
                });

            var updateSubscriber = state.Source.Subscribe(
                updates =>
                {
                    if (state.Paused)
                    {
                        state.Buffer.AddRange(updates);
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
                state.Dispose();
            });
        });

    private sealed class BufferIfState<TItem> : IDisposable
        where TItem : notnull
    {
        public readonly Observable<IChangeSet<TItem>> Source;
        public readonly Observable<bool> PauseIfTrueSelector;
        public readonly TimeSpan TimeOut;
        public readonly bool InitialPauseState;
        public readonly Subject<bool> TimeoutSubject = new();
        public readonly SerialDisposable TimeoutSubscriber = new();

        public bool Paused;
        public ChangeSet<TItem> Buffer;

        public BufferIfState(
            Observable<IChangeSet<TItem>> source,
            Observable<bool> pauseIfTrueSelector,
            TimeSpan timeOut,
            bool initialPauseState)
        {
            Source = source;
            PauseIfTrueSelector = pauseIfTrueSelector;
            TimeOut = timeOut;
            InitialPauseState = initialPauseState;
            Paused = initialPauseState;
            Buffer = new ChangeSet<TItem>();
        }

        public void Dispose()
        {
            TimeoutSubject.OnCompleted();
            TimeoutSubject.Dispose();
            TimeoutSubscriber.Dispose();
        }
    }
}
