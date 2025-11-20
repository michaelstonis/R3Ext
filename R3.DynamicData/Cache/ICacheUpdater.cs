// Port of DynamicData to R3.

namespace R3.DynamicData.Cache;

/// <summary>
/// API for updating a cache within a batch operation.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ICacheUpdater<TObject, TKey>
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
    /// Looks up the value for the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value if found, otherwise an empty optional.</returns>
    Kernel.Optional<TObject> Lookup(TKey key);

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
    /// Removes items that match the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    void RemoveKeys(Func<TKey, bool> predicate);

    /// <summary>
    /// Removes items that match the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    void RemoveItems(Func<TObject, bool> predicate);

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Refreshes the item with the specified key, causing subscribers to re-evaluate.
    /// </summary>
    /// <param name="key">The key.</param>
    void Refresh(TKey key);

    /// <summary>
    /// Refreshes multiple items with the specified keys.
    /// </summary>
    /// <param name="keys">The keys to refresh.</param>
    void Refresh(IEnumerable<TKey> keys);
}
