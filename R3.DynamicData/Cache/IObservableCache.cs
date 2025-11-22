// Port of DynamicData to R3.
// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.

using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// A read-only observable cache for querying and observing in-memory data.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IObservableCache<TObject, TKey> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// Gets the total count of cached items.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets all items currently in the cache.
    /// </summary>
    IReadOnlyList<TObject> Items { get; }

    /// <summary>
    /// Gets all keys currently in the cache.
    /// </summary>
    IReadOnlyList<TKey> Keys { get; }

    /// <summary>
    /// Gets all key-value pairs currently in the cache.
    /// </summary>
    IReadOnlyDictionary<TKey, TObject> KeyValues { get; }

    /// <summary>
    /// Gets an observable that emits the count whenever it changes.
    /// </summary>
    Observable<int> CountChanged { get; }

    /// <summary>
    /// Looks up a single item using the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>An optional with the looked up value.</returns>
    Optional<TObject> Lookup(TKey key);

    /// <summary>
    /// Connects to the cache and observes changes, applying an optional predicate filter.
    /// </summary>
    /// <param name="predicate">Optional predicate to filter items.</param>
    /// <param name="suppressEmptyChangeSets">By default, empty change sets are not emitted. Set to false to emit them.</param>
    /// <returns>An observable of change sets.</returns>
    Observable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true);

    /// <summary>
    /// Returns a filtered stream of cache changes (not preceded with initial state).
    /// </summary>
    /// <param name="predicate">Optional predicate to filter items.</param>
    /// <returns>An observable of change sets.</returns>
    Observable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null);

    /// <summary>
    /// Watches a single item by key.
    /// </summary>
    /// <param name="key">The key to watch.</param>
    /// <returns>An observable of changes for the specified key.</returns>
    Observable<Change<TObject, TKey>> Watch(TKey key);
}
