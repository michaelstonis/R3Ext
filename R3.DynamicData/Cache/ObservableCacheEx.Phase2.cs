using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;

namespace R3.DynamicData.Cache;

// Phase 2 cache operators for R3 port.
// NOTE: Adapted from DynamicData concepts; simplified to match R3's Observable<T> (not System.IObservable<T>). Inner observable completions NEVER complete outer streams.
public static partial class ObservableCacheEx
{
    // ------------------ AddKey ------------------

    /// <summary>
    /// Converts a list change set to a cache change set by adding keys using the provided selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source list change set observable.</param>
    /// <param name="keySelector">Function to extract keys from objects.</param>
    /// <returns>An observable cache change set.</returns>
    public static Observable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(
        this Observable<IChangeSet<TObject>> source,
        Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        var state = new AddKeyState<TObject, TKey>(source, keySelector);
        return Observable.Create<IChangeSet<TObject, TKey>, AddKeyState<TObject, TKey>>(
            state,
            static (observer, state) =>
        {
            // Track current items so we can emit proper removes on Clear / RemoveRange.
            var current = new Dictionary<TKey, TObject>();
            return state.Source.Subscribe(
                changes =>
            {
                var converted = new List<Change<TObject, TKey>>();
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            {
                                var key = state.KeySelector(change.Item);
                                current[key] = change.Item;
                                converted.Add(new Change<TObject, TKey>(ChangeReason.Add, key, change.Item));
                                break;
                            }

                        case ListChangeReason.AddRange:
                            foreach (var item in change.Range)
                            {
                                var key = state.KeySelector(item);
                                current[key] = item;
                                converted.Add(new Change<TObject, TKey>(ChangeReason.Add, key, item));
                            }

                            break;
                        case ListChangeReason.Replace:
                            {
                                var key = state.KeySelector(change.Item);

                                // Assume key stable; if changed treat as remove+add.
                                if (change.PreviousItem is not null && !EqualityComparer<TKey>.Default.Equals(state.KeySelector(change.PreviousItem), key))
                                {
                                    var oldKey = state.KeySelector(change.PreviousItem);
                                    if (current.Remove(oldKey))
                                    {
                                        converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, oldKey, change.PreviousItem));
                                    }

                                    current[key] = change.Item;
                                    converted.Add(new Change<TObject, TKey>(ChangeReason.Add, key, change.Item));
                                }
                                else
                                {
                                    if (change.PreviousItem is not null)
                                    {
                                        current[key] = change.Item;
                                        converted.Add(new Change<TObject, TKey>(ChangeReason.Update, key, change.Item, change.PreviousItem));
                                    }
                                    else
                                    {
                                        current[key] = change.Item;
                                        converted.Add(new Change<TObject, TKey>(ChangeReason.Add, key, change.Item));
                                    }
                                }

                                break;
                            }

                        case ListChangeReason.Remove:
                            {
                                var key = state.KeySelector(change.Item);
                                if (current.Remove(key))
                                {
                                    converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, change.Item));
                                }

                                break;
                            }

                        case ListChangeReason.RemoveRange:
                            foreach (var item in change.Range)
                            {
                                var key = state.KeySelector(item);
                                if (current.Remove(key))
                                {
                                    converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, item));
                                }
                            }

                            break;
                        case ListChangeReason.Moved:
                            // Cache has no ordering concept; treat as Refresh.
                            converted.Add(new Change<TObject, TKey>(ChangeReason.Refresh, state.KeySelector(change.Item), change.Item));
                            break;
                        case ListChangeReason.Refresh:
                            converted.Add(new Change<TObject, TKey>(ChangeReason.Refresh, state.KeySelector(change.Item), change.Item));
                            break;
                        case ListChangeReason.Clear:
                            // Remove all previously tracked items if range includes them; if empty fallback to clearing dictionary.
                            if (change.Range.Count > 0)
                            {
                                foreach (var item in change.Range)
                                {
                                    var key = state.KeySelector(item);
                                    if (current.Remove(key))
                                    {
                                        converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, item));
                                    }
                                }
                            }
                            else
                            {
                                foreach (var kvp in current.ToArray())
                                {
                                    converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value));
                                }

                                current.Clear();
                            }

                            break;
                    }
                }

                if (converted.Count > 0)
                {
                    var cs = new ChangeSet<TObject, TKey>(converted.Count);
                    cs.AddRange(converted);
                    observer.OnNext(cs);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    // ------------------ Cast ------------------

    /// <summary>
    /// Transforms objects in the change set using the provided selector function.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The destination object type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="selector">Function to transform objects.</param>
    /// <returns>An observable with transformed objects.</returns>
    public static Observable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TDestination> selector)
        where TSource : notnull
        where TDestination : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var state = new CastState<TSource, TKey, TDestination>(source, selector);
        return Observable.Create<IChangeSet<TDestination, TKey>, CastState<TSource, TKey, TDestination>>(
            state,
            static (observer, state) =>
        {
            return state.Source.Subscribe(
                changes =>
            {
                var converted = new List<Change<TDestination, TKey>>(changes.Count);
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Add, change.Key, state.Selector(change.Current)));
                            break;
                        case ChangeReason.Update:
                            var prevVal = change.Previous.HasValue ? state.Selector(change.Previous.Value) : state.Selector(change.Current);
                            var currVal = state.Selector(change.Current);
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Update, change.Key, currVal, prevVal));
                            break;
                        case ChangeReason.Remove:
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Remove, change.Key, state.Selector(change.Current)));
                            break;
                        case ChangeReason.Refresh:
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Refresh, change.Key, state.Selector(change.Current)));
                            break;
                    }
                }

                if (converted.Count > 0)
                {
                    var cs = new ChangeSet<TDestination, TKey>(converted.Count);
                    cs.AddRange(converted);
                    observer.OnNext(cs);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    // ------------------ ToObservableOptional ------------------

    /// <summary>
    /// Observes a single value by key, emitting Optional values when the key is added, updated, or removed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="key">The key to observe.</param>
    /// <returns>An observable that emits Optional values for the specified key.</returns>
    public static Observable<Optional<TObject>> ToObservableOptional<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var state = new ToObservableOptionalState<TObject, TKey>(source, key);
        return Observable.Create<Optional<TObject>, ToObservableOptionalState<TObject, TKey>>(state, static (observer, state) =>
        {
            TObject? latest = default;
            bool hasValue = false;
            observer.OnNext(Optional<TObject>.None);
            return state.Source.Subscribe(
                changes =>
            {
                bool changed = false;
                foreach (var change in changes)
                {
                    if (!EqualityComparer<TKey>.Default.Equals(change.Key, state.Key))
                    {
                        continue;
                    }

                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                        case ChangeReason.Refresh:
                            latest = change.Current;
                            hasValue = true;
                            changed = true;
                            break;
                        case ChangeReason.Remove:
                            latest = default;
                            hasValue = false;
                            changed = true;
                            break;
                    }
                }

                if (changed)
                {
                    observer.OnNext(hasValue ? Optional<TObject>.Some(latest!) : Optional<TObject>.None);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    private readonly struct ToObservableOptionalState<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        public readonly Observable<IChangeSet<TObject, TKey>> Source;
        public readonly TKey Key;

        public ToObservableOptionalState(Observable<IChangeSet<TObject, TKey>> source, TKey key)
        {
            Source = source;
            Key = key;
        }
    }

    // ------------------ EditDiff ------------------

    /// <summary>
    /// Edits the source cache by computing the differences between current items and new items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source cache to edit.</param>
    /// <param name="newItems">The new items to compare against.</param>
    /// <param name="equalityComparator">Function to compare objects for equality.</param>
    /// <param name="keySelector">Function to extract keys from objects.</param>
    public static void EditDiff<TObject, TKey>(
        this ISourceCache<TObject, TKey> source,
        IEnumerable<TObject> newItems,
        Func<TObject, TObject, bool> equalityComparator,
        Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (newItems is null)
        {
            throw new ArgumentNullException(nameof(newItems));
        }

        if (equalityComparator is null)
        {
            throw new ArgumentNullException(nameof(equalityComparator));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        var incoming = newItems.ToDictionary(keySelector, x => x);
        var toRemove = source.Items.Where(existing => !incoming.ContainsKey(keySelector(existing))).ToList();
        var toUpdate = source.Items
            .Select(existing => (existing, keySelector(existing)))
            .Where(tuple => incoming.ContainsKey(tuple.Item2) && !equalityComparator(tuple.existing, incoming[tuple.Item2]))
            .Select(tuple => incoming[tuple.Item2])
            .ToList();
        var toAdd = incoming.Where(kvp => !source.Lookup(kvp.Key).HasValue).Select(kvp => kvp.Value).ToList();

        source.Edit(editor =>
        {
            foreach (var rem in toRemove)
            {
                editor.Remove(keySelector(rem));
            }

            foreach (var add in toAdd)
            {
                editor.AddOrUpdate(add);
            }

            foreach (var upd in toUpdate)
            {
                editor.AddOrUpdate(upd);
            }
        });
    }

    // ------------------ Combine Operators ------------------

    /// <summary>
    /// Combines cache change sets using AND logic - emits items present in all sources.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="primary">The primary observable.</param>
    /// <param name="others">Additional observables to combine.</param>
    /// <returns>An observable with items present in all sources.</returns>
    public static Observable<IChangeSet<TObject, TKey>> And<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
        => CombineInternal(primary, others, CacheCombineOperator.And);

    /// <summary>
    /// Combines cache change sets using OR logic - emits items present in any source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="primary">The primary observable.</param>
    /// <param name="others">Additional observables to combine.</param>
    /// <returns>An observable with items present in any source.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
        => CombineInternal(primary, others, CacheCombineOperator.Or);

    /// <summary>
    /// Combines cache change sets using EXCEPT logic - emits items in primary but not in others.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="primary">The primary observable.</param>
    /// <param name="others">Observables whose items should be excluded.</param>
    /// <returns>An observable with items in primary but not in others.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
        => CombineInternal(primary, others, CacheCombineOperator.Except);

    /// <summary>
    /// Combines cache change sets using XOR logic - emits items present in exactly one source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="primary">The primary observable.</param>
    /// <param name="others">Additional observables to combine.</param>
    /// <returns>An observable with items present in exactly one source.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
        => CombineInternal(primary, others, CacheCombineOperator.Xor);

    private static Observable<IChangeSet<TObject, TKey>> CombineInternal<TObject, TKey>(
        Observable<IChangeSet<TObject, TKey>> primary,
        Observable<IChangeSet<TObject, TKey>>[] others,
        CacheCombineOperator op)
        where TObject : notnull
        where TKey : notnull
    {
        if (primary is null)
        {
            throw new ArgumentNullException(nameof(primary));
        }

        if (others is null)
        {
            throw new ArgumentNullException(nameof(others));
        }

        var all = new[] { primary }.Concat(others).ToArray();
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var states = all.Select(_ => new Dictionary<TKey, TObject>()).ToArray();
            var subscriptions = new List<IDisposable>();
            var lastKeys = new HashSet<TKey>();

            void Recompute()
            {
                var resultKeys = op switch
                {
                    CacheCombineOperator.And => Intersection(states),
                    CacheCombineOperator.Or => Union(states),
                    CacheCombineOperator.Except => ExceptFirst(states),
                    CacheCombineOperator.Xor => Xor(states),
                    _ => Enumerable.Empty<TKey>(),
                };
                var newSet = new HashSet<TKey>(resultKeys);
                var changes = new List<Change<TObject, TKey>>();

                // Removed
                foreach (var k in lastKeys.Where(k => !newSet.Contains(k)))
                {
                    // Need previous value for remove; pick first dictionary containing key (before removal)
                    var prevVal = states.Select(s => s.TryGetValue(k, out var v) ? v : default).FirstOrDefault(v => v is not null);
                    if (prevVal is not null)
                    {
                        changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, k, prevVal));
                    }
                }

                // Added
                foreach (var k in newSet.Where(k => !lastKeys.Contains(k)))
                {
                    var val = states.Select(s => s.TryGetValue(k, out var v) ? v : default).FirstOrDefault(v => v is not null);
                    if (val is not null)
                    {
                        changes.Add(new Change<TObject, TKey>(ChangeReason.Add, k, val));
                    }
                }

                if (changes.Count > 0)
                {
                    lastKeys = newSet;
                    var cs = new ChangeSet<TObject, TKey>(changes.Count);
                    cs.AddRange(changes);
                    observer.OnNext(cs);
                }
            }

            for (int idx = 0; idx < all.Length; idx++)
            {
                int capture = idx;
                var sub = all[capture].Subscribe(
                    changes =>
                {
                    foreach (var change in changes)
                    {
                        var dict = states[capture];
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                            case ChangeReason.Refresh:
                                dict[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                dict.Remove(change.Key);
                                break;
                        }
                    }

                    Recompute();
                }, observer.OnErrorResume, observer.OnCompleted);
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

    private static IEnumerable<TKey> Intersection<TObject, TKey>(Dictionary<TKey, TObject>[] states)
        where TObject : notnull
        where TKey : notnull
        => states.Length == 0
            ? Enumerable.Empty<TKey>()
            : states.Skip(1).Aggregate(
                new HashSet<TKey>(states[0].Keys),
                (acc, s) =>
                {
                    acc.IntersectWith(s.Keys);
                    return acc;
                });

    private static IEnumerable<TKey> Union<TObject, TKey>(Dictionary<TKey, TObject>[] states)
        where TObject : notnull
        where TKey : notnull
        => states.SelectMany(s => s.Keys).Distinct();

    private static IEnumerable<TKey> ExceptFirst<TObject, TKey>(Dictionary<TKey, TObject>[] states)
        where TObject : notnull
        where TKey : notnull
        => states.Length == 0 ? Enumerable.Empty<TKey>() : states[0].Keys.Where(k => states.Skip(1).All(s => !s.ContainsKey(k)));

    private static IEnumerable<TKey> Xor<TObject, TKey>(Dictionary<TKey, TObject>[] states)
        where TObject : notnull
        where TKey : notnull
    {
        var allKeys = states.SelectMany(s => s.Keys).ToList();
        return allKeys.GroupBy(k => k).Where(g => g.Count() == 1).Select(g => g.Key);
    }

    private enum CacheCombineOperator
    {
        And,
        Or,
        Except,
        Xor,
    }

    // ------------------ TrueForAny ------------------

    /// <summary>
    /// Returns an observable that emits true when any items in the cache satisfy the condition from their inner observables.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of value from the inner observable.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="observableSelector">Function to select an inner observable for each object.</param>
    /// <param name="equalityCondition">Condition to evaluate each object against its inner value.</param>
    /// <returns>An observable of bool indicating if any items satisfy the condition.</returns>
    public static Observable<bool> TrueForAny<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<TValue>> observableSelector,
        Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (observableSelector is null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        if (equalityCondition is null)
        {
            throw new ArgumentNullException(nameof(equalityCondition));
        }

        var state = new TrueForAnyState<TObject, TKey, TValue>(source, observableSelector, equalityCondition);
        return Observable.Create<bool, TrueForAnyState<TObject, TKey, TValue>>(
            state,
            static (observer, state) =>
            {
                var itemStates = new Dictionary<TKey, (TObject Item, TValue? Latest)>();
                var innerSubs = new Dictionary<TKey, IDisposable>();
                void Recompute()
                {
                    bool any = itemStates.Any(kvp => kvp.Value.Latest is TValue v && state.EqualityCondition(kvp.Value.Item, v));
                    observer.OnNext(any);
                }

                // Initial (empty) => false
                observer.OnNext(false);

                var outer = state.Source.Subscribe(changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                var existingLatest = itemStates.TryGetValue(change.Key, out var tuple) ? tuple.Latest : default;
                                itemStates[change.Key] = (change.Current, existingLatest);
                                if (!innerSubs.ContainsKey(change.Key))
                                {
                                    var obs = state.ObservableSelector(change.Current);
                                    var innerState = new InnerSubscriptionState<TObject, TKey, TValue>(change.Current, change.Key, itemStates, state.EqualityCondition, Recompute);
                                    innerSubs[change.Key] = obs.Subscribe(innerState, static (val, innerState) =>
                                    {
                                        innerState.ItemStates[innerState.Key] = (innerState.Current, val);
                                        innerState.Recompute();
                                    });
                                }

                                break;

                            case ChangeReason.Remove:
                                if (innerSubs.Remove(change.Key, out var disp))
                                {
                                    disp.Dispose();
                                }

                                itemStates.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (itemStates.TryGetValue(change.Key, out var existing))
                                {
                                    itemStates[change.Key] = (change.Current, existing.Latest);
                                }

                                break;
                        }
                    }

                    Recompute();
                });

                return Disposable.Create(() =>
                {
                    outer.Dispose();
                    foreach (var d in innerSubs.Values)
                    {
                        d.Dispose();
                    }
                });
            });
    }

    private sealed class TrueForAnyState<TObj, TK, TV>
        where TObj : notnull
        where TK : notnull
        where TV : notnull
    {
        public readonly Observable<IChangeSet<TObj, TK>> Source;
        public readonly Func<TObj, Observable<TV>> ObservableSelector;
        public readonly Func<TObj, TV, bool> EqualityCondition;

        public TrueForAnyState(Observable<IChangeSet<TObj, TK>> source, Func<TObj, Observable<TV>> observableSelector, Func<TObj, TV, bool> equalityCondition)
        {
            Source = source;
            ObservableSelector = observableSelector;
            EqualityCondition = equalityCondition;
        }
    }

    private sealed class InnerSubscriptionState<TObj, TK, TV>
        where TObj : notnull
        where TK : notnull
        where TV : notnull
    {
        public readonly TObj Current;
        public readonly TK Key;
        public readonly Dictionary<TK, (TObj Item, TV? Latest)> ItemStates;
        public readonly Func<TObj, TV, bool> EqualityCondition;
        public readonly Action Recompute;

        public InnerSubscriptionState(TObj current, TK key, Dictionary<TK, (TObj Item, TV? Latest)> itemStates, Func<TObj, TV, bool> equalityCondition, Action recompute)
        {
            Current = current;
            Key = key;
            ItemStates = itemStates;
            EqualityCondition = equalityCondition;
            Recompute = recompute;
        }
    }

    // ------------------ TrueForAll ------------------

    /// <summary>
    /// Returns an observable that emits true when all items in the cache satisfy the condition from their inner observables.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of value from the inner observable.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="observableSelector">Function to select an inner observable for each object.</param>
    /// <param name="equalityCondition">Condition to evaluate each object against its inner value.</param>
    /// <returns>An observable of bool indicating if all items satisfy the condition.</returns>
    public static Observable<bool> TrueForAll<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<TValue>> observableSelector,
        Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (observableSelector is null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        if (equalityCondition is null)
        {
            throw new ArgumentNullException(nameof(equalityCondition));
        }

        var state = new TrueForAllState<TObject, TKey, TValue>(source, observableSelector, equalityCondition);
        return Observable.Create<bool, TrueForAllState<TObject, TKey, TValue>>(
            state,
            static (observer, state) =>
            {
                var itemStates = new Dictionary<TKey, (TObject Item, TValue? Latest)>();
                var innerSubs = new Dictionary<TKey, IDisposable>();
                void Recompute()
                {
                    bool all = itemStates.Count == 0 || itemStates.All(kvp => kvp.Value.Latest is TValue v && state.EqualityCondition(kvp.Value.Item, v));
                    observer.OnNext(all);
                }

                // Empty set vacuously true
                observer.OnNext(true);

                var outer = state.Source.Subscribe(changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                var existingLatest = itemStates.TryGetValue(change.Key, out var tuple) ? tuple.Latest : default;
                                itemStates[change.Key] = (change.Current, existingLatest);
                                if (!innerSubs.ContainsKey(change.Key))
                                {
                                    var obs = state.ObservableSelector(change.Current);
                                    var innerState = new InnerSubscriptionState<TObject, TKey, TValue>(change.Current, change.Key, itemStates, state.EqualityCondition, Recompute);
                                    innerSubs[change.Key] = obs.Subscribe(innerState, static (val, innerState) =>
                                    {
                                        innerState.ItemStates[innerState.Key] = (innerState.Current, val);
                                        innerState.Recompute();
                                    });
                                }

                                break;

                            case ChangeReason.Remove:
                                if (innerSubs.Remove(change.Key, out var disp))
                                {
                                    disp.Dispose();
                                }

                                itemStates.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                if (itemStates.TryGetValue(change.Key, out var existing))
                                {
                                    itemStates[change.Key] = (change.Current, existing.Latest);
                                }

                                break;
                        }
                    }

                    Recompute();
                });

                return Disposable.Create(() =>
                {
                    outer.Dispose();
                    foreach (var d in innerSubs.Values)
                    {
                        d.Dispose();
                    }
                });
            });
    }

    private sealed class TrueForAllState<TObj, TK, TV>
        where TObj : notnull
        where TK : notnull
        where TV : notnull
    {
        public readonly Observable<IChangeSet<TObj, TK>> Source;
        public readonly Func<TObj, Observable<TV>> ObservableSelector;
        public readonly Func<TObj, TV, bool> EqualityCondition;

        public TrueForAllState(Observable<IChangeSet<TObj, TK>> source, Func<TObj, Observable<TV>> observableSelector, Func<TObj, TV, bool> equalityCondition)
        {
            Source = source;
            ObservableSelector = observableSelector;
            EqualityCondition = equalityCondition;
        }
    }

    // ------------------ QueryWhenChanged / ToCollection ------------------

    /// <summary>
    /// Converts the cache changeset into an observable that emits an IQuery interface for querying the current state.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>An observable of IQuery providing access to the cache state.</returns>
    public static Observable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<IQuery<TObject, TKey>>(observer =>
        {
            var dict = new Dictionary<TKey, TObject>();
            observer.OnNext(new CacheQuery<TObject, TKey>(dict)); // initial empty snapshot
            return source.Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                        case ChangeReason.Refresh:
                            dict[change.Key] = change.Current;
                            break;
                        case ChangeReason.Remove:
                            dict.Remove(change.Key);
                            break;
                    }
                }

                observer.OnNext(new CacheQuery<TObject, TKey>(dict));
            });
        });
    }

    /// <summary>
    /// Converts the cache into an observable collection of items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>An observable of a read-only list of objects.</returns>
    public static Observable<IReadOnlyList<TObject>> ToCollection<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
        => source.QueryWhenChanged().Select(q => (IReadOnlyList<TObject>)q.Items.ToList());

    /// <summary>
    /// Converts the cache changeset into an observable that projects the IQuery state using the specified selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TResult">The type of the projected result.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="selector">Function to project the query state.</param>
    /// <returns>An observable of projected results.</returns>
    public static Observable<TResult> QueryWhenChanged<TObject, TKey, TResult>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<IQuery<TObject, TKey>, TResult> selector)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return source.QueryWhenChanged().Select(selector);
    }
}

// ------------------ Query Interfaces ------------------

/// <summary>
/// Provides a query interface for accessing the current state of a cache.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IQuery<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets the count of items in the cache.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets all items in the cache.
    /// </summary>
    IEnumerable<TObject> Items { get; }

    /// <summary>
    /// Gets all keys in the cache.
    /// </summary>
    IEnumerable<TKey> Keys { get; }

    /// <summary>
    /// Gets all key-value pairs in the cache.
    /// </summary>
    IEnumerable<KeyValuePair<TKey, TObject>> KeyValues { get; }

    /// <summary>
    /// Looks up an item by key.
    /// </summary>
    /// <param name="key">The key to lookup.</param>
    /// <returns>An optional containing the value if found.</returns>
    Optional<TObject> Lookup(TKey key);
}

internal sealed class CacheQuery<TObject, TKey>(IReadOnlyDictionary<TKey, TObject> data)
    : IQuery<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, TObject> _data = data;

    public int Count => _data.Count;

    public IEnumerable<TObject> Items => _data.Values;

    public IEnumerable<TKey> Keys => _data.Keys;

    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;

    public Optional<TObject> Lookup(TKey key) => _data.TryGetValue(key, out var value) ? Optional<TObject>.Some(value) : Optional<TObject>.None;
}

internal readonly struct AddKeyState<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public readonly Observable<IChangeSet<TObject>> Source;
    public readonly Func<TObject, TKey> KeySelector;

    public AddKeyState(Observable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
    {
        Source = source;
        KeySelector = keySelector;
    }
}

internal readonly struct CastState<TSource, TKey, TDestination>
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public readonly Observable<IChangeSet<TSource, TKey>> Source;
    public readonly Func<TSource, TDestination> Selector;

    public CastState(Observable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> selector)
    {
        Source = source;
        Selector = selector;
    }
}
