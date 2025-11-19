// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.Cache;

/// <summary>
/// A collection of changes for a cache.
/// API-compatible with DynamicData.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IChangeSet<TObject, TKey> : IChangeSet, IEnumerable<Change<TObject, TKey>>
    where TKey : notnull
{
    /// <summary>
    /// Gets the change at the specified index.
    /// </summary>
    Change<TObject, TKey> this[int index] { get; }
}
