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
        var state = new DisposeManyState(_source, _disposeAction);
        return Observable.Create<IChangeSet<TObject, TKey>, DisposeManyState>(
            state,
            static (observer, state) =>
        {
            var current = new Dictionary<TKey, TObject>();
            var disp = state.Source.Subscribe(changes =>
            {
                try
                {
                    state.ProcessChanges(current, changes);
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
                Disposable.Create((current, state), static state =>
                {
                    foreach (var item in state.current.Values)
                    {
                        DisposeManyState.SafeDispose(item, state.state.DisposeAction);
                    }
                    state.current.Clear();
                }));
        });
    }

    private readonly struct DisposeManyState
    {
        public readonly Observable<IChangeSet<TObject, TKey>> Source;
        public readonly Action<TObject>? DisposeAction;

        public DisposeManyState(Observable<IChangeSet<TObject, TKey>> source, Action<TObject>? disposeAction)
        {
            Source = source;
            DisposeAction = disposeAction;
        }

        public void ProcessChanges(Dictionary<TKey, TObject> current, IChangeSet<TObject, TKey> changes)
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
                            SafeDispose(old, DisposeAction);
                        }
                        current[change.Key] = change.Current;
                        break;
                    case Kernel.ChangeReason.Remove:
                        if (current.TryGetValue(change.Key, out var removed))
                        {
                            SafeDispose(removed, DisposeAction);
                            current.Remove(change.Key);
                        }
                        break;
                    case Kernel.ChangeReason.Refresh:
                        // No disposal; item unchanged.
                        break;
                }
            }
        }

        public static void SafeDispose(TObject item, Action<TObject>? disposeAction)
        {
            try
            {
                if (disposeAction != null)
                {
                    disposeAction(item);
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
}
