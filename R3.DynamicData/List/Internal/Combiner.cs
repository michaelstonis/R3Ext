using R3;

namespace R3.DynamicData.List.Internal;

internal enum CombineOperator
{
    And,
    Or,
    Except,
    Xor,
}

internal sealed class Combiner<T>
    where T : notnull
{
    private readonly IEnumerable<Observable<IChangeSet<T>>> _sources;
    private readonly CombineOperator _type;
    private readonly IEqualityComparer<T> _comparer;

    public Combiner(
        IEnumerable<Observable<IChangeSet<T>>> sources,
        CombineOperator type,
        IEqualityComparer<T>? comparer = null)
    {
        _sources = sources;
        _type = type;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var sourceLists = _sources.Select(_ => new List<T>()).ToList();
            var subscriptions = new List<IDisposable>();

            for (int i = 0; i < _sources.Count(); i++)
            {
                var index = i;
                var subscription = _sources.ElementAt(i).Subscribe(
                    changes =>
                    {
                        ApplyChanges(sourceLists[index], changes);
                        var result = CalculateCombinedResult(sourceLists);
                        observer.OnNext(result);
                    },
                    observer.OnErrorResume,
                    observer.OnCompleted);

                subscriptions.Add(subscription);
            }

            return R3.Disposable.Create(() =>
            {
                foreach (var sub in subscriptions)
                {
                    sub.Dispose();
                }
            });
        });
    }

    private void ApplyChanges(List<T> list, IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    list.Insert(change.CurrentIndex, change.Item);
                    break;

                case ListChangeReason.AddRange:
                    list.InsertRange(change.CurrentIndex, change.Range);
                    break;

                case ListChangeReason.Remove:
                    list.RemoveAt(change.CurrentIndex);
                    break;

                case ListChangeReason.RemoveRange:
                    for (int i = 0; i < change.Range.Count; i++)
                    {
                        list.RemoveAt(change.CurrentIndex);
                    }

                    break;

                case ListChangeReason.Replace:
                    list[change.CurrentIndex] = change.Item;
                    break;

                case ListChangeReason.Moved:
                    var movedItem = list[change.PreviousIndex];
                    list.RemoveAt(change.PreviousIndex);
                    list.Insert(change.CurrentIndex, movedItem);
                    break;

                case ListChangeReason.Clear:
                    list.Clear();
                    break;
            }
        }
    }

    private IChangeSet<T> CalculateCombinedResult(List<List<T>> sourceLists)
    {
        var resultSet = new HashSet<T>(_comparer);
        var changes = new List<Change<T>>();

        switch (_type)
        {
            case CombineOperator.And:
                // Items in all sources
                if (sourceLists.Count > 0 && sourceLists[0].Count > 0)
                {
                    var candidates = new HashSet<T>(sourceLists[0], _comparer);
                    for (int i = 1; i < sourceLists.Count; i++)
                    {
                        candidates.IntersectWith(sourceLists[i]);
                    }

                    resultSet = candidates;
                }

                break;

            case CombineOperator.Or:
                // Items in any source
                foreach (var list in sourceLists)
                {
                    resultSet.UnionWith(list);
                }

                break;

            case CombineOperator.Except:
                // Items in first source but not in others
                if (sourceLists.Count > 0)
                {
                    resultSet = new HashSet<T>(sourceLists[0], _comparer);
                    for (int i = 1; i < sourceLists.Count; i++)
                    {
                        resultSet.ExceptWith(sourceLists[i]);
                    }
                }

                break;

            case CombineOperator.Xor:
                // Items in exactly one source - need to check which sources contain each item
                var itemToSources = new Dictionary<T, HashSet<int>>(_comparer);
                for (int sourceIndex = 0; sourceIndex < sourceLists.Count; sourceIndex++)
                {
                    var uniqueInSource = new HashSet<T>(sourceLists[sourceIndex], _comparer);
                    foreach (var item in uniqueInSource)
                    {
                        if (!itemToSources.ContainsKey(item))
                        {
                            itemToSources[item] = new HashSet<int>();
                        }

                        itemToSources[item].Add(sourceIndex);
                    }
                }

                // XOR: include items that appear in exactly one source
                foreach (var kvp in itemToSources.Where(kvp => kvp.Value.Count == 1))
                {
                    resultSet.Add(kvp.Key);
                }

                break;
        }

        // Emit Clear followed by AddRange to ensure clean state
        changes.Add(new Change<T>(ListChangeReason.Clear, Array.Empty<T>(), 0));
        if (resultSet.Count > 0)
        {
            changes.Add(new Change<T>(ListChangeReason.AddRange, resultSet, 0));
        }

        return new ChangeSet<T>(changes);
    }
}
