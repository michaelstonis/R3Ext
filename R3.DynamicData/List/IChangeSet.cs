// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// A collection of changes for a list.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IChangeSet<T> : IEnumerable<Change<T>>
{
    /// <summary>
    /// Gets the number of changes.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the total number of items after the changes.
    /// </summary>
    int TotalChanges { get; }

    /// <summary>
    /// Gets the number of adds.
    /// </summary>
    int Adds { get; }

    /// <summary>
    /// Gets the number of removes.
    /// </summary>
    int Removes { get; }

    /// <summary>
    /// Gets the number of moves.
    /// </summary>
    int Moves { get; }

    /// <summary>
    /// Gets the number of refreshes.
    /// </summary>
    int Refreshes { get; }
}
