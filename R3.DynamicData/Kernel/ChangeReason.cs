// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.Kernel;

/// <summary>
/// The reason for a change in a collection.
/// </summary>
public enum ChangeReason
{
    /// <summary>
    /// An item was added to the collection.
    /// </summary>
    Add,

    /// <summary>
    /// An item was updated in the collection.
    /// </summary>
    Update,

    /// <summary>
    /// An item was removed from the collection.
    /// </summary>
    Remove,

    /// <summary>
    /// An item was refreshed (notifying without actual change).
    /// </summary>
    Refresh,

    /// <summary>
    /// An item was moved within the collection.
    /// </summary>
    Moved,
}
