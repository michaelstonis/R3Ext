// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace R3.DynamicData.Cache.Internal;

internal sealed class ManagedGroup<TObject, TKey, TGroupKey> : IGroup<TObject, TKey, TGroupKey>, IDisposable
    where TObject : notnull
    where TKey : notnull
{
    // Track items and their keys so we can provide a key selector
    private readonly Dictionary<TObject, TKey> _itemToKey = new();
    private readonly SourceCache<TObject, TKey> _cache;

    public ManagedGroup(TGroupKey groupKey)
    {
        Key = groupKey;

        // Create cache with a key selector that looks up from our mapping
        _cache = new SourceCache<TObject, TKey>(item => _itemToKey[item]);
    }

    public IObservableCache<TObject, TKey> Cache => _cache.AsObservableCache();

    public TGroupKey Key { get; }

    internal int Count => _cache.Count;

    public void Dispose() => _cache.Dispose();

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ManagedGroup<TObject, TKey, TGroupKey> managedGroup && Equals(managedGroup);
    }

    public override int GetHashCode() => Key is null ? 0 : EqualityComparer<TGroupKey>.Default.GetHashCode(Key);

    public override string ToString() => $"Group: {Key}";

    internal void AddOrUpdate(TObject item, TKey key)
    {
        _itemToKey[item] = key;
        _cache.Edit(updater => updater.AddOrUpdate(item));
    }

    internal void Remove(TKey key)
    {
        // Find and remove the item with this key
        TObject? itemToRemove = default;
        foreach (var kvp in _itemToKey)
        {
            if (EqualityComparer<TKey>.Default.Equals(kvp.Value, key))
            {
                itemToRemove = kvp.Key;
                break;
            }
        }

        if (itemToRemove != null)
        {
            _itemToKey.Remove(itemToRemove);
            _cache.Edit(updater => updater.Remove(key));
        }
    }

    internal void Refresh(TKey key)
    {
        _cache.Edit(updater => updater.Refresh(key));
    }

    private bool Equals(ManagedGroup<TObject, TKey, TGroupKey> other) => EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
}
