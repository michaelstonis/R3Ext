// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3.DynamicData.Cache;

namespace R3.DynamicData.Operators;

/// <summary>
/// Extension methods for filtering observable change sets.
/// </summary>
public static class FilterOperator
{
    /// <summary>
    /// Filters the observable change set using the specified predicate.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="predicate">The predicate to filter items.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, bool> predicate)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var filteredData = new Dictionary<TKey, TObject>();

            return source.Subscribe(
                changes =>
                {
                    var filteredChanges = new ChangeSet<TObject, TKey>();

                    foreach (var change in changes)
                    {
                        var key = change.Key;
                        var current = change.Current;
                        var matchesFilter = predicate(current);
                        var wasInFilter = filteredData.ContainsKey(key);

                        switch (change.Reason)
                        {
                            case Kernel.ChangeReason.Add:
                                if (matchesFilter)
                                {
                                    filteredData[key] = current;
                                    filteredChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Add,
                                        key,
                                        current));
                                }

                                break;

                            case Kernel.ChangeReason.Update:
                                if (matchesFilter && wasInFilter)
                                {
                                    var previous = filteredData[key];
                                    filteredData[key] = current;
                                    filteredChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Update,
                                        key,
                                        current,
                                        previous));
                                }
                                else if (matchesFilter && !wasInFilter)
                                {
                                    filteredData[key] = current;
                                    filteredChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Add,
                                        key,
                                        current));
                                }
                                else if (!matchesFilter && wasInFilter)
                                {
                                    var previous = filteredData[key];
                                    filteredData.Remove(key);
                                    filteredChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Remove,
                                        key,
                                        previous,
                                        previous));
                                }

                                break;

                            case Kernel.ChangeReason.Remove:
                                if (wasInFilter)
                                {
                                    var previous = filteredData[key];
                                    filteredData.Remove(key);
                                    filteredChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Remove,
                                        key,
                                        previous,
                                        previous));
                                }

                                break;

                            case Kernel.ChangeReason.Refresh:
                                if (wasInFilter)
                                {
                                    filteredChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Refresh,
                                        key,
                                        current));
                                }

                                break;
                        }
                    }

                    if (filteredChanges.Count > 0)
                    {
                        observer.OnNext(filteredChanges);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Filters the observable change set using a dynamic predicate observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="predicateChanged">An observable that emits new predicates.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Observable<Func<TObject, bool>> predicateChanged)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicateChanged == null)
        {
            throw new ArgumentNullException(nameof(predicateChanged));
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var allData = new Dictionary<TKey, TObject>();
            var filteredData = new Dictionary<TKey, TObject>();
            Func<TObject, bool>? currentPredicate = null;

            var predicateSubscription = predicateChanged.Subscribe(
                predicate =>
                {
                    currentPredicate = predicate;

                    // Re-evaluate all items with the new predicate
                    var changes = new ChangeSet<TObject, TKey>();
                    var newFilteredKeys = new HashSet<TKey>();

                    foreach (var kvp in allData)
                    {
                        var key = kvp.Key;
                        var item = kvp.Value;
                        var matchesFilter = currentPredicate(item);
                        var wasInFilter = filteredData.ContainsKey(key);

                        if (matchesFilter)
                        {
                            newFilteredKeys.Add(key);
                        }

                        if (matchesFilter && !wasInFilter)
                        {
                            changes.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Add,
                                key,
                                item));
                        }
                        else if (!matchesFilter && wasInFilter)
                        {
                            changes.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Remove,
                                key,
                                item,
                                item));
                        }
                    }

                    filteredData.Clear();
                    foreach (var key in newFilteredKeys)
                    {
                        filteredData[key] = allData[key];
                    }

                    if (changes.Count > 0)
                    {
                        observer.OnNext(changes);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            var sourceSubscription = source.Subscribe(
                changes =>
                {
                    var outputChanges = new ChangeSet<TObject, TKey>();

                    foreach (var change in changes)
                    {
                        var key = change.Key;
                        var current = change.Current;

                        switch (change.Reason)
                        {
                            case Kernel.ChangeReason.Add:
                            case Kernel.ChangeReason.Update:
                                allData[key] = current;

                                if (currentPredicate != null)
                                {
                                    var matchesFilter = currentPredicate(current);
                                    var wasInFilter = filteredData.ContainsKey(key);

                                    if (matchesFilter && !wasInFilter)
                                    {
                                        filteredData[key] = current;
                                        outputChanges.Add(new Change<TObject, TKey>(
                                            Kernel.ChangeReason.Add,
                                            key,
                                            current));
                                    }
                                    else if (matchesFilter && wasInFilter)
                                    {
                                        var previous = filteredData[key];
                                        filteredData[key] = current;
                                        outputChanges.Add(new Change<TObject, TKey>(
                                            Kernel.ChangeReason.Update,
                                            key,
                                            current,
                                            previous));
                                    }
                                    else if (!matchesFilter && wasInFilter)
                                    {
                                        var previous = filteredData[key];
                                        filteredData.Remove(key);
                                        outputChanges.Add(new Change<TObject, TKey>(
                                            Kernel.ChangeReason.Remove,
                                            key,
                                            previous,
                                            previous));
                                    }
                                }

                                break;

                            case Kernel.ChangeReason.Remove:
                                allData.Remove(key);

                                if (filteredData.ContainsKey(key))
                                {
                                    var previous = filteredData[key];
                                    filteredData.Remove(key);
                                    outputChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Remove,
                                        key,
                                        previous,
                                        previous));
                                }

                                break;

                            case Kernel.ChangeReason.Refresh:
                                if (filteredData.ContainsKey(key))
                                {
                                    outputChanges.Add(new Change<TObject, TKey>(
                                        Kernel.ChangeReason.Refresh,
                                        key,
                                        current));
                                }

                                break;
                        }
                    }

                    if (outputChanges.Count > 0)
                    {
                        observer.OnNext(outputChanges);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Combine(predicateSubscription, sourceSubscription);
        });
    }
}
