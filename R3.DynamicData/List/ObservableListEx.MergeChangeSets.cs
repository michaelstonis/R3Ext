using System;
using System.Collections.Generic;
using System.Linq;

namespace R3.DynamicData.List;

/// <summary>
/// Extension methods for observable list change sets.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Merges (unions) multiple list change set streams producing a set-like union of items.
    /// Emits Add when an item first appears in any source. Emits Remove only when the item
    /// is absent from all sources. Order of the resulting list is the order of first appearance.
    /// </summary>
    /// <typeparam name="T">The type of items in the change sets.</typeparam>
    /// <param name="sources">The source observables to merge.</param>
    /// <returns>An observable that emits the merged change sets.</returns>
    public static Observable<IChangeSet<T>> MergeChangeSets<T>(params Observable<IChangeSet<T>>[] sources)
        where T : notnull
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (sources.Length == 0)
        {
            return Observable.Create<IChangeSet<T>>(obs =>
            {
                obs.OnNext(ChangeSet<T>.Empty);
                obs.OnCompleted();
                return Disposable.Empty;
            });
        }

        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var states = sources.Select(_ => new HashSet<T>(EqualityComparer<T>.Default)).ToArray();
            var subscriptions = new List<IDisposable>();
            var resultItems = new List<T>(); // Maintains ordered union
            var firstAppearanceOrder = new Dictionary<T, long>(EqualityComparer<T>.Default);
            long appearanceCounter = 0;

            void Recompute()
            {
                // Union of all states
                var unionSet = new HashSet<T>(states.SelectMany(s => s), EqualityComparer<T>.Default);
                var unionItems = unionSet.ToList();

                var changes = new List<Change<T>>();

                // Removals: items currently in resultItems but not in union
                var toRemove = resultItems.Where(item => !unionSet.Contains(item)).Select(item => item).ToList();
                if (toRemove.Count > 0)
                {
                    // Remove from highest index downward
                    foreach (var removeItem in toRemove)
                    {
                        var idx = resultItems.IndexOf(removeItem);
                        if (idx >= 0)
                        {
                            resultItems.RemoveAt(idx);
                            changes.Add(new Change<T>(ListChangeReason.Remove, removeItem, idx));
                        }
                    }
                }

                // Additions: items in union but not yet in resultItems
                var newItems = unionItems.Where(item => !resultItems.Contains(item)).ToList();
                if (newItems.Count > 0)
                {
                    // Assign appearance order if first time seen
                    foreach (var ni in newItems)
                    {
                        if (!firstAppearanceOrder.ContainsKey(ni))
                        {
                            firstAppearanceOrder[ni] = appearanceCounter++;
                        }
                    }

                    // Order new items by their first appearance sequence
                    foreach (var ni in newItems.OrderBy(x => firstAppearanceOrder[x]))
                    {
                        var idx = resultItems.Count;
                        resultItems.Add(ni);
                        changes.Add(new Change<T>(ListChangeReason.Add, ni, idx));
                    }
                }

                if (changes.Count > 0)
                {
                    var cs = new ChangeSet<T>(changes.Count);
                    cs.AddRange(changes);
                    observer.OnNext(cs);
                }
            }

            for (int i = 0; i < sources.Length; i++)
            {
                int capture = i;
                var sub = sources[capture].Subscribe(
                    changeSet =>
                    {
                        var state = states[capture];
                        foreach (var change in changeSet)
                        {
                            switch (change.Reason)
                            {
                                case ListChangeReason.Add:
                                    state.Add(change.Item);
                                    break;
                                case ListChangeReason.AddRange:
                                    foreach (var item in change.Range)
                                    {
                                        state.Add(item);
                                    }

                                    break;
                                case ListChangeReason.Remove:
                                    state.Remove(change.Item);
                                    break;
                                case ListChangeReason.RemoveRange:
                                    foreach (var item in change.Range)
                                    {
                                        state.Remove(item);
                                    }

                                    break;
                                case ListChangeReason.Replace:
                                    if (change.PreviousItem is not null)
                                    {
                                        state.Remove(change.PreviousItem);
                                    }

                                    state.Add(change.Item);
                                    break;
                                case ListChangeReason.Clear:
                                    state.Clear();
                                    break;
                                case ListChangeReason.Moved:
                                case ListChangeReason.Refresh:
                                    break;
                            }
                        }

                        Recompute();
                    },
                    observer.OnErrorResume,
                    observer.OnCompleted);
                subscriptions.Add(sub);
            }

            return Disposable.Create(() =>
            {
                foreach (var s in subscriptions)
                {
                    s.Dispose();
                }
            });
        });
    }
}
