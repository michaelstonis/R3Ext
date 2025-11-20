
// This is a port of DynamicData to R3 for enhanced performance.
// Original DynamicData: Copyright (c) 2011-2023 Roland Pheasant. Licensed under the MIT license.

namespace R3.DynamicData;

/// <summary>
/// Base interface representing a set of changes.
/// API-compatible with DynamicData.IChangeSet.
/// </summary>
public interface IChangeSet
{
    /// <summary>
    /// Gets the number of additions.
    /// </summary>
    int Adds { get; }

    /// <summary>
    /// Gets or sets the capacity of the change set.
    /// </summary>
    int Capacity { get; set; }

    /// <summary>
    /// Gets the total update count.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the number of moves.
    /// </summary>
    int Moves { get; }

    /// <summary>
    /// Gets the number of refreshes.
    /// </summary>
    int Refreshes { get; }

    /// <summary>
    /// Gets the number of removes.
    /// </summary>
    int Removes { get; }

    /// <summary>
    /// Gets the number of updates.
    /// </summary>
    int Updates { get; }
}
