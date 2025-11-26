// Upgraded Group operator implementation for R3.DynamicData
// Now returns IGroup with IObservableCache children for TreeBuilder compatibility
using System;
using System.Collections.Generic;
using System.Linq;
using R3.DynamicData.Cache.Internal;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extension methods for observable cache change sets.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Groups items in the cache by the specified selector.
    /// Each group maintains its own observable cache with incremental change tracking.
    /// </summary>
    /// <typeparam name="TObject">Object type.</typeparam>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TGroupKey">Group key type.</typeparam>
    /// <param name="source">Upstream cache changes.</param>
    /// <param name="groupSelector">Selector producing a group key.</param>
    /// <returns>Observable change set of groups with observable cache children.</returns>
    public static Observable<IChangeSet<IGroup<TObject, TKey, TGroupKey>, TGroupKey>> GroupOn<TObject, TKey, TGroupKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TGroupKey> groupSelector)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (groupSelector is null)
        {
            throw new ArgumentNullException(nameof(groupSelector));
        }

        return Observable.Create<IChangeSet<IGroup<TObject, TKey, TGroupKey>, TGroupKey>>(
            observer =>
            {
                var groupCache = new Dictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>>();
                var itemCache = new Dictionary<TKey, (TObject Item, TGroupKey GroupKey)>();

                return source.Subscribe(
                    changeSet =>
                    {
                        try
                        {
                            var result = new ChangeSet<IGroup<TObject, TKey, TGroupKey>, TGroupKey>();

                            foreach (var change in changeSet)
                            {
                                var key = change.Key;
                                var current = change.Current;

                                switch (change.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        {
                                            var groupKey = groupSelector(current);

                                            // Check if item was previously in a different group
                                            if (itemCache.TryGetValue(key, out var previous))
                                            {
                                                if (!EqualityComparer<TGroupKey>.Default.Equals(previous.GroupKey, groupKey))
                                                {
                                                    // Remove from old group
                                                    if (groupCache.TryGetValue(previous.GroupKey, out var oldGroup))
                                                    {
                                                        oldGroup.Remove(key);
                                                        if (oldGroup.Count == 0)
                                                        {
                                                            groupCache.Remove(previous.GroupKey);
                                                            oldGroup.Dispose();
                                                            result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(
                                                                ChangeReason.Remove, previous.GroupKey, oldGroup));
                                                        }
                                                    }
                                                }
                                            }

                                            // Get or create target group
                                            if (!groupCache.TryGetValue(groupKey, out var group))
                                            {
                                                group = new ManagedGroup<TObject, TKey, TGroupKey>(groupKey);
                                                groupCache[groupKey] = group;
                                                result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(
                                                    ChangeReason.Add, groupKey, group));
                                            }

                                            // Add/update item in group
                                            group.AddOrUpdate(current, key);
                                            itemCache[key] = (current, groupKey);
                                            break;
                                        }

                                    case ChangeReason.Remove:
                                        {
                                            if (itemCache.TryGetValue(key, out var previous))
                                            {
                                                if (groupCache.TryGetValue(previous.GroupKey, out var group))
                                                {
                                                    group.Remove(key);
                                                    if (group.Count == 0)
                                                    {
                                                        groupCache.Remove(previous.GroupKey);
                                                        group.Dispose();
                                                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(
                                                            ChangeReason.Remove, previous.GroupKey, group));
                                                    }
                                                }

                                                itemCache.Remove(key);
                                            }

                                            break;
                                        }

                                    case ChangeReason.Refresh:
                                        {
                                            var newGroupKey = groupSelector(current);

                                            if (itemCache.TryGetValue(key, out var previous))
                                            {
                                                if (!EqualityComparer<TGroupKey>.Default.Equals(previous.GroupKey, newGroupKey))
                                                {
                                                    // Remove from old group
                                                    if (groupCache.TryGetValue(previous.GroupKey, out var oldGroup))
                                                    {
                                                        oldGroup.Remove(key);
                                                        if (oldGroup.Count == 0)
                                                        {
                                                            groupCache.Remove(previous.GroupKey);
                                                            oldGroup.Dispose();
                                                            result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(
                                                                ChangeReason.Remove, previous.GroupKey, oldGroup));
                                                        }
                                                    }

                                                    // Add to new group
                                                    if (!groupCache.TryGetValue(newGroupKey, out var newGroup))
                                                    {
                                                        newGroup = new ManagedGroup<TObject, TKey, TGroupKey>(newGroupKey);
                                                        groupCache[newGroupKey] = newGroup;
                                                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(
                                                            ChangeReason.Add, newGroupKey, newGroup));
                                                    }

                                                    newGroup.AddOrUpdate(current, key);
                                                    itemCache[key] = (current, newGroupKey);
                                                }
                                                else
                                                {
                                                    // Same group - just refresh
                                                    if (groupCache.TryGetValue(previous.GroupKey, out var group))
                                                    {
                                                        group.Refresh(key);
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                }
                            }

                            if (result.Count > 0)
                            {
                                observer.OnNext(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            observer.OnErrorResume(ex);
                        }
                    }, observer.OnErrorResume, observer.OnCompleted);
            });
    }
}
