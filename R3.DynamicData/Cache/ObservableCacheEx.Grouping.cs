// Initial minimal grouping implementation (Partial) for DynamicData port.
// Emits a full logical reset of group list (Clear + Add for all groups) upon any upstream change.
// This is intentionally naive and will be optimized to emit diffs only.

using System.Collections.ObjectModel;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Groups items in the cache by the specified selector. Minimal implementation: on each upstream change
    /// recomputes all groups and emits a reset (Clear + Add for each group). Marked Partial in migration matrix.
    /// </summary>
    /// <typeparam name="TObject">Object type.</typeparam>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TGroupKey">Group key type.</typeparam>
    /// <param name="source">Upstream cache changes.</param>
    /// <param name="groupSelector">Selector producing a group key.</param>
    /// <returns>Observable change set of groups.</returns>
    public static Observable<IChangeSet<Group<TObject, TGroupKey>>> GroupOn<TObject, TKey, TGroupKey>(
        return Observable.Create<IChangeSet<Group<TObject, TGroupKey>>>(observer =>
        {
            // State: current groups keyed by group key.
            var groups = new Dictionary<TGroupKey, List<TObject>>();
            var previousSnapshot = new List<Group<TObject, TGroupKey>>();

            return source.Subscribe(changeSet =>
            {
                try
                {
                    // Apply changes to group membership.
                    foreach (var change in changeSet)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                var obj = change.Current;
                                var gk = groupSelector(obj);
                                if (!groups.TryGetValue(gk, out var list))
                                {
                                    list = new List<TObject>();
                                    groups[gk] = list;
                                }
                                if (change.Reason == ChangeReason.Update && change.Previous.HasValue)
                                {
                                    var prevObj = change.Previous.Value;
                                    var prevKey = groupSelector(prevObj);
                                    if (!EqualityComparer<TGroupKey>.Default.Equals(prevKey, gk) && groups.TryGetValue(prevKey, out var prevList))
                                    {
                                        prevList.Remove(prevObj);
                                        if (prevList.Count == 0) groups.Remove(prevKey);
                                    }
                                }
                                if (!list.Contains(obj)) list.Add(obj);
                                break;
                            case ChangeReason.Remove:
                                var remObj = change.Current;
                                var remKey = groupSelector(remObj);
                                if (groups.TryGetValue(remKey, out var remList))
                                {
                                    remList.Remove(remObj);
                                    if (remList.Count == 0) groups.Remove(remKey);
                                }
                                break;
                            case ChangeReason.Refresh:
                                var refObj = change.Current;
                                TGroupKey? oldKey = default;
                                foreach (var kvp in groups)
                                {
                                    if (kvp.Value.Contains(refObj)) { oldKey = kvp.Key; break; }
                                }
                                var newKey = groupSelector(refObj);
                                if (oldKey != null && !EqualityComparer<TGroupKey>.Default.Equals(oldKey, newKey))
                                {
                                    if (groups.TryGetValue(oldKey!, out var oldList))
                                    {
                                        oldList.Remove(refObj);
                                        if (oldList.Count == 0) groups.Remove(oldKey!);
                                    }
                                    if (!groups.TryGetValue(newKey, out var newList))
                                    {
                                        newList = new List<TObject>();
                                        groups[newKey] = newList;
                                    }
                                    newList.Add(refObj);
                                }
                                break;
                        }
                    }

                    // Build current snapshot.
                    var currentSnapshot = groups.Select(kvp => new Group<TObject, TGroupKey>(kvp.Key, new ReadOnlyCollection<TObject>(kvp.Value.ToList()))).ToList();

                    // Emit logical reset: Clear with previous snapshot then Add for each current group.
                    var changes = new List<Change<Group<TObject, TGroupKey>>>();
                    if (previousSnapshot.Count > 0)
                    {
                        changes.Add(new Change<Group<TObject, TGroupKey>>(ListChangeReason.Clear, previousSnapshot, 0));
                    }
                    foreach (var g in currentSnapshot)
                    {
                        changes.Add(new Change<Group<TObject, TGroupKey>>(ListChangeReason.Add, g, -1));
                    }
                    previousSnapshot = currentSnapshot;
                    observer.OnNext(new ChangeSet<Group<TObject, TGroupKey>>(changes));
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }
}

/// <summary>
/// Represents a grouping of items by a key. Minimal form.
/// </summary>
/// <typeparam name="TObject">Item type.</typeparam>
/// <typeparam name="TGroupKey">Group key type.</typeparam>
public readonly record struct Group<TObject, TGroupKey>(TGroupKey Key, IReadOnlyList<TObject> Items);
