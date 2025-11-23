// Port of DynamicData to R3.

using System;
using System.Collections.Generic;
using System.Linq;
using R3;

namespace R3.DynamicData.List.Internal;

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

            void RemoveExpired(TObject item)
            {
                var index = list.IndexOf(item);
                if (index >= 0)
                {
                    list.RemoveAt(index);
                    observer.OnNext(new ChangeSet<TObject>(new[] { new Change<TObject>(ListChangeReason.Remove, item, index) }));
                }

                if (expirations.Remove(item, out var disposable))
                {
                    disposable.Dispose();
                }
            }

            void EnforceLimit()
            {
                while (limitSizeTo > 0 && list.Count > limitSizeTo)
                {
                    var item = list[0];
                    list.RemoveAt(0);
                    observer.OnNext(new ChangeSet<TObject>(new[] { new Change<TObject>(ListChangeReason.Remove, item, 0) }));

                    if (expirations.Remove(item, out var disposable))
                    {
                        disposable.Dispose();
                    }
                }
            }

            var subscription = source.Subscribe(
                items =>
                {
                    var changes = new List<Change<TObject>>();

                    foreach (var item in items)
                    {
                        var index = list.Count;
                        list.Add(item);
                        changes.Add(new Change<TObject>(ListChangeReason.Add, item, index));

                        // Setup expiration if needed
                        if (expireAfter != null)
                        {
                            var expiry = expireAfter(item);
                            if (expiry.HasValue)
                            {
                                var timer = Observable.Timer(expiry.Value).Subscribe(_ => RemoveExpired(item));
                                expirations[item] = timer;
                            }
                        }
                    }

                    if (changes.Count > 0)
                    {
                        observer.OnNext(new ChangeSet<TObject>(changes));
                        EnforceLimit();
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                foreach (var disposable in expirations.Values)
                {
                    disposable.Dispose();
                }

                expirations.Clear();
            });
        });
    }
}
