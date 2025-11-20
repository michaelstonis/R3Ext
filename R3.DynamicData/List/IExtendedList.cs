// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// Represents a list which supports range operations.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IExtendedList<T> : IList<T>
{
    /// <summary>
    /// Inserts the elements of a collection into the list at the specified index.
    /// </summary>
    /// <param name="collection">The collection whose elements should be inserted.</param>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    void InsertRange(IEnumerable<T> collection, int index);

    /// <summary>
    /// Moves an item from the original to the destination index.
    /// </summary>
    /// <param name="original">The original index.</param>
    /// <param name="destination">The destination index.</param>
    void Move(int original, int destination);

    /// <summary>
    /// Removes a range of elements from the list.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range of elements to remove.</param>
    /// <param name="count">The number of elements to remove.</param>
    void RemoveRange(int index, int count);
}
