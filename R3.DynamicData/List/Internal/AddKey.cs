// Port of DynamicData AddKey to R3.
using System;
using System.Collections.Generic;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.List.Internal;

internal sealed class AddKey<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject>> _source;
    private readonly Func<TObject, TKey> _keySelector;

    public AddKey(Observable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
    }

    public Observable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
    {
        var current = new Dictionary<TKey, TObject>();
        return _source.Subscribe(
            changes =>
            {
                if (changes.Count == 0)
                {
                    return;
                }

                var keyed = new List<Change<TObject, TKey>>(changes.Count);
                foreach (var c in changes)
                {
                    switch (c.Reason)
                    {
                        case ListChangeReason.Add:
                        {
                            var key = _keySelector(c.Item);
                            current[key] = c.Item;
                            keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, c.Item));
                            break;
                        }

                        case ListChangeReason.AddRange:
                            if (c.Range.Count > 0)
                            {
                                foreach (var item in c.Range)
                                {
                                    var key = _keySelector(item);
                                    current[key] = item;
                                    keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, item));
                                }
                            }
                            else
                            {
                                var key = _keySelector(c.Item);
                                current[key] = c.Item;
                                keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, c.Item));
                            }

                            break;

                        case ListChangeReason.Remove:
                        {
                            var key = _keySelector(c.Item);
                            if (current.Remove(key))
                            {
                                keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, c.Item));
                            }

                            break;
                        }

                        case ListChangeReason.RemoveRange:
                            if (c.Range.Count > 0)
                            {
                                foreach (var item in c.Range)
                                {
                                    var key = _keySelector(item);
                                    if (current.Remove(key))
                                    {
                                        keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, item));
                                    }
                                }
                            }
                            else
                            {
                                var key = _keySelector(c.Item);
                                if (current.Remove(key))
                                {
                                    keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, c.Item));
                                }
                            }

                            break;

                        case ListChangeReason.Replace:
                        {
                            var newKey = _keySelector(c.Item);
                            var prevItem = c.PreviousItem!;
                            var prevKey = _keySelector(prevItem);
                            if (!EqualityComparer<TKey>.Default.Equals(prevKey, newKey))
                            {
                                if (current.Remove(prevKey))
                                {
                                    keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, prevKey, prevItem));
                                }

                                current[newKey] = c.Item;
                                keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, newKey, c.Item));
                            }
                            else
                            {
                                current[newKey] = c.Item;
                                keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Update, newKey, c.Item, prevItem));
                            }

                            break;
                        }

                        case ListChangeReason.Moved:
                            // Represent move as refresh since ordering lost.
                            keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Refresh, _keySelector(c.Item), c.Item));
                            break;

                        case ListChangeReason.Clear:
                            // Emit removes for all tracked items
                            foreach (var kvp in current.ToArray())
                            {
                                keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, kvp.Key, kvp.Value));
                            }

                            current.Clear();
                            break;

                        case ListChangeReason.Refresh:
                            keyed.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Refresh, _keySelector(c.Item), c.Item));
                            break;
                    }
                }

                if (keyed.Count > 0)
                {
                    var cs = new ChangeSet<TObject, TKey>(keyed.Count);
                    cs.AddRange(keyed);
                    observer.OnNext(cs);
                }
            },
            observer.OnErrorResume,
            observer.OnCompleted);
    });
}
