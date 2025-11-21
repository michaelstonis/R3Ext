using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;

#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1116 // Parameters should begin on the line after the declaration when spanning multiple lines
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable SA1514 // Element documentation header should be preceded by blank line
#pragma warning disable SA1127 // Generic type constraints should be on their own line
#pragma warning disable SA1502 // Element should not be on a single line
#pragma warning disable SA1136 // Enum values should be on separate lines
#pragma warning disable SA1413 // Use trailing comma in multi-line initializers
#pragma warning disable SA1107 // Code should not contain multiple statements on one line
#pragma warning disable SA1516 // Elements should be separated by blank line

namespace R3.DynamicData.Cache;

// Phase 2 cache operators for R3 port.
// NOTE: Adapted from DynamicData concepts; simplified to match R3's Observable<T> (not System.IObservable<T>). Inner observable completions NEVER complete outer streams.
public static partial class ObservableCacheEx
{
    // ------------------ AddKey ------------------
    public static Observable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(
        this Observable<IChangeSet<TObject>> source,
        Func<TObject, TKey> keySelector)
        where TObject : notnull where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            // Track current items so we can emit proper removes on Clear / RemoveRange.
            var current = new Dictionary<TKey, TObject>();
            return source.Subscribe(changes =>
            {
                var converted = new List<Change<TObject, TKey>>();
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            {
                                var key = keySelector(change.Item);
                                current[key] = change.Item;
                                converted.Add(new Change<TObject, TKey>(ChangeReason.Add, key, change.Item));
                                break;
                            }
                        case ListChangeReason.AddRange:
                            foreach (var item in change.Range)
                            {
                                var key = keySelector(item);
                                current[key] = item;
                                converted.Add(new Change<TObject, TKey>(ChangeReason.Add, key, item));
                            }
                            break;
                        case ListChangeReason.Replace:
                            {
                                var key = keySelector(change.Item);
                                // Assume key stable; if changed treat as remove+add.
                                if (change.PreviousItem is not null && !EqualityComparer<TKey>.Default.Equals(keySelector(change.PreviousItem), key))
                                {
                                    var oldKey = keySelector(change.PreviousItem);
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
                                var key = keySelector(change.Item);
                                if (current.Remove(key))
                                {
                                    converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, change.Item));
                                }
                                break;
                            }
                        case ListChangeReason.RemoveRange:
                            foreach (var item in change.Range)
                            {
                                var key = keySelector(item);
                                if (current.Remove(key))
                                {
                                    converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, item));
                                }
                            }
                            break;
                        case ListChangeReason.Moved:
                            // Cache has no ordering concept; treat as Refresh.
                            converted.Add(new Change<TObject, TKey>(ChangeReason.Refresh, keySelector(change.Item), change.Item));
                            break;
                        case ListChangeReason.Refresh:
                            converted.Add(new Change<TObject, TKey>(ChangeReason.Refresh, keySelector(change.Item), change.Item));
                            break;
                        case ListChangeReason.Clear:
                            // Remove all previously tracked items if range includes them; if empty fallback to clearing dictionary.
                            if (change.Range.Count > 0)
                            {
                                foreach (var item in change.Range)
                                {
                                    var key = keySelector(item);
                                    if (current.Remove(key))
                                        converted.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, item));
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
    public static Observable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TDestination> selector)
        where TSource : notnull where TDestination : notnull where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            return source.Subscribe(changes =>
            {
                var converted = new List<Change<TDestination, TKey>>(changes.Count);
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Add, change.Key, selector(change.Current)));
                            break;
                        case ChangeReason.Update:
                            var prevVal = change.Previous.HasValue ? selector(change.Previous.Value) : selector(change.Current);
                            var currVal = selector(change.Current);
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Update, change.Key, currVal, prevVal));
                            break;
                        case ChangeReason.Remove:
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Remove, change.Key, selector(change.Current)));
                            break;
                        case ChangeReason.Refresh:
                            converted.Add(new Change<TDestination, TKey>(ChangeReason.Refresh, change.Key, selector(change.Current)));
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
    public static Observable<Optional<TObject>> ToObservableOptional<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        TKey key)
        where TObject : notnull where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Observable.Create<Optional<TObject>>(observer =>
        {
            TObject? latest = default;
            bool hasValue = false;
            observer.OnNext(Optional<TObject>.None);
            return source.Subscribe(changes =>
            {
                bool changed = false;
                foreach (var change in changes)
                {
                    if (!EqualityComparer<TKey>.Default.Equals(change.Key, key)) continue;
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

    // ------------------ EditDiff ------------------
    public static void EditDiff<TObject, TKey>(
        this ISourceCache<TObject, TKey> source,
        IEnumerable<TObject> newItems,
        Func<TObject, TObject, bool> equalityComparator,
        Func<TObject, TKey> keySelector)
        where TObject : notnull where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (newItems is null) throw new ArgumentNullException(nameof(newItems));
        if (equalityComparator is null) throw new ArgumentNullException(nameof(equalityComparator));
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));

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
                editor.Remove(keySelector(rem));
            foreach (var add in toAdd)
                editor.AddOrUpdate(add);
            foreach (var upd in toUpdate)
                editor.AddOrUpdate(upd);
        });
    }

    // ------------------ Combine Operators ------------------
    public static Observable<IChangeSet<TObject, TKey>> And<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull where TKey : notnull => CombineInternal(primary, others, CacheCombineOperator.And);

    public static Observable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull where TKey : notnull => CombineInternal(primary, others, CacheCombineOperator.Or);

    public static Observable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull where TKey : notnull => CombineInternal(primary, others, CacheCombineOperator.Except);

    public static Observable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> primary,
        params Observable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull where TKey : notnull => CombineInternal(primary, others, CacheCombineOperator.Xor);

    private static Observable<IChangeSet<TObject, TKey>> CombineInternal<TObject, TKey>(
        Observable<IChangeSet<TObject, TKey>> primary,
        Observable<IChangeSet<TObject, TKey>>[] others,
        CacheCombineOperator op)
        where TObject : notnull where TKey : notnull
    {
        if (primary is null) throw new ArgumentNullException(nameof(primary));
        if (others is null) throw new ArgumentNullException(nameof(others));
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
                    _ => Enumerable.Empty<TKey>()
                };
                var newSet = new HashSet<TKey>(resultKeys);
                var changes = new List<Change<TObject, TKey>>();
                // Removed
                foreach (var k in lastKeys.Where(k => !newSet.Contains(k)))
                {
                    // Need previous value for remove; pick first dictionary containing key (before removal)
                    var prevVal = states.Select(s => s.TryGetValue(k, out var v) ? v : default).FirstOrDefault(v => v is not null);
                    if (prevVal is not null)
                        changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, k, prevVal));
                }
                // Added
                foreach (var k in newSet.Where(k => !lastKeys.Contains(k)))
                {
                    var val = states.Select(s => s.TryGetValue(k, out var v) ? v : default).FirstOrDefault(v => v is not null);
                    if (val is not null)
                        changes.Add(new Change<TObject, TKey>(ChangeReason.Add, k, val));
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
                var sub = all[capture].Subscribe(changes =>
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
                foreach (var s in subscriptions) s.Dispose();
            });
        });
    }

    private static IEnumerable<TKey> Intersection<TObject, TKey>(Dictionary<TKey, TObject>[] states) where TObject : notnull where TKey : notnull
        => states.Length == 0 ? Enumerable.Empty<TKey>() : states.Skip(1).Aggregate(new HashSet<TKey>(states[0].Keys), (acc, s) => { acc.IntersectWith(s.Keys); return acc; });
    private static IEnumerable<TKey> Union<TObject, TKey>(Dictionary<TKey, TObject>[] states) where TObject : notnull where TKey : notnull
        => states.SelectMany(s => s.Keys).Distinct();
    private static IEnumerable<TKey> ExceptFirst<TObject, TKey>(Dictionary<TKey, TObject>[] states) where TObject : notnull where TKey : notnull
        => states.Length == 0 ? Enumerable.Empty<TKey>() : states[0].Keys.Where(k => states.Skip(1).All(s => !s.ContainsKey(k)));
    private static IEnumerable<TKey> Xor<TObject, TKey>(Dictionary<TKey, TObject>[] states) where TObject : notnull where TKey : notnull
    {
        var allKeys = states.SelectMany(s => s.Keys).ToList();
        return allKeys.GroupBy(k => k).Where(g => g.Count() == 1).Select(g => g.Key);
    }

    private enum CacheCombineOperator { And, Or, Except, Xor }

    // ------------------ TrueForAny ------------------
    public static Observable<bool> TrueForAny<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<TValue>> observableSelector,
        Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull where TKey : notnull where TValue : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (observableSelector is null) throw new ArgumentNullException(nameof(observableSelector));
        if (equalityCondition is null) throw new ArgumentNullException(nameof(equalityCondition));
        return Observable.Create<bool>(observer =>
        {
            var itemStates = new Dictionary<TKey, (TObject Item, TValue? Latest)>();
            var innerSubs = new Dictionary<TKey, IDisposable>();
            void Recompute()
            {
                bool any = itemStates.Any(kvp => kvp.Value.Latest is TValue v && equalityCondition(kvp.Value.Item, v));
                observer.OnNext(any);
            }
            // Initial (empty) => false
            observer.OnNext(false);

            var outer = source.Subscribe(changes =>
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
                                var obs = observableSelector(change.Current);
                                innerSubs[change.Key] = obs.Subscribe(val =>
                                {
                                    itemStates[change.Key] = (change.Current, val);
                                    Recompute();
                                });
                            }
                            break;
                        case ChangeReason.Remove:
                            if (innerSubs.Remove(change.Key, out var disp)) disp.Dispose();
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
                foreach (var d in innerSubs.Values) d.Dispose();
            });
        });
    }

    // ------------------ TrueForAll ------------------
    public static Observable<bool> TrueForAll<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<TValue>> observableSelector,
        Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull where TKey : notnull where TValue : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (observableSelector is null) throw new ArgumentNullException(nameof(observableSelector));
        if (equalityCondition is null) throw new ArgumentNullException(nameof(equalityCondition));
        return Observable.Create<bool>(observer =>
        {
            var itemStates = new Dictionary<TKey, (TObject Item, TValue? Latest)>();
            var innerSubs = new Dictionary<TKey, IDisposable>();
            void Recompute()
            {
                bool all = itemStates.Count == 0 || itemStates.All(kvp => kvp.Value.Latest is TValue v && equalityCondition(kvp.Value.Item, v));
                observer.OnNext(all);
            }
            // Empty set vacuously true
            observer.OnNext(true);

            var outer = source.Subscribe(changes =>
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
                                var obs = observableSelector(change.Current);
                                innerSubs[change.Key] = obs.Subscribe(val =>
                                {
                                    itemStates[change.Key] = (change.Current, val);
                                    Recompute();
                                });
                            }
                            break;
                        case ChangeReason.Remove:
                            if (innerSubs.Remove(change.Key, out var disp)) disp.Dispose();
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
                foreach (var d in innerSubs.Values) d.Dispose();
            });
        });
    }

    // ------------------ QueryWhenChanged / ToCollection ------------------
    public static Observable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
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

    public static Observable<IReadOnlyList<TObject>> ToCollection<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull where TKey : notnull
        => source.QueryWhenChanged().Select(q => (IReadOnlyList<TObject>)q.Items.ToList());

    // Overload with projection selector used by tests: QueryWhenChanged(q => q.Count)
    public static Observable<TResult> QueryWhenChanged<TObject, TKey, TResult>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<IQuery<TObject, TKey>, TResult> selector)
        where TObject : notnull where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return source.QueryWhenChanged().Select(selector);
    }
}

// ------------------ Query Interfaces ------------------
public interface IQuery<TObject, TKey>
    where TObject : notnull where TKey : notnull
{
    int Count { get; }
    IEnumerable<TObject> Items { get; }
    IEnumerable<TKey> Keys { get; }
    IEnumerable<KeyValuePair<TKey, TObject>> KeyValues { get; }
    Optional<TObject> Lookup(TKey key);
}

internal sealed class CacheQuery<TObject, TKey>(IReadOnlyDictionary<TKey, TObject> data)
    : IQuery<TObject, TKey>
    where TObject : notnull where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, TObject> _data = data;
    public int Count => _data.Count;
    public IEnumerable<TObject> Items => _data.Values;
    public IEnumerable<TKey> Keys => _data.Keys;
    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;
    public Optional<TObject> Lookup(TKey key) => _data.TryGetValue(key, out var value) ? Optional<TObject>.Some(value) : Optional<TObject>.None;
}