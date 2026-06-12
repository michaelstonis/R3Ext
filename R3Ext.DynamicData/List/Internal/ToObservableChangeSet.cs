// Port of DynamicData to R3.
// DD #1027 audit: ToObservableChangeSet uses Observable.Timer for item expirations;
// each timer subscription is stored in `expirations` dict. The Disposable.Create cleanup
// iterates and disposes all entries in expirations, then calls subscription.Dispose().
// No scheduler-held reference can outlive the operator subscription. SAFE.

using System;
using System.Collections.Generic;
using System.Linq;
using R3;

namespace R3Ext.DynamicData.List.Internal;

internal sealed class ToObservableChangeSet<TObject>
    where TObject : notnull
{
    public static Observable<IChangeSet<TObject>> Create(
        Observable<TObject> source,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var state = new ToObservableChangeSetState<TObject>(source, expireAfter, limitSizeTo);
        return Observable.Create<IChangeSet<TObject>, ToObservableChangeSetState<TObject>>(
            state,
            static (observer, state) =>
            {
                return CreateFromEnumerable(
                    state.Source.Select(state.Buffer, static (item, buf) =>
                    {
                        buf[0] = item;
                        return (IEnumerable<TObject>)buf;
                    }),
                    state.ExpireAfter,
                    state.LimitSizeTo).Subscribe(observer);
            });
    }

    private readonly struct ToObservableChangeSetState<T>
        where T : notnull
    {
        public readonly Observable<T> Source;
        public readonly Func<T, TimeSpan?>? ExpireAfter;
        public readonly int LimitSizeTo;
        public readonly T[] Buffer;

        public ToObservableChangeSetState(Observable<T> source, Func<T, TimeSpan?>? expireAfter, int limitSizeTo)
        {
            Source = source;
            ExpireAfter = expireAfter;
            LimitSizeTo = limitSizeTo;
            Buffer = new T[1];
        }
    }

    public static Observable<IChangeSet<TObject>> CreateFromEnumerable(
        Observable<IEnumerable<TObject>> source,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<IChangeSet<TObject>>(observer =>
        {
            var list = new List<TObject>();
            var expirations = new Dictionary<TObject, IDisposable>();
            var gate = new object();

            void RemoveExpired(TObject item)
            {
                IChangeSet<TObject>? changeSet = null;
                lock (gate)
                {
                    var index = list.IndexOf(item);
                    if (index < 0)
                    {
                        return;
                    }

                    list.RemoveAt(index);
                    changeSet = new ChangeSet<TObject>(new[] { new Change<TObject>(ListChangeReason.Remove, item, index) });
                    expirations.Remove(item, out _);
                }

                observer.OnNext(changeSet);
            }

            void EnforceLimit(List<Change<TObject>> removalChanges)
            {
                // Must be called while holding gate lock
                while (limitSizeTo > 0 && list.Count > limitSizeTo)
                {
                    var item = list[0];
                    list.RemoveAt(0);
                    removalChanges.Add(new Change<TObject>(ListChangeReason.Remove, item, 0));
                    if (expirations.Remove(item, out var disposable))
                    {
                        disposable.Dispose();
                    }
                }
            }

            var subscription = source.Subscribe(
                items =>
                {
                    List<Change<TObject>> addChanges;
                    List<Change<TObject>>? removalChanges = null;
                    List<(TObject item, TimeSpan expiry)>? pendingTimers = null;

                    lock (gate)
                    {
                        addChanges = new List<Change<TObject>>();

                        foreach (var item in items)
                        {
                            var index = list.Count;
                            list.Add(item);
                            addChanges.Add(new Change<TObject>(ListChangeReason.Add, item, index));

                            if (expireAfter != null)
                            {
                                var expiry = expireAfter(item);
                                if (expiry.HasValue)
                                {
                                    pendingTimers ??= new List<(TObject, TimeSpan)>();
                                    pendingTimers.Add((item, expiry.Value));
                                }
                            }
                        }

                        if (limitSizeTo > 0 && list.Count > limitSizeTo)
                        {
                            removalChanges = new List<Change<TObject>>();
                            EnforceLimit(removalChanges);

                            // Remove timers for items evicted by EnforceLimit (they are no longer in the list).
                            // Use a HashSet for O(1) lookups when pendingTimers and list are large.
                            if (pendingTimers != null)
                            {
                                var listSet = new HashSet<TObject>(list);
                                pendingTimers.RemoveAll(pt => !listSet.Contains(pt.item));
                            }
                        }
                    }

                    if (addChanges.Count > 0)
                    {
                        observer.OnNext(new ChangeSet<TObject>(addChanges));
                    }

                    if (removalChanges is { Count: > 0 })
                    {
                        observer.OnNext(new ChangeSet<TObject>(removalChanges));
                    }

                    if (pendingTimers != null)
                    {
                        foreach (var (item, expiry) in pendingTimers)
                        {
                            // Subscribe the timer OUTSIDE the lock to prevent a deadlock if
                            // Observable.Timer can invoke the callback synchronously (e.g., zero
                            // expiry on a test scheduler).  After subscribing, acquire the lock
                            // briefly to register the subscription; if a concurrent path already
                            // registered one for this item, dispose the duplicate.
                            var capturedItem = item;
                            var timerSubscription = Observable.Timer(expiry).Subscribe(_ => RemoveExpired(capturedItem));
                            lock (gate)
                            {
                                if (expirations.ContainsKey(capturedItem))
                                {
                                    timerSubscription.Dispose();
                                }
                                else
                                {
                                    expirations[capturedItem] = timerSubscription;
                                }
                            }
                        }
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                lock (gate)
                {
                    foreach (var disposable in expirations.Values)
                    {
                        disposable.Dispose();
                    }

                    expirations.Clear();
                }
            });
        });
    }
}
