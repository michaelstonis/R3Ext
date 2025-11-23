// Minimal InnerJoin implementation for DynamicData port. Marks Joins as Partial.
// Provides diff emission (Add/Update/Remove) for joined pairs. Future work: Left/Right/Full joins and Many joins.
#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable SA1116 // Parameters should begin on new line when spanning multiple lines
#pragma warning disable SA1127 // Generic type constraints should be on their own line
#pragma warning disable SA1210 // Using directives ordering

using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Performs an inner join between two cache change streams with matching keys.
    /// Emits Add when a matching pair first appears, Update when either side changes and result changes,
    /// and Remove when either side of an existing pair is removed. If only one side has the key no result is emitted.
    /// </summary>
    public static Observable<IChangeSet<TResult, TKey>> InnerJoin<TLeft, TRight, TKey, TResult>(
        this Observable<IChangeSet<TLeft, TKey>> left,
        Observable<IChangeSet<TRight, TKey>> right,
        Func<TLeft, TRight, TResult> resultSelector,
        IEqualityComparer<TResult>? resultComparer = null)
        where TKey : notnull
        where TLeft : notnull
        where TRight : notnull
        where TResult : notnull
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        if (resultSelector is null) throw new ArgumentNullException(nameof(resultSelector));
        var cmp = resultComparer ?? EqualityComparer<TResult>.Default;

        return Observable.Create<IChangeSet<TResult, TKey>>(observer =>
        {
            var leftItems = new Dictionary<TKey, TLeft>();
            var rightItems = new Dictionary<TKey, TRight>();
            var currentResults = new Dictionary<TKey, TResult>();

            void RecomputeAndEmit()
            {
                // Build new results for overlapping keys.
                var newResults = new Dictionary<TKey, TResult>();
                foreach (var kvp in leftItems)
                {
                    if (rightItems.TryGetValue(kvp.Key, out var r))
                    {
                        var res = resultSelector(kvp.Value, r);
                        newResults[kvp.Key] = res;
                    }
                }

                var changes = new List<Change<TResult, TKey>>();
                // Removals
                foreach (var existing in currentResults)
                {
                    if (!newResults.ContainsKey(existing.Key))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Remove, existing.Key, existing.Value, existing.Value));
                    }
                }
                // Additions & Updates
                foreach (var kvp in newResults)
                {
                    if (!currentResults.TryGetValue(kvp.Key, out var prev))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                    }
                    else if (!cmp.Equals(prev, kvp.Value))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, prev));
                    }
                }

                if (changes.Count > 0)
                {
                    // Apply changes to snapshot.
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                currentResults[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                currentResults.Remove(change.Key);
                                break;
                        }
                    }
                    var outSet = new ChangeSet<TResult, TKey>();
                    outSet.AddRange(changes);
                    observer.OnNext(outSet);
                }
            }

            var dispLeft = left.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                leftItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                // treat refresh as potential update; if result changes diff logic will emit Update
                                if (leftItems.ContainsKey(change.Key)) leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            var dispRight = right.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                rightItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (rightItems.ContainsKey(change.Key)) rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            return Disposable.Combine(dispLeft, dispRight);
        });
    }

    /// <summary>
    /// Performs a left outer join between two cache change streams. Emits a result for every left item,
    /// pairing with the right item if present or null if absent.
    /// </summary>
    public static Observable<IChangeSet<TResult, TKey>> LeftJoin<TLeft, TRight, TKey, TResult>(
        this Observable<IChangeSet<TLeft, TKey>> left,
        Observable<IChangeSet<TRight, TKey>> right,
        Func<TLeft, TRight?, TResult> resultSelector,
        IEqualityComparer<TResult>? resultComparer = null)
        where TKey : notnull
        where TLeft : notnull
        where TRight : class
        where TResult : notnull
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        if (resultSelector is null) throw new ArgumentNullException(nameof(resultSelector));
        var cmp = resultComparer ?? EqualityComparer<TResult>.Default;

        return Observable.Create<IChangeSet<TResult, TKey>>(observer =>
        {
            var leftItems = new Dictionary<TKey, TLeft>();
            var rightItems = new Dictionary<TKey, TRight>();
            var currentResults = new Dictionary<TKey, TResult>();

            void RecomputeAndEmit()
            {
                var newResults = new Dictionary<TKey, TResult>();
                foreach (var kvp in leftItems)
                {
                    rightItems.TryGetValue(kvp.Key, out var r);
                    var res = resultSelector(kvp.Value, r);
                    newResults[kvp.Key] = res;
                }

                var changes = new List<Change<TResult, TKey>>();
                foreach (var existing in currentResults)
                {
                    if (!newResults.ContainsKey(existing.Key))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Remove, existing.Key, existing.Value, existing.Value));
                    }
                }
                foreach (var kvp in newResults)
                {
                    if (!currentResults.TryGetValue(kvp.Key, out var prev))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                    }
                    else if (!cmp.Equals(prev, kvp.Value))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, prev));
                    }
                }

                if (changes.Count > 0)
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                currentResults[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                currentResults.Remove(change.Key);
                                break;
                        }
                    }
                    var outSet = new ChangeSet<TResult, TKey>();
                    outSet.AddRange(changes);
                    observer.OnNext(outSet);
                }
            }

            var dispLeft = left.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                leftItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (leftItems.ContainsKey(change.Key)) leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            var dispRight = right.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                rightItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (rightItems.ContainsKey(change.Key)) rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            return Disposable.Combine(dispLeft, dispRight);
        });
    }

    /// <summary>
    /// Performs a right outer join between two cache change streams. Emits a result for every right item,
    /// pairing with the left item if present or null if absent.
    /// </summary>
    public static Observable<IChangeSet<TResult, TKey>> RightJoin<TLeft, TRight, TKey, TResult>(
        this Observable<IChangeSet<TLeft, TKey>> left,
        Observable<IChangeSet<TRight, TKey>> right,
        Func<TLeft?, TRight, TResult> resultSelector,
        IEqualityComparer<TResult>? resultComparer = null)
        where TKey : notnull
        where TLeft : class
        where TRight : notnull
        where TResult : notnull
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        if (resultSelector is null) throw new ArgumentNullException(nameof(resultSelector));
        var cmp = resultComparer ?? EqualityComparer<TResult>.Default;

        return Observable.Create<IChangeSet<TResult, TKey>>(observer =>
        {
            var leftItems = new Dictionary<TKey, TLeft>();
            var rightItems = new Dictionary<TKey, TRight>();
            var currentResults = new Dictionary<TKey, TResult>();

            void RecomputeAndEmit()
            {
                var newResults = new Dictionary<TKey, TResult>();
                foreach (var kvp in rightItems)
                {
                    leftItems.TryGetValue(kvp.Key, out var l);
                    var res = resultSelector(l, kvp.Value);
                    newResults[kvp.Key] = res;
                }

                var changes = new List<Change<TResult, TKey>>();
                foreach (var existing in currentResults)
                {
                    if (!newResults.ContainsKey(existing.Key))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Remove, existing.Key, existing.Value, existing.Value));
                    }
                }
                foreach (var kvp in newResults)
                {
                    if (!currentResults.TryGetValue(kvp.Key, out var prev))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                    }
                    else if (!cmp.Equals(prev, kvp.Value))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, prev));
                    }
                }

                if (changes.Count > 0)
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                currentResults[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                currentResults.Remove(change.Key);
                                break;
                        }
                    }
                    var outSet = new ChangeSet<TResult, TKey>();
                    outSet.AddRange(changes);
                    observer.OnNext(outSet);
                }
            }

            var dispLeft = left.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                leftItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (leftItems.ContainsKey(change.Key)) leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            var dispRight = right.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                rightItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (rightItems.ContainsKey(change.Key)) rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            return Disposable.Combine(dispLeft, dispRight);
        });
    }

    /// <summary>
    /// Performs a full outer join between two cache change streams. Emits a result for every key present in either cache,
    /// with both sides nullable when absent.
    /// </summary>
    public static Observable<IChangeSet<TResult, TKey>> FullOuterJoin<TLeft, TRight, TKey, TResult>(
        this Observable<IChangeSet<TLeft, TKey>> left,
        Observable<IChangeSet<TRight, TKey>> right,
        Func<TLeft?, TRight?, TResult> resultSelector,
        IEqualityComparer<TResult>? resultComparer = null)
        where TKey : notnull
        where TLeft : class
        where TRight : class
        where TResult : notnull
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        if (resultSelector is null) throw new ArgumentNullException(nameof(resultSelector));
        var cmp = resultComparer ?? EqualityComparer<TResult>.Default;

        return Observable.Create<IChangeSet<TResult, TKey>>(observer =>
        {
            var leftItems = new Dictionary<TKey, TLeft>();
            var rightItems = new Dictionary<TKey, TRight>();
            var currentResults = new Dictionary<TKey, TResult>();

            void RecomputeAndEmit()
            {
                var allKeys = new HashSet<TKey>(leftItems.Keys);
                allKeys.UnionWith(rightItems.Keys);

                var newResults = new Dictionary<TKey, TResult>();
                foreach (var key in allKeys)
                {
                    leftItems.TryGetValue(key, out var l);
                    rightItems.TryGetValue(key, out var r);
                    var res = resultSelector(l, r);
                    newResults[key] = res;
                }

                var changes = new List<Change<TResult, TKey>>();
                foreach (var existing in currentResults)
                {
                    if (!newResults.ContainsKey(existing.Key))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Remove, existing.Key, existing.Value, existing.Value));
                    }
                }
                foreach (var kvp in newResults)
                {
                    if (!currentResults.TryGetValue(kvp.Key, out var prev))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                    }
                    else if (!cmp.Equals(prev, kvp.Value))
                    {
                        changes.Add(new Change<TResult, TKey>(ChangeReason.Update, kvp.Key, kvp.Value, prev));
                    }
                }

                if (changes.Count > 0)
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                currentResults[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                currentResults.Remove(change.Key);
                                break;
                        }
                    }
                    var outSet = new ChangeSet<TResult, TKey>();
                    outSet.AddRange(changes);
                    observer.OnNext(outSet);
                }
            }

            var dispLeft = left.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                leftItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (leftItems.ContainsKey(change.Key)) leftItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            var dispRight = right.Subscribe(changes =>
            {
                try
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Update:
                                rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                rightItems.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                if (rightItems.ContainsKey(change.Key)) rightItems[change.Key] = change.Current;
                                break;
                            case ChangeReason.Moved:
                                break;
                        }
                    }
                    RecomputeAndEmit();
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted);

            return Disposable.Combine(dispLeft, dispRight);
        });
    }
}

/// <summary>
/// Placeholder for future Full/Left/Right join result modeling.
/// </summary>
public readonly record struct JoinPair<TKey, TLeft, TRight>(TKey Key, TLeft Left, TRight Right) where TKey : notnull;
