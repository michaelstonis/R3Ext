// Port of DynamicData to R3.

using System;
using System.Collections.Generic;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Batches the underlying cache changes over the specified time span.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="timeSpan">The time span to batch changes over.</param>
    /// <param name="timeProvider">Optional time provider for testing. Uses ObservableSystem.DefaultTimeProvider if null.</param>
    /// <returns>An observable that emits batched change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        TimeSpan timeSpan,
        TimeProvider? timeProvider = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (timeSpan <= TimeSpan.Zero)
        {
            throw new ArgumentException("Time span must be greater than zero", nameof(timeSpan));
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var buffer = new List<Change<TObject, TKey>>();
            var gate = new object();
            var timeProviderToUse = timeProvider ?? ObservableSystem.DefaultTimeProvider;

            var sourceSubscription = source.Subscribe(
                changes =>
                {
                    lock (gate)
                    {
                        buffer.AddRange(changes);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            var timerSubscription = Observable
                .Interval(timeSpan, timeProviderToUse)
                .Subscribe(_ =>
                {
                    List<Change<TObject, TKey>> toEmit;
                    lock (gate)
                    {
                        if (buffer.Count == 0)
                        {
                            return;
                        }

                        toEmit = new List<Change<TObject, TKey>>(buffer);
                        buffer.Clear();
                    }

                    var changeSet = new ChangeSet<TObject, TKey>(toEmit.Count);
                    changeSet.AddRange(toEmit);
                    observer.OnNext(changeSet);
                });

            return Disposable.Create(() =>
            {
                sourceSubscription.Dispose();
                timerSubscription.Dispose();
            });
        });
    }

    /// <summary>
    /// Batches the underlying cache changes based on a pause/resume signal.
    /// When paused (true), changes are buffered. When resumed (false), buffered changes are emitted.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="pauseIfTrueSelector">Observable that signals when to pause (true) or resume (false).</param>
    /// <param name="initialPauseState">Initial pause state. Default is false (not paused).</param>
    /// <returns>An observable that emits batched change sets based on pause/resume signals.</returns>
    public static Observable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Observable<bool> pauseIfTrueSelector,
        bool initialPauseState = false)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (pauseIfTrueSelector is null)
        {
            throw new ArgumentNullException(nameof(pauseIfTrueSelector));
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var buffer = new List<Change<TObject, TKey>>();
            var isPaused = initialPauseState;
            var gate = new object();

            var pauseSubscription = pauseIfTrueSelector.Subscribe(shouldPause =>
            {
                List<Change<TObject, TKey>>? toEmit = null;
                lock (gate)
                {
                    var wasPaused = isPaused;
                    isPaused = shouldPause;

                    // Emit buffered changes when transitioning from paused to not paused
                    if (wasPaused && !isPaused && buffer.Count > 0)
                    {
                        toEmit = new List<Change<TObject, TKey>>(buffer);
                        buffer.Clear();
                    }
                }

                if (toEmit != null)
                {
                    var changeSet = new ChangeSet<TObject, TKey>(toEmit.Count);
                    changeSet.AddRange(toEmit);
                    observer.OnNext(changeSet);
                }
            });

            var sourceSubscription = source.Subscribe(
                changes =>
                {
                    lock (gate)
                    {
                        if (isPaused)
                        {
                            buffer.AddRange(changes);
                        }
                        else
                        {
                            observer.OnNext(changes);
                        }
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                pauseSubscription.Dispose();
                sourceSubscription.Dispose();
            });
        });
    }
}
