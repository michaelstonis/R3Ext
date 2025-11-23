// Port of DynamicData to R3.

namespace R3.DynamicData.Cache;

/// <summary>
/// An editable cache that exposes an observable change set for tracking modifications.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ISourceCache<TObject, TKey> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// Gets the total count of cached items.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets all keys currently in the cache.
    /// </summary>
    IEnumerable<TKey> Keys { get; }

    /// <summary>
    /// Gets all items currently in the cache.
    /// </summary>
    IEnumerable<TObject> Items { get; }

    /// <summary>
    /// Gets all key-value pairs currently in the cache.
    /// </summary>
    IEnumerable<KeyValuePair<TObject, TKey>> KeyValues { get; }

    /// <summary>
    /// Gets an observable that emits the count whenever the cache count changes.
    /// </summary>
    Observable<int> CountChanged { get; }

    /// <summary>
    /// Looks up the value for the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value if found, otherwise an empty optional.</returns>
    Kernel.Optional<TObject> Lookup(TKey key);

    /// <summary>
    /// Returns a read-only snapshot of all items.
    /// </summary>
    /// <returns>A read-only collection of items.</returns>
    IReadOnlyCollection<TObject> Preview();

    /// <summary>
    /// Connects to the cache and observes changes.
    /// </summary>
    /// <returns>An observable of change sets.</returns>
    Observable<IChangeSet<TObject, TKey>> Connect();

    /// <summary>
    /// Connects to the cache and observes changes, applying an optional predicate filter.
    /// </summary>
    /// <param name="predicate">Optional predicate to filter items.</param>
    /// <returns>An observable of change sets.</returns>
    Observable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate);

    /// <summary>
    /// Watches a single item by key.
    /// </summary>
    /// <param name="key">The key to watch.</param>
    /// <returns>An observable of changes for the specified key.</returns>
    Observable<Change<TObject, TKey>> Watch(TKey key);

    /// <summary>
    /// Executes a batch update operation.
    /// </summary>
    /// <param name="updateAction">The update action.</param>
    void Edit(Action<ICacheUpdater<TObject, TKey>> updateAction);

    /// <summary>
    /// Adds or updates the item with the specified key.
    /// </summary>
    /// <param name="item">The item to add or update.</param>
    void AddOrUpdate(TObject item);

    /// <summary>
    /// Adds or updates multiple items.
    /// </summary>
    /// <param name="items">The items to add or update.</param>
    void AddOrUpdate(IEnumerable<TObject> items);

    /// <summary>
    /// Removes the item with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    void Remove(TKey key);

    /// <summary>
    /// Removes multiple items with the specified keys.
    /// </summary>
    /// <param name="keys">The keys to remove.</param>
    void Remove(IEnumerable<TKey> keys);

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    void Clear();
}
