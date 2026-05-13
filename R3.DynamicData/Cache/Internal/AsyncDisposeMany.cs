// Port of DynamicData to R3.
namespace R3.DynamicData.Cache.Internal;

internal sealed class AsyncDisposeMany<TObject, TKey>
    where TObject : notnull, IAsyncDisposable
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;

    public AsyncDisposeMany(Observable<IChangeSet<TObject, TKey>> source)
    {
        _source = source;
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>, Observable<IChangeSet<TObject, TKey>>>(
            _source,
            static (observer, source) =>
            {
                var current = new Dictionary<TKey, TObject>();
                var disp = source.Subscribe(
                    changes =>
                    {
                        try
                        {
                            ProcessChanges(current, changes);
                            if (changes.Count > 0)
                            {
                                observer.OnNext(changes);
                            }
                        }
                        catch (Exception ex)
                        {
                            observer.OnErrorResume(ex);
                        }
                    }, observer.OnErrorResume, observer.OnCompleted);

                return Disposable.Combine(
                    disp,
                    Disposable.Create(current, static current =>
                    {
                        foreach (var item in current.Values)
                        {
                            SafeAsyncDispose(item);
                        }

                        current.Clear();
                    }));
            });
    }

    private static void ProcessChanges(Dictionary<TKey, TObject> current, IChangeSet<TObject, TKey> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case Kernel.ChangeReason.Add:
                    current[change.Key] = change.Current;
                    break;

                case Kernel.ChangeReason.Update:
                    if (current.TryGetValue(change.Key, out var old))
                    {
                        SafeAsyncDispose(old);
                    }

                    current[change.Key] = change.Current;
                    break;

                case Kernel.ChangeReason.Remove:
                    if (current.TryGetValue(change.Key, out var removed))
                    {
                        SafeAsyncDispose(removed);
                        current.Remove(change.Key);
                    }

                    break;

                case Kernel.ChangeReason.Refresh:
                    // No disposal; item unchanged.
                    break;
            }
        }
    }

    private static void SafeAsyncDispose(TObject item)
    {
        try
        {
            _ = item.DisposeAsync().AsTask();
        }
        catch
        {
            // Swallow disposal exceptions.
        }
    }
}
