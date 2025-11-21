// Initial minimal grouping implementation (Partial) for DynamicData port.
// Emits a full logical reset of group list (Clear + Add for all groups) upon any upstream change.
// This is intentionally naive and will be optimized to emit diffs only.
#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable SA1116 // Parameters should begin on new line when spanning multiple lines
#pragma warning disable SA1501 // Statement should not be on a single line
#pragma warning disable SA1107 // Multiple statements on one line
#pragma warning disable SA1210 // Using directives should be ordered alphabetically

using System.Collections.ObjectModel;
using R3.DynamicData.List;
using R3.DynamicData.Kernel;

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
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TGroupKey> groupSelector)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (groupSelector is null) throw new ArgumentNullException(nameof(groupSelector));

        return Observable.Create<IChangeSet<Group<TObject, TGroupKey>>>(observer =>
        {
            var groups = new Dictionary<TGroupKey, List<TObject>>();
            var previousSnapshot = new List<Group<TObject, TGroupKey>>();

            return source.Subscribe(changeSet =>
            {
                try
                {
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

                    var currentSnapshot = groups.Select(kvp => new Group<TObject, TGroupKey>(kvp.Key, new ReadOnlyCollection<TObject>(kvp.Value.ToList()))).ToList();
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
    }
}

/// <summary>
/// Represents a grouping of items by a key. Minimal form.
/// </summary>
/// <typeparam name="TObject">Item type.</typeparam>
/// <typeparam name="TGroupKey">Group key type.</typeparam>
public readonly record struct Group<TObject, TGroupKey>(TGroupKey Key, IReadOnlyList<TObject> Items);
