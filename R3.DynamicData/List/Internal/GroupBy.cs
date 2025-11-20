// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class GroupBy<T, TKey>
    where TKey : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Func<T, TKey> _keySelector;
    private readonly IEqualityComparer<TKey> _keyComparer;

    public GroupBy(Observable<IChangeSet<T>> source, Func<T, TKey> keySelector, IEqualityComparer<TKey>? keyComparer = null)
    {
        _source = source;
        _keySelector = keySelector;
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
    }

    public Observable<IChangeSet<Group<TKey, T>>> Run()
    {
        return Observable.Create<IChangeSet<Group<TKey, T>>>(observer =>
        {
            var groupsIndex = new Dictionary<TKey, Group<TKey, T>>(_keyComparer);
            var groupsList = new ChangeAwareList<Group<TKey, T>>();

            var subscription = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        Process(groupsIndex, groupsList, changes);
                        var output = groupsList.CaptureChanges();
                        if (output.Count > 0)
                        {
                            observer.OnNext(output);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return subscription;
        });
    }

    private void Process(Dictionary<TKey, Group<TKey, T>> index, ChangeAwareList<Group<TKey, T>> groups, IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    AddToGroup(index, groups, change.Item);
                    break;
                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        foreach (var item in change.Range)
                        {
                            AddToGroup(index, groups, item);
                        }
                    }
                    else
                    {
                        AddToGroup(index, groups, change.Item);
                    }

                    break;
                case ListChangeReason.Remove:
                    RemoveFromGroup(index, groups, change.Item);
                    break;
                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        foreach (var item in change.Range)
                        {
                            RemoveFromGroup(index, groups, item);
                        }
                    }
                    else
                    {
                        RemoveFromGroup(index, groups, change.Item);
                    }

                    break;
                case ListChangeReason.Replace:
                    if (change.PreviousItem != null)
                    {
                        var prevKey = _keySelector(change.PreviousItem);
                        var nextKey = _keySelector(change.Item);
                        if (_keyComparer.Equals(prevKey, nextKey))
                        {
                            // In-place update within group => mark group refreshed
                            if (index.TryGetValue(nextKey, out var grp))
                            {
                                var gIdx = groups.IndexOf(grp);
                                if (gIdx >= 0)
                                {
                                    // Use Replace to trigger update signal on group
                                    groups[gIdx] = grp;
                                }
                            }
                        }
                        else
                        {
                            RemoveFromGroup(index, groups, change.PreviousItem);
                            AddToGroup(index, groups, change.Item);
                        }
                    }
                    else
                    {
                        AddToGroup(index, groups, change.Item);
                    }

                    break;
                case ListChangeReason.Moved:
                    // Order within groups not maintained; no-op at group level
                    break;
                case ListChangeReason.Clear:
                    index.Clear();
                    groups.Clear();
                    break;
                case ListChangeReason.Refresh:
                    // Re-evaluate key, move group if changed
                    var oldKey = _keySelector(change.Item);

                    // Can't know previous key; treat as potential move: ensure membership
                    RemoveFromGroup(index, groups, change.Item);
                    AddToGroup(index, groups, change.Item);
                    break;
            }
        }
    }

    private void AddToGroup(Dictionary<TKey, Group<TKey, T>> index, ChangeAwareList<Group<TKey, T>> groups, T item)
    {
        var key = _keySelector(item);
        if (!index.TryGetValue(key, out var group))
        {
            group = new Group<TKey, T>(key);
            index[key] = group;
            groups.Add(group);
        }

        group.Items.Add(item);

        // mark group as refreshed to signal inner change
        var gIdx = groups.IndexOf(group);
        if (gIdx >= 0)
        {
            groups[gIdx] = group;
        }
    }

    private void RemoveFromGroup(Dictionary<TKey, Group<TKey, T>> index, ChangeAwareList<Group<TKey, T>> groups, T item)
    {
        var key = _keySelector(item);
        if (!index.TryGetValue(key, out var group))
        {
            return;
        }

        var itemIdx = group.Items.IndexOf(item);
        if (itemIdx >= 0)
        {
            group.Items.RemoveAt(itemIdx);
        }

        if (group.Items.Count == 0)
        {
            index.Remove(key);
            var idx = groups.IndexOf(group);
            if (idx >= 0)
            {
                groups.RemoveAt(idx);
            }
        }
        else
        {
            var gIdx = groups.IndexOf(group);
            if (gIdx >= 0)
            {
                groups[gIdx] = group;
            }
        }
    }
}
