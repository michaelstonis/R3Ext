// Port of DynamicData to R3.

// Style suppression pragmas for internal operator.
#pragma warning disable SA1116, SA1513, SA1516, SA1503, SA1127, SA1210
namespace R3.DynamicData.Cache.Internal;

internal sealed class DisposeMany<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Action<TObject>? _disposeAction;

    public DisposeMany(Observable<IChangeSet<TObject, TKey>> source, Action<TObject>? disposeAction = null)
    {
        _source = source;
        _disposeAction = disposeAction;
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var current = new Dictionary<TKey, TObject>();
            var disp = _source.Subscribe(changes =>
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

            return Disposable.Create(() =>
            {
                foreach (var item in current.Values)
                {
                    SafeDispose(item);
                }
                current.Clear();
                disp.Dispose();
            });
        });
    }

    private void ProcessChanges(Dictionary<TKey, TObject> current, IChangeSet<TObject, TKey> changes)
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
                        SafeDispose(old);
                    }
                    current[change.Key] = change.Current;
                    break;
                case Kernel.ChangeReason.Remove:
                    if (current.TryGetValue(change.Key, out var removed))
                    {
                        SafeDispose(removed);
                        current.Remove(change.Key);
                    }
                    break;
                case Kernel.ChangeReason.Refresh:
                    // No disposal; item unchanged.
                    break;
            }
        }
    }

    private void SafeDispose(TObject item)
    {
        try
        {
            if (_disposeAction != null)
            {
                _disposeAction(item);
                return;
            }
            if (item is IDisposable d)
            {
                d.Dispose();
            }
        }
        catch
        {
            // Swallow disposal exceptions.
        }
    }
}
