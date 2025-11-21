using System;
using System.Collections.Generic;
using System.Linq;

namespace R3.DynamicData.List;

/// <summary>
/// Eviction policy for <see cref="ObservableListEx.LimitSizeTo"/>.
/// </summary>
public enum LimitSizeToEviction
{
    /// <summary>
    /// Remove items from the start (oldest first - FIFO).
    /// </summary>
    RemoveOldest,

    /// <summary>
    /// Remove items from the end (newest first - LIFO).
    /// </summary>
    RemoveNewest,
}

public static partial class ObservableListEx
{
    /// <summary>
    /// Limits the size of the resulting list change stream to <paramref name="maxSize"/> items.
    /// When the size would exceed the maximum, items are evicted according to the eviction policy.
    /// Default eviction policy removes the oldest items first (FIFO).
    /// </summary>
    public static Observable<IChangeSet<T>> LimitSizeTo<T>(
        this Observable<IChangeSet<T>> source,
        int maxSize,
        LimitSizeToEviction eviction = LimitSizeToEviction.RemoveOldest)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize));
        }

        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var items = new List<T>();

            return source.Subscribe(
                changes =>
                {
                var outgoing = new List<Change<T>>();

                // First apply incoming changes to local state and record outgoing equivalents
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            // Insert at index if possible, else append
                            var addIndex = change.CurrentIndex >= 0 && change.CurrentIndex <= items.Count ? change.CurrentIndex : items.Count;
                            items.Insert(addIndex, change.Item);
                            outgoing.Add(new Change<T>(ListChangeReason.Add, change.Item, addIndex));
                            break;
                        case ListChangeReason.AddRange:
                            // InsertRange at index (if provided) or append
                            var baseIndex = change.CurrentIndex >= 0 && change.CurrentIndex <= items.Count ? change.CurrentIndex : items.Count;
                            var idx = baseIndex;
                            foreach (var item in change.Range)
                            {
                                items.Insert(idx, item);
                                outgoing.Add(new Change<T>(ListChangeReason.Add, item, idx));
                                idx++;
                            }

                            break;
                        case ListChangeReason.Remove:
                            if (change.CurrentIndex >= 0 && change.CurrentIndex < items.Count && EqualityComparer<T>.Default.Equals(items[change.CurrentIndex], change.Item))
                            {
                                items.RemoveAt(change.CurrentIndex);
                                outgoing.Add(new Change<T>(ListChangeReason.Remove, change.Item, change.CurrentIndex));
                            }
                            else
                            {
                                // Fallback search
                                var ri = items.IndexOf(change.Item);
                                if (ri >= 0)
                                {
                                    items.RemoveAt(ri);
                                    outgoing.Add(new Change<T>(ListChangeReason.Remove, change.Item, ri));
                                }
                            }

                            break;
                        case ListChangeReason.RemoveRange:
                            // Remove each item individually
                            foreach (var item in change.Range)
                            {
                                var ri = items.IndexOf(item);
                                if (ri >= 0)
                                {
                                    items.RemoveAt(ri);
                                    outgoing.Add(new Change<T>(ListChangeReason.Remove, item, ri));
                                }
                            }

                            break;
                        case ListChangeReason.Replace:
                            if (change.CurrentIndex >= 0 && change.CurrentIndex < items.Count)
                            {
                                var prev = items[change.CurrentIndex];
                                items[change.CurrentIndex] = change.Item;
                                outgoing.Add(new Change<T>(ListChangeReason.Replace, change.Item, prev, change.CurrentIndex));
                            }
                            else
                            {
                                // Treat as add if index invalid
                                items.Add(change.Item);
                                outgoing.Add(new Change<T>(ListChangeReason.Add, change.Item, items.Count - 1));
                            }

                            break;
                        case ListChangeReason.Moved:
                            if (change.PreviousIndex >= 0 && change.PreviousIndex < items.Count && change.CurrentIndex >= 0 && change.CurrentIndex < items.Count)
                            {
                                var moved = items[change.PreviousIndex];
                                items.RemoveAt(change.PreviousIndex);
                                var target = Math.Min(change.CurrentIndex, items.Count);
                                items.Insert(target, moved);
                                outgoing.Add(new Change<T>(ListChangeReason.Moved, moved, target, change.PreviousIndex));
                            }

                            break;
                        case ListChangeReason.Clear:
                            if (items.Count > 0)
                            {
                                var cleared = items.ToList();
                                items.Clear();
                                outgoing.Add(new Change<T>(ListChangeReason.Clear, cleared, 0));
                            }

                            break;
                        case ListChangeReason.Refresh:
                            outgoing.Add(Change<T>.Refresh);
                            break;
                    }
                }

                // Evict if needed
                if (items.Count > maxSize)
                {
                    var toEvict = items.Count - maxSize;
                    if (eviction == LimitSizeToEviction.RemoveOldest)
                    {
                        for (int i = 0; i < toEvict; i++)
                        {
                            var victim = items[0];
                            items.RemoveAt(0);
                            outgoing.Add(new Change<T>(ListChangeReason.Remove, victim, 0));
                        }
                    }
                    else if (eviction == LimitSizeToEviction.RemoveNewest)
                    {
                        for (int i = 0; i < toEvict; i++)
                        {
                            var lastIndex = items.Count - 1;
                            var victim = items[lastIndex];
                            items.RemoveAt(lastIndex);
                            outgoing.Add(new Change<T>(ListChangeReason.Remove, victim, lastIndex));
                        }
                    }
                }

                if (outgoing.Count > 0)
                {
                    var cs = new ChangeSet<T>(outgoing.Count);
                    cs.AddRange(outgoing);
                    observer.OnNext(cs);
                }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }
}
